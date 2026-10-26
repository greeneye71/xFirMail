// SPDX-License-Identifier: EUPL-1.2
using Xfir.Core;
using Xunit;

namespace Xfir.Tests;

public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "xfir-atomic-" + Guid.NewGuid().ToString("N"));
    private string Destination => Path.Combine(folder, "copy.pdf");
    public AtomicFileWriterTests() => Directory.CreateDirectory(folder);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedWritePreservesDestinationAndRemovesOnlyItsTemporaryFile(bool existing)
    {
        if (existing) File.WriteAllText(Destination, "original");
        var unrelated = Path.Combine(folder, ".xfirw-unrelated.tmp");
        File.WriteAllText(unrelated, "unrelated");
        Assert.Throws<IOException>(() => AtomicFileWriter.Write(Destination, stream =>
        {
            stream.Write(new byte[] { 1, 2, 3 });
            throw new IOException("Simulated storage failure");
        }, overwrite: existing));
        if (existing) Assert.Equal("original", File.ReadAllText(Destination));
        else Assert.False(File.Exists(Destination));
        Assert.Equal("unrelated", File.ReadAllText(unrelated));
        Assert.Single(Directory.EnumerateFiles(folder, "*.tmp"));
    }

    [Fact]
    public void ConcurrentCreationWinsWithoutBeingOverwritten()
    {
        Assert.Throws<IOException>(() => AtomicFileWriter.Write(Destination, stream =>
        {
            stream.WriteByte(1);
            File.WriteAllText(Destination, "concurrent file");
        }));
        Assert.Equal("concurrent file", File.ReadAllText(Destination));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
    }

    [Fact]
    public void CancellationBeforeRenameLeavesNoPartialDestination()
    {
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => AtomicFileWriter.Write(Destination, stream =>
        {
            stream.WriteByte(1);
            cancellation.Cancel();
        }, cancellationToken: cancellation.Token));
        Assert.Empty(Directory.EnumerateFiles(folder));
    }

    [Fact]
    public void ReplacesExistingFileOnlyAfterSuccessfulWrite()
    {
        File.WriteAllText(Destination, "old");
        AtomicFileWriter.Write(Destination, new byte[] { 4, 5, 6 }, overwrite: true);
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Destination));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
    }

    [Fact]
    public void ReplacementWithOpenReaderUsesPlatformFileSharingRules()
    {
        File.WriteAllText(Destination, "original");
        using (var locked = new FileStream(Destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Throws<IOException>(() => AtomicFileWriter.Write(Destination, new byte[] { 1 }, overwrite: true));
                Assert.Equal("original", File.ReadAllText(Destination));
            }
            else
            {
                // Unix permits replacing an open file; existing readers retain the old inode.
                AtomicFileWriter.Write(Destination, new byte[] { 1 }, overwrite: true);
                Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Destination));
                using var reader = new StreamReader(locked, leaveOpen: true);
                Assert.Equal("original", reader.ReadToEnd());
            }
        }
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
    }

    public void Dispose() => Directory.Delete(folder, true);
}
