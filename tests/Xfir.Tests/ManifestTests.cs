// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Xfir.Tests;

public class ManifestTests
{
    [Fact]
    public void EmbeddedProbeManifestUsesTheAssemblyVersion()
    {
        var library = LoadLibraryEx(StartupProcessTests.Probe, 0, 0x02 | 0x20);
        Assert.NotEqual(0, library);
        try
        {
            var resource = FindResource(library, 1, 24);
            Assert.NotEqual(0, resource);
            var size = SizeofResource(library, resource);
            Assert.InRange(size, 1U, 1_048_576U);
            var pointer = LockResource(LoadResource(library, resource));
            Assert.NotEqual(0, pointer);
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
            var xml = XDocument.Parse(Encoding.UTF8.GetString(bytes).Trim('\uFEFF', '\0'));
            var identity = xml.Root!.Element(XName.Get("assemblyIdentity", "urn:schemas-microsoft-com:asm.v1"))!;
            Assert.Equal(FileVersionInfo.GetVersionInfo(StartupProcessTests.Probe).FileVersion, identity.Attribute("version")!.Value);
        }
        finally { FreeLibrary(library); }
    }

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint LoadLibraryEx(string file, nint reserved, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "FindResourceW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint FindResource(nint module, nint name, nint type);
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint SizeofResource(nint module, nint resource);
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint LoadResource(nint module, nint resource);
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint LockResource(nint resource);
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool FreeLibrary(nint module);
}
