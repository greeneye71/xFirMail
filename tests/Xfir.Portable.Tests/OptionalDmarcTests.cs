// SPDX-License-Identifier: EUPL-1.2
using System.Text.Json;
using MimeKit;
using Xfir.Mail;
using Xunit;

namespace Xfir.Tests;

public sealed class OptionalDmarcTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xfirmail-dmarc-" + Guid.NewGuid().ToString("N"));
    public OptionalDmarcTests() => Directory.CreateDirectory(root);

    [Fact]
    public void MissingAuthenticationIsRejectedByDefaultAndAllowedOnlyByExplicitOption()
    {
        using var message = MailTests.Message();
        message.Headers.RemoveAll(HeaderId.AuthenticationResults);
        Assert.True(new Settings().RequireDmarc);
        Assert.Null(MessagePolicy.AuthorizedSender(message, MailTests.Config(root)));
        Assert.Equal("sender@example.invalid", MessagePolicy.AuthorizedSender(message,
            MailTests.Config(root, requireDmarc: false)));
    }

    [Fact]
    public void CanLoadExplicitOptionFromJson()
    {
        var path = Path.Combine(root, "appsettings.json");
        File.WriteAllText(path, JsonSerializer.Serialize(MailTests.Config(root, requireDmarc: false)));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Assert.False(Settings.Load(path).RequireDmarc);
    }

    [Theory]
    [InlineData("Auto-Submitted", "auto-replied")]
    [InlineData("X-XfirMail", "1")]
    [InlineData("List-Id", "list.example.invalid")]
    [InlineData("Precedence", "bulk")]
    [InlineData("Return-Path", "<>")]
    [InlineData("X-Auto-Response-Suppress", "All")]
    public void WithoutDmarcWildcardStillRejectsLoops(string header, string value)
    {
        using var message = MailTests.Message();
        message.Headers.RemoveAll(HeaderId.AuthenticationResults); message.Headers.Add(header, value);
        Assert.Null(MessagePolicy.AuthorizedSender(message,
            MailTests.Config(root, allowedSenders: ["*"], requireDmarc: false)));
    }

    [Fact]
    public void WithoutDmarcStillRejectsSelfUnlistedAndAmbiguousSenders()
    {
        using var message = MailTests.Message(); message.Headers.RemoveAll(HeaderId.AuthenticationResults);
        message.From.Clear(); message.From.Add(MailboxAddress.Parse("another@other.invalid"));
        Assert.Null(MessagePolicy.AuthorizedSender(message, MailTests.Config(root, requireDmarc: false)));
        var open = MailTests.Config(root, allowedSenders: ["*"], requireDmarc: false);
        Assert.Equal("another@other.invalid", MessagePolicy.AuthorizedSender(message, open));
        message.From.Clear(); message.From.Add(MailboxAddress.Parse(open.Smtp.Account));
        Assert.Null(MessagePolicy.AuthorizedSender(message, open));
        message.From.Add(MailboxAddress.Parse("another@other.invalid"));
        Assert.Null(MessagePolicy.AuthorizedSender(message, open));
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
