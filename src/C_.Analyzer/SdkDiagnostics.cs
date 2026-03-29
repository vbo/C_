using Microsoft.CodeAnalysis;

namespace C_.Analyzer;

/// <summary>
/// Diagnostic descriptors for <c>C_.SDK</c> API contracts (<c>C_SDK*</c> ids). Registered by
/// <see cref="ArenaCopyAnalyzer"/> and <see cref="ArenaFieldAnalyzer"/>, not <see cref="HotPathAnalyzer"/>.
/// </summary>
internal static class SdkDiagnostics
{
    internal static readonly DiagnosticDescriptor ArenaCopy = new(
        id: "C_SDK0001",
        title: "Do not copy C_.Memory.Arena by value",
        messageFormat: "Do not copy Arena by value. The bump cursor is duplicated while the backing span is shared, so ScopeGuard rollback and allocation bookkeeping break. Use one variable and pass ref (e.g. Scope(ref arena)).",
        category: "C_SDK",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Arena is a ref struct bump allocator; by-value assignment, arguments, and returns copy _position separately from the shared span, breaking ScopeGuard rollback and allocation bookkeeping.");

    internal static readonly DiagnosticDescriptor ArenaField = new(
        id: "C_SDK0002",
        title: "C_.Memory.Arena must not be a field or property",
        messageFormat: "Do not declare Arena as a field or property on type '{0}'. Copying or moving the enclosing instance duplicates the bump cursor while the backing span may still be shared (see docs/sdk.md).",
        category: "C_SDK",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Arena must live as a local or parameter (typically ref), not as an instance of class, struct, or ref struct.");
}
