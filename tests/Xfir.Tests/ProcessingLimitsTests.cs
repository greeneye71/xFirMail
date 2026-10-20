// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Xml.Linq;
using Xfir.CommandLine;
using Xfir.Core;
using Xfir.Rendering;
using Xunit;

namespace Xfir.Tests;

public class ProcessingLimitsTests
{
    [Fact]
    public void StopsDeepXmlDuringLoading()
    {
        var nested = new XElement("Leaf", "text");
        for (var i = 0; i < 20; i++) nested = new XElement("Nested", nested);
        var budget = new ProcessingBudget(new() { MaxXmlDepth = 5 });
        var error = Assert.Throws<ProcessingLimitException>(() => new XfirReader().Read(
            ReaderTests.Sample(departure: nested.ToString()), budget: budget));
        Assert.Contains("annidamento", error.Message);
    }

    [Fact]
    public void LimitsAggregateNodesAndAttributes()
    {
        var budget = new ProcessingBudget(new() { MaxXmlNodes = 5 });
        Assert.Contains("nodi XML", Assert.Throws<ProcessingLimitException>(() =>
            new XfirReader().Read(ReaderTests.Sample(), budget: budget)).Message);
    }

    [Fact]
    public void LimitsFieldsBeforePreparingTheUiRows()
    {
        var budget = new ProcessingBudget(new() { MaxFields = 2 });
        Assert.Contains("campi", Assert.Throws<ProcessingLimitException>(() =>
            new XfirReader().Read(ReaderTests.Sample(), budget: budget)).Message);
    }

    [Fact]
    public void LimitsPagesAndRendererCanBeReusedAfterFailure()
    {
        var document = new XfirReader().Read(ReaderTests.Sample());
        var renderer = new FormPdfRenderer();
        Assert.Contains("pagine", Assert.Throws<ProcessingLimitException>(() =>
            renderer.Render(document, budget: new(new() { MaxPages = 1 }))).Message);
        Assert.NotEmpty(renderer.Render(document, budget: new(new() { MaxPages = 2 })));
    }

    [Fact]
    public void ChecksCancellationBeforeOpeningAFile()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new XfirReader().Read("missing.xfir",
            new(cancellationToken: cancellation.Token)));
    }

    [Fact]
    public void CancellationBetweenReadAndRenderAbortsTheSameOperation()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new ProcessingBudget(cancellationToken: cancellation.Token);
        var document = new XfirReader().Read(ReaderTests.Sample(), budget: budget);
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new FormPdfRenderer().Render(document, budget: budget));
    }

    [Fact]
    public void DeadlineIsCheckedExplicitly()
    {
        var budget = new ProcessingBudget(new() { MaxDuration = TimeSpan.FromTicks(1) });
        Assert.Contains("tempo massimo", Assert.Throws<ProcessingLimitException>(budget.Check).Message);
    }

    [Fact]
    public void CachesFieldsOnlyForTheCurrentOperation()
    {
        var part = new XmlPart("test", new XElement("Root", new XElement("Value", "one")));
        var budget = new ProcessingBudget(new() { MaxFields = 1 });
        Assert.Same(budget.GetFields(part), budget.GetFields(part));
        Assert.Throws<ProcessingLimitException>(() => budget.GetFields(new("other", new XElement("Value", "two"))));
        part.Root.Element("Value")!.Value = "updated";
        Assert.Equal("updated", Assert.Single(new ProcessingBudget().GetFields(part)).Value);
    }

    [Fact]
    public void OriginalUiRowsArePreparedAndHaveTheSameContents()
    {
        var document = new XfirReader().Read(ReaderTests.Sample());
        var expected = document.Parts.SelectMany(part => part.Fields
            .Where(field => !field.Field.Contains("Signature", StringComparison.Ordinal))
            .Select(field => new DataField(part.Name + " · " + field.Field, field.Value)));
        Assert.Equal(expected, document.OriginalFields);
    }

    [Fact]
    public void CliRefusesExcessivePagesWithoutSavingAPartialPdf()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xfir-page-limit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var zip = new ZipArchive(new MemoryStream(ReaderTests.Sample()));
            using var entry = zip.GetEntry("partenza.xml")!.Open();
            var departure = XElement.Load(entry);
            departure.Element(XName.Get("Annotazioni", XfirReader.FormNamespace))!.Value =
                string.Concat(Enumerable.Repeat("Annotazione sintetica per prova controllata. ", 35000));
            var source = Path.Combine(folder, "long.xfir");
            File.WriteAllBytes(source, ReaderTests.Sample(departure: departure.ToString()));
            var error = new StringWriter();
            Assert.Equal(1, ConversionCommand.Run([source], new StringWriter(), error, "test"));
            Assert.Contains("pagine", error.ToString());
            Assert.False(File.Exists(Path.ChangeExtension(source, ".pdf")));
            Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
        }
        finally { Directory.Delete(folder, true); }
    }
}
