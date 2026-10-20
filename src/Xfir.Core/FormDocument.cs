// SPDX-License-Identifier: EUPL-1.2
using System.Globalization;
using System.Xml.Linq;

namespace Xfir.Core;

public sealed record XmlPart(string Name, XElement Root)
{
    public string Get(string path) => Element(path)?.Value.Trim() ?? "";
    public XElement? Element(string path)
    {
        XElement? node = Root;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            node = node?.Element(Root.Name.Namespace + segment);
        return node;
    }
    public string Attribute(string path, string attribute) => Element(path)?.Attribute(attribute)?.Value ?? "";
    public IEnumerable<XmlPart> Children(string path)
    {
        var elements = Element(path)?.Elements() ?? [];
        return elements.Select(e => new XmlPart(Name, e));
    }
    public IReadOnlyList<DataField> Fields => EnumerateFields().ToArray();
    internal IEnumerable<DataField> EnumerateFields() => Root.DescendantsAndSelf()
        .SelectMany(e => e.Attributes().Where(a => !a.IsNamespaceDeclaration)
            .Select(a => new DataField(Path(e) + "/@" + a.Name.LocalName, a.Value))
            .Concat(!e.HasElements ? [new DataField(Path(e), e.Value.Trim())]
                : e.Nodes().OfType<XText>().Where(text => !string.IsNullOrWhiteSpace(text.Value))
                    .Select(text => new DataField(Path(e) + "/text()", text.Value.Trim()))));
    private static string Path(XElement e) => string.Join(" / ", e.AncestorsAndSelf().Reverse().Select(n => n.Name.LocalName));
}

public sealed record DataField(string Field, string Value);
public sealed record SignatureInfo(string FileName, string Signer, string Subject, string SignedAt, IReadOnlyList<string> References);
public sealed record AttachmentInfo(string Name, int Size);

public sealed class FormDocument
{
    public required string SourceName { get; init; }
    public required string SourceSha256 { get; init; }
    public required XmlPart Endorsement { get; init; }
    public XmlPart? Departure { get; init; }
    public XmlPart? Acceptance { get; init; }
    public XmlPart? PrimaryCarrier { get; init; }
    public required IReadOnlyList<XmlPart> Transports { get; init; }
    public required IReadOnlyList<XmlPart> Parts { get; init; }
    public IReadOnlyList<DataField> OriginalFields { get; init; } = [];
    public required IReadOnlyList<SignatureInfo> Signatures { get; init; }
    public required IReadOnlyList<AttachmentInfo> Attachments { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<XmlPart> AdditionalParts { get; init; }
    public IReadOnlyList<DataField> SupplementaryFields { get; init; } = [];
    public byte[]? QrPayload { get; init; }
    public string Number => Endorsement.Get("NumeroFir");
    public string Producer => Departure?.Get("Produttore/Denominazione") ?? "";
    public string Recipient => Departure?.Get("Destinatario/Denominazione") ?? "";
    public string WasteCode => Departure?.Get("Rifiuto/CodiceEER") ?? "";
    public string Quantity => FormFormatting.Quantity(Departure?.Get("Rifiuto/Quantita"));
    public string Unit => Departure?.Attribute("Rifiuto/Quantita", "unitaMisura") ?? "";
    public bool IsPartial => AdditionalParts.Count > 0 || SupplementaryFields.Count > 0;
    public string State => Parts.Any(p => p.Root.Name == XName.Get("Annullamento", XfirReader.FormNamespace)) ? "Nota di annullamento presente"
        : Acceptance is not null ? Acceptance.Get("TipoAccettazione") switch
        {
            "A" => "Accettazione per intero registrata", "P" => "Accettazione parziale registrata",
            "R" => "Respingimento registrato", _ => "Accettazione da interpretare"
        }
        : Transports.Count > 0 ? "Trasporto presente, accettazione assente"
        : Departure is not null ? "Dati di partenza presenti" : "Solo vidimazione";
}

public static class FormFormatting
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    // IANA ids need ICU on Windows; fall back to the Windows id when the conversion is unavailable.
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Rome", out var rome) ? rome
        : TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    public static string Quantity(string? value) => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
        CultureInfo.InvariantCulture, out var number) ? number.ToString("0.############################", Italian) : value ?? "";
    public static string Date(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day.ToString("dd/MM/yyyy", Italian) :LocalTime(value, "dd/MM/yyyy");
    }
    public static string LocalTime(string? value, string format = "dd/MM/yyyy HH:mm")
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        // Do not let the machine's time zone reinterpret an unqualified date/time.
        if (!(value.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(value, @"[+-]\d{2}:\d{2}$")))
            return value + " (fuso non indicato)";
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? TimeZoneInfo.ConvertTime(time, Rome).ToString(format, Italian) : value;
    }
    public static string Eer(string code) => code.Length == 6 && code.All(char.IsDigit)
        ? $"{code[..2]}.{code[2..4]}.{code[4..]}" : code;
    public static string YesNo(string value) => value switch { "true" or "1" => "Sì", "false" or "0" => "No", _ => value };
    public static string Address(XmlPart part, string path)
    {
        string G(string name) => part.Get(path + "/" + name);
        return string.Join(", ", new[] { string.Join(" ", new[] { G("Indirizzo"), G("Civico") }.Where(s => s.Length > 0)),
            string.Join(" ", new[] { G("CAP"), G("Citta/Comune"), G("Citta/ComuneEstero") }.Where(s => s.Length > 0)) }.Where(s => s.Length > 0));
    }
}
