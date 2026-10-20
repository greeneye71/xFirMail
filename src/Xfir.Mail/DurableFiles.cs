// SPDX-License-Identifier: EUPL-1.2
using System.Runtime.InteropServices;
using Xfir.Core;

namespace Xfir.Mail;

internal static class DurableFiles
{
    public static void Write(string path, byte[] bytes) => Write(path, s => s.Write(bytes));
    public static void Write(string path, Action<Stream> write, CancellationToken token = default)
    {
        AtomicFileWriter.Write(path, write, overwrite: true, cancellationToken: token);
        SyncDirectory(Path.GetDirectoryName(path)!);
    }
    public static void SyncDirectory(string path)
    {
        // A flushed file alone does not make its rename durable across power loss on Linux.
        if (!OperatingSystem.IsLinux()) return;
        var descriptor = Open(path, 0);
        if (descriptor < 0) throw new IOException("Directory dello stato non sincronizzabile.");
        try { if (Fsync(descriptor) != 0) throw new IOException("Sincronizzazione dello stato fallita."); }
        finally { Close(descriptor); }
    }
    [DllImport("libc", EntryPoint = "open", SetLastError = true)] private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)] private static extern int Fsync(int fd);
    [DllImport("libc", EntryPoint = "close")] private static extern int Close(int fd);
}
