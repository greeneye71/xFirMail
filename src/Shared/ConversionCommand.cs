// SPDX-License-Identifier: EUPL-1.2
using System.IO;
using System.Text.Json;
using Xfir.Core;
using Xfir.Rendering;

namespace Xfir.CommandLine;

// Shared by the console tool and the WPF executable so both exports behave identically.
internal static class ConversionCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error, string command)
    {
        var help = args is ["--help" or "-h"];
        if (help || args.Length is < 1 or > 2 || args.Any(string.IsNullOrWhiteSpace)
            || args[0].StartsWith('-') || (args.Length == 2 && args[1].StartsWith('-') && args[1] != "--inspect"))
        {
            var writer = help ? output : error;
            writer.WriteLine($"Uso: {command} <documento.xfir> [copia.pdf | --inspect]");
            writer.WriteLine("Senza copia.pdf salva nella cartella del file XFIR, con lo stesso nome ed estensione .pdf.");
            writer.WriteLine("Non sovrascrive file esistenti. Codici di uscita: 0 riuscito, 1 errore, 2 argomenti non validi.");
            return help ? 0 : 2;
        }

        try
        {
            var document = new XfirReader().Read(args[0]);
            if (args is [_, "--inspect"])
                output.WriteLine(JsonSerializer.Serialize(new { document.Number, document.State, document.Producer, document.Recipient,
                    document.WasteCode, document.Quantity, document.Unit, document.SourceSha256, document.Warnings,
                    Signatures = document.Signatures.Count, Parts = document.Parts.Select(p => p.Name) }, new JsonSerializerOptions { WriteIndented = true }));
            else
            {
                var destination = Path.GetFullPath(args.Length == 2 ? args[1] : Path.ChangeExtension(args[0], ".pdf"));
                var pdf = new FormPdfRenderer().Render(document);
                using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                    file.Write(pdf);
                output.WriteLine("PDF creato: " + destination);
                foreach (var warning in document.Warnings) error.WriteLine("Avviso: " + warning);
            }
            return 0;
        }
        catch (Exception e) when (e is XfirReadException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            error.WriteLine(e.Message);
            return 1;
        }
    }
}
