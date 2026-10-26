// SPDX-License-Identifier: EUPL-1.2
using MimeKit;
using MimeKit.Cryptography;

namespace Xfir.Mail;

public static class MessagePolicy
{
    public static string? AuthorizedSender(MimeMessage message, Settings settings)
    {
        // Duplicate identity headers and ambiguous From lists fail closed.
        if (message.Headers.Count(x => x.Id == HeaderId.From) != 1 || message.From.Count != 1 ||
            message.From[0] is not MailboxAddress from || !settings.IsSenderAllowed(from.Address) ||
            message.Headers.Contains("X-XfirMail") || message.Headers.Contains(HeaderId.ListId) ||
            message.Headers.Contains(HeaderId.ListUnsubscribe) || message.Headers.Contains("X-Auto-Response-Suppress") ||
            message.Headers.Contains(HeaderId.Precedence) || message.Headers.Contains(HeaderId.ReturnReceiptTo) ||
            message.Body?.ContentType.IsMimeType("multipart", "report") == true ||
            message.Headers.Any(h => h.Id == HeaderId.AutoSubmitted && !h.Value.Trim().Equals("no", StringComparison.OrdinalIgnoreCase)) ||
            message.BodyParts.Any(p => p.ContentType.IsMimeType("message", "delivery-status") || p.ContentType.IsMimeType("multipart", "report")) ||
            message.Headers.Any(h => h.Id == HeaderId.ReturnPath && h.Value.Replace(" ", "").Trim() == "<>"))
            return null;

        if (!settings.RequireDmarc) return from.Address;

        var trusted = settings.TrustedAuthservId.Length == 0 ? settings.Imap.Server : settings.TrustedAuthservId;
        var domain = from.Address[(from.Address.LastIndexOf('@') + 1)..];
        var results = message.Headers.Where(h => h.Id == HeaderId.AuthenticationResults)
            .Select(h => AuthenticationResults.TryParse(h.RawValue, out var result) ? result : null)
            .Where(r => r is not null && string.Equals(r.AuthenticationServiceIdentifier, trusted, StringComparison.OrdinalIgnoreCase)).ToList();
        // The receiving MTA MUST strip incoming headers claiming its authserv-id before inserting its own.
        if (results.Count != 1) return null;
        var dmarc = results[0]!.Results.Where(r => string.Equals(r.Method, "dmarc", StringComparison.OrdinalIgnoreCase)).ToList();
        if (dmarc.Count != 1 || !string.Equals(dmarc[0].Result, "pass", StringComparison.OrdinalIgnoreCase)) return null;
        var fromProperties = dmarc[0].Properties.Where(p => p.Property == "from" && p.PropertyType == "header").ToList();
        if (fromProperties.Count != 1 || !fromProperties[0].Value.Equals(domain, StringComparison.OrdinalIgnoreCase)) return null;
        return from.Address;
    }

    public static List<MimePart> Attachments(MimeMessage message)
    {
        var parts = message.BodyParts.Where(p => p.IsAttachment || !string.IsNullOrEmpty(p.ContentDisposition?.FileName) ||
            !string.IsNullOrEmpty(p.ContentType.Name)).ToList();
        if (parts.Count is < 1 or > Settings.MaxAttachments || parts.Any(p => p is not MimePart ||
            !(((MimePart)p).FileName?.EndsWith(".xfir", StringComparison.OrdinalIgnoreCase) ?? false)))
            throw new InputRejectedException();
        return parts.Cast<MimePart>().ToList();
    }
}

public sealed class InputRejectedException : Exception;
