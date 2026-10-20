// SPDX-License-Identifier: EUPL-1.2
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Xfir.Core;

public sealed class XfirReadException(string message) : Exception(message);

public sealed class XfirReader
{
    public const string FormNamespace = "urn:it:rentri:formulari:1.0";
    public const string EndorsementNamespace = "urn:it:rentri:vidimazione-fir:1.0";
    private const int MaxArchiveBytes = 16 * 1024 * 1024;
    private const int MaxEntryBytes = 8 * 1024 * 1024;
    private const int MaxTotalBytes = 32 * 1024 * 1024;
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    public FormDocument Read(string path, ProcessingBudget? budget = null)
    {
        budget ??= new();
        budget.Check();
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaxArchiveBytes) throw new XfirReadException("Il file supera il limite di lettura di 16 MB.");
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = file.Read(buffer)) > 0)
        {
            budget.Check();
            if (data.Length + count > MaxArchiveBytes) throw new XfirReadException("Il file supera il limite di lettura di 16 MB.");
            data.Write(buffer, 0, count);
        }
        return Read(data.ToArray(), Path.GetFileName(path), budget);
    }

    public FormDocument Read(byte[] source, string sourceName = "documento.xfir", ProcessingBudget? budget = null)
    {
        budget ??= new();
        budget.Check();
        try { return ReadArchive(source, sourceName, budget); }
        catch (Exception e) when (e is InvalidDataException or XmlException or FormatException or OverflowException)
        { throw new XfirReadException("Il contenitore XFIR non è leggibile: " + e.Message); }
    }

    private static FormDocument ReadArchive(byte[] source, string sourceName, ProcessingBudget budget)
    {
        if (source.Length > MaxArchiveBytes) throw new XfirReadException("Il file supera il limite di lettura di 16 MB.");
        using var stream = new MemoryStream(source, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        if (archive.Entries.Count > 128) throw new XfirReadException("Il contenitore ha troppi elementi (massimo 128).");
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            budget.Check();
            var name = entry.FullName;
            CheckPath(name.TrimEnd('/'));
            if (!names.Add(name.TrimEnd('/'))) throw new XfirReadException("Elemento duplicato o ambiguo: " + name);
            if (name.EndsWith('/')) continue;
            if (entry.Length > MaxEntryBytes || (total += entry.Length) > MaxTotalBytes)
                throw new XfirReadException("Il contenitore supera i limiti di decompressione.");
            using var input = entry.Open();
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = input.Read(buffer)) > 0)
            {
                budget.Check();
                if (output.Length + count > MaxEntryBytes || output.Length + count > entry.Length)
                    throw new XfirReadException("Dimensione decompressa non valida: " + name);
                output.Write(buffer, 0, count);
            }
            files.Add(name, output.ToArray());
        }
        if (!files.TryGetValue("mimetype", out var mime) || Encoding.ASCII.GetString(mime) != "application/vnd.etsi.asic-e+zip")
            throw new XfirReadException("Il file non dichiara il formato ASiC-E previsto per XFIR.");
        if (!files.TryGetValue("META-INF/manifest.xml", out var manifestBytes))
            throw new XfirReadException("Il manifest ASiC-E è assente.");

        var warnings = new List<string>();
        var manifest = ParseXml(manifestBytes, budget);
        XNamespace manifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        if (manifest.Name != manifestNs + "manifest") throw new XfirReadException("Il manifest ASiC-E non è riconosciuto.");
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifest.Elements(manifestNs + "file-entry"))
        {
            var name = item.Attribute(manifestNs + "full-path")?.Value ?? "";
            if (name == "/") continue;
            CheckPath(name);
            if (!declared.Add(name)) throw new XfirReadException("Riferimento duplicato nel manifest: " + name);
            if (!files.ContainsKey(name)) throw new XfirReadException("File dichiarato nel manifest ma assente: " + name);
        }
        foreach (var name in files.Keys.Where(n => n != "mimetype" && !n.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)))
            if (!declared.Contains(name)) warnings.Add("Elemento non dichiarato nel manifest: " + name);

        var parts = new List<XmlPart>();
        var signatures = new List<SignatureInfo>();
        foreach (var (name, bytes) in files.Where(f => f.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !f.Key.Equals("META-INF/manifest.xml", StringComparison.OrdinalIgnoreCase)))
        {
            var root = ParseXml(bytes, budget);
            foreach (var signature in root.DescendantsAndSelf(Ds + "Signature"))
            {
                budget.Check();
                var references = signature.Element(Ds + "SignedInfo")?.Elements(Ds + "Reference").Select(r => r.Attribute("URI")?.Value ?? "").ToList() ?? [];
                // URI="" signs the enclosing document and "#id" an element inside it: neither names a container file.
                foreach (var reference in references.Where(r => r.Length > 0 && !r.StartsWith('#')))
                {
                    budget.Check();
                    var target = Uri.UnescapeDataString(reference);
                    CheckPath(target);
                    if (!files.ContainsKey(target)) warnings.Add("Riferimento di firma assente: " + target);
                }
                var signer = "Certificato non disponibile";
                var subject = "";
                var certificate = signature.Descendants(Ds + "X509Certificate").FirstOrDefault()?.Value;
                if (certificate is not null)
                {
                    try
                    {
                        using var cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certificate));
                        signer = cert.GetNameInfo(X509NameType.SimpleName, false);
                        subject = cert.Subject;
                    }
                    catch (Exception e) when (e is CryptographicException or FormatException)
                    { warnings.Add("Certificato non leggibile in " + name); }
                }
                var signedAt = signature.Descendants().FirstOrDefault(e => e.Name.LocalName == "SigningTime" && e.Name.NamespaceName.StartsWith("http://uri.etsi.org/01903/", StringComparison.Ordinal))?.Value ?? "";
                signatures.Add(new(name, signer, subject, signedAt, references));
            }
            if (!name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) parts.Add(new(name, root));
        }

        var endorsements = parts.Where(p => p.Root.Name == XName.Get("eFIR", EndorsementNamespace)).ToList();
        if (endorsements.Count != 1) throw new XfirReadException("È richiesta una sola vidimazione XML riconosciuta.");
        var endorsement = endorsements[0];
        var number = endorsement.Get("NumeroFir");
        if (string.IsNullOrWhiteSpace(number)) throw new XfirReadException("Numero FIR assente nella vidimazione.");
        var formParts = parts.Where(p => p.Root.Name.NamespaceName == FormNamespace).ToList();
        XmlPart? One(string name)
        {
            var found = formParts.Where(p => p.Root.Name.LocalName == name).ToList();
            if (found.Count > 1) throw new XfirReadException("Blocco dati ambiguo: " + name);
            return found.SingleOrDefault();
        }
        var departure = One("DatiPartenza");
        // Subsequent acceptance blocks are represented in the supplementary appendix.
        var acceptance = formParts.SingleOrDefault(p => p.Name.Equals("accettazione.xml", StringComparison.OrdinalIgnoreCase) && p.Root.Name.LocalName == "Accettazione");
        if (departure is not null && departure.Get("NumeroFIR") != number)
            throw new XfirReadException("Il numero FIR dei dati di partenza non corrisponde alla vidimazione.");
        var transports = formParts.Where(p => p.Root.Name.LocalName == "Trasporto").OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        var carriers = departure?.Children("Trasportatori")
            .Where(p => p.Root.Name == XName.Get("Trasportatore", FormNamespace)).ToList() ?? [];
        var ids = new HashSet<string>();
        foreach (var carrier in carriers)
            if (!ids.Add(carrier.Root.Attribute("id")?.Value ?? "")) throw new XfirReadException("Identificativo del trasportatore duplicato.");
        foreach (var transport in transports)
        {
            var reference = transport.Root.Attribute("idRef")?.Value;
            if (string.IsNullOrWhiteSpace(reference) || !ids.Contains(reference))
                warnings.Add("Trasporto senza anagrafica collegata: " + transport.Name + " (idRef: " + (reference ?? "assente") + ")");
        }

        var additional = parts.Where(p => p != endorsement && p != departure && p != acceptance && !transports.Contains(p) && p.Root.Name != XName.Get("Metadati", FormNamespace)).ToList();
        // The two-page layout covers one terrestrial leg. Keep every other leg visible in the appendix.
        if (transports.Count > 1) additional.AddRange(transports.Skip(1));
        if (transports.FirstOrDefault() is { } first && first.Element("TrasportoTerrestre") is null) additional.Add(first);
        // Preliminary documents may show their first carrier; existing legs require a valid link.
        var firstTransport = transports.FirstOrDefault();
        var firstReference = firstTransport?.Root.Attribute("idRef")?.Value;
        var displayedCarrier = firstTransport is null ? carriers.FirstOrDefault()
            : string.IsNullOrWhiteSpace(firstReference) ? null
            : carriers.FirstOrDefault(c => c.Root.Attribute("id")?.Value == firstReference);
        additional.AddRange(carriers.Where(c => c != displayedCarrier).Select(c => new XmlPart("partenza.xml · trasportatore " + c.Root.Attribute("id")?.Value, c.Root)));
        var supplementary = LayoutCoverage.Unmapped(endorsement, departure, firstTransport, acceptance, displayedCarrier, additional);
        if (additional.Count > 0 || supplementary.Count > 0) warnings.Add("Alcuni dati sono riportati nell'appendice: il modulo principale è una rappresentazione parziale.");
        if (departure is null) warnings.Add("Dati di partenza assenti: il documento contiene soltanto una fase preliminare.");

        var cborName = Path.ChangeExtension(endorsement.Name, ".cbor");
        files.TryGetValue(cborName, out var qr);
        if (qr is null || qr.Length == 0) { qr = null; warnings.Add("QR di vidimazione assente: la copia non contiene il QR originale."); }
        else if (qr.Length > 1024) throw new XfirReadException("Dati QR troppo grandi per una rappresentazione affidabile.");
        var attachments = files.Where(f => f.Key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).Select(f => new AttachmentInfo(f.Key, f.Value.Length)).ToList();
        foreach (var unknown in files.Keys.Where(n => !n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && n != "mimetype" && !n.Equals(cborName, StringComparison.OrdinalIgnoreCase)))
            warnings.Add("Elemento non rappresentato: " + unknown);
        if (attachments.Count > 0) warnings.Add("Sono presenti allegati PDF: elencati nei dettagli, non inclusi nella stampa del formulario.");
        var originalFields = new List<DataField>();
        foreach (var part in parts)
            foreach (var field in budget.GetFields(part).Where(field => !field.Field.Contains("Signature", StringComparison.Ordinal)))
                originalFields.Add(new DataField(part.Name + " · " + field.Field, field.Value));
        budget.Check();
        return new FormDocument
        {
            SourceName = sourceName, SourceSha256 = Convert.ToHexString(SHA256.HashData(source)), Endorsement = endorsement,
            Departure = departure, Acceptance = acceptance, PrimaryCarrier = displayedCarrier, Transports = transports, Parts = parts,
            OriginalFields = originalFields.AsReadOnly(),
            Signatures = signatures, Attachments = attachments, AdditionalParts = additional, SupplementaryFields = supplementary, QrPayload = qr, Warnings = warnings
        };
    }

    private static XElement ParseXml(byte[] bytes, ProcessingBudget budget)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 };
        using var stream = new MemoryStream(bytes, false);
        using var reader = XmlReader.Create(stream, settings);
        using var bounded = new BudgetXmlReader(reader, budget);
        return XElement.Load(bounded);
    }

    private static void CheckPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\') || name.Contains(':') || name.StartsWith('/') || name.Contains('\0')
            || name.Split('/').Any(s => s is ".." or "." or ""))
            throw new XfirReadException("Percorso non ammesso nel contenitore: " + name);
    }
}
