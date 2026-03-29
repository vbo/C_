extern alias SdkOut;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace C_.Analyzer.Tests;

public sealed class MetadataResolutionTests
{
    [Fact]
    public void Compilation_with_sdk_has_no_errors_and_resolves_exempt_via_sdk_assembly()
    {
        var src = """
            public class C { }
            """;
        var tree = CSharpSyntaxTree.ParseText(src, path: "t.cs");
        var comp = CSharpCompilation.Create(
            "x",
            [tree],
            AnalyzerTestHarness.LoadPlatformReferences().Add(AnalyzerTestHarness.SdkReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = comp.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        var sdkAsm = comp.References
            .Select(comp.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .Single(a => a.Name == "C_.SDK");
        var exempt = sdkAsm.GlobalNamespace
            .GetNamespaceMembers()
            .Single(n => n.Name == "C_")
            .GetTypeMembers("ExemptAttribute")
            .Single();
        Assert.NotNull(exempt);
        Assert.Equal(typeof(SdkOut::C_.ExemptAttribute).FullName, exempt.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""));
    }
}
