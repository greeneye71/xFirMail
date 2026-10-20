// SPDX-License-Identifier: EUPL-1.2
namespace Xfir.Core;

public static class AtomicFileWriter
{
    public static void Write(string destination, byte[] content, bool overwrite = false, CancellationToken cancellationToken = default) =>
        Write(destination, stream => stream.Write(content), overwrite, cancellationToken);

    public static void Write(string destination, Action<Stream> write, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        destination = Path.GetFullPath(destination);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".xfirw-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                write(stream);
                cancellationToken.ThrowIfCancellationRequested();
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Preserve existing file permissions on replacement. If a destination appears
            // concurrently, the create-only move still fails without damaging that file.
            if (overwrite && File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        finally
        {
            if (created)
                try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
