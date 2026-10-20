// SPDX-License-Identifier: EUPL-1.2
using System.Xml.Linq;

namespace Xfir.Core;

// Track the XML occurrences actually selected by the layout. Other occurrences,
// foreign namespaces and unknown attributes must remain visible in the appendix.
internal static class LayoutCoverage
{
    private static readonly string[] EndorsementFields =
    ["NumeroFir", "DataRichiesta", "CCIAANome", "CodiceFiscaleSoggetto", "DenominazioneSoggetto"];
    private static readonly string[] DepartureFields =
    [
        "DataEmissione", "NumeroFIR", "Annotazioni",
        "Rifiuto/CodiceEER", "Rifiuto/Descrizione", "Rifiuto/StatoFisico", "Rifiuto/Provenienza",
        "Rifiuto/Quantita", "Rifiuto/VerificatoInPartenza", "Rifiuto/NumeroColli", "Rifiuto/Rinfusa",
        "Rifiuto/CaratteristicheChimicoFisiche",
        "Rifiuto/DatiADR/Classe", "Rifiuto/DatiADR/NumeroONU", "Rifiuto/DatiADR/Note"
    ];
    private static readonly string[] ActorFields =
    ["Denominazione", "CodiceFiscale", "Indirizzo/Indirizzo", "Indirizzo/Civico", "Indirizzo/CAP",
        "Indirizzo/Citta/Comune", "Indirizzo/Citta/ComuneEstero", "Autorizzazione/Numero", "Autorizzazione/Tipo"];
    private static readonly string[] CarrierFields = ["Denominazione", "CodiceFiscale", "NumeroIscrizioneAlbo", "TipoTrasporto"];
    private static readonly string[] IntermediaryFields = ["Denominazione", "CodiceFiscale", "NumeroIscrizioneAlbo"];
    private static readonly string[] TransportFields =
    [
        "TrasportoTerrestre/Conducente/Nome", "TrasportoTerrestre/Conducente/Cognome", "TrasportoTerrestre/TargaAutomezzo",
        "TrasportoTerrestre/TargaRimorchio", "TrasportoTerrestre/DataOraInizioTrasporto", "TrasportoTerrestre/Percorso"
    ];
    private static readonly string[] AcceptanceFields =
    [
        "QuantitaAccettata", "DataOraArrivo", "AttesaVerificaAnalitica", "MotivoRespingimento"
    ];
    internal static IReadOnlyList<DataField> Unmapped(XmlPart endorsement, XmlPart? departure,
        XmlPart? transport, XmlPart? acceptance, XmlPart? displayedCarrier, IEnumerable<XmlPart> additional)
    {
        var used = new HashSet<XObject>();
        void Value(XElement? element)
        {
            if (element is not null)
                foreach (var text in element.DescendantNodes().OfType<XText>()) used.Add(text);
        }
        void Values(XmlPart? part, IEnumerable<string> paths)
        {
            foreach (var path in paths) Value(part?.Element(path));
        }
        void Attribute(XAttribute? attribute)
        {
            if (attribute is not null) used.Add(attribute);
        }
        void Subtree(XmlPart part)
        {
            Value(part.Root);
            foreach (var attribute in part.Root.DescendantsAndSelf().Attributes()) used.Add(attribute);
        }

        Values(endorsement, EndorsementFields);
        Values(departure, DepartureFields);
        if (departure?.Get("Rifiuto/TrasportoADR") is "true" or "false" or "1" or "0")
            Value(departure.Element("Rifiuto/TrasportoADR"));
        foreach (var actor in new[] { "Produttore", "Destinatario" })
            Values(departure, ActorFields.Select(field => actor + "/" + field));
        Values(departure, ["Produttore/NumeroIscrizioneAlbo", "Destinatario/Attivita"]);
        Values(displayedCarrier, CarrierFields);
        Attribute(displayedCarrier?.Root.Attribute("id"));
        Attribute(departure?.Element("Rifiuto/Quantita")?.Attribute("unitaMisura"));
        Values(transport, TransportFields);
        Attribute(transport?.Root.Attribute("idRef"));
        Values(acceptance, AcceptanceFields);
        if (acceptance?.Get("TipoAccettazione") is "A" or "P" or "R") Value(acceptance.Element("TipoAccettazione"));
        Attribute(acceptance?.Element("QuantitaAccettata")?.Attribute("unitaMisura"));

        foreach (var hazard in departure?.Children("Rifiuto/ClassiPericolo") ?? []) Value(hazard.Root);
        var intermediaries = departure?.Children("Intermediari").ToList() ?? [];
        Values(intermediaries.FirstOrDefault(), IntermediaryFields);
        // Further intermediaries and unselected carriers are printed in full in the appendix.
        foreach (var part in intermediaries.Skip(1).Concat(additional)) Subtree(part);

        return new[] { endorsement, departure, transport, acceptance }.OfType<XmlPart>()
            .SelectMany(part => Fields(part, used)).ToList();
    }
    private static IEnumerable<DataField> Fields(XmlPart part, HashSet<XObject> used)
    {
        string Name(XName name) => name.Namespace == part.Root.Name.Namespace || name.Namespace == XNamespace.None
            ? name.LocalName : name.ToString();
        string Path(XElement element) => string.Join('/', element.AncestorsAndSelf()
            .TakeWhile(e => e != part.Root).Reverse().Select(e => Name(e.Name)));
        foreach (var element in part.Root.DescendantsAndSelf())
        {
            var path = Path(element);
            foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration && !used.Contains(a)))
                yield return new DataField(part.Name + " · " + path + "/@" + Name(attribute.Name), attribute.Value);
            var unrepresented = element.Nodes().OfType<XText>()
                .Where(text => !used.Contains(text) && !string.IsNullOrWhiteSpace(text.Value)).ToList();
            if (unrepresented.Count > 0)
                yield return new DataField(part.Name + " · " + path + (element.HasElements ? "/text()" : ""),
                    string.Concat(unrepresented.Select(text => text.Value)).Trim());
        }
    }
}
