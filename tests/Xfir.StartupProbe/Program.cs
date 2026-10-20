// SPDX-License-Identifier: EUPL-1.2
using Xfir.App;

// Exercise the real startup route in a test-only executable. Never open an application window.
if (CommandLineStartup.TryRun(args, out var code)) return code;
Console.WriteLine("GUI_REQUESTED");
return 203;
