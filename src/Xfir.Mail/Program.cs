// SPDX-License-Identifier: EUPL-1.2
using System.Runtime.InteropServices;
using Xfir.Mail;

if (args is not [var path] || path is "--help" or "-h")
{
    Console.WriteLine("Uso: Xfir.Mail /percorso/appsettings.json (configurazione riletta al riavvio)");
    return args is ["--help" or "-h"] ? 0 : 2;
}
try
{
    var settings = Settings.Load(path);
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    using var signal = !OperatingSystem.IsWindows() ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx => { ctx.Cancel = true; stop.Cancel(); }) : null;
    using var store = new RequestStore(settings.StatePath);
    store.Recover();
    store.Cleanup(settings.RetentionDays, DateTimeOffset.UtcNow);
    Directory.CreateDirectory(settings.WorkPath);
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(settings.WorkPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    foreach (var dir in Directory.EnumerateDirectories(settings.WorkPath, "xfirmail-*")) Directory.Delete(dir, recursive: true);
    var worker = new MailWorker(settings, store, new ProcessConverter(settings), new SmtpReplySender(settings), Console.WriteLine);
    Console.WriteLine("xFirMail avviato; configurazione valida.");
    while (!stop.IsCancellationRequested)
    {
        try
        {
            store.Cleanup(settings.RetentionDays, DateTimeOffset.UtcNow);
            await worker.SendReadyAsync(stop.Token);
            await using var inbox = await ImapInbox.ConnectAsync(settings, stop.Token);
            await worker.PollAsync(inbox, stop.Token);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
        catch (Exception) { Console.Error.WriteLine("Ciclo non completato: controllare rete, credenziali, certificati e stato locale. Nessun dettaglio riservato registrato."); }
        try { await Task.Delay(TimeSpan.FromSeconds(settings.PollSeconds), stop.Token); }
        catch (OperationCanceledException) { break; }
    }
    return 0;
}
catch (ConfigurationException e) { Console.Error.WriteLine(e.Message); return 2; }
catch (Exception) { Console.Error.WriteLine("Avvio fallito: controllare permessi, spazio disponibile e assenza di altre istanze sullo stesso stato."); return 1; }
