// SPDX-License-Identifier: EUPL-1.2
namespace Xfir.CommandLine;

internal static class ConsoleCancellation
{
    public static int Run(Func<CancellationToken, int> command)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try { return command(cancellation.Token); }
        finally { Console.CancelKeyPress -= handler; }
    }
}
