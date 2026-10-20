// SPDX-License-Identifier: EUPL-1.2
using System.Reflection;

namespace Xfir.Core;

public static class ProductInfo
{
    // All projects receive this metadata from Directory.Build.props (or the packaging build).
    public static string Version { get; } = typeof(ProductInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(ProductInfo).Assembly.GetName().Version?.ToString(3) ?? "—";
}
