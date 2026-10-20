// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using MimeKit;
using Xfir.Mail;
using Xunit;

namespace Xfir.Tests;

public sealed class ConverterProcessTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "xfirmail-process-" + Guid.NewGuid().ToString("N"));
    internal static string CliPath => Path.Combine(AppContext.BaseDirectory, "cli", OperatingSystem.IsWindows() ? "Xfir.Cli.exe" : "Xfir.Cli");
    public ConverterProcessTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task RealChildProducesPdfAndDeletesInputsAndOutputs()
    {
        var settings = MailTests.Config(root, converter: CliPath);
        using var input = MailTests.Message();
        var (pdf, warnings) = await new ProcessConverter(settings).ConvertAsync(MessagePolicy.Attachments(input)[0], default);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf.AsSpan(0, 8)));
        Assert.Empty(warnings); Assert.Empty(Directory.GetFileSystemEntries(settings.WorkPath));
    }
    [Fact]
    public async Task MalformedAttachmentFailsWithoutLeavingDocuments()
    {
        var settings = MailTests.Config(root, converter: CliPath);
        using var input = new MimePart("application", "octet-stream") { Content = new MimeContent(new MemoryStream("invalid"u8.ToArray())) };
        await Assert.ThrowsAsync<InputRejectedException>(() => new ProcessConverter(settings).ConvertAsync(input, default));
        Assert.Empty(Directory.GetFileSystemEntries(settings.WorkPath));
    }
    [Fact]
    public async Task TimeoutKillsChildAndCleansWorkDirectory()
    {
        var settings = MailTests.Config(root, converter: CliPath); using var input = MailTests.Message();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProcessConverter(settings, TimeSpan.FromTicks(1))
            .ConvertAsync(MessagePolicy.Attachments(input)[0], default));
        Assert.Empty(Directory.GetFileSystemEntries(settings.WorkPath));
    }
    [Fact]
    public async Task DecodedAttachmentLimitIsEnforcedBeforeStartingChild()
    {
        var settings = MailTests.Config(root, converter: CliPath);
        using var input = new MimePart("application", "octet-stream")
        { Content = new MimeContent(new MemoryStream(new byte[Settings.MaxAttachmentBytes + 1])) };
        await Assert.ThrowsAsync<InputRejectedException>(() => new ProcessConverter(settings).ConvertAsync(input, default));
        Assert.Empty(Directory.GetFileSystemEntries(settings.WorkPath));
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
