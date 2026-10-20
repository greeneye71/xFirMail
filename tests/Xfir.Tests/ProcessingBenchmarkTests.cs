// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Xfir.Core;
using Xfir.Rendering;
using Xunit;

namespace Xfir.Tests;

public class ProcessingBenchmarkTests
{
    [Fact]
    public void OptionalBenchmark()
    {
        var destination = Environment.GetEnvironmentVariable("XFIR_BENCHMARK_OUTPUT");
        if (string.IsNullOrEmpty(destination)) return;
        var results = new List<object>();
        foreach (var scenario in new[] { "normal", "long" })
        {
            var source = ReaderTests.Sample();
            if (scenario == "long")
            {
                using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(source));
                using var stream = archive.GetEntry("partenza.xml")!.Open();
                var departure = XElement.Load(stream);
                departure.Element(XName.Get("Annotazioni", XfirReader.FormNamespace))!.Value =
                    string.Concat(Enumerable.Repeat("Annotazione sintetica per confronto prestazioni. ", 3500));
                source = ReaderTests.Sample(departure: departure.ToString());
            }
            var elapsed = new List<double>();
            var allocated = new List<long>();
            for (var iteration = 0; iteration < 9; iteration++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var stopwatch = Stopwatch.StartNew();
                var document = new XfirReader().Read(source);
                // The original UI materialized these rows itself. Later readers may prepare them once.
                var rows = typeof(FormDocument).GetProperty("OriginalFields")?.GetValue(document)
                    ?? document.Parts.SelectMany(part => part.Fields).ToArray();
                var pdf = new FormPdfRenderer().Render(document);
                GC.KeepAlive(rows);
                GC.KeepAlive(pdf);
                if (iteration < 2) continue;
                elapsed.Add(stopwatch.Elapsed.TotalMilliseconds);
                allocated.Add(GC.GetAllocatedBytesForCurrentThread() - before);
            }
            results.Add(new { scenario, medianMs = elapsed.Order().ElementAt(3), medianAllocatedBytes = allocated.Order().ElementAt(3) });
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
