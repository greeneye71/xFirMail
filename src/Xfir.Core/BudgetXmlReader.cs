// SPDX-License-Identifier: EUPL-1.2
using System.Xml;

namespace Xfir.Core;

// Check depth and total work before LINQ to XML materializes each incoming node.
internal sealed class BudgetXmlReader(XmlReader inner, ProcessingBudget budget) : XmlReader
{
    public override bool Read()
    {
        budget.Check();
        if (!inner.Read()) return false;
        budget.XmlNode(inner.Depth, inner.AttributeCount, inner.NodeType == XmlNodeType.Element);
        return true;
    }
    public override int AttributeCount => inner.AttributeCount;
    public override string BaseURI => inner.BaseURI;
    public override int Depth => inner.Depth;
    public override bool EOF => inner.EOF;
    public override bool HasValue => inner.HasValue;
    public override bool IsEmptyElement => inner.IsEmptyElement;
    public override string LocalName => inner.LocalName;
    public override string NamespaceURI => inner.NamespaceURI;
    public override XmlNameTable NameTable => inner.NameTable;
    public override XmlNodeType NodeType => inner.NodeType;
    public override string Prefix => inner.Prefix;
    public override ReadState ReadState => inner.ReadState;
    public override string Value => inner.Value;
    public override string? GetAttribute(string name) => inner.GetAttribute(name);
    public override string? GetAttribute(string name, string? namespaceURI) => inner.GetAttribute(name, namespaceURI);
    public override string GetAttribute(int i) => inner.GetAttribute(i);
    public override string? LookupNamespace(string prefix) => inner.LookupNamespace(prefix);
    public override bool MoveToAttribute(string name) => inner.MoveToAttribute(name);
    public override bool MoveToAttribute(string name, string? ns) => inner.MoveToAttribute(name, ns);
    public override bool MoveToElement() => inner.MoveToElement();
    public override bool MoveToFirstAttribute() => inner.MoveToFirstAttribute();
    public override bool MoveToNextAttribute() => inner.MoveToNextAttribute();
    public override bool ReadAttributeValue() => inner.ReadAttributeValue();
    public override void ResolveEntity() => inner.ResolveEntity();
    public override void Close() => inner.Close();
}
