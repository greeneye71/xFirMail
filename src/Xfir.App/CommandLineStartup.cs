// SPDX-License-Identifier: EUPL-1.2
using System.IO;
using System.Runtime.InteropServices;
using Xfir.CommandLine;

namespace Xfir.App;

internal static class CommandLineStartup
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || args[0] is not ("--pdf" or "--help" or "-h")) return false;

        // WinExe has no console of its own. Reuse the caller's console when present;
        // redirected streams also work when started by a script without a console.
        AttachConsole(uint.MaxValue);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        exitCode = ConversionCommand.Run(args[0] == "--pdf" ? args[1..] : args,
            Console.Out, Console.Error, "xFirW.exe --pdf");
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);
}
