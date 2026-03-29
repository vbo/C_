using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace C_.Analyzer;

/// <summary>
/// Enforces C_ hot-path rules: allocations, I/O, reflection, dispatch, exemptions, and related
/// diagnostics.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HotPathAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => HotPathDiagnostics.All;

    /// <summary>
    /// Registers operation, symbol, and syntax callbacks for hot-path analysis.
    /// </summary>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var compilation = compilationStart.Compilation;
            var exemptAttr = ExemptMetadata.GetExemptAttributeType(compilation);
            var debugExemptAttr = ExemptMetadata.GetDebugExemptAttributeType(compilation);
            var compilationDefinesDebug = ExemptMetadata.CompilationDefinesDebug(compilation);
            var systemConditionalAttr = ExemptMetadata.GetSystemConditionalAttributeType(compilation);
            var hotPathAttr = ExemptMetadata.GetHotPathAttributeType(compilation);
            var entryPoint = compilation.GetEntryPoint(compilationStart.CancellationToken);
            var configProvider = compilationStart.Options.AnalyzerConfigOptionsProvider;

            compilationStart.RegisterOperationAction(
                c => AnalyzeOperation(
                    c,
                    exemptAttr,
                    debugExemptAttr,
                    systemConditionalAttr,
                    compilationDefinesDebug,
                    entryPoint,
                    configProvider,
                    hotPathAttr),
                OperationKind.Throw,
                OperationKind.ObjectCreation,
                OperationKind.ArrayCreation,
                OperationKind.AnonymousObjectCreation,
                OperationKind.InterpolatedString,
                OperationKind.Await,
                OperationKind.Invocation,
                OperationKind.Conversion);

            compilationStart.RegisterSymbolAction(
                c => AnalyzeSymbol(
                    c,
                    exemptAttr,
                    debugExemptAttr,
                    systemConditionalAttr,
                    compilationDefinesDebug,
                    configProvider,
                    hotPathAttr),
                SymbolKind.NamedType,
                SymbolKind.Method);

            compilationStart.RegisterSyntaxNodeAction(
                c => AnalyzeSyntaxNode(
                    c,
                    exemptAttr,
                    debugExemptAttr,
                    systemConditionalAttr,
                    compilationDefinesDebug,
                    configProvider,
                    hotPathAttr),
                SyntaxKind.QueryExpression,
                SyntaxKind.YieldReturnStatement,
                SyntaxKind.AddExpression,
                SyntaxKind.CatchClause);

            compilationStart.RegisterSyntaxNodeAction(
                c => AnalyzeClosureSyntax(
                    c,
                    exemptAttr,
                    debugExemptAttr,
                    systemConditionalAttr,
                    compilationDefinesDebug,
                    configProvider,
                    hotPathAttr),
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression,
                SyntaxKind.AnonymousMethodExpression,
                SyntaxKind.LocalFunctionStatement);
        });
    }

    /// <summary>
    /// True if the operation’s containing symbol is analyzed as hot path (see
    /// <see cref="HotPathScope.IsEffectiveHotPath"/>).
    /// </summary>
    private static bool IsHotPath(
        OperationAnalysisContext context,
        INamedTypeSymbol? exemptAttr,
        INamedTypeSymbol? debugExemptAttr,
        INamedTypeSymbol? systemConditionalAttr,
        INamedTypeSymbol? hotPathAttr,
        bool compilationDefinesDebug,
        AnalyzerConfigOptionsProvider configProvider)
    {
        if (exemptAttr is null && debugExemptAttr is null)
            return false;

        return HotPathScope.IsEffectiveHotPath(
            context.ContainingSymbol,
            context.Operation.Syntax.SyntaxTree,
            configProvider,
            exemptAttr,
            debugExemptAttr,
            systemConditionalAttr,
            hotPathAttr,
            compilationDefinesDebug);
    }

    /// <summary>
    /// True if the syntax node’s enclosing symbol is analyzed as hot path (see
    /// <see cref="HotPathScope.IsEffectiveHotPath"/>).
    /// </summary>
    private static bool IsHotPath(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? exemptAttr,
        INamedTypeSymbol? debugExemptAttr,
        INamedTypeSymbol? systemConditionalAttr,
        INamedTypeSymbol? hotPathAttr,
        bool compilationDefinesDebug,
        AnalyzerConfigOptionsProvider configProvider)
    {
        if (exemptAttr is null && debugExemptAttr is null)
            return false;

        var model = context.SemanticModel;
        var symbol = model.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);
        return symbol is not null
            && HotPathScope.IsEffectiveHotPath(
                symbol,
                context.Node.SyntaxTree,
                configProvider,
                exemptAttr,
                debugExemptAttr,
                systemConditionalAttr,
                hotPathAttr,
                compilationDefinesDebug);
    }

    /// <summary>
    /// True if <paramref name="containingSymbol"/> is the compilation entry point or nested lexically
    /// inside it (C_.0017 exception for <c>Main</c>).
    /// </summary>
    private static bool IsUnderEntryPoint(ISymbol? containingSymbol, IMethodSymbol? entryPoint)
    {
        if (entryPoint is null || containingSymbol is null)
            return false;

        for (var s = containingSymbol; s is not null; s = s.ContainingSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(s, entryPoint))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Handles throw, allocations, await, conversions, invocations, and C_.0017 exempt-callee checks
    /// for hot-path operations.
    /// </summary>
    private static void AnalyzeOperation(
        OperationAnalysisContext context,
        INamedTypeSymbol? exemptAttr,
        INamedTypeSymbol? debugExemptAttr,
        INamedTypeSymbol? systemConditionalAttr,
        bool compilationDefinesDebug,
        IMethodSymbol? entryPoint,
        AnalyzerConfigOptionsProvider configProvider,
        INamedTypeSymbol? hotPathAttr)
    {
        var op = context.Operation;
        var callerHot = IsHotPath(
            context,
            exemptAttr,
            debugExemptAttr,
            systemConditionalAttr,
            hotPathAttr,
            compilationDefinesDebug,
            configProvider);
        var forbidMarkedCallee = callerHot && !IsUnderEntryPoint(context.ContainingSymbol, entryPoint);

        if (forbidMarkedCallee && exemptAttr is not null)
        {
            if (op is IInvocationOperation inv &&
                ExemptMetadata.CalleeDeclaresExempt(inv.TargetMethod, exemptAttr))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    HotPathDiagnostics.HotPathCallsExemptMarked,
                    inv.Syntax.GetLocation(),
                    inv.TargetMethod.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
                return;
            }

            if (op is IObjectCreationOperation { Constructor: { } ctor } oc &&
                ExemptMetadata.CalleeDeclaresExempt(ctor, exemptAttr))
            {
                var calleeLabel = oc.Type?.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)
                    ?? ctor.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                context.ReportDiagnostic(Diagnostic.Create(
                    HotPathDiagnostics.HotPathCallsExemptMarked,
                    oc.Syntax.GetLocation(),
                    calleeLabel));
                return;
            }
        }

        if (!callerHot)
            return;

        switch (op.Kind)
        {
            case OperationKind.Throw:
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.Throw, op.Syntax.GetLocation()));
                return;

            case OperationKind.ObjectCreation:
                if (op is IObjectCreationOperation oc)
                {
                    if (oc.Constructor is { } ctor && HotPathIoRules.IsDisallowedIo(ctor))
                    {
                        var label = oc.Type?.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)
                            ?? ctor.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                        context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.HotPathIo, oc.Syntax.GetLocation(), label));
                        return;
                    }

                    if (oc.Type is null || oc.Type.IsReferenceType)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            HotPathDiagnostics.HeapAllocation,
                            op.Syntax.GetLocation(),
                            "new (reference type)"));
                    }
                }

                return;

            case OperationKind.ArrayCreation:
                context.ReportDiagnostic(Diagnostic.Create(
                    HotPathDiagnostics.HeapAllocation,
                    op.Syntax.GetLocation(),
                    "new[] / new T[n]"));
                return;

            case OperationKind.AnonymousObjectCreation:
                context.ReportDiagnostic(Diagnostic.Create(
                    HotPathDiagnostics.HeapAllocation,
                    op.Syntax.GetLocation(),
                    "anonymous type"));
                return;

            case OperationKind.InterpolatedString:
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.StringInterpolation, op.Syntax.GetLocation()));
                return;

            case OperationKind.Await:
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.Await, op.Syntax.GetLocation()));
                return;

            case OperationKind.Conversion:
                if (op is IConversionOperation conv &&
                    conv.IsImplicit &&
                    conv.Type is { IsReferenceType: true } &&
                    conv.Operand?.Type is { IsValueType: true, TypeKind: not TypeKind.Pointer } ot &&
                    !ot.IsRefLikeType)
                {
                    context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.Boxing, op.Syntax.GetLocation()));
                }

                return;

            case OperationKind.Invocation:
                AnalyzeInvocation(context, (IInvocationOperation)op);
                return;
        }
    }

    /// <summary>
    /// Reports lambdas, anonymous methods, and local functions that capture outer variables on the hot
    /// path (C_.0011).
    /// </summary>
    private static void AnalyzeClosureSyntax(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? exemptAttr,
        INamedTypeSymbol? debugExemptAttr,
        INamedTypeSymbol? systemConditionalAttr,
        bool compilationDefinesDebug,
        AnalyzerConfigOptionsProvider configProvider,
        INamedTypeSymbol? hotPathAttr)
    {
        if (!IsHotPath(
                context,
                exemptAttr,
                debugExemptAttr,
                systemConditionalAttr,
                hotPathAttr,
                compilationDefinesDebug,
                configProvider))
            return;

        if (context.SemanticModel is not { } model)
            return;

        if (context.Node is AnonymousFunctionExpressionSyntax anon)
        {
            var df = model.AnalyzeDataFlow(anon);
            if (df is { Captured: { IsEmpty: false } })
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.ClosureCapture, context.Node.GetLocation()));
            return;
        }

        if (context.Node is LocalFunctionStatementSyntax local)
        {
            var df = model.AnalyzeDataFlow(local);
            if (df is { Captured: { IsEmpty: false } })
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.ClosureCapture, context.Node.GetLocation()));
        }
    }

    /// <summary>
    /// Hot-path checks for I/O, string APIs, reflection, <see cref="ArrayPool{T}"/>, interface
    /// dispatch, and related invocation patterns.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context, IInvocationOperation inv)
    {
        var method = inv.TargetMethod;

        if (HotPathIoRules.IsDisallowedIo(method))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                HotPathDiagnostics.HotPathIo,
                inv.Syntax.GetLocation(),
                method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
            return;
        }

        if (method.Name == "ToString" &&
            method.Parameters.Length == 0 &&
            inv.Instance is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.ToStringCall, inv.Syntax.GetLocation()));
            return;
        }

        if (method.Name == "Format" &&
            method.ContainingType?.SpecialType == SpecialType.System_String)
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.StringFormat, inv.Syntax.GetLocation()));
            return;
        }

        if (method.Name == "Rent" &&
            method.ContainingType?.Name == "ArrayPool" &&
            method.ContainingType.ContainingNamespace?.ToDisplayString() == "System.Buffers")
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.ArrayPoolRent, inv.Syntax.GetLocation()));
            return;
        }

        if (method.Name == "GetType" &&
            method.Parameters.Length == 0 &&
            inv.Instance is not null &&
            method.ContainingType?.SpecialType == SpecialType.System_Object)
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.Reflection, inv.Syntax.GetLocation(), "GetType()"));
            return;
        }

        var ns = method.ContainingType?.ContainingNamespace?.ToDisplayString();
        if (ns == "System.Reflection")
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.Reflection, inv.Syntax.GetLocation(), method.Name));
            return;
        }

        if (method.ContainingType?.Name == "Activator" && ns == "System")
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.Reflection, inv.Syntax.GetLocation(), "Activator." + method.Name));
            return;
        }

        if (method.ContainingType?.Name == "Marshal" &&
            method.ContainingType.ContainingNamespace?.ToDisplayString() == "System.Runtime.InteropServices")
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.Reflection, inv.Syntax.GetLocation(), "Marshal." + method.Name));
            return;
        }

        if (inv.Instance?.Type?.TypeKind == TypeKind.Interface)
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.InterfaceDispatch, inv.Syntax.GetLocation()));
        }
    }

    /// <summary>
    /// Syntax-only rules: LINQ queries, <c>yield return</c>, string <c>+</c>, and <c>catch</c> on the hot
    /// path.
    /// </summary>
    private static void AnalyzeSyntaxNode(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? exemptAttr,
        INamedTypeSymbol? debugExemptAttr,
        INamedTypeSymbol? systemConditionalAttr,
        bool compilationDefinesDebug,
        AnalyzerConfigOptionsProvider configProvider,
        INamedTypeSymbol? hotPathAttr)
    {
        if (!IsHotPath(
                context,
                exemptAttr,
                debugExemptAttr,
                systemConditionalAttr,
                hotPathAttr,
                compilationDefinesDebug,
                configProvider))
            return;

        switch (context.Node)
        {
            case QueryExpressionSyntax query:
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.LinqQuery, query.GetLocation()));
                return;

            case YieldStatementSyntax y when y.IsKind(SyntaxKind.YieldReturnStatement):
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.YieldReturn, y.GetLocation()));
                return;

            case BinaryExpressionSyntax add when add.IsKind(SyntaxKind.AddExpression):
                var tLeft = context.SemanticModel.GetTypeInfo(add.Left, context.CancellationToken).Type;
                var tRight = context.SemanticModel.GetTypeInfo(add.Right, context.CancellationToken).Type;
                if (tLeft?.SpecialType == SpecialType.System_String || tRight?.SpecialType == SpecialType.System_String)
                {
                    context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.StringConcat, add.GetLocation()));
                }

                return;

            case CatchClauseSyntax catchClause:
                context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.CatchOnHotPath, catchClause.CatchKeyword.GetLocation()));
                return;
        }
    }

    /// <summary>
    /// Type and generic-method declarations: unconstrained or interface-only type parameters (C_.0013).
    /// </summary>
    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol? exemptAttr,
        INamedTypeSymbol? debugExemptAttr,
        INamedTypeSymbol? systemConditionalAttr,
        bool compilationDefinesDebug,
        AnalyzerConfigOptionsProvider configProvider,
        INamedTypeSymbol? hotPathAttr)
    {
        if (exemptAttr is null && debugExemptAttr is null)
            return;

        switch (context.Symbol)
        {
            case INamedTypeSymbol nt:
                if (!nt.Locations.Any(static l => l.IsInSource))
                    return;

                if (!HotPathScope.IsEffectiveHotPath(
                        nt,
                        syntaxTree: null,
                        configProvider,
                        exemptAttr,
                        debugExemptAttr,
                        systemConditionalAttr,
                        hotPathAttr,
                        compilationDefinesDebug))
                    return;

                foreach (var tp in nt.TypeParameters)
                {
                    var loc = nt.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is TypeDeclarationSyntax tds
                        ? tds.Identifier.GetLocation()
                        : Location.None;
                    ReportBadTypeParameter(context, tp, loc);
                }

                return;

            case IMethodSymbol { Arity: > 0 } ms:
                if (!ms.Locations.Any(static l => l.IsInSource))
                    return;

                if (!HotPathScope.IsEffectiveHotPath(
                        ms,
                        syntaxTree: null,
                        configProvider,
                        exemptAttr,
                        debugExemptAttr,
                        systemConditionalAttr,
                        hotPathAttr,
                        compilationDefinesDebug))
                    return;

                foreach (var tp in ms.TypeParameters)
                {
                    var loc = ms.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) switch
                    {
                        MethodDeclarationSyntax m => m.Identifier.GetLocation(),
                        _ => Location.None
                    };
                    ReportBadTypeParameter(context, tp, loc);
                }

                return;
        }
    }

    /// <summary>
    /// Emits C_.0013 when <paramref name="tp"/> is unconstrained or constrained only to interfaces.
    /// </summary>
    private static void ReportBadTypeParameter(SymbolAnalysisContext context, ITypeParameterSymbol tp, Location location)
    {
        if (tp.ConstraintTypes.Any(ct => ct.TypeKind == TypeKind.Interface))
        {
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.BadTypeParameterConstraint, location, tp.Name));
            return;
        }

        var hasAnyConstraint =
            tp.HasValueTypeConstraint ||
            tp.HasReferenceTypeConstraint ||
            tp.HasUnmanagedTypeConstraint ||
            tp.HasNotNullConstraint ||
            !tp.ConstraintTypes.IsEmpty;

        if (!hasAnyConstraint)
            context.ReportDiagnostic(Diagnostic.Create(HotPathDiagnostics.BadTypeParameterConstraint, location, tp.Name));
    }
}
