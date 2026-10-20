// SPDX-License-Identifier: EUPL-1.2
using System.Windows;
using System.Windows.Threading;

namespace Xfir.App;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        if (CommandLineStartup.TryRun(e.Args, out var exitCode))
        {
            Shutdown(exitCode);
            return;
        }

        if (!DisclaimerStore.IsAccepted())
        {
            // Keep the process alive while the notice is the only window.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (new DisclaimerWindow().ShowDialog() != true)
            {
                Shutdown(1);
                return;
            }
            DisclaimerStore.MarkAccepted();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        DispatcherUnhandledException += OnUnhandledException;
        PreviewStorage.RemoveOrphans();
        var window = new MainWindow(e.Args.FirstOrDefault());
        MainWindow = window;
        window.Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // An unexpected failure in one operation must not close the window with the open document.
        var text = "Si è verificato un errore imprevisto. L'operazione è stata interrotta.\n\n" + e.Exception.Message;
        if (MainWindow is not { IsLoaded: true } owner)
        {
            MessageBox.Show(text, "xFirW", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        e.Handled = true;
        MessageBox.Show(owner, text, "xFirW", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
