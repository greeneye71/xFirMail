// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Text;
using System.Reflection;
using Xfir.Core;
using Xfir.Rendering;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using Xunit;

namespace Xfir.Tests;

public class ReaderTests
{
    [Fact]
    public void ReadsIncompleteDocumentWithoutInventingAcceptance()
    {
        var form = new XfirReader().Read(Sample());
        Assert.Equal("TESTX 000001 AA", form.Number);
        Assert.Equal("12,5", form.Quantity);
        Assert.Equal("kg", form.Unit);
        Assert.Null(form.Acceptance);
        Assert.Contains("accettazione assente", form.State);
        Assert.Empty(form.Warnings);
    }

    [Theory]
    [InlineData("../outside.xml")]
    [InlineData("META-INF/../../outside.xml")]
    [InlineData("C:/outside.xml")]
    [InlineData("/outside.xml")]
    [InlineData("folder\\outside.xml")]
    public void RejectsUnsafeArchivePaths(string path) => Assert.Throws<XfirReadException>(() => new XfirReader().Read(Sample(extraName: path)));

    [Fact]
    public void RejectsCaseInsensitiveDuplicateEntries() => Assert.Throws<XfirReadException>(() => new XfirReader().Read(Sample(extraName: "PARTENZA.XML")));

    [Fact]
    public void RejectsDtdAndExternalEntities() => Assert.Throws<XfirReadException>(() => new XfirReader().Read(Sample(departure: "<!DOCTYPE x [<!ENTITY xxe SYSTEM 'file:///C:/secret'>]><DatiPartenza>&xxe;</DatiPartenza>")));

    [Fact]
    public void RejectsDifferentFirNumber() => Assert.Throws<XfirReadException>(() => new XfirReader().Read(Sample(departure: Departure.Replace("TESTX 000001 AA", "OTHER 000002 BB"))));

    [Fact]
    public void ReportsDanglingCarrierReference()
    {
        var form = new XfirReader().Read(Sample(transport: Transport.Replace("idRef=\"1\"", "idRef=\"9\"")));
        Assert.Contains(form.Warnings, w => w.Contains("anagrafica"));
    }

    [Fact]
    public void PreservesAdditionalEventsInAppendix()
    {
        var form = new XfirReader().Read(Sample(extraName: "annotazione001.xml", extra: "<AnnotazioneAggiuntiva xmlns='urn:it:rentri:formulari:1.0'><Testo>Nota sintetica</Testo></AnnotazioneAggiuntiva>"));
        Assert.True(form.IsPartial);
        Assert.Equal("Nota sintetica", Assert.Single(form.AdditionalParts).Get("Testo"));
        using var pdf = PdfReader.Open(new MemoryStream(new FormPdfRenderer().Render(form)), PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount >= 3);
    }

    [Fact]
    public void WarnsAboutMissingQrWithoutInventingOne()
    {
        var form = new XfirReader().Read(Sample(includeQr: false));
        Assert.Null(form.QrPayload);
        Assert.Contains(form.Warnings, w => w.Contains("QR"));
    }

    [Theory]
    [InlineData("2026-09-18T12:58:55Z", "18/09/2026 14:58")]
    [InlineData("2026-01-18T12:58:55Z", "18/01/2026 13:58")]
    [InlineData("2026-09-18T14:58:55+02:00", "18/09/2026 14:58")]
    [InlineData("2026-03-29T00:30:00Z", "29/03/2026 01:30")]
    [InlineData("2026-03-29T01:30:00Z", "29/03/2026 03:30")]
    public void ConvertsInstantsToItalianTime(string input, string expected) => Assert.Equal(expected, FormFormatting.LocalTime(input));

    [Fact]
    public void DoesNotGuessUnqualifiedTime() => Assert.Contains("fuso non indicato", FormFormatting.LocalTime("2026-01-18T12:00:00"));

    [Theory]
    [InlineData("AB", "BB8")]
    [InlineData("Hello!!", "%69 VD92EX0")]
    [InlineData("base-45", "UJCLQE7W581")]
    public void Base45MatchesRfcVectors(string input, string expected) => Assert.Equal(expected, Base45.Encode(Encoding.ASCII.GetBytes(input)));

    [Fact]
    public void RenderingDoesNotChangeSourceAndCreatesA4Pages()
    {
        var bytes = Sample();
        var original = bytes.ToArray();
        var form = new XfirReader().Read(bytes);
        var output = new FormPdfRenderer().Render(form);
        Assert.Equal(original, bytes);
        using var pdf = PdfReader.Open(new MemoryStream(output), PdfDocumentOpenMode.Import);
        Assert.Equal(2, pdf.PageCount);
        Assert.InRange(pdf.Pages[0].Width.Point, 595, 596);
        Assert.InRange(pdf.Pages[0].Height.Point, 841, 843);
    }

    [Fact]
    public void PdfVersionFollowsBuildMetadata()
    {
        var expected = typeof(FormPdfRenderer).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        Assert.Equal(expected, ProductInfo.Version);

        using var pdf = PdfReader.Open(new MemoryStream(new FormPdfRenderer().Render(new XfirReader().Read(Sample()))), PdfDocumentOpenMode.Import);
        Assert.Equal($"xFirMail {expected}", pdf.Info.Creator);
        foreach (var page in pdf.Pages)
        {
            var text = ContentReader.ReadContent(page).OfType<COperator>()
                .Where(op => op.OpCode.Name == "Tj")
                .SelectMany(op => op.Operands.OfType<CString>()).Select(value => value.Value);
            Assert.Contains(text, value => value.Contains($"xFirMail {expected}"));
        }
    }

    [Fact]
    public void LongNotesCreateSupplementaryPages()
    {
        var text = string.Join(' ', Enumerable.Repeat("Annotazione molto lunga con dati da conservare.", 150));
        var form = new XfirReader().Read(Sample(departure: Departure.Replace("<Annotazioni />", "<Annotazioni>" + text + "</Annotazioni>")));
        using var pdf = PdfReader.Open(new MemoryStream(new FormPdfRenderer().Render(form)), PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount > 2);
    }

    [Fact]
    public void UnknownNestedDataIsNotSilentlyOmitted()
    {
        var form = new XfirReader().Read(Sample(departure: Departure.Replace("</Rifiuto>", "<CampoFuturo>Da conservare</CampoFuturo></Rifiuto>")));
        Assert.True(form.IsPartial);
        Assert.Contains(form.SupplementaryFields, f => f.Value == "Da conservare");
    }

    [Fact]
    public void RejectsMissingManifestFile()
    {
        var bytes = Sample(extraName: "missing.xml", omitExtra: true);
        Assert.Throws<XfirReadException>(() => new XfirReader().Read(bytes));
    }

    [Fact]
    public void AcceptsSignatureOverEnclosingDocument()
    {
        const string signature = "<ds:Signature xmlns:ds='http://www.w3.org/2000/09/xmldsig#'><ds:SignedInfo>"
            + "<ds:Reference URI=''/><ds:Reference URI='#props'/><ds:Reference URI='partenza.xml'/></ds:SignedInfo></ds:Signature>";
        var form = new XfirReader().Read(Sample(extraName: "META-INF/signatures001.xml", extra: signature));
        Assert.Equal(new[] { "", "#props", "partenza.xml" }, Assert.Single(form.Signatures).References);
        Assert.DoesNotContain(form.Warnings, w => w.Contains("firma"));
    }

    [Fact]
    public void FormatsDatesAsItalianDayMonthYear() => Assert.Equal("18/09/2026", FormFormatting.Date("2026-09-18"));

    private const string Departure = """
        <DatiPartenza xmlns="urn:it:rentri:formulari:1.0">
        <DataEmissione>2026-09-18</DataEmissione><NumeroFIR>TESTX 000001 AA</NumeroFIR>
        <Produttore><Denominazione>Produttore dimostrativo</Denominazione><CodiceFiscale>00000000000</CodiceFiscale></Produttore>
        <Destinatario><Denominazione>Destinatario dimostrativo</Denominazione></Destinatario>
        <Trasportatori><Trasportatore id="1"><Denominazione>Trasportatore dimostrativo</Denominazione><TipoTrasporto>Terrestre</TipoTrasporto></Trasportatore></Trasportatori>
        <Rifiuto><CodiceEER>150101</CodiceEER><Quantita unitaMisura="kg">12.500</Quantita><TrasportoADR>false</TrasportoADR></Rifiuto>
        <Annotazioni /></DatiPartenza>
        """;
    private const string Transport = """
        <Trasporto idRef="1" xmlns="urn:it:rentri:formulari:1.0"><TrasportoTerrestre><TargaAutomezzo>XX000XX</TargaAutomezzo><DataOraInizioTrasporto>2026-09-18T12:58:55Z</DataOraInizioTrasporto></TrasportoTerrestre></Trasporto>
        """;
    internal static byte[] Sample(string? departure = null, string? transport = null, string? extraName = null, string extra = "<extra />", bool includeQr = true, bool omitExtra = false, bool includeTransport = true)
    {
        var files = new List<(string Name, byte[] Content)>
        {
            ("mimetype", Encoding.ASCII.GetBytes("application/vnd.etsi.asic-e+zip")),
            ("TESTX 000001 AA.xml", Encoding.UTF8.GetBytes("<eFIR xmlns='urn:it:rentri:vidimazione-fir:1.0'><NumeroFir>TESTX 000001 AA</NumeroFir></eFIR>")),
            ("partenza.xml", Encoding.UTF8.GetBytes(departure ?? Departure))
        };
        if (includeTransport) files.Add(("trasporto001.xml", Encoding.UTF8.GetBytes(transport ?? Transport)));
        if (includeQr) files.Add(("TESTX 000001 AA.cbor", [0xA1, 0x00, 0x01]));
        if (extraName is not null) files.Add((extraName, Encoding.UTF8.GetBytes(extra)));
        var manifest = "<manifest:manifest xmlns:manifest='urn:oasis:names:tc:opendocument:xmlns:manifest:1.0'>"
            + "<manifest:file-entry manifest:full-path='/' manifest:media-type='application/vnd.etsi.asic-e+zip'/>"
            + string.Join("", files.Skip(1).Select(f => "<manifest:file-entry manifest:full-path='" + f.Name + "' manifest:media-type='text/xml'/>")) + "</manifest:manifest>";
        files.Add(("META-INF/manifest.xml", Encoding.UTF8.GetBytes(manifest)));
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var file in files.Where(f => !omitExtra || f.Name != extraName))
            { using var stream = zip.CreateEntry(file.Name).Open(); stream.Write(file.Content); }
        return output.ToArray();
    }
}
