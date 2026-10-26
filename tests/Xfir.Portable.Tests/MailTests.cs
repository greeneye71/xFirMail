// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using System.Text.Json;
using MimeKit;
using Xfir.Mail;
using Xunit;

namespace Xfir.Tests;

public sealed class MailTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xfirmail-tests-" + Guid.NewGuid().ToString("N"));
    public MailTests() => Directory.CreateDirectory(root);
    internal static Settings Config(string root, int imap = 993, int smtp = 465, int quota = 100, string? converter = null,
        string[]? allowedSenders = null, bool requireDmarc = true) => new()
    {
        Imap = new() { Server = "localhost", Port = imap, Account = "service@example.invalid", Password = "test-only-password" },
        Smtp = new() { Server = "localhost", Port = smtp, Account = "service@example.invalid", Password = "test-only-password" },
        AllowedSenders = allowedSenders ?? ["sender@example.invalid"], StatePath = Path.Combine(root, "state"), WorkPath = Path.Combine(root, "work"),
        ConverterPath = converter ?? typeof(MailTests).Assembly.Location, DailyQuota = quota, RequireDmarc = requireDmarc
    };
    internal static MimeMessage Message(int attachments = 1)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.invalid"));
        message.To.Add(MailboxAddress.Parse("service@example.invalid"));
        message.ReplyTo.Add(MailboxAddress.Parse("trap@example.invalid"));
        message.Subject = "Dati sintetici";
        message.MessageId = "synthetic@example.invalid";
        message.Headers.Add("Authentication-Results", "localhost; dmarc=pass header.from=example.invalid");
        var builder = new BodyBuilder { TextBody = "Collaudo sintetico." };
        for (var i = 0; i < attachments; i++) builder.Attachments.Add($"../../test-{i}.xfir", ReaderTests.Sample());
        message.Body = builder.ToMessageBody();
        return message;
    }
    internal static byte[] Bytes(MimeMessage message) { using var stream = new MemoryStream(); message.WriteTo(stream); return stream.ToArray(); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequiresTrustedAlignedAuthenticationAndRejectsForgedDuplicates(bool allowAllSenders)
    {
        var settings = Config(root, allowedSenders: allowAllSenders ? ["*"] : null);
        using var message = Message();
        Assert.Equal("sender@example.invalid", MessagePolicy.AuthorizedSender(message, settings));
        message.Headers[HeaderId.AuthenticationResults] = "untrusted.invalid; dmarc=pass header.from=example.invalid";
        Assert.Null(MessagePolicy.AuthorizedSender(message, settings));
        message.Headers[HeaderId.AuthenticationResults] = "localhost; dmarc=pass header.from=other.invalid";
        Assert.Null(MessagePolicy.AuthorizedSender(message, settings));
        message.Headers[HeaderId.AuthenticationResults] = "localhost; dmarc=fail header.from=example.invalid";
        Assert.Null(MessagePolicy.AuthorizedSender(message, settings));
        message.Headers[HeaderId.AuthenticationResults] = "localhost; dmarc=pass header.from=example.invalid";
        message.Headers.Add("Authentication-Results", "localhost; dmarc=pass header.from=example.invalid");
        Assert.Null(MessagePolicy.AuthorizedSender(message, settings));
    }

    [Theory]
    [InlineData("Auto-Submitted", "auto-replied")]
    [InlineData("X-XfirMail", "1")]
    [InlineData("List-Id", "list.example.invalid")]
    [InlineData("Precedence", "bulk")]
    [InlineData("Return-Path", "<>")]
    [InlineData("X-Auto-Response-Suppress", "All")]
    public void RejectsLoops(string header, string value)
    { using var message = Message(); message.Headers.Add(header, value); Assert.Null(MessagePolicy.AuthorizedSender(message, Config(root))); }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void AttachmentCountIsBounded(int count)
    { using var message = Message(count); Assert.Throws<InputRejectedException>(() => MessagePolicy.Attachments(message)); }


    [Fact]
    public async Task SendsOnlyToAuthorizedFromAndDoesNotRepeatUidOrDuplicateMime()
    {
        var settings = Config(root);
        using var message = Message(2);
        await using var inbox = new FakeInbox(Bytes(message));
        using var store = new RequestStore(settings.StatePath);
        var sender = new FakeSender(); var converter = new FakeConverter();
        var logs = new List<string>();
        var worker = new MailWorker(settings, store, converter, sender, logs.Add);
        await worker.PollAsync(inbox, default);
        await worker.PollAsync(inbox, default);
        Assert.Equal(1, sender.Calls);
        Assert.Equal(2, converter.Calls);
        using var reply = MimeMessage.Load(new MemoryStream(sender.Last!));
        Assert.Equal("sender@example.invalid", Assert.Single(reply.To.Mailboxes).Address);
        Assert.Empty(reply.Cc); Assert.Empty(reply.Bcc); Assert.Empty(reply.ReplyTo);
        Assert.Equal(new[] { "test-0.pdf", "test-1.pdf", "avvertenze.txt" }, reply.Attachments.Cast<MimePart>().Select(p => p.FileName));
        Assert.DoesNotContain(logs, s => s.Contains("password") || s.Contains("sender@") || s.Contains("Dati sintetici"));
        inbox.Add(Bytes(message));
        await worker.PollAsync(inbox, default);
        Assert.Equal(1, sender.Calls);
        Assert.Contains(store.All(), r => r.Reason == "duplicate");
        Assert.Empty(Directory.GetFiles(Path.Combine(settings.StatePath, "outbox")));
    }

    [Theory]
    [InlineData("Formulario 2026.xfir", "Formulario 2026.pdf")]
    [InlineData("FIR.12.34.XFIR", "FIR.12.34.pdf")]
    [InlineData("Rifiuti città.xfir", "Rifiuti città.pdf")]
    [InlineData("../../modulo.xfir", "modulo.pdf")]
    [InlineData("C:\\cartella\\modulo.xfir", "modulo.pdf")]
    [InlineData(".xfir", "formulario-1.pdf")]
    public async Task ReplyPreservesOriginalAttachmentBasenameWithPdfExtension(string original, string expected)
    {
        var settings = Config(root); using var message = Message();
        Assert.Single(message.Attachments.Cast<MimePart>()).FileName = original;
        await using var inbox = new FakeInbox(Bytes(message));
        using var store = new RequestStore(settings.StatePath);
        var sender = new FakeSender();
        await new MailWorker(settings, store, new FakeConverter(), sender, _ => { }).PollAsync(inbox, default);
        Assert.Equal(RequestStage.Sent, Assert.Single(store.All()).Stage);
        using var reply = MimeMessage.Load(new MemoryStream(sender.Last!));
        Assert.Equal(expected, Assert.Single(reply.Attachments.Cast<MimePart>(),
            part => part.ContentType.IsMimeType("application", "pdf")).FileName);
    }

    [Fact]
    public async Task FailedSmtpRetriesAreBoundedAndUncertainNeverRetries()
    {
        var settings = Config(root);
        using var message = Message();
        await using var inbox = new FakeInbox(Bytes(message));
        using var store = new RequestStore(settings.StatePath);
        var sender = new FakeSender { Result = SendResult.Retry };
        var worker = new MailWorker(settings, store, new FakeConverter(), sender, _ => { });
        await worker.PollAsync(inbox, default);
        var record = Assert.Single(store.All());
        Assert.Equal(RequestStage.Ready, record.Stage);
        for (var i = 0; i < 2; i++)
        { record = store.Find(record.Id)!; record.NextAttempt = DateTimeOffset.MinValue; store.Save(record); await worker.SendReadyAsync(default); }
        Assert.Equal(RequestStage.Failed, store.Find(record.Id)!.Stage);
        Assert.Equal(3, sender.Calls);

        inbox.Add(Bytes(Message()));
        // A different MIME input with a fresh identity exercises ambiguous SMTP without deduplication.
        using var another = Message(); another.Subject = "Seconda prova";
        inbox.Add(Bytes(another)); sender.Result = SendResult.Uncertain;
        await worker.PollAsync(inbox, default);
        var calls = sender.Calls;
        await worker.PollAsync(inbox, default);
        Assert.Equal(calls, sender.Calls);
        Assert.Contains(store.All(), r => r.Stage == RequestStage.Uncertain);
    }

    [Theory]
    [InlineData(RequestStage.Received, RequestStage.Received)]
    [InlineData(RequestStage.Ready, RequestStage.Ready)]
    [InlineData(RequestStage.Sending, RequestStage.Uncertain)]
    [InlineData(RequestStage.Sent, RequestStage.Sent)]
    public void RestartPreservesCursorAndConservativelyRecoversSend(RequestStage before, RequestStage after)
    {
        var settings = Config(root); var scope = RequestStore.Hash("scope"); var id = RequestStore.Hash("request");
        using (var store = new RequestStore(settings.StatePath))
        { store.Save(new(id, scope, 23, DateTimeOffset.UtcNow) { Stage = before }); store.Advance(scope, 23); }
        using var restored = new RequestStore(settings.StatePath); restored.Recover();
        Assert.Equal(23u, restored.Cursor(scope)); Assert.Equal(after, restored.Find(id)!.Stage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartSendsPreparedOutboxWithoutReconversion(bool allowAllSenders)
    {
        var settings = Config(root, allowedSenders: allowAllSenders ? ["*"] : null);
        using var message = Message(); await using var inbox = new FakeInbox(Bytes(message));
        var sender = new FakeSender { Result = SendResult.Retry }; var converter = new FakeConverter();
        using (var store = new RequestStore(settings.StatePath))
        {
            await new MailWorker(settings, store, converter, sender, _ => { }).PollAsync(inbox, default);
            var r = Assert.Single(store.All()); r.NextAttempt = DateTimeOffset.MinValue; store.Save(r);
        }
        sender.Result = SendResult.Sent;
        using (var store = new RequestStore(settings.StatePath))
        { store.Recover(); await new MailWorker(settings, store, converter, sender, _ => { }).PollAsync(inbox, default); Assert.Equal(RequestStage.Sent, Assert.Single(store.All()).Stage); }
        Assert.Equal(1, converter.Calls); Assert.Equal(2, sender.Calls);
    }

    [Theory]
    [InlineData(RequestStage.Received, 1)]
    [InlineData(RequestStage.Sent, 0)]
    public async Task RestartResumesOwnedRequestAndDoesNotReplayCompletedUid(RequestStage stage, int calls)
    {
        var settings = Config(root); using var message = Message(); await using var inbox = new FakeInbox(Bytes(message));
        var id = RequestStore.Hash(inbox.Scope + ":1");
        using (var store = new RequestStore(settings.StatePath))
        { store.Save(new(id, inbox.Scope, 1, DateTimeOffset.UtcNow) { Stage = stage }); store.Advance(inbox.Scope, 1); }
        using var reopened = new RequestStore(settings.StatePath); reopened.Recover();
        var converter = new FakeConverter(); var sender = new FakeSender();
        await new MailWorker(settings, reopened, converter, sender, _ => { }).PollAsync(inbox, default);
        Assert.Equal(calls, converter.Calls); Assert.Equal(calls, sender.Calls);
        Assert.Equal(RequestStage.Sent, reopened.Find(id)!.Stage);
    }

    [Fact]
    public async Task RejectsOversizeWithoutDownloadingAndAppliesDailyQuota()
    {
        var settings = Config(root, quota: 1); using var message = Message();
        await using var inbox = new FakeInbox(Bytes(message));
        inbox.Add(Encoding.UTF8.GetBytes("oversized"), Settings.MaxMessageBytes + 1);
        using var second = Message(); second.Subject = "oltre quota"; inbox.Add(Bytes(second));
        using var store = new RequestStore(settings.StatePath); var sender = new FakeSender();
        await new MailWorker(settings, store, new FakeConverter(), sender, _ => { }).PollAsync(inbox, default);
        Assert.Equal(1, sender.Calls); Assert.Equal(2, inbox.Reads);
        Assert.Contains(store.All(), r => r.Reason == "message-size"); Assert.Contains(store.All(), r => r.Reason == "quota");
    }

    [Fact]
    public void CleanupKeepsCursorAndUncertainMetadataButRemovesDocumentsAndExpiredTerminalRecords()
    {
        using var store = new RequestStore(Config(root).StatePath); var scope = RequestStore.Hash("scope");
        foreach (var stage in new[] { RequestStage.Sent, RequestStage.Uncertain, RequestStage.Ready })
        {
            var record = new RequestRecord(RequestStore.Hash(stage.ToString()), scope, 1, DateTimeOffset.UtcNow.AddDays(-31)) { Stage = stage };
            store.Save(record); File.WriteAllText(store.Outbox(record.Id), "documento sintetico");
        }
        store.Advance(scope, 100); store.Cleanup(30, DateTimeOffset.UtcNow);
        Assert.Equal(2, store.All().Count); Assert.Equal(100u, store.Cursor(scope));
        Assert.Single(Directory.GetFiles(Path.Combine(Config(root).StatePath, "outbox")));
    }

    [Fact]
    public void ConfigurationErrorsNeverIncludeValuesOrSecrets()
    {
        var path = Path.Combine(root, "appsettings.json");
        File.WriteAllText(path, "{\"Password\":\"do-not-print-this\",\"unknown\":1}");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var error = Assert.Throws<ConfigurationException>(() => Settings.Load(path));
        Assert.DoesNotContain("do-not-print-this", error.Message);
        File.WriteAllText(path, JsonSerializer.Serialize(Config(root))); Assert.NotNull(Settings.Load(path));
        File.WriteAllText(path, "{\"Imap\":null}");
        Assert.Throws<ConfigurationException>(() => Settings.Load(path));
    }

    [Fact]
    public void SingleInstanceAndCorruptStateFailClosed()
    {
        var settings = Config(root); using var store = new RequestStore(settings.StatePath);
        Assert.Throws<IOException>(() => new RequestStore(settings.StatePath));
        File.WriteAllText(Path.Combine(settings.StatePath, "requests", RequestStore.Hash("bad") + ".json"), "broken-json");
        Assert.Throws<JsonException>(() => store.Recover());
    }

    [Fact]
    public async Task MissingOutboxFailsClosedAndDoesNotBlockOtherRequests()
    {
        var settings = Config(root); using var store = new RequestStore(settings.StatePath);
        var record = new RequestRecord(RequestStore.Hash("missing"), RequestStore.Hash("scope"), 1, DateTimeOffset.UtcNow)
        { Stage = RequestStage.Ready, Sender = settings.AllowedSenders[0] };
        store.Save(record); var sender = new FakeSender();
        await new MailWorker(settings, store, new FakeConverter(), sender, _ => { }).SendReadyAsync(default);
        Assert.Equal(0, sender.Calls); Assert.Equal("outbox-unreadable", store.Find(record.Id)!.Reason);
    }

    [Fact]
    public async Task CrashAfterLastConversionAttemptDoesNotResetAttemptLimit()
    {
        var settings = Config(root); using var message = Message(); await using var inbox = new FakeInbox(Bytes(message));
        using var store = new RequestStore(settings.StatePath);
        var id = RequestStore.Hash(inbox.Scope + ":1");
        store.Save(new(id, inbox.Scope, 1, DateTimeOffset.UtcNow) { ConvertAttempts = Settings.MaxAttempts });
        store.Advance(inbox.Scope, 1); var converter = new FakeConverter();
        await new MailWorker(settings, store, converter, new FakeSender(), _ => { }).PollAsync(inbox, default);
        Assert.Equal(0, converter.Calls); Assert.Equal(RequestStage.Failed, store.Find(id)!.Stage);
    }

    [Fact]
    public void UnixConfigRejectsGroupReadableAndJsonRejectsDuplicateProperties()
    {
        var path = Path.Combine(root, "appsettings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(Config(root)));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            Assert.Throws<ConfigurationException>(() => Settings.Load(path));
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        File.WriteAllText(path, "{\"PollSeconds\":60,\"PollSeconds\":70}");
        Assert.Throws<ConfigurationException>(() => Settings.Load(path));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
    internal sealed class FakeConverter : IConverter
    {
        public int Calls;
        public Task<(byte[] Pdf, string Warnings)> ConvertAsync(MimePart attachment, CancellationToken token)
        { Calls++; return Task.FromResult((Encoding.ASCII.GetBytes("%PDF-synthetic"), "Avvertenza sintetica")); }
    }
    internal sealed class FakeSender : IReplySender
    {
        public int Calls; public byte[]? Last; public SendResult Result = SendResult.Sent;
        public Task<SendResult> SendAsync(MimeMessage message, CancellationToken token)
        { Calls++; Last = Bytes(message); return Task.FromResult(Result); }
    }
    internal sealed class FakeInbox(byte[] first) : IInbox
    {
        public string Scope => RequestStore.Hash("synthetic-inbox");
        private readonly List<(byte[] Bytes, uint Size)> messages = [(first, (uint)first.Length)];
        public int Reads;
        public List<uint> MarkedRead { get; } = [];
        public bool FailMarkRead { get; set; }
        public void Add(byte[] bytes, int? size = null) => messages.Add((bytes, (uint)(size ?? bytes.Length)));
        public Task<MailBatch> NextAsync(uint after, CancellationToken token) => Task.FromResult(new MailBatch(
            messages.Select((m, i) => new MailItem((uint)i + 1, m.Size)).Where(m => m.Uid > after).ToList(), (uint)messages.Count));
        public Task<byte[]> ReadAsync(uint uid, CancellationToken token) { Reads++; return Task.FromResult(messages[(int)uid - 1].Bytes); }
        public Task MarkReadAsync(uint uid, CancellationToken token)
        {
            if (FailMarkRead) throw new IOException("Synthetic flag failure");
            MarkedRead.Add(uid);
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
