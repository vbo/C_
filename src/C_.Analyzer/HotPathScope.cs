using System;
using C_;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace C_.Analyzer;

/// <summary>
/// EditorConfig key <c>c_.default_scope</c>: <c>exempt</c> (gradual adoption) vs <c>hot</c> or unset
/// (strict default). When exempt, use <see cref="HotPathAttribute"/> or a subtree with
/// <c>c_.default_scope = hot</c> to opt in.
/// </summary>
internal static class HotPathScope
{
    private const string DefaultScopeKey = "c_.default_scope";

    /// <summary>
    /// True when merged options for a syntax tree say default is &quot;exempt&quot; (not hot).
    /// Unset or &quot;hot&quot; → false (strict / legacy behavior).
    /// </summary>
    internal static bool MergedScopeIsExemptDefault(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(DefaultScopeKey, out var v))
            return false;

        return string.Equals(v.Trim(), "exempt", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True if <paramref name="symbol"/> or an ancestor method/type declares
    /// <see cref="HotPathAttribute"/>.
    /// </summary>
    internal static bool SymbolOrAncestorsDeclareHotPath(ISymbol symbol, INamedTypeSymbol? hotPathAttrType)
    {
        if (hotPathAttrType is null)
            return false;

        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            if (current is not (IMethodSymbol or INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct }))
                continue;

            foreach (var a in current.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, hotPathAttrType))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when every declaring syntax tree uses exempt default (for symbol callbacks with no single
    /// operation tree).
    /// </summary>
    internal static bool AllDeclaringSyntaxTreesUseExemptDefault(
        ISymbol symbol,
        AnalyzerConfigOptionsProvider provider)
    {
        var refs = symbol.DeclaringSyntaxReferences;
        if (refs.IsEmpty)
            return false;

        foreach (var r in refs)
        {
            var o = provider.GetOptions(r.SyntaxTree);
            if (!MergedScopeIsExemptDefault(o))
                return false;
        }

        return true;
    }

    /// <summary>
    /// True if any containing named type has a declaration file whose merged scope is not exempt-default.
    /// </summary>
    internal static bool AnyContainingNamedTypeHasNonExemptDefaultTree(
        ISymbol symbol,
        AnalyzerConfigOptionsProvider provider)
    {
        for (var s = symbol; s is not null; s = s.ContainingSymbol)
        {
            if (s is not INamedTypeSymbol nt)
                continue;

            foreach (var r in nt.DeclaringSyntaxReferences)
            {
                var o = provider.GetOptions(r.SyntaxTree);
                if (!MergedScopeIsExemptDefault(o))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether hot-path diagnostics apply to <paramref name="containingSymbol"/> for code in
    /// <paramref name="syntaxTree"/> (or symbol-only analysis when <paramref name="syntaxTree"/> is null).
    /// </summary>
    internal static bool IsEffectiveHotPath(
        ISymbol containingSymbol,
        SyntaxTree? syntaxTree,
        AnalyzerConfigOptionsProvider config,
        INamedTypeSymbol? exemptAttr,
        INamedTypeSymbol? debugExemptAttr,
        INamedTypeSymbol? systemConditionalAttr,
        INamedTypeSymbol? hotPathAttr,
        bool compilationDefinesDebug)
    {
        if (exemptAttr is null && debugExemptAttr is null)
            return false;

        if (ExemptMetadata.SymbolOrAncestorsSkipHotPathRules(
                containingSymbol,
                exemptAttr,
                debugExemptAttr,
                systemConditionalAttr,
                compilationDefinesDebug))
            return false;

        bool usesExemptDefault;
        if (syntaxTree is not null)
        {
            usesExemptDefault = MergedScopeIsExemptDefault(config.GetOptions(syntaxTree));
        }
        else
        {
            usesExemptDefault = AllDeclaringSyntaxTreesUseExemptDefault(
                containingSymbol,
                config);
        }

        if (!usesExemptDefault)
            return true;

        return SymbolOrAncestorsDeclareHotPath(containingSymbol, hotPathAttr)
            || AnyContainingNamedTypeHasNonExemptDefaultTree(containingSymbol, config);
    }
}
