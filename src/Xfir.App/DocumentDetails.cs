// SPDX-License-Identifier: EUPL-1.2
using System.Text;
using Xfir.Core;

namespace Xfir.App;

internal static class DocumentDetails
{
    public static string Build(FormDocument document, ProcessingBudget budget)
    {
        var text = new StringBuilder("FIRME PRESENTI · VERIFICA COMPLETA NON ESEGUITA\n\n");
        foreach (var signature in document.Signatures)
        {
            budget.Check();
            text.AppendLine(signature.Signer).AppendLine(signature.FileName)
                .AppendLine("Data dichiarata: " + FormFormatting.LocalTime(signature.SignedAt))
                .AppendLine("Certificato: " + signature.Subject)
                .AppendLine("Riferimenti: " + string.Join(", ", signature.References)).AppendLine();
        }
        text.AppendLine("ALLEGATI PDF");
        text.AppendLine(document.Attachments.Count == 0 ? "Nessun allegato presente."
            : "Gli allegati sono elencati, ma apertura ed esportazione non sono ancora implementate.");
        foreach (var attachment in document.Attachments)
        {
            budget.Check();
            text.AppendLine(attachment.Name + " · " + attachment.Size + " byte");
        }
        return text.AppendLine().AppendLine("SHA-256 del file originale:").AppendLine(document.SourceSha256).ToString();
    }
}
