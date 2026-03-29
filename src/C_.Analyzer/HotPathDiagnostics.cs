using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace C_.Analyzer;

/// <summary>
/// Diagnostic descriptors for hot-path rules; <see cref="All"/> is registered by
/// <see cref="HotPathAnalyzer"/>.
/// </summary>
internal static class HotPathDiagnostics
{
    internal static readonly DiagnosticDescriptor Throw = new(
        id: "C_0001",
        title: "Throw not allowed on the C_ hot path",
        messageFormat: "Do not throw on the C_ hot path without an [Exempt] or [DebugExempt] exemption (docs/lang.md §4.2)",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Throw, throw expressions, and bare rethrow are forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor CatchOnHotPath = new(
        id: "C_0018",
        title: "catch not allowed on the C_ hot path",
        messageFormat: "catch is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption (docs/lang.md §4.2)",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "catch clauses (including catch when) are forbidden on the hot path unless exempted; try/finally without catch remains permitted.");

    internal static readonly DiagnosticDescriptor HeapAllocation = new(
        id: "C_0002",
        title: "Heap allocation not allowed on the C_ hot path",
        messageFormat: "Heap allocation ('{0}') is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Reference-type, array, and anonymous-type allocations are forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor StringInterpolation = new(
        id: "C_0003",
        title: "String interpolation not allowed on the C_ hot path",
        messageFormat: "String interpolation is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Interpolated strings allocate; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor StringConcat = new(
        id: "C_0004",
        title: "String concatenation not allowed on the C_ hot path",
        messageFormat: "String concatenation with '+' is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "String concatenation allocates; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor StringFormat = new(
        id: "C_0005",
        title: "string.Format not allowed on the C_ hot path",
        messageFormat: "string.Format is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "string.Format allocates; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor ArrayPoolRent = new(
        id: "C_0006",
        title: "ArrayPool.Rent not allowed on the C_ hot path",
        messageFormat: "ArrayPool.Shared.Rent is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Renting pooled arrays is treated as allocation for hot-path purposes unless exempted.");

    internal static readonly DiagnosticDescriptor LinqQuery = new(
        id: "C_0007",
        title: "LINQ query syntax not allowed on the C_ hot path",
        messageFormat: "LINQ query syntax is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "LINQ allocates; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor YieldReturn = new(
        id: "C_0008",
        title: "yield return not allowed on the C_ hot path",
        messageFormat: "yield return is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Iterators allocate; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor Await = new(
        id: "C_0009",
        title: "await not allowed on the C_ hot path",
        messageFormat: "await is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Async patterns are out of scope for the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor Reflection = new(
        id: "C_0010",
        title: "Reflection not allowed on the C_ hot path",
        messageFormat: "Reflection or runtime type discovery ('{0}') is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Reflection APIs are forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor ClosureCapture = new(
        id: "C_0011",
        title: "Capturing closure not allowed on the C_ hot path",
        messageFormat: "Lambdas and local functions must not capture outer variables on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Capturing closures allocate; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor InterfaceDispatch = new(
        id: "C_0012",
        title: "Interface dispatch not allowed on the C_ hot path",
        messageFormat: "Calls through interface types are not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Interface instance dispatch is forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor BadTypeParameterConstraint = new(
        id: "C_0013",
        title: "Generic type parameter constraints violate C_ rules",
        messageFormat: "Type parameter '{0}' must be constrained and must not be constrained to interfaces (docs/lang.md §3.3)",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Unconstrained type parameters and interface constraints are forbidden for hot-path declarations.");

    internal static readonly DiagnosticDescriptor Boxing = new(
        id: "C_0014",
        title: "Implicit boxing not allowed on the C_ hot path",
        messageFormat: "Implicit boxing is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Implicit conversions that box value types allocate; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor ToStringCall = new(
        id: "C_0015",
        title: "ToString not allowed on the C_ hot path",
        messageFormat: "ToString is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ToString allocates; forbidden on the hot path unless exempted.");

    internal static readonly DiagnosticDescriptor HotPathIo = new(
        id: "C_0016",
        title: "I/O not allowed on the C_ hot path",
        messageFormat: "'{0}' performs I/O (console, network, filesystem, pipes, or similar) and is not allowed on the C_ hot path without an [Exempt] or [DebugExempt] exemption",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Hot-path frames must not perform console, network, filesystem, pipe, or serial I/O unless exempted.");

    internal static readonly DiagnosticDescriptor HotPathCallsExemptMarked = new(
        id: "C_0017",
        title: "Hot path must not call [Exempt] code",
        messageFormat: "The C_ hot path must not call '{0}', which is marked with [Exempt] (startup / exempt code only; docs/lang.md §8)",
        category: "C_",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Unexempt code may not invoke methods or constructors in a scope marked [Exempt]; entry Main may call startup only.");

    internal static ImmutableArray<DiagnosticDescriptor> All { get; } =
        ImmutableArray.Create(
            Throw,
            CatchOnHotPath,
            HeapAllocation,
            StringInterpolation,
            StringConcat,
            StringFormat,
            ArrayPoolRent,
            LinqQuery,
            YieldReturn,
            Await,
            Reflection,
            ClosureCapture,
            InterfaceDispatch,
            BadTypeParameterConstraint,
            Boxing,
            ToStringCall,
            HotPathIo,
            HotPathCallsExemptMarked);
}
