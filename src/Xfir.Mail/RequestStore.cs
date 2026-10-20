// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Xfir.Mail;

public enum RequestStage { Received, Ready, Sending, Sent, Rejected, Failed, Uncertain }

public sealed record RequestRecord(string Id, string Scope, uint Uid, DateTimeOffset Created)
{
    public RequestStage Stage { get; set; }
    public string? Sender { get; set; }
    public string? ContentHash { get; set; }
    public int ConvertAttempts { get; set; }
    public int SendAttempts { get; set; }
    public DateTimeOffset NextAttempt { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>One writer, durable atomic records and cursors. Corrupt state fails closed.</summary>
public sealed class RequestStore : IDisposable
{
    private readonly string root;
    private readonly FileStream lease;
    public RequestStore(string root)
    {
        this.root = root;
        Directory.CreateDirectory(root);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        lease = new FileStream(Path.Combine(root, "worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Directory.CreateDirectory(Path.Combine(root, "requests"));
        Directory.CreateDirectory(Path.Combine(root, "outbox"));
        DurableFiles.SyncDirectory(root);
    }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public string Outbox(string id) => Path.Combine(root, "outbox", Safe(id) + ".eml");
    private string RecordPath(string id) => Path.Combine(root, "requests", Safe(id) + ".json");
    private static string Safe(string id) => id.Length == 64 && id.All(Uri.IsHexDigit) ? id : throw new InvalidDataException("Identificatore stato non valido.");
    public RequestRecord? Find(string id) => File.Exists(RecordPath(id)) ? Read<RequestRecord>(RecordPath(id)) : null;
    public List<RequestRecord> All() => Directory.EnumerateFiles(Path.Combine(root, "requests"), "*.json").Select(Read<RequestRecord>).ToList();
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Registro non leggibile.");
    public void Save(RequestRecord request) => DurableFiles.Write(RecordPath(request.Id), JsonSerializer.SerializeToUtf8Bytes(request));
    public long OutboxBytes => Directory.EnumerateFiles(Path.Combine(root, "outbox"), "*.eml").Sum(p => new FileInfo(p).Length);
    public uint Cursor(string scope)
    {
        var path = Path.Combine(root, Safe(scope) + ".cursor");
        return File.Exists(path) ? Read<uint>(path) : 0;
    }
    public void Advance(string scope, uint uid) => DurableFiles.Write(Path.Combine(root, Safe(scope) + ".cursor"), JsonSerializer.SerializeToUtf8Bytes(uid));
    public void Recover()
    {
        foreach (var request in All().Where(r => r.Stage == RequestStage.Sending))
        { request.Stage = RequestStage.Uncertain; request.Reason = "interrupted-send"; Save(request); }
    }
    public bool ExceedsQuota(string sender, int quota, DateTimeOffset now) => All().Count(r =>
        r.Sender?.Equals(sender, StringComparison.OrdinalIgnoreCase) == true && r.Created.UtcDateTime.Date == now.UtcDateTime.Date &&
        r.Reason != "quota") >= quota;
    public bool Duplicate(string sender, string hash, string except) => All().Any(r => r.Id != except &&
        r.Sender?.Equals(sender, StringComparison.OrdinalIgnoreCase) == true && r.ContentHash == hash && r.Reason != "quota");
    public void Cleanup(int days, DateTimeOffset now)
    {
        foreach (var r in All())
        {
            if (r.Stage is RequestStage.Sent or RequestStage.Rejected or RequestStage.Failed or RequestStage.Uncertain)
                File.Delete(Outbox(r.Id));
            // Keep unsettled and uncertain records for operator review. Cursors prevent replay after terminal expiry.
            if (r.Created < now.AddDays(-days) && (r.Stage is RequestStage.Sent or RequestStage.Rejected or RequestStage.Failed))
                File.Delete(RecordPath(r.Id));
        }
        foreach (var file in Directory.EnumerateFiles(root, ".xfirw-*.tmp", SearchOption.AllDirectories)) File.Delete(file);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "outbox"), "*.eml"))
            if (!File.Exists(RecordPath(Path.GetFileNameWithoutExtension(file)))) File.Delete(file);
    }
    public void Dispose() => lease.Dispose();
}
