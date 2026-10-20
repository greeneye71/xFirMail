// SPDX-License-Identifier: EUPL-1.2
using System.Text.Json;
using PdfSharp.Pdf.IO;
using Xfir.CommandLine;
using Xunit;

namespace Xfir.Tests;

public sealed class ConversionCommandTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "xFirW-cli-tests-" + Guid.NewGuid().ToString("N"));
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public ConversionCommandTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void SingleArgumentCreatesPdfBesideSourceAndPreservesOriginal()
    {
        var source = WriteSource();
        var original = File.ReadAllBytes(source);

        Assert.Equal(0, Run(source));

        var destination = Path.ChangeExtension(source, ".pdf");
        using var pdf = PdfReader.Open(destination, PdfDocumentOpenMode.Import);
        Assert.Equal(2, pdf.PageCount);
        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Contains(destination, output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Fact]
    public void AcceptsRelativePathsAndExplicitDestinationWithSpaces()
    {
        var source = Path.GetRelativePath(Environment.CurrentDirectory, WriteSource());
        var destination = Path.Combine(folder, "copia scelta.pdf");

        Assert.Equal(0, Run(source, Path.GetRelativePath(Environment.CurrentDirectory, destination)));

        using var pdf = PdfReader.Open(destination, PdfDocumentOpenMode.Import);
        Assert.Equal(2, pdf.PageCount);
        Assert.False(File.Exists(Path.ChangeExtension(source, ".pdf")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DoesNotOverwriteExistingDestination(bool explicitDestination)
    {
        var source = WriteSource();
        var destination = Path.ChangeExtension(source, ".pdf");
        File.WriteAllText(destination, "Contenuto da conservare");

        Assert.Equal(1, Run(explicitDestination ? [source, destination] : [source]));

        Assert.Equal("Contenuto da conservare", File.ReadAllText(destination));
        Assert.NotEmpty(error.ToString());
        Assert.Equal("", output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrInvalidSourceReturnsErrorWithoutCreatingPdf(bool invalid)
    {
        var source = Path.Combine(folder, "inesistente.xfir");
        if (invalid) File.WriteAllText(source, "Non un archivio XFIR");

        Assert.Equal(1, Run(source));

        Assert.False(File.Exists(Path.ChangeExtension(source, ".pdf")));
        Assert.NotEmpty(error.ToString());
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void ReportsOutputDirectoryErrors()
    {
        Assert.Equal(1, Run(WriteSource(), Path.Combine(folder, "inesistente", "copia.pdf")));
        Assert.NotEmpty(error.ToString());
    }

    [Fact]
    public void InspectRemainsAvailableWithoutCreatingPdf()
    {
        var source = WriteSource();

        Assert.Equal(0, Run(source, "--inspect"));

        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("TESTX 000001 AA", json.RootElement.GetProperty("Number").GetString());
        Assert.False(File.Exists(Path.ChangeExtension(source, ".pdf")));
    }

    [Fact]
    public void WarningsDoNotPreventConversion()
    {
        var source = WriteSource(includeQr: false);

        Assert.Equal(0, Run(source));

        Assert.True(File.Exists(Path.ChangeExtension(source, ".pdf")));
        Assert.Contains("Avviso:", error.ToString());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpSucceedsWithoutReadingFiles(string argument)
    {
        Assert.Equal(0, Run(argument));
        Assert.Contains("Uso:", output.ToString());
        Assert.Equal("", error.ToString());
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void InvalidArgumentsReturnUsageError(string[] args)
    {
        Assert.Equal(2, Run(args));
        Assert.Contains("Uso:", error.ToString());
        Assert.Equal("", output.ToString());
    }

    public static TheoryData<string[]> InvalidArguments => new()
    {
        Array.Empty<string>(),
        new[] { "--sconosciuto" },
        new[] { "file.xfir", "--sconosciuto" },
        new[] { "file.xfir", "copia.pdf", "extra" },
        new[] { " " }
    };

    private int Run(params string[] args) => ConversionCommand.Run(args, output, error, "xFirW.exe --pdf");

    private string WriteSource(bool includeQr = true)
    {
        var path = Path.Combine(folder, "formulario di prova.XFIR");
        File.WriteAllBytes(path, ReaderTests.Sample(includeQr: includeQr));
        return path;
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
