// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using System.Text;
using MimeKit;

namespace Xfir.Mail;

public interface IConverter
{
    Task<(byte[] Pdf, string Warnings)> ConvertAsync(MimePart attachment, CancellationToken token);
}

public sealed class ProcessConverter : IConverter
{
    private readonly Settings settings;
    private readonly TimeSpan maxDuration;
    public ProcessConverter(Settings settings) : this(settings, TimeSpan.FromSeconds(45)) { }
    internal ProcessConverter(Settings settings, TimeSpan maxDuration) { this.settings = settings; this.maxDuration = maxDuration; }
    public async Task<(byte[] Pdf, string Warnings)> ConvertAsync(MimePart attachment, CancellationToken token)
    {
        var directory = Path.Combine(settings.WorkPath, "xfirmail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var input = Path.Combine(directory, "input.xfir");
            var output = Path.Combine(directory, "output.pdf");
            using (var stream = new LimitedMemoryStream(Settings.MaxAttachmentBytes))
            {
                if (attachment.Content is null) throw new InputRejectedException();
                await attachment.Content.DecodeToAsync(stream, token);
                await File.WriteAllBytesAsync(input, stream.ToArray(), token);
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(maxDuration);
            var start = new ProcessStartInfo(settings.ConverterPath)
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
              WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(input); start.ArgumentList.Add(output);
            using var process = Process.Start(start) ?? throw new IOException("Converter non avviato.");
            // Drain both pipes concurrently and cap captured warnings. Never log child output.
            var stdout = Drain(process.StandardOutput, timeout.Token);
            var stderr = Drain(process.StandardError, timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                await stdout;
                var warnings = await stderr;
                if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length > Settings.MaxMessageBytes)
                    throw new InputRejectedException();
                var pdf = await File.ReadAllBytesAsync(output, token);
                if (!pdf.AsSpan().StartsWith("%PDF-"u8)) throw new InputRejectedException();
                return (pdf, warnings);
            }
            catch
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { }
                throw;
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task<string> Drain(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder(); var buffer = new char[2048]; int count;
        while ((count = await reader.ReadAsync(buffer, token)) > 0)
            if (result.Length < 16000) result.Append(buffer, 0, Math.Min(count, 16000 - result.Length));
        return result.ToString();
    }
}

public sealed class LimitedMemoryStream(long limit) : MemoryStream
{
    private void Check(int count) { if (Position + count > limit) throw new InputRejectedException(); }
    public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
    public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
    { Write(buffer, offset, count); return Task.CompletedTask; }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    { token.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
}
