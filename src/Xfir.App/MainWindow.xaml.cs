// SPDX-License-Identifier: EUPL-1.2
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Xfir.Core;
using Xfir.Rendering;

namespace Xfir.App;

public partial class MainWindow : Window
{
    private readonly string? initialFile;
    private readonly string sessionPath = Path.Combine(PreviewStorage.Root, Guid.NewGuid().ToString("N"));
    private readonly List<string> previewFiles = [];
    private FileStream? sessionLock;
    private FormDocument? current;
    private byte[]? currentPdf;
    private Uri? currentPreview;
    private bool busy;
    private bool previewReady;
    private bool closed;
    private bool browserInitialized;

    public MainWindow(string? initialFile = null)
    {
        InitializeComponent();
        AuthorLabel.Text = $"{AppInfo.Author} · xFirW {AppInfo.Version}";
        this.initialFile = initialFile;
        WindowState = WindowState.Maximized;
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Fit restore bounds and minimum sizes before Windows maximizes the window.
        WindowPlacement.FitToWorkArea(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(initialFile)) await OpenFile(initialFile);
    }
    private void CanOpen(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy;
    private void CanExport(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && currentPdf is not null;
    private void CanPrint(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = !busy && previewReady;
    private async void OnOpen(object sender, ExecutedRoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Apri un formulario digitale", Filter = "Formulario digitale (*.xfir)|*.xfir", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) await OpenFile(dialog.FileName);
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private async void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (busy || e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        if (files.Length != 1) { MessageBox.Show(this, "Apri un formulario alla volta.", "xFirW"); return; }
        if (!files[0].EndsWith(".xfir", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "Trascina un file .xfir.", "xFirW"); return; }
        await OpenFile(files[0]);
    }
    private async Task OpenFile(string path)
    {
        if (busy || closed) return;
        busy = true;
        StatusLabel.Text = "Lettura del formulario e preparazione del PDF…";
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var result = await Task.Run(() =>
            {
                var document = new XfirReader().Read(path);
                var pdf = new FormPdfRenderer().Render(document);
                return (document, pdf);
            });
            if (closed) return;
            // Commit the new document only after a successful read/render; a failed open preserves the previous one.
            current = result.document;
            currentPdf = result.pdf;
            previewReady = false;
            NumberLabel.Text = current.Number;
            StateLabel.Text = current.State;
            ProducerLabel.Text = Value(current.Producer);
            RecipientLabel.Text = Value(current.Recipient);
            WasteLabel.Text = Value(FormFormatting.Eer(current.WasteCode));
            QuantityLabel.Text = Value((current.Quantity + " " + current.Unit).Trim());
            var acceptedQuantity = current.Acceptance?.Get("QuantitaAccettata");
            AcceptedQuantityLabel.Text = string.IsNullOrWhiteSpace(acceptedQuantity) ? "Non indicata"
                : (FormFormatting.Quantity(acceptedQuantity) + " " + current.Acceptance?.Attribute("QuantitaAccettata", "unitaMisura")).Trim();
            FileLabel.Text = current.SourceName;
            WarningLabel.Text = string.Join("\n\n", current.Warnings);
            DataGrid.ItemsSource = current.Parts.SelectMany(p => p.Fields
                .Where(f => !f.Field.Contains("Signature", StringComparison.Ordinal))
                .Select(f => new DataField(p.Name + " · " + f.Field, f.Value))).ToList();
            var details = new StringBuilder("FIRME PRESENTI · VERIFICA COMPLETA NON ESEGUITA\n\n");
            foreach (var signature in current.Signatures)
            {
                details.AppendLine(signature.Signer).AppendLine(signature.FileName)
                    .AppendLine("Data dichiarata: " + FormFormatting.LocalTime(signature.SignedAt))
                    .AppendLine("Certificato: " + signature.Subject)
                    .AppendLine("Riferimenti: " + string.Join(", ", signature.References)).AppendLine();
            }
            details.AppendLine("ALLEGATI PDF");
            details.AppendLine(current.Attachments.Count == 0 ? "Nessun allegato presente." : "Gli allegati sono elencati, ma apertura ed esportazione non sono ancora implementate.");
            foreach (var attachment in current.Attachments) details.AppendLine(attachment.Name + " · " + attachment.Size + " byte");
            details.AppendLine().AppendLine("SHA-256 del file originale:").AppendLine(current.SourceSha256);
            SignaturesText.Text = details.ToString();
            Title = current.Number + " · xFirW";
            Tabs.SelectedIndex = 0;
            await ShowPreview(currentPdf);
        }
        catch (Exception ex) when (ex is XfirReadException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            StatusLabel.Text = "Apertura non riuscita. " + (current is null ? "Scegli un altro file." : "Il documento precedente è ancora disponibile.");
            MessageBox.Show(this, ex.Message, "Impossibile aprire il formulario", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { busy = false; CommandManager.InvalidateRequerySuggested(); }
    }

    private async Task ShowPreview(byte[] pdf)
    {
        try
        {
            Preview.Visibility = Visibility.Visible;
            EmptyPanel.Visibility = Visibility.Collapsed;
            if (!browserInitialized)
            {
                var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "xFirW", "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
                if (closed) return;
                await Preview.EnsureCoreWebView2Async(environment);
                if (closed) return;
                Preview.CoreWebView2.Settings.IsStatusBarEnabled = false;
                Preview.CoreWebView2.Settings.AreDevToolsEnabled = false;
                Preview.CoreWebView2.Settings.HiddenPdfToolbarItems = CoreWebView2PdfToolbarItems.Save | CoreWebView2PdfToolbarItems.SaveAs;
                Preview.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
                Preview.CoreWebView2.NavigationStarting += (_, e) =>
                {
                    if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || currentPreview is null || !uri.IsFile
                        || !string.Equals(uri.LocalPath, currentPreview.LocalPath, StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
                };
                Preview.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                Preview.CoreWebView2.WebResourceRequested += (_, e) =>
                {
                    if (e.Request.Uri.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || e.Request.Uri.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
                        e.Response = environment.CreateWebResourceResponse(null, 403, "External resources disabled", "");
                };
                Preview.CoreWebView2.NavigationCompleted += (_, e) =>
                {
                    previewReady = e.IsSuccess;
                    if (e.IsSuccess) RemoveStalePreviews();
                    StatusLabel.Text = e.IsSuccess ? "PDF pronto · " + (current?.Warnings.Count > 0 ? "Consulta le avvertenze" : "Lettura completata")
                        : "Anteprima non disponibile. Puoi esportare il PDF.";
                    CommandManager.InvalidateRequerySuggested();
                };
                browserInitialized = true;
            }
            sessionLock ??= PreviewStorage.CreateSession(sessionPath);
            var file = Path.Combine(sessionPath, Guid.NewGuid().ToString("N") + ".pdf");
            // Keep creation and registration together so closing the window cannot leave an untracked PDF.
            File.WriteAllBytes(file, pdf);
            previewFiles.Add(file);
            if (closed) return;
            currentPreview = new Uri(file);
            Preview.CoreWebView2.Navigate(currentPreview.AbsoluteUri);
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            previewReady = false;
            Preview.Visibility = Visibility.Collapsed;
            EmptyPanel.Visibility = Visibility.Visible;
            EmptyMessage.Text = "Il PDF è pronto per l'esportazione. L'anteprima richiede Microsoft Edge WebView2 Runtime. " + ex.Message;
            StatusLabel.Text = "Anteprima non disponibile · Esporta PDF rimane utilizzabile";
        }
    }

    private void OnExport(object sender, ExecutedRoutedEventArgs e)
    {
        if (currentPdf is null || current is null) return;
        var safeName = string.Concat(current.Number.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var dialog = new SaveFileDialog { Title = "Esporta la copia del formulario", Filter = "Documento PDF (*.pdf)|*.pdf", FileName = safeName + ".pdf", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllBytes(dialog.FileName, currentPdf); StatusLabel.Text = "PDF esportato: " + Path.GetFileName(dialog.FileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "Esportazione non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void OnPrint(object sender, ExecutedRoutedEventArgs e)
    {
        if (!previewReady) return;
        try { Preview.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        { MessageBox.Show(this, "Stampa non disponibile. Esporta il PDF e aprilo con il tuo lettore PDF.\n\n" + ex.Message, "Stampa", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void OnAbout(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    private void OnClosed(object? sender, EventArgs e)
    {
        closed = true;
        Preview.Dispose();
        foreach (var file in previewFiles)
            try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        sessionLock?.Dispose();
        try { if (Directory.Exists(sessionPath)) Directory.Delete(sessionPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    // Only the displayed PDF is needed; earlier ones would otherwise accumulate until the window closes.
    private void RemoveStalePreviews()
    {
        foreach (var file in previewFiles.Where(f => !string.Equals(f, currentPreview?.LocalPath, StringComparison.OrdinalIgnoreCase)).ToList())
            try { File.Delete(file); previewFiles.Remove(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private static string Value(string text) => string.IsNullOrWhiteSpace(text) ? "—" : text;
}
