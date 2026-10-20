// SPDX-License-Identifier: EUPL-1.2
using PdfSharp.Fonts;

namespace Xfir.Rendering;

/// <summary>Embedded, unmodified Liberation Sans 2.1.5 (SIL OFL 1.1); no OS font lookup.</summary>
public sealed class PortableFontResolver : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? (isItalic ? "BoldItalic" : "Bold") : (isItalic ? "Italic" : "Regular"));

    public byte[] GetFont(string faceName)
    {
        if (faceName is not ("Regular" or "Bold" or "Italic" or "BoldItalic"))
            throw new ArgumentException("Carattere non previsto.");
        using var stream = typeof(PortableFontResolver).Assembly.GetManifestResourceStream(
            $"Xfir.Rendering.Fonts.LiberationSans-{faceName}.ttf")
            ?? throw new InvalidOperationException("Font Liberation Sans incorporato non disponibile.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }
}
