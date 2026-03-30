extern alias SdkOut;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace C_.Analyzer.Tests;

public sealed class AttributeBindingTests
{
    [Fact]
    public void ExemptAttribute_on_method_matches_sdk_type_symbol()
    {
        var src = """
            using C_;
            public class C
            {
                [Exempt(Reason = "test")]
                void M() { }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(src, path: "t.cs");
        var comp = CSharpCompilation.Create(
            "x",
            [tree],
            AnalyzerTestHarness.LoadPlatformReferences().Add(AnalyzerTestHarness.SdkReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);

        var model = comp.GetSemanticModel(tree);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "M");
        var sym = model.GetDeclaredSymbol(method);
        Assert.NotNull(sym);

        var sdkAsm = comp.References
            .Select(comp.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .Single(a => a.Name == "C_.SDK");
        var exemptFromAsm = sdkAsm.GlobalNamespace
            .GetNamespaceMembers()
            .Single(n => n.Name == "C_")
            .GetTypeMembers("ExemptAttribute")
            .Single();

        var attrClass = sym!.GetAttributes().Single().AttributeClass;
        Assert.NotNull(attrClass);
        Assert.True(SymbolEqualityComparer.Default.Equals(attrClass, exemptFromAsm));
    }
}
