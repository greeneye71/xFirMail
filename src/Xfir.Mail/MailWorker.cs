// SPDX-License-Identifier: EUPL-1.2
using System.Security.Cryptography;
using System.Text;
using MimeKit;
using Xfir.Core;

namespace Xfir.Mail;

public sealed class MailWorker(Settings settings, RequestStore store, IConverter converter, IReplySender sender, Action<string> log)
{
    public async Task PollAsync(IInbox inbox, CancellationToken token)
    {
        await SendReadyAsync(token);
        store.Cleanup(settings.RetentionDays, DateTimeOffset.UtcNow);
        foreach (var pending in store.All().Where(r => r.Stage == RequestStage.Received && r.NextAttempt <= DateTimeOffset.UtcNow))
        {
            if (store.OutboxBytes >= 256L * 1024 * 1024) break;
            if (pending.Scope != inbox.Scope)
            { pending.Stage = RequestStage.Failed; pending.Reason = "mailbox-identity-changed"; store.Save(pending); }
            else await PrepareAsync(inbox, pending, token);
        }
        var batch = await inbox.NextAsync(store.Cursor(inbox.Scope), token);
        foreach (var item in batch.Items)
        {
            token.ThrowIfCancellationRequested();
            var id = RequestStore.Hash(inbox.Scope + ":" + item.Uid);
            if (store.Find(id) is null)
            {
                if (store.All().Count >= 10000 || store.OutboxBytes >= 256L * 1024 * 1024)
                { log("Acquisizione sospesa: limite locale di stato/outbox raggiunto."); return; }
                var record = new RequestRecord(id, inbox.Scope, item.Uid, DateTimeOffset.UtcNow);
                if (item.Size > Settings.MaxMessageBytes) { record.Stage = RequestStage.Rejected; record.Reason = "message-size"; }
                store.Save(record); // Persist ownership before advancing the cursor.
                if (record.Stage == RequestStage.Received) await PrepareAsync(inbox, record, token);
            }
            store.Advance(inbox.Scope, item.Uid);
        }
        store.Advance(inbox.Scope, batch.Through);
        await SendReadyAsync(token);
        store.Cleanup(settings.RetentionDays, DateTimeOffset.UtcNow);
    }

    private async Task PrepareAsync(IInbox inbox, RequestRecord record, CancellationToken token)
    {
        try
        {
            if (record.ConvertAttempts >= Settings.MaxAttempts)
            { record.Stage = RequestStage.Failed; record.Reason = "conversion-attempts-exhausted"; store.Save(record); log($"request={record.Id} stage=Failed reason=conversion-attempts-exhausted"); return; }
            record.ConvertAttempts++;
            store.Save(record);
            var raw = await inbox.ReadAsync(record.Uid, token);
            if (raw.Length > Settings.MaxMessageBytes) throw new InputRejectedException();
            using var stream = new MemoryStream(raw, writable: false);
            using var message = await MimeMessage.LoadAsync(new ParserOptions { MaxMimeDepth = 32 }, stream, token);
            var authorized = MessagePolicy.AuthorizedSender(message, settings);
            if (authorized is null) { Reject(record, "sender-or-loop"); return; }
            // Ignore Message-Id for identity: it is controlled by the sender. Hash the full bounded MIME input.
            var hash = Convert.ToHexString(SHA256.HashData(raw));
            if (store.Duplicate(authorized, hash, record.Id)) { Reject(record, "duplicate"); return; }
            if (record.Sender is null && store.ExceedsQuota(authorized, settings.DailyQuota, DateTimeOffset.UtcNow))
            { Reject(record, "quota"); return; }
            record.Sender = authorized; record.ContentHash = hash; store.Save(record);
            var attachments = MessagePolicy.Attachments(message);
            var builder = new BodyBuilder { TextBody = "Copia di consultazione XFIR. Firme e validità della vidimazione non verificate. Conservare l'originale XFIR." };
            var warnings = new StringBuilder();
            long total = 0;
            for (var i = 0; i < attachments.Count; i++)
            {
                var converted = await converter.ConvertAsync(attachments[i], token);
                total += converted.Pdf.Length;
                if (total > 16 * 1024 * 1024) throw new InputRejectedException();
                // Never reuse sender-controlled filenames or subjects.
                builder.Attachments.Add($"formulario-{i + 1}.pdf", converted.Pdf, ContentType.Parse("application/pdf"));
                if (converted.Warnings.Length > 0) warnings.AppendLine($"Documento {i + 1}:").AppendLine(converted.Warnings);
            }
            if (warnings.Length > 0) builder.Attachments.Add("avvertenze.txt", Encoding.UTF8.GetBytes(warnings.ToString()), ContentType.Parse("text/plain"));
            using var reply = new MimeMessage();
            reply.From.Add(MailboxAddress.Parse(settings.Smtp.Account));
            reply.To.Add(MailboxAddress.Parse(authorized));
            reply.Subject = "xFirMail - copia PDF";
            reply.MessageId = $"{record.Id}@{settings.Smtp.Account.Split('@')[1]}";
            reply.Date = DateTimeOffset.UtcNow;
            reply.Headers.Add(HeaderId.AutoSubmitted, "auto-replied");
            reply.Headers.Add("X-Auto-Response-Suppress", "All");
            reply.Headers.Add("X-XfirMail", "1");
            reply.Body = builder.ToMessageBody();
            DurableFiles.Write(store.Outbox(record.Id), s => reply.WriteTo(s), token);
            record.Stage = RequestStage.Ready; record.Reason = ""; store.Save(record);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception e) when (e is InputRejectedException or FormatException)
        { Reject(record, "invalid-input"); }
        catch (Exception)
        {
            record.Stage = record.ConvertAttempts >= Settings.MaxAttempts ? RequestStage.Failed : RequestStage.Received;
            record.Reason = "read-or-convert"; record.NextAttempt = DateTimeOffset.UtcNow.AddMinutes(5 * record.ConvertAttempts); store.Save(record);
        }
        log($"request={record.Id} stage={record.Stage}");
    }
    private void Reject(RequestRecord record, string reason)
    { record.Stage = RequestStage.Rejected; record.Reason = reason; store.Save(record); log($"request={record.Id} stage=Rejected reason={reason}"); }

    public async Task SendReadyAsync(CancellationToken token)
    {
        foreach (var record in store.All().Where(r => r.Stage == RequestStage.Ready && r.NextAttempt <= DateTimeOffset.UtcNow))
        {
            token.ThrowIfCancellationRequested();
            MimeMessage prepared;
            try { prepared = await MimeMessage.LoadAsync(store.Outbox(record.Id), token); }
            catch (Exception e) when (e is IOException or FormatException)
            { record.Stage = RequestStage.Failed; record.Reason = "outbox-unreadable"; store.Save(record); log($"request={record.Id} stage=Failed reason=outbox-unreadable"); continue; }
            using var message = prepared;
            if (record.Sender is null || !settings.AllowedSenders.Contains(record.Sender, StringComparer.OrdinalIgnoreCase) ||
                message.To.Count != 1 || message.To[0] is not MailboxAddress to || !to.Address.Equals(record.Sender, StringComparison.OrdinalIgnoreCase) ||
                message.Cc.Count != 0 || message.Bcc.Count != 0 || message.From.Count != 1 ||
                message.From[0] is not MailboxAddress from || !from.Address.Equals(settings.Smtp.Account, StringComparison.OrdinalIgnoreCase))
            { Reject(record, "recipient-policy"); continue; }
            record.SendAttempts++; record.Stage = RequestStage.Sending; store.Save(record);
            SendResult result;
            try { result = await sender.SendAsync(message, token); }
            catch (Exception) { result = SendResult.Uncertain; }
            record.Stage = result switch
            {
                SendResult.Sent => RequestStage.Sent,
                SendResult.Uncertain => RequestStage.Uncertain,
                SendResult.Rejected => RequestStage.Failed,
                _ => record.SendAttempts >= Settings.MaxAttempts ? RequestStage.Failed : RequestStage.Ready
            };
            record.Reason = result.ToString();
            record.NextAttempt = DateTimeOffset.UtcNow.AddMinutes(5 * record.SendAttempts); store.Save(record);
            if (record.Stage != RequestStage.Ready) File.Delete(store.Outbox(record.Id));
            log($"request={record.Id} stage={record.Stage}");
        }
    }
}
