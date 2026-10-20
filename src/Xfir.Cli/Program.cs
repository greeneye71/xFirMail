// SPDX-License-Identifier: EUPL-1.2
using Xfir.CommandLine;

return ConsoleCancellation.Run(token => ConversionCommand.Run(args, Console.Out, Console.Error, "Xfir.Cli", token));
