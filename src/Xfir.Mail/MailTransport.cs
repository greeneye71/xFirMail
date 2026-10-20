// SPDX-License-Identifier: EUPL-1.2
using System.Security.Authentication;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace Xfir.Mail;

public enum SendResult { Sent, Retry, Rejected, Uncertain }
public sealed record MailItem(uint Uid, uint Size);
public sealed record MailBatch(IReadOnlyList<MailItem> Items, uint Through);

public interface IInbox : IAsyncDisposable
{
    string Scope { get; }
    Task<MailBatch> NextAsync(uint after, CancellationToken token);
    Task<byte[]> ReadAsync(uint uid, CancellationToken token);
}
public interface IReplySender
{
    Task<SendResult> SendAsync(MimeMessage message, CancellationToken token);
}

public sealed class ImapInbox : IInbox
{
    private readonly ImapClient client;
    public string Scope { get; }
    private ImapInbox(ImapClient client, Settings settings)
    {
        this.client = client;
        Scope = RequestStore.Hash($"{settings.Imap.Server.ToLowerInvariant()}:{settings.Imap.Port}\n{settings.Imap.Account}\nINBOX\n{client.Inbox.UidValidity}");
    }
    public static Task<ImapInbox> ConnectAsync(Settings settings, CancellationToken token) => ConnectAsync(settings, token, new ImapClient());
    internal static async Task<ImapInbox> ConnectAsync(Settings settings, CancellationToken token, ImapClient client)
    {
        client.Timeout = 30000;
        client.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        try
        {
            var endpoint = settings.Imap;
            await client.ConnectAsync(endpoint.Server, endpoint.Port, endpoint.Port == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, token);
            await client.AuthenticateAsync(endpoint.Account, endpoint.Password, token);
            await client.Inbox.OpenAsync(FolderAccess.ReadOnly, token);
            return new(client, settings);
        }
        catch { client.Dispose(); throw; }
    }
    public async Task<MailBatch> NextAsync(uint after, CancellationToken token)
    {
        var next = client.Inbox.UidNext?.Id;
        if (next is null || next <= 1 || after >= next - 1 || after == uint.MaxValue) return new([], after);
        var end = (uint)Math.Min((ulong)after + 500, (ulong)next.Value - 1);
        var ids = (await client.Inbox.SearchAsync(SearchQuery.Uids(new UniqueIdRange(new(after + 1), new(end))), token))
            .OrderBy(u => u.Id).ToList();
        var selected = ids.Take(50).ToList();
        if (selected.Count == 0) return new([], end);
        var summaries = await client.Inbox.FetchAsync(selected, MessageSummaryItems.UniqueId | MessageSummaryItems.Size, token);
        var items = selected.Select(uid => new MailItem(uid.Id, summaries.FirstOrDefault(s => s.UniqueId == uid)?.Size ?? uint.MaxValue)).ToList();
        return new(items, ids.Count > 50 ? selected[^1].Id : end);
    }
    public async Task<byte[]> ReadAsync(uint uid, CancellationToken token)
    {
        using var source = await client.Inbox.GetStreamAsync(new UniqueId(uid), token, new SizeProgress());
        using var bytes = new LimitedMemoryStream(Settings.MaxMessageBytes);
        await source.CopyToAsync(bytes, token);
        return bytes.ToArray();
    }
    public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
    private sealed class SizeProgress : ITransferProgress
    {
        public void Report(long transferred, long totalSize) => Report(transferred);
        public void Report(long transferred) { if (transferred > Settings.MaxMessageBytes) throw new InputRejectedException(); }
    }
}

public sealed class SmtpReplySender : IReplySender
{
    private readonly Settings settings;
    private readonly Func<SmtpClient> createClient;
    public SmtpReplySender(Settings settings) : this(settings, () => new SmtpClient()) { }
    internal SmtpReplySender(Settings settings, Func<SmtpClient> createClient)
    { this.settings = settings; this.createClient = createClient; }
    public async Task<SendResult> SendAsync(MimeMessage message, CancellationToken token)
    {
        using var client = createClient();
        client.Timeout = 30000;
        client.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        var sending = false;
        try
        {
            var endpoint = settings.Smtp;
            await client.ConnectAsync(endpoint.Server, endpoint.Port, endpoint.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, token);
            await client.AuthenticateAsync(endpoint.Account, endpoint.Password, token);
            // Explicit envelope excludes Reply-To, CC, BCC and any accidental MIME recipients.
            var recipient = message.To.Mailboxes.Single();
            sending = true;
            await client.SendAsync(message, MailboxAddress.Parse(settings.Smtp.Account), new[] { recipient }, token);
            return SendResult.Sent;
        }
        catch (SmtpCommandException e) when ((int)e.StatusCode is >= 400 and <= 599)
        { return (int)e.StatusCode < 500 ? SendResult.Retry : SendResult.Rejected; }
        catch (Exception) { return sending ? SendResult.Uncertain : SendResult.Retry; }
    }
}
