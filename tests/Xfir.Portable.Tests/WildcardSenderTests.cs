// SPDX-License-Identifier: EUPL-1.2
using System.Text.Json;
using MimeKit;
using Xfir.Mail;
using Xunit;

namespace Xfir.Tests;

public sealed class WildcardSenderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xfirmail-wildcard-" + Guid.NewGuid().ToString("N"));
    public WildcardSenderTests() => Directory.CreateDirectory(root);

    [Fact]
    public void LoadsWildcardFromJsonAndStillExcludesServiceAndInvalidAddresses()
    {
        var path = Path.Combine(root, "appsettings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(MailTests.Config(root, allowedSenders: ["*"])));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var settings = Settings.Load(path);
        Assert.True(settings.IsSenderAllowed("another@other.invalid"));
        Assert.False(settings.IsSenderAllowed("SERVICE@example.invalid"));
        Assert.False(settings.IsSenderAllowed(null));
        Assert.False(settings.IsSenderAllowed("*"));
        Assert.False(settings.IsSenderAllowed("invalid"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" * ")]
    [InlineData("invalid")]
    [InlineData("Nome <sender@example.invalid>")]
    [InlineData("service@example.invalid")]
    public void WildcardDoesNotHideInvalidExplicitEntries(string entry)
    {
        var settings = MailTests.Config(root, allowedSenders: ["*", entry]);
        Assert.Throws<ConfigurationException>(settings.Validate);
    }

    [Fact]
    public void AcceptsUnlistedAuthenticatedSenderFromAnotherDomain()
    {
        using var message = MailTests.Message();
        message.From.Clear(); message.From.Add(MailboxAddress.Parse("another@other.invalid"));
        message.Headers[HeaderId.AuthenticationResults] = "localhost; dmarc=pass header.from=other.invalid";
        Assert.Null(MessagePolicy.AuthorizedSender(message, MailTests.Config(root)));
        Assert.Equal("another@other.invalid", MessagePolicy.AuthorizedSender(message,
            MailTests.Config(root, allowedSenders: ["*"])));
    }

    [Fact]
    public void RejectsServiceAsIncomingSenderEvenWithValidAuthentication()
    {
        using var message = MailTests.Message();
        message.From.Clear(); message.From.Add(MailboxAddress.Parse("SERVICE@example.invalid"));
        Assert.Null(MessagePolicy.AuthorizedSender(message, MailTests.Config(root, allowedSenders: ["*"])));
    }

    [Theory]
    [InlineData("Auto-Submitted", "auto-replied")]
    [InlineData("X-XfirMail", "1")]
    [InlineData("List-Id", "list.example.invalid")]
    [InlineData("Precedence", "bulk")]
    [InlineData("Return-Path", "<>")]
    [InlineData("X-Auto-Response-Suppress", "All")]
    public void WildcardStillRejectsLoops(string header, string value)
    {
        using var message = MailTests.Message(); message.Headers.Add(header, value);
        Assert.Null(MessagePolicy.AuthorizedSender(message, MailTests.Config(root, allowedSenders: ["*"])));
    }

    [Fact]
    public async Task RejectsQueuedReplyToServiceUnderWildcardPolicy()
    {
        var settings = MailTests.Config(root, allowedSenders: ["*"]);
        using var store = new RequestStore(settings.StatePath);
        var record = new RequestRecord(RequestStore.Hash("self-reply"), RequestStore.Hash("scope"), 1, DateTimeOffset.UtcNow)
        { Stage = RequestStage.Ready, Sender = settings.Smtp.Account };
        using var reply = new MimeMessage();
        reply.From.Add(MailboxAddress.Parse(settings.Smtp.Account));
        reply.To.Add(MailboxAddress.Parse(settings.Smtp.Account));
        reply.Body = new TextPart("plain") { Text = "Prova sintetica" };
        reply.WriteTo(store.Outbox(record.Id)); store.Save(record);
        var sender = new MailTests.FakeSender();
        await new MailWorker(settings, store, new MailTests.FakeConverter(), sender, _ => { }).SendReadyAsync(default);
        Assert.Equal(0, sender.Calls);
        Assert.Equal(RequestStage.Rejected, store.Find(record.Id)!.Stage);
        Assert.Equal("recipient-policy", store.Find(record.Id)!.Reason);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
