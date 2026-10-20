// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Xml.Linq;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using Xfir.Core;
using Xfir.Rendering;
using Xunit;

namespace Xfir.Tests;

public class BlockARegressionTests
{
    private static readonly XNamespace Ns = XfirReader.FormNamespace;

    [Theory]
    [InlineData("site", "AUDIT_ISCR_SITO")]
    [InlineData("attribute", "AUDIT_ATTR_PERSO")]
    [InlineData("duplicate", "AUDIT_SECONDO_PRODUTTORE")]
    [InlineData("duplicate-parent", "AUDIT_ALTRO_PRODUTTORE")]
    public void PreservesUnrepresentedOccurrencesInPdf(string scenario, string sentinel)
    {
        var form = Read(departure =>
        {
            switch (scenario)
            {
                case "site":
                    departure.Element(Ns + "Trasportatori")!.Element(Ns + "Trasportatore")!
                        .Add(new XElement(Ns + "NumIscrSito", sentinel));
                    break;
                case "attribute":
                    departure.Element(Ns + "Rifiuto")!.SetAttributeValue("datoExtra", sentinel);
                    break;
                case "duplicate":
                    departure.Element(Ns + "Produttore")!.Add(new XElement(Ns + "Denominazione", sentinel));
                    break;
                case "duplicate-parent":
                    departure.Add(new XElement(Ns + "Produttore", new XElement(Ns + "Denominazione", sentinel)));
                    break;
            }
        });

        Assert.Contains(form.SupplementaryFields, field => field.Value == sentinel);
        Assert.True(form.IsPartial);
        Assert.NotEmpty(form.Warnings);
        var pages = PdfText(Render(form, scenario));
        Assert.True(pages.Length >= 3);
        Assert.Contains(sentinel, string.Join('\n', pages));
    }

    [Fact]
    public void KeepsDuplicateQuantityAndItsUnit()
    {
        var form = Read(departure => departure.Element(Ns + "Rifiuto")!.Add(
            new XElement(Ns + "Quantita", new XAttribute("unitaMisura", "AUDIT_UNITA"), "99.500")));
        Assert.Equal("12,5", form.Quantity);
        Assert.Equal("kg", form.Unit);
        Assert.Contains(form.SupplementaryFields, field => field.Value == "99.500");
        Assert.Contains(form.SupplementaryFields, field => field.Value == "AUDIT_UNITA");
        Assert.Contains("AUDIT_UNITA", string.Join('\n', PdfText(Render(form))));
    }

    [Fact]
    public void DoesNotTreatForeignAncestorsAsRenderedFields()
    {
        var form = Read(departure => departure.Add(new XElement(XName.Get("Rifiuto", "urn:audit:foreign"),
            new XElement(Ns + "CodiceEER", "AUDIT_FOREIGN_ANCESTOR"))));
        Assert.Contains(form.SupplementaryFields, field => field.Value == "AUDIT_FOREIGN_ANCESTOR");
        Assert.Equal("150101", form.WasteCode);
        Assert.Contains("AUDIT_FOREIGN_ANCESTOR", string.Join('\n', PdfText(Render(form))));
    }

    [Fact]
    public void PreservesMixedTextAndNamespacedAttributes()
    {
        var form = Read(departure => departure.Add(new XElement(Ns + "Estensione",
            new XAttribute(XName.Get("dato", "urn:audit:foreign"), "AUDIT_ATTR_NS"),
            "AUDIT_PRIMA", new XElement(Ns + "Interno", "AUDIT_INTERNO"), "AUDIT_DOPO")));
        var text = string.Join('\n', PdfText(Render(form)));
        foreach (var sentinel in new[] { "AUDIT_PRIMA", "AUDIT_INTERNO", "AUDIT_DOPO", "AUDIT_ATTR_NS" })
            Assert.Contains(sentinel, text);
        Assert.Contains(form.SupplementaryFields, field => field.Field.Contains("{urn:audit:foreign}dato"));
    }

    [Fact]
    public void KeepsInvalidAdrFlagInsteadOfRenderingItOnlyAsUnchecked()
    {
        var form = Read(departure => departure.Element(Ns + "Rifiuto")!.Element(Ns + "TrasportoADR")!.Value = "AUDIT_ADR_INVALIDO");
        Assert.Contains(form.SupplementaryFields, field => field.Value == "AUDIT_ADR_INVALIDO");
        Assert.Contains("AUDIT_ADR_INVALIDO", string.Join('\n', PdfText(Render(form))));
    }

    [Fact]
    public void CollectionsAndRenderedUnitsDoNotCreateFalsePartialWarnings()
    {
        var form = Read(departure => departure.Element(Ns + "Rifiuto")!.Add(new XElement(Ns + "ClassiPericolo",
            new XElement(Ns + "ClassePericolo", "HP1"), new XElement(Ns + "ClassePericolo", "HP2"))));
        Assert.Empty(form.SupplementaryFields);
        Assert.Empty(form.Warnings);
        var pages = PdfText(Render(form, "baseline"));
        Assert.Equal(2, pages.Length);
        Assert.Contains("HP1, HP2", pages[0]);
        Assert.Contains("Terrestre", pages[0]);
    }

    [Fact]
    public void KeepsMixedTextOfAdditionalParts()
    {
        var form = new XfirReader().Read(ReaderTests.Sample(extraName: "extra.xml",
            extra: "<Extra xmlns='urn:audit:extra'>AUDIT_PRIMA<Interno>AUDIT_INTERNO</Interno>AUDIT_DOPO</Extra>"));
        var text = string.Join('\n', PdfText(Render(form)));
        foreach (var sentinel in new[] { "AUDIT_PRIMA", "AUDIT_INTERNO", "AUDIT_DOPO" }) Assert.Contains(sentinel, text);
    }

    [Theory]
    [InlineData("Classe")]
    [InlineData("NumeroONU")]
    public void LongAdrValuesContinueInAnActualAppendix(string field)
    {
        var form = Read(departure => departure.Element(Ns + "Rifiuto")!.Add(new XElement(Ns + "DatiADR",
            new XElement(Ns + field, string.Join(' ', Enumerable.Repeat("TEST", 70)) + " AUDIT_CODA_ADR"))));
        var pages = PdfText(Render(form, "adr-" + field));
        Assert.True(pages.Length >= 3);
        Assert.Contains("[segue]", pages[0]);
        Assert.DoesNotContain("AUDIT_CODA_ADR", pages[0]);
        Assert.Contains("AUDIT_CODA_ADR", string.Join('\n', pages.Skip(2)));
        Assert.Contains("Classe ADR / numero ONU", string.Join('\n', pages.Skip(2)));
    }

    [Fact]
    public void ShortAdrValuesDoNotCreateUnnecessaryContinuations()
    {
        var form = Read(departure => departure.Element(Ns + "Rifiuto")!.Add(new XElement(Ns + "DatiADR",
            new XElement(Ns + "Classe", "3"), new XElement(Ns + "NumeroONU", "1234"))));
        var pages = PdfText(Render(form));
        Assert.Equal(2, pages.Length);
        Assert.DoesNotContain("[segue]", string.Join('\n', pages));
        Assert.Contains("ONU 1234", pages[0]);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UnresolvedCarrierNeverFallsBackToAnotherIdentity(string? reference)
    {
        var form = Read(transport: transport => transport.SetAttributeValue("idRef", reference));
        Assert.Null(form.PrimaryCarrier);
        Assert.Contains(form.Warnings, warning => warning.Contains("anagrafica"));
        Assert.Contains(form.AdditionalParts, part => part.Get("Denominazione") == "Trasportatore dimostrativo");
        var pages = PdfText(Render(form, reference == "9" ? "unlinked-carrier" : null));
        Assert.Contains("Anagrafica non collegata", pages[0]);
        Assert.DoesNotContain("Trasportatore dimostrativo", pages[0]);
        Assert.Contains("Trasportatore dimostrativo", string.Join('\n', pages.Skip(2)));
    }

    [Fact]
    public void MissingIdsDoNotAccidentallyMatchEachOther()
    {
        var form = Read(departure => departure.Element(Ns + "Trasportatori")!.Element(Ns + "Trasportatore")!.Attribute("id")!.Remove(),
            transport => transport.Attribute("idRef")!.Remove());
        Assert.Null(form.PrimaryCarrier);
        Assert.Contains(form.Warnings, warning => warning.Contains("anagrafica"));
    }

    [Fact]
    public void SelectsTheReferencedCarrierEvenWhenItIsNotFirst()
    {
        var form = Read(departure => departure.Element(Ns + "Trasportatori")!.Add(new XElement(Ns + "Trasportatore",
            new XAttribute("id", "2"), new XElement(Ns + "Denominazione", "AUDIT_CARRIER_2"))),
            transport => transport.SetAttributeValue("idRef", "2"));
        Assert.Equal("AUDIT_CARRIER_2", form.PrimaryCarrier?.Get("Denominazione"));
        var pages = PdfText(Render(form));
        Assert.Contains("AUDIT_CARRIER_2", pages[0]);
        Assert.DoesNotContain("Trasportatore dimostrativo", pages[0]);
        Assert.Contains("Trasportatore dimostrativo", string.Join('\n', pages.Skip(2)));
    }

    [Fact]
    public void ForeignCarrierElementsCannotResolveAFormRelationship()
    {
        var form = Read(departure => departure.Element(Ns + "Trasportatori")!.Add(
            new XElement(XName.Get("Trasportatore", "urn:audit:foreign"), new XAttribute("id", "9"),
                new XElement(Ns + "Denominazione", "AUDIT_FOREIGN_CARRIER"))),
            transport => transport.SetAttributeValue("idRef", "9"));
        Assert.Null(form.PrimaryCarrier);
        Assert.Contains(form.SupplementaryFields, field => field.Value == "AUDIT_FOREIGN_CARRIER");
    }

    [Fact]
    public void PreliminaryDocumentCanShowItsDeclaredCarrier()
    {
        var form = new XfirReader().Read(ReaderTests.Sample(includeTransport: false));
        Assert.Empty(form.Transports);
        Assert.Equal("Trasportatore dimostrativo", form.PrimaryCarrier?.Get("Denominazione"));
        Assert.Empty(form.AdditionalParts);
        Assert.Empty(form.Warnings);
        Assert.Contains("Trasportatore dimostrativo", PdfText(Render(form))[0]);
    }

    [Theory]
    [InlineData(XfirReader.FormNamespace, true)]
    [InlineData("urn:audit:foreign", false)]
    [InlineData("", false)]
    public void OnlyRecognizedCancellationNamespaceChangesState(string xmlNamespace, bool cancellationExpected)
    {
        var extra = new XElement(XName.Get("Annullamento", xmlNamespace), new XElement("Nota", "Test sintetico"));
        var form = new XfirReader().Read(ReaderTests.Sample(extraName: "annullamento.xml", extra: extra.ToString()));
        Assert.Equal(cancellationExpected, form.State.Contains("annullamento"));
        Assert.Contains(form.AdditionalParts, part => part.Name == "annullamento.xml");
        if (!cancellationExpected) Assert.Contains("Trasporto presente", form.State);
    }

    private static FormDocument Read(Action<XElement>? departure = null, Action<XElement>? transport = null)
    {
        using var zip = new ZipArchive(new MemoryStream(ReaderTests.Sample()));
        XElement Xml(string name)
        {
            using var input = zip.GetEntry(name)!.Open();
            return XElement.Load(input);
        }
        var departureXml = Xml("partenza.xml");
        var transportXml = Xml("trasporto001.xml");
        departure?.Invoke(departureXml);
        transport?.Invoke(transportXml);
        return new XfirReader().Read(ReaderTests.Sample(departure: departureXml.ToString(), transport: transportXml.ToString()));
    }

    private static byte[] Render(FormDocument form, string? qaName = null)
    {
        var pdf = new FormPdfRenderer().Render(form);
        // Optional diagnostic output for visual review; never used as a source fixture or release artifact.
        var qaDirectory = Environment.GetEnvironmentVariable("XFIR_TEST_PDF_OUTPUT");
        if (qaName is not null && !string.IsNullOrEmpty(qaDirectory))
        {
            Directory.CreateDirectory(qaDirectory);
            File.WriteAllBytes(Path.Combine(qaDirectory, qaName + ".pdf"), pdf);
        }
        return pdf;
    }

    private static string[] PdfText(byte[] bytes)
    {
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        return pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().Select(page => string.Join('\n', ContentReader.ReadContent(page).OfType<COperator>()
            .Where(op => op.OpCode.Name == "Tj").SelectMany(op => op.Operands.OfType<CString>()).Select(value => value.Value))).ToArray();
    }
}
