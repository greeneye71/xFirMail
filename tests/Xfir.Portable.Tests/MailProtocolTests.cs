// SPDX-License-Identifier: EUPL-1.2
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MimeKit;
using Xfir.Mail;
using Xunit;

namespace Xfir.Tests;

public sealed class MailProtocolTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xfirmail-protocol-" + Guid.NewGuid().ToString("N"));
    public MailProtocolTests() => Directory.CreateDirectory(root);
    private static ImapClient Imap(LoopbackMailServer server) => new()
    { ServerCertificateValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString() == server.Certificate.GetCertHashString() };
    private static SmtpClient Smtp(LoopbackMailServer server) => new()
    { ServerCertificateValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString() == server.Certificate.GetCertHashString() };

    [Fact]
    public async Task ReceivesOverImapStartTlsAndRepliesOverSmtpStartTls()
    {
        using var input = MailTests.Message();
        await using var imap = new LoopbackMailServer(true) { Input = MailTests.Bytes(input) };
        await using var smtp = new LoopbackMailServer(false);
        var settings = MailTests.Config(root, imap.Port, smtp.Port, converter: ConverterProcessTests.CliPath);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        IInbox connected;
        try { connected = await ImapInbox.ConnectAsync(settings, timeout.Token, Imap(imap)); }
        catch { Assert.Fail(imap.ServerError ?? "No server error"); throw; }
        await using var inbox = connected;
        using var store = new RequestStore(settings.StatePath);
        var worker = new MailWorker(settings, store, new ProcessConverter(settings), new SmtpReplySender(settings, () => Smtp(smtp)), _ => { });
        await worker.PollAsync(inbox, timeout.Token);
        Assert.Equal(RequestStage.Sent, Assert.Single(store.All()).Stage);
        Assert.NotNull(smtp.Delivered);
        Assert.Equal("RCPT TO:<sender@example.invalid>", smtp.EnvelopeRecipient);
        using var reply = MimeMessage.Load(new MemoryStream(smtp.Delivered!));
        Assert.Equal("sender@example.invalid", Assert.Single(reply.To.Mailboxes).Address);
    }

    [Theory]
    [InlineData(451, false, SendResult.Retry)]
    [InlineData(550, false, SendResult.Rejected)]
    [InlineData(250, true, SendResult.Uncertain)]
    public async Task DistinguishesExplicitSmtpFailureFromAmbiguousDisconnect(int status, bool drop, SendResult expected)
    {
        await using var smtp = new LoopbackMailServer(false) { DataStatus = status, DropAfterData = drop };
        var settings = MailTests.Config(root, smtp: smtp.Port);
        using var reply = new MimeMessage(); reply.From.Add(MailboxAddress.Parse(settings.Smtp.Account));
        reply.To.Add(MailboxAddress.Parse(settings.AllowedSenders[0])); reply.Body = new TextPart("plain") { Text = "Prova sintetica" };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Assert.Equal(expected, await new SmtpReplySender(settings, () => Smtp(smtp)).SendAsync(reply, timeout.Token));
    }

    [Fact]
    public async Task RejectsBadImapCredentialsWithoutExposingPassword()
    {
        await using var imap = new LoopbackMailServer(true) { RejectAuthentication = true };
        var settings = MailTests.Config(root, imap.Port);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await Assert.ThrowsAsync<MailKit.Security.AuthenticationException>(() => ImapInbox.ConnectAsync(settings, timeout.Token, Imap(imap)));
    }

    [Fact]
    public async Task ProductionClientRejectsUntrustedCertificate()
    {
        await using var imap = new LoopbackMailServer(true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await Assert.ThrowsAsync<MailKit.Security.SslHandshakeException>(() => ImapInbox.ConnectAsync(MailTests.Config(root, imap.Port), timeout.Token));
    }

    [Fact]
    public async Task RequiresStartTlsAndDoesNotAuthenticateInCleartext()
    {
        await using var imap = new LoopbackMailServer(true) { OfferTls = false };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await Assert.ThrowsAsync<NotSupportedException>(() => ImapInbox.ConnectAsync(MailTests.Config(root, imap.Port), timeout.Token, Imap(imap)));
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
