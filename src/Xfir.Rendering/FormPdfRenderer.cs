// SPDX-License-Identifier: EUPL-1.2
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using QRCoder;
using Xfir.Core;

namespace Xfir.Rendering;

public sealed record PdfRenderOptions(bool IncludeEmptySecondPage = true);

public sealed class FormPdfRenderer
{
    private static readonly object FontLock = new();
    private static readonly XColor Blue = XColor.FromArgb(47, 82, 147);
    private static readonly XColor Border = XColor.FromArgb(126, 158, 212);
    private static readonly XBrush Ink = new XSolidBrush(XColor.FromArgb(22, 28, 36));
    private static readonly XBrush Label = new XSolidBrush(Blue);
    private readonly List<(string Title, string Text)> overflow = [];
    private readonly Dictionary<(double, bool), XFont> fonts = [];
    private XGraphics g = null!;
    private FormDocument form = null!;
    private PdfDocument pdf = null!;
    private ProcessingBudget budget = null!;
    private QRCodeData? qrData;
    private const double Left = 28, Width = 539;

    public byte[] Render(FormDocument document, PdfRenderOptions? options = null, ProcessingBudget? budget = null)
    {
        this.budget = budget ?? new();
        this.budget.Check();
        g = null!;
        try
        {
            options ??= new();
            lock (FontLock) GlobalFontSettings.FontResolver ??= new WindowsFontResolver();
            form = document;
            overflow.Clear();
            using var result = new PdfDocument();
            pdf = result;
            pdf.Info.Title = "Formulario " + form.Number;
            pdf.Info.Author = "xFirW";
            pdf.Info.Creator = $"xFirW {ProductInfo.Version}";
            pdf.Info.Subject = "Copia di consultazione del FIR digitale - verifica firme non eseguita";
            AddPage("FORMULARIO RIFIUTI");
            MainPage();
            g.Dispose();
            if (options.IncludeEmptySecondPage || form.AdditionalParts.Count > 0)
            {
                AddPage("Integrazione FORMULARIO RIFIUTI   ·   2° foglio");
                SecondPage();
                g.Dispose();
            }
            var supplements = overflow.ToList();
            if (form.SupplementaryFields.Count > 0) supplements.Add(("Campi aggiuntivi del formulario", string.Join("\n", form.SupplementaryFields.Select(f => f.Field + ": " + f.Value))));
            if (form.Warnings.Count > 0) supplements.Add(("Avvertenze di lettura", string.Join("\n", form.Warnings)));
            foreach (var part in form.AdditionalParts)
                supplements.Add(("Dati integrativi · " + part.Name, string.Join("\n", this.budget.GetFields(part).Select(f => f.Field + ": " + (f.Value.Length == 0 ? "(vuoto)" : f.Value)))));
            if (form.Attachments.Count > 0)
                supplements.Add(("Allegati presenti nel contenitore (non inclusi in questa copia)", string.Join("\n", form.Attachments.Select(a => a.Name + " · " + a.Size + " byte"))));
            if (supplements.Count > 0) Appendix(supplements);
            var generatedAt = FormFormatting.LocalTime(DateTimeOffset.UtcNow.ToString("O"));
            for (var i = 0; i < pdf.PageCount; i++)
            {
                this.budget.Check();
                using var footer = XGraphics.FromPdfPage(pdf.Pages[i], XGraphicsPdfPageOptions.Append);
                footer.DrawString($"{form.Number} · xFirW {ProductInfo.Version} · {generatedAt}", Font(7), Ink, new XPoint(Left, 807));
                footer.DrawString($"Pagina {i + 1} di {pdf.PageCount}", Font(7), Ink, new XRect(450, 799, 117, 12), XStringFormats.CenterRight);
                footer.DrawString("Copia di consultazione · Firme e validità della vidimazione non verificate", Font(6.5), Label, new XPoint(Left, 821));
            }
            using var output = new MemoryStream();
            this.budget.Check();
            pdf.Save(output, false);
            this.budget.Check();
            return output.ToArray();
        }
        finally
        {
            g?.Dispose();
            qrData?.Dispose();
            qrData = null;
            this.budget = null!;
        }
    }

    private XFont Font(double size, bool bold = false)
    {
        if (!fonts.TryGetValue((size, bold), out var font))
            fonts[(size, bold)] = font = new XFont("Arial", size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
        return font;
    }

    private void AddPage(string title)
    {
        budget.AddPage();
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        g = XGraphics.FromPdfPage(page);
        var state = g.Save();
        g.TranslateTransform(295, 447);
        g.RotateTransform(-52);
        g.DrawString("COPIA FIR DIGITALE", Font(47, true), new XSolidBrush(XColor.FromArgb(235, 237, 240)),
            new XRect(-320, -32, 640, 64), XStringFormats.Center);
        g.Restore(state);
        Text(title, Left, 35, 315, 22, title.Length > 28 ? 10 : 13, true, color: Label);
        // The header repeats on every page: record its overflow once, before the appendix is assembled.
        var firstPage = pdf.PageCount == 1;
        Cell("DATA EMISSIONE", FormFormatting.Date(form.Departure?.Get("DataEmissione")), 354, 32, 83, 28, 10, track: firstPage);
        Cell("NUMERO FIR", form.Number, 443, 32, 124, 28, 11, true, track: firstPage);
        if (form.IsPartial) Text("MODULO PARZIALE · Consultare l'appendice dati", Left, 60, Width, 10, 6.5, true, color: XBrushes.DarkRed);
    }

    private void MainPage()
    {
        var d = form.Departure;
        string D(string p) => d?.Get(p) ?? "";
        Section("1", "PRODUTTORE / 2 DETENTORE", 68, 95);
        Cell("Denominazione", D("Produttore/Denominazione"), 34, 85, 527, 26);
        Cell("Unità locale", d is null ? "" : FormFormatting.Address(d, "Produttore/Indirizzo"), 34, 112, 527, 23);
        Cell("Codice fiscale", D("Produttore/CodiceFiscale"), 34, 138, 190, 21);
        Cell("Numero iscrizione Albo", D("Produttore/NumeroIscrizioneAlbo"), 230, 138, 165, 21);
        Cell("Autorizzazione / tipo", string.Join(" ", new[] { D("Produttore/Autorizzazione/Numero"), D("Produttore/Autorizzazione/Tipo") }), 401, 138, 160, 21);

        Section("3", "DESTINATARIO", 166, 82);
        Cell("Denominazione", D("Destinatario/Denominazione"), 34, 182, 527, 23);
        Cell("Unità locale", d is null ? "" : FormFormatting.Address(d, "Destinatario/Indirizzo"), 34, 207, 527, 21);
        Cell("Codice fiscale", D("Destinatario/CodiceFiscale"), 34, 230, 123, 15, 6.5);
        Cell("Autorizzazione", D("Destinatario/Autorizzazione/Numero"), 163, 230, 186, 15, 6.5);
        Cell("Tipo", D("Destinatario/Autorizzazione/Tipo"), 355, 230, 134, 15, 6.5);
        Cell("Destinazione", D("Destinatario/Attivita"), 495, 230, 66, 15, 6.5);

        var transport = form.Transports.FirstOrDefault();
        var carrier = form.PrimaryCarrier;
        Section("4", "TRASPORTATORE", 251, 54);
        Cell("Denominazione", carrier?.Get("Denominazione")
            ?? (transport is null ? "" : "Anagrafica non collegata: consultare l'appendice"), 34, 267, 527, 19);
        Cell("Codice fiscale", carrier?.Get("CodiceFiscale") ?? "", 34, 287, 260, 15, 6.5);
        Cell("Numero iscrizione Albo", carrier?.Get("NumeroIscrizioneAlbo") ?? "", 300, 287, 170, 15, 6.5);
        Cell("Tipo trasporto", carrier?.Get("TipoTrasporto") ?? "", 476, 287, 85, 15, 6.5);

        Section("5", "INTERMEDIARIO O COMMERCIANTE", 308, 40);
        var intermediaries = d?.Children("Intermediari").ToList() ?? [];
        var intermediary = intermediaries.FirstOrDefault();
        Cell("Denominazione", intermediary?.Get("Denominazione") ?? "", 34, 324, 275, 20);
        Cell("Codice fiscale / iscrizione Albo", string.Join(" · ", new[] { intermediary?.Get("CodiceFiscale"), intermediary?.Get("NumeroIscrizioneAlbo") }.Where(s => !string.IsNullOrEmpty(s))), 315, 324, 246, 20);
        foreach (var extra in intermediaries.Skip(1)) overflow.Add(("Ulteriore intermediario", string.Join("\n", budget.GetFields(extra).Select(f => f.Field + ": " + f.Value))));

        Section("6", "CARATTERISTICHE DEL RIFIUTO", 351, 129);
        Cell("Codice EER", FormFormatting.Eer(form.WasteCode), 34, 368, 125, 23, 10);
        Cell("Stato fisico", D("Rifiuto/StatoFisico"), 165, 368, 70, 23);
        Cell("Caratteristiche di pericolo", string.Join(", ", d?.Children("Rifiuto/ClassiPericolo").Select(p => p.Root.Value) ?? []), 241, 368, 205, 23);
        Cell("Provenienza", D("Rifiuto/Provenienza") switch { "S" => "Speciale", "U" => "Urbano", var p => p }, 452, 368, 109, 23);
        Cell("Descrizione", D("Rifiuto/Descrizione"), 34, 393, 527, 18);
        Cell("Quantità", form.Quantity + " " + form.Unit, 34, 414, 125, 23, 10);
        Cell("Peso verificato in partenza", FormFormatting.YesNo(D("Rifiuto/VerificatoInPartenza")), 165, 414, 170, 23);
        Cell("Numero colli / contenitori", D("Rifiuto/NumeroColli"), 341, 414, 128, 23);
        Cell("Alla rinfusa", FormFormatting.YesNo(D("Rifiuto/Rinfusa")), 475, 414, 86, 23);
        Cell("Caratteristiche chimico-fisiche", D("Rifiuto/CaratteristicheChimicoFisiche"), 34, 439, 527, 16, 6.5);
        Text("Trasporto ADR / RID", 34, 459, 85, 10, 6.5, color: Label);
        Check(121, 460, D("Rifiuto/TrasportoADR") is "true" or "1");
        Text("Classe " + D("Rifiuto/DatiADR/Classe") + "   ONU " + D("Rifiuto/DatiADR/NumeroONU"), 139, 459, 160, 10, 7,
            overflowTitle: "Classe ADR / numero ONU");
        // Notes have a separate generous band, avoiding truncation in the tightly packed ADR row.
        Section("", "NOTE ADR / RID", 483, 40);
        Text(D("Rifiuto/DatiADR/Note"), 34, 499, 527, 21, 6.5, overflowTitle: "Note ADR / RID");

        Section("9", "TRASPORTO", 526, 38);
        string T(string p) => transport?.Get("TrasportoTerrestre/" + p) ?? "";
        Cell("Targa automezzo", T("TargaAutomezzo"), 34, 542, 145, 18);
        Cell("Targa rimorchio", T("TargaRimorchio"), 185, 542, 145, 18);
        Cell("Percorso", T("Percorso"), 336, 542, 225, 18);
        Section("8", "CONDUCENTE E INIZIO TRASPORTO", 567, 40);
        Cell("Cognome e nome", (T("Conducente/Cognome") + " " + T("Conducente/Nome")).Trim(), 34, 583, 290, 20);
        Cell("Data e ora (Italia)", FormFormatting.LocalTime(T("DataOraInizioTrasporto")), 330, 583, 231, 20);
        Section("11 / 7", "DATI DELLE FIRME · TRASPORTATORE E PRODUTTORE", 610, 38);
        Text(Signature("signatures-trasporto001.xml"), 34, 626, 257, 19, 6.5, overflowTitle: "Firma del trasportatore");
        Text(Signature("signatures-produttore.xml"), 302, 626, 259, 19, 6.5, overflowTitle: "Firma del produttore");

        var a = form.Acceptance;
        Section("12", "RISERVATO AL DESTINATARIO", 651, 63);
        Check(35, 669, a?.Get("TipoAccettazione") == "A"); Text("Accettato per intero", 47, 668, 133, 10, 7);
        Check(186, 669, a?.Get("TipoAccettazione") == "P"); Text("Accettato parzialmente", 198, 668, 157, 10, 7);
        Check(365, 669, a?.Get("TipoAccettazione") == "R"); Text("Respinto", 377, 668, 115, 10, 7);
        Cell("Quantità accettata", FormFormatting.Quantity(a?.Get("QuantitaAccettata")) + " " + (a?.Attribute("QuantitaAccettata", "unitaMisura") ?? ""), 34, 681, 118, 19);
        Cell("Data e ora arrivo (Italia)", FormFormatting.LocalTime(a?.Get("DataOraArrivo")), 158, 681, 179, 19);
        Cell("Dati firma destinatario", Signature("signatures-accettazione.xml"), 343, 681, 218, 29, 6);
        Text("Verifica analitica: " + FormFormatting.YesNo(a?.Get("AttesaVerificaAnalitica") ?? "") + "    " + (a?.Get("MotivoRespingimento") ?? ""), 34, 702, 298, 9, 6, overflowTitle: "Accettazione / respingimento");
        Section("17", "ANNOTAZIONI", 717, 47);
        Text(D("Annotazioni"), 34, 733, 430, 26, 7, overflowTitle: "Annotazioni del formulario");
        Endorsement(767, qrTop: 718);

    }

    private void SecondPage()
    {
        Section("13", "TRASBORDO PARZIALE / FRAZIONAMENTO DEL CARICO", 68, 171);
        for (var i = 0; i < 3; i++)
        {
            var y = 85 + i * 50;
            Cell("Denominazione", "", 34, y, 527, 22);
            Cell("Codice fiscale / numero iscrizione Albo", "", 34, y + 24, 260, 20);
            Cell("Riferimento formulario / quantità residua", "", 300, y + 24, 261, 20);
        }
        Section("14", "TRASBORDO TOTALE", 242, 108);
        Cell("Denominazione del nuovo trasportatore", "", 34, 260, 527, 23);
        Cell("Codice fiscale / numero iscrizione Albo", "", 34, 286, 527, 23);
        Cell("Targa automezzo / rimorchio", "", 34, 312, 260, 32);
        Cell("Conducente / data e ora presa in carico", "", 300, 312, 261, 32);
        Section("15", "SOSTA TECNICA", 353, 154);
        for (var i = 0; i < 3; i++)
        {
            var y = 371 + i * 43;
            Cell("Luogo di stazionamento", "", 34, y, 527, 18);
            Cell("Sospensione del trasporto · data e ora", "", 34, y + 20, 260, 19);
            Cell("Ripresa del trasporto · data e ora", "", 300, y + 20, 261, 19);
        }
        Section("16", "SECONDO DESTINATARIO", 510, 139);
        Cell("Denominazione", "", 34, 528, 527, 23);
        Cell("Unità locale", "", 34, 554, 527, 23);
        Cell("Codice fiscale / autorizzazione", "", 34, 580, 260, 23);
        Cell("Destinazione / tipo", "", 300, 580, 261, 23);
        Cell("Quantità accettata / data e ora arrivo", "", 34, 607, 260, 35);
        Cell("Dati firma destinatario", "", 300, 607, 261, 35);
        Section("17", "ANNOTAZIONI (SEGUE)", 652, 112);
        if (form.AdditionalParts.Count > 0)
            Text("Gli eventi integrativi presenti nell'XFIR sono riportati nell'appendice dati delle pagine successive. I riquadri di questo foglio non sono compilati da xFirW.", 34, 673, 427, 55, 9, true);
        Endorsement(767, qrTop: 688);
    }

    private void Endorsement(double y, double qrTop)
    {
        g.DrawRectangle(new XPen(Border, .4), Left, y, Width, 27);
        var text = "Vid.Virt. del " + FormFormatting.LocalTime(form.Endorsement.Get("DataRichiesta"))
            + " per conto della " + form.Endorsement.Get("CCIAANome") + ", rich. da " + form.Endorsement.Get("CodiceFiscaleSoggetto")
            + " - " + form.Endorsement.Get("DenominazioneSoggetto");
        Text(text, 32, y + 3, 263, 21, 5.3, overflowTitle: "Vidimazione");
        Text(form.Number, 307, y + 7, 164, 16, 10, true, color: Label);
        g.DrawRectangle(XBrushes.White, 480, qrTop, 82, 76);
        if (form.QrPayload is not null)
        {
            if (qrData is null)
            {
                using var generator = new QRCodeGenerator();
                qrData = generator.CreateQrCode(Base45.Encode(form.QrPayload), QRCodeGenerator.ECCLevel.H);
            }
            // Drawing QR modules as vector squares preserves sharp edges at all print resolutions.
            var matrix = qrData.ModuleMatrix;
            var module = 74.0 / matrix.Count;
            for (var row = 0; row < matrix.Count; row++)
            {
                budget.Check();
                for (var col = 0; col < matrix.Count; col++)
                    if (matrix[row][col]) g.DrawRectangle(XBrushes.Black, 484 + col * module, qrTop + row * module, module, module);
            }
        }
        else Text("QR assente", 484, qrTop + 27, 74, 18, 8, color: XBrushes.DarkRed);
    }

    private string Signature(string suffix)
    {
        var s = form.Signatures.FirstOrDefault(s => s.FileName.EndsWith('/' + suffix, StringComparison.OrdinalIgnoreCase));
        if (s is null && suffix is "signatures-trasporto001.xml" or "signatures-produttore.xml")
            s = form.Signatures.FirstOrDefault(s => s.FileName.EndsWith("/signatures-produttore-trasportatore.xml", StringComparison.OrdinalIgnoreCase));
        return s is null ? "Firma non presente" : s.Signer + " · " + FormFormatting.Date(s.SignedAt);
    }

    private void Section(string number, string name, double y, double height)
    {
        g.DrawRectangle(new XPen(Border, .45), Left, y, Width, height);
        g.DrawRectangle(new XSolidBrush(XColor.FromArgb(243, 246, 251)), Left, y, Width, 14);
        if (number.Length > 0)
        {
            g.DrawRectangle(new XSolidBrush(Blue), Left, y, 29, 14);
            Text(number, Left + 2, y + 2, 25, 10, 7, true, color: XBrushes.White);
        }
        Text(name, Left + 34, y + 2, Width - 40, 10, 7, true, color: Label);
    }

    private void Cell(string label, string value, double x, double y, double width, double height, double size = 7.5, bool bold = false, bool track = true)
    {
        g.DrawRectangle(new XPen(Border, .3), x, y, width, height);
        Text(label, x + 3, y + 1, width - 6, 8, 5.6, color: Label);
        var valueTop = height < 19 ? 7 : 9;
        Text(value, x + 3, y + valueTop, width - 6, Math.Max(height - valueTop - 1, 7), size, bold, overflowTitle: track ? label : null);
    }

    private void Check(double x, double y, bool selected)
    {
        g.DrawRectangle(new XPen(Border, .5), XBrushes.White, x, y, 8, 8);
        if (!selected) return;
        var pen = new XPen(XColors.Black, 1.2);
        g.DrawLines(pen, [new XPoint(x + 1.4, y + 4), new XPoint(x + 3.3, y + 6.3), new XPoint(x + 6.8, y + 1.8)]);
    }

    private void Text(string value, double x, double y, double width, double height, double size = 7.5, bool bold = false, string? overflowTitle = null, XBrush? color = null)
    {
        if (string.IsNullOrEmpty(value)) return;
        var font = Font(size, bold);
        var lineHeight = size * 1.18;
        var maxLines = Math.Max(1, (int)(height / lineHeight));
        var lines = Wrap(value, width, font).Take(maxLines + 1).ToList();
        if (lines.Count > maxLines)
        {
            if (overflowTitle is not null) overflow.Add((overflowTitle, value));
            // Only promise a continuation when the complete value has been registered.
            var tail = overflowTitle is null ? " …" : " … [segue]";
            var final = lines[maxLines - 1];
            while (final.Length > 0 && g.MeasureString(final + tail, font).Width > width) final = final[..^1];
            lines[maxLines - 1] = final + tail;
        }
        for (var i = 0; i < Math.Min(maxLines, lines.Count); i++)
            g.DrawString(lines[i], font, color ?? Ink, new XPoint(x, y + size + i * lineHeight));
    }

    private IEnumerable<string> Wrap(string text, double width, XFont font)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            budget.Check();
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                budget.Check();
                var candidate = line.Length > 0 ? line + " " + word : word;
                if (g.MeasureString(candidate, font).Width <= width) { line = candidate; continue; }
                if (line.Length > 0)
                {
                    yield return line;
                    line = "";
                    if (g.MeasureString(word, font).Width <= width) { line = word; continue; }
                }
                foreach (var character in word)
                {
                    budget.Check();
                    if (line.Length > 0 && g.MeasureString(line + character, font).Width > width) { yield return line; line = ""; }
                    line += character;
                }
            }
            yield return line;
        }
    }

    private void Appendix(List<(string Title, string Text)> supplements)
    {
        AddPage("APPENDICE · DATI E AVVERTENZE");
        double y = 78;
        foreach (var item in supplements)
        {
            if (y > 731) { g.Dispose(); AddPage("APPENDICE · SEGUE"); y = 78; }
            Text(item.Title, Left, y, Width, 30, 9, true, color: Label);
            y += 29;
            var lines = Wrap(item.Text, Width - 8, Font(8));
            foreach (var line in lines)
            {
                if (y > 776) { g.Dispose(); AddPage("APPENDICE · SEGUE"); y = 78; }
                g.DrawString(line, Font(8), Ink, new XPoint(Left + 4, y + 8));
                y += 11;
            }
            y += 15;
        }
        g.Dispose();
    }
}
