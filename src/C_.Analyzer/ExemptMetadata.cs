using System;
using System.Diagnostics;
using System.Linq;
using C_;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace C_.Analyzer;

/// <summary>
/// Attribute resolution for the compilation under analysis. SDK types are linked into the analyzer
/// assembly for packaging; when resolving symbols we prefer <c>C_.SDK</c> so
/// <see cref="Compilation.GetTypeByMetadataName"/> is unambiguous if another assembly also exposes
/// <c>C_.*Attribute</c> (e.g. the analyzer itself referenced alongside the SDK in the same process).
/// </summary>
internal static class ExemptMetadata
{
    /// <summary>
    /// Resolves <see cref="ExemptAttribute"/> in the compilation.
    /// </summary>
    internal static INamedTypeSymbol? GetExemptAttributeType(Compilation compilation) =>
        GetC_SdkAttributeType(compilation, typeof(ExemptAttribute).FullName!)
        ?? compilation.GetTypeByMetadataName(typeof(ExemptAttribute).FullName!);

    /// <summary>
    /// Resolves <see cref="DebugExemptAttribute"/> in the compilation.
    /// </summary>
    internal static INamedTypeSymbol? GetDebugExemptAttributeType(Compilation compilation) =>
        GetC_SdkAttributeType(compilation, typeof(DebugExemptAttribute).FullName!)
        ?? compilation.GetTypeByMetadataName(typeof(DebugExemptAttribute).FullName!);

    /// <summary>
    /// Resolves <see cref="ConditionalAttribute"/> for pairing with DEBUG and
    /// <see cref="DebugExemptAttribute"/>.
    /// </summary>
    internal static INamedTypeSymbol? GetSystemConditionalAttributeType(Compilation compilation) =>
        compilation.GetTypeByMetadataName(typeof(ConditionalAttribute).FullName!);

    /// <summary>
    /// Resolves <see cref="HotPathAttribute"/> for opt-in when <c>c_.default_scope = exempt</c>.
    /// </summary>
    internal static INamedTypeSymbol? GetHotPathAttributeType(Compilation compilation) =>
        GetC_SdkAttributeType(compilation, typeof(HotPathAttribute).FullName!)
        ?? compilation.GetTypeByMetadataName(typeof(HotPathAttribute).FullName!);

    /// <summary>
    /// Looks up a named type by metadata name (namespace segments + type name) inside the referenced
    /// <c>C_.SDK</c> assembly only.
    /// </summary>
    private static INamedTypeSymbol? GetC_SdkAttributeType(Compilation compilation, string metadataName)
    {
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm)
                continue;
            if (!string.Equals(asm.Name, "C_.SDK", StringComparison.Ordinal))
                continue;
            return FindTypeByMetadataNameInNamespaceTree(asm.GlobalNamespace, metadataName);
        }

        return null;
    }

    private static INamedTypeSymbol? FindTypeByMetadataNameInNamespaceTree(
        INamespaceSymbol globalNamespace,
        string metadataName)
    {
        var lastDot = metadataName.LastIndexOf('.');
        if (lastDot <= 0)
            return null;

        var nsPath = metadataName.Substring(0, lastDot);
        var typeName = metadataName.Substring(lastDot + 1);
        var current = globalNamespace;
        foreach (var segment in nsPath.Split('.'))
        {
            var next = current.GetNamespaceMembers().FirstOrDefault(n => n.Name == segment);
            if (next is null)
                return null;
            current = next;
        }

        return current.GetTypeMembers(typeName).FirstOrDefault();
    }

    /// <summary>
    /// True when at least one syntax tree is parsed with the DEBUG conditional compilation symbol.
    /// </summary>
    internal static bool CompilationDefinesDebug(Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (tree.Options is CSharpParseOptions opt)
            {
                foreach (var name in opt.PreprocessorSymbolNames)
                {
                    if (string.Equals(name, "DEBUG", StringComparison.Ordinal))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// True when hot-path rules should not apply to <paramref name="symbol"/> (walking containing
    /// symbols): <see cref="ExemptAttribute"/>; <see cref="DebugExemptAttribute"/> when
    /// <paramref name="compilationDefinesDebug"/>; or, when DEBUG is not defined, an enclosing method
    /// with <see cref="ConditionalAttribute"/>(&quot;DEBUG&quot;) (body treated like non–hot-path;
    /// not <see cref="ExemptAttribute"/>).
    /// </summary>
    internal static bool SymbolOrAncestorsSkipHotPathRules(
        ISymbol symbol,
        INamedTypeSymbol? exemptAttrType,
        INamedTypeSymbol? debugExemptAttrType,
        INamedTypeSymbol? systemConditionalAttributeType,
        bool compilationDefinesDebug)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            if (current is IMethodSymbol method &&
                !compilationDefinesDebug &&
                systemConditionalAttributeType is not null &&
                MethodHasConditionalSymbol(method, systemConditionalAttributeType, "DEBUG"))
            {
                return true;
            }

            if (current is not (IMethodSymbol or INamedTypeSymbol))
                continue;

            foreach (var a in current.GetAttributes())
            {
                var ac = a.AttributeClass;
                if (ac is null)
                    continue;

                if (exemptAttrType is not null && SymbolEqualityComparer.Default.Equals(ac, exemptAttrType))
                    return true;

                if (compilationDefinesDebug && debugExemptAttrType is not null &&
                    SymbolEqualityComparer.Default.Equals(ac, debugExemptAttrType))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True if the method has [Conditional(symbol)] with the given condition string (e.g. DEBUG).
    /// </summary>
    private static bool MethodHasConditionalSymbol(
        IMethodSymbol method,
        INamedTypeSymbol conditionalAttributeType,
        string conditionSymbol)
    {
        foreach (var a in method.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(a.AttributeClass, conditionalAttributeType))
                continue;

            foreach (var arg in a.ConstructorArguments)
            {
                if (arg.Value is string s && string.Equals(s, conditionSymbol, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True if the method (e.g. callee) or any containing class/struct declares [Exempt] (not
    /// [DebugExempt]). Used for C_0017 — hot path must not call permanent exempt code.
    /// </summary>
    internal static bool CalleeDeclaresExempt(IMethodSymbol method, INamedTypeSymbol? exemptAttrType)
    {
        if (exemptAttrType is null)
            return false;

        for (var current = (ISymbol)method; current is not null; current = current.ContainingSymbol)
        {
            if (current is not (IMethodSymbol or INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct }))
                continue;

            if (current.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, exemptAttrType)))
                return true;
        }

        return false;
    }
}
