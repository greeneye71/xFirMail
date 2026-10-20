// SPDX-License-Identifier: EUPL-1.2
using System.IO;

namespace Xfir.CommandLine;

internal static class AppArguments
{
    public static bool OpensWindow(string[] args) => args.Length == 0
        || (args is [var path] && !string.IsNullOrWhiteSpace(path) && !path.StartsWith('-'));

    public static int Run(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (args is ["--pdf", ..]) return ConversionCommand.Run(args[1..], output, error, "xFirW.exe --pdf", cancellationToken);
        if (args is ["--help" or "-h"]) return ConversionCommand.Run(args, output, error, "xFirW.exe --pdf", cancellationToken);
        error.WriteLine("Argomenti non validi.");
        return ConversionCommand.Run([], output, error, "xFirW.exe --pdf", cancellationToken);
    }
}
