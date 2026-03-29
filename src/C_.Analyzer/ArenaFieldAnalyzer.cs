using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace C_.Analyzer;

/// <summary>
/// Forbids declaring <c>C_.Memory.Arena</c> as a field or as a property whose type is <c>Arena</c> when the
/// property is not backed by an associated compiler field (auto-properties are diagnosed via that implicit
/// field). Enclosing types include <c>class</c>, <c>struct</c>, and <c>ref struct</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArenaFieldAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(SdkDiagnostics.ArenaField);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var arenaType = compilationStart.Compilation.GetTypeByMetadataName("C_.Memory.Arena");
            if (arenaType is null)
                return;

            compilationStart.RegisterSymbolAction(
                c => AnalyzeField(c, arenaType),
                SymbolKind.Field);

            compilationStart.RegisterSymbolAction(
                c => AnalyzeProperty(c, arenaType),
                SymbolKind.Property);
        });
    }

    private static void AnalyzeField(SymbolAnalysisContext context, INamedTypeSymbol arenaType)
    {
        if (context.Symbol is not IFieldSymbol field)
            return;
        if (!TypeMatchesArena(field.Type, arenaType))
            return;
        if (!ShouldAnalyzeContainingType(field.ContainingType))
            return;

        Location? loc;
        if (field.IsImplicitlyDeclared)
        {
            if (field.AssociatedSymbol is not IPropertySymbol prop)
                return;
            loc = TryGetSourceLocation(prop);
        }
        else
        {
            loc = TryGetSourceLocation(field);
        }

        if (loc is null)
            return;

        Report(context, field.ContainingType, loc);
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, INamedTypeSymbol arenaType)
    {
        if (context.Symbol is not IPropertySymbol prop)
            return;
        if (prop.IsIndexer)
            return;
        if (!TypeMatchesArena(prop.Type, arenaType))
            return;
        if (!ShouldAnalyzeContainingType(prop.ContainingType))
            return;
        if (HasAssociatedFieldOfTypeArena(prop, arenaType))
            return;
        if (TryGetSourceLocation(prop) is not { } loc)
            return;

        Report(context, prop.ContainingType, loc);
    }

    /// <summary>
    /// True if the property has a compiler-associated field (e.g. auto-property backing) typed as
    /// <c>Arena</c>, so <see cref="AnalyzeField"/> already reports the implicit field at the property site.
    /// </summary>
    private static bool HasAssociatedFieldOfTypeArena(IPropertySymbol prop, INamedTypeSymbol arenaType)
    {
        foreach (var m in prop.ContainingType.GetMembers())
        {
            if (m is IFieldSymbol f
                && SymbolEqualityComparer.Default.Equals(f.AssociatedSymbol, prop)
                && TypeMatchesArena(f.Type, arenaType))
            {
                return true;
            }
        }

        return false;
    }

    private static void Report(SymbolAnalysisContext context, INamedTypeSymbol containing, Location loc)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            SdkDiagnostics.ArenaField,
            loc,
            containing.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static bool ShouldAnalyzeContainingType(INamedTypeSymbol type) =>
        type.TypeKind is TypeKind.Class or TypeKind.Struct;

    private static bool TypeMatchesArena(ITypeSymbol type, INamedTypeSymbol arenaType)
    {
        var t = type;
        if (t is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nt
            && nt.TypeArguments.Length == 1)
        {
            t = nt.TypeArguments[0];
        }

        return SymbolEqualityComparer.Default.Equals(t, arenaType);
    }

    private static Location? TryGetSourceLocation(ISymbol symbol)
    {
        foreach (var loc in symbol.Locations)
        {
            if (loc.IsInSource)
                return loc;
        }

        return null;
    }
}
