using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace C_.Analyzer;

/// <summary>
/// Flags by-value copies of <c>C_.Memory.Arena</c> (assignment, locals, arguments, returns,
/// <c>?:</c> / <c>??</c> / <c>switch</c> expressions when any branch copies an existing instance) so
/// <c>ScopeGuard</c> cannot desynchronize from the live bump cursor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArenaCopyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(SdkDiagnostics.ArenaCopy);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStart =>
        {
            var arenaType = compilationStart.Compilation.GetTypeByMetadataName("C_.Memory.Arena");
            if (arenaType is null)
                return;

            compilationStart.RegisterOperationAction(
                c => AnalyzeSimpleAssignment(c, arenaType),
                OperationKind.SimpleAssignment);

            compilationStart.RegisterOperationAction(
                c => AnalyzeVariableDeclarator(c, arenaType),
                OperationKind.VariableDeclarator);

            compilationStart.RegisterOperationAction(
                c => AnalyzeArgument(c, arenaType),
                OperationKind.Argument);

            compilationStart.RegisterOperationAction(
                c => AnalyzeReturn(c, arenaType),
                OperationKind.Return);

            compilationStart.RegisterOperationAction(
                c => AnalyzeFieldInitializer(c, arenaType),
                OperationKind.FieldInitializer);
        });
    }

    private static void AnalyzeSimpleAssignment(OperationAnalysisContext context, INamedTypeSymbol arenaType)
    {
        if (context.Operation is not ISimpleAssignmentOperation assign)
            return;
        if (!IsArenaType(assign.Type, arenaType))
            return;
        if (assign.Target is IDiscardOperation)
            return;
        if (!CopiesExistingArena(assign.Value, arenaType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(SdkDiagnostics.ArenaCopy, assign.Syntax.GetLocation()));
    }

    private static void AnalyzeVariableDeclarator(OperationAnalysisContext context, INamedTypeSymbol arenaType)
    {
        if (context.Operation is not IVariableDeclaratorOperation decl)
            return;
        if (decl.Initializer is null)
            return;
        if (!IsArenaType(decl.Symbol.Type, arenaType))
            return;
        if (!CopiesExistingArena(decl.Initializer.Value, arenaType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            SdkDiagnostics.ArenaCopy,
            decl.Initializer.Syntax.GetLocation()));
    }

    private static void AnalyzeArgument(OperationAnalysisContext context, INamedTypeSymbol arenaType)
    {
        if (context.Operation is not IArgumentOperation arg)
            return;
        if (arg.Parameter is not { } parameter)
            return;
        if (parameter.RefKind != RefKind.None)
            return;
        if (!IsArenaType(parameter.Type, arenaType))
            return;
        if (!CopiesExistingArena(arg.Value, arenaType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(SdkDiagnostics.ArenaCopy, arg.Syntax.GetLocation()));
    }

    private static void AnalyzeReturn(OperationAnalysisContext context, INamedTypeSymbol arenaType)
    {
        if (context.Operation is not IReturnOperation ret)
            return;
        if (ret.ReturnedValue is null)
            return;
        if (context.ContainingSymbol is not IMethodSymbol method)
            return;
        if (method.ReturnsByRefReadonly || method.ReturnsByRef)
            return;
        if (!IsArenaType(method.ReturnType, arenaType))
            return;
        if (!CopiesExistingArena(ret.ReturnedValue, arenaType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            SdkDiagnostics.ArenaCopy,
            ret.ReturnedValue.Syntax.GetLocation()));
    }

    private static void AnalyzeFieldInitializer(OperationAnalysisContext context, INamedTypeSymbol arenaType)
    {
        if (context.Operation is not IFieldInitializerOperation init)
            return;
        if (init.InitializedFields.Length != 1)
            return;
        if (!IsArenaType(init.InitializedFields[0].Type, arenaType))
            return;
        if (!CopiesExistingArena(init.Value, arenaType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(SdkDiagnostics.ArenaCopy, init.Syntax.GetLocation()));
    }

    private static bool IsArenaType(ITypeSymbol? type, INamedTypeSymbol arenaType)
    {
        if (type is null)
            return false;
        var t = type is ITypeParameterSymbol tp ? tp : type;
        return SymbolEqualityComparer.Default.Equals(t, arenaType);
    }

    /// <summary>
    /// True when <paramref name="value"/> transfers an existing arena instance (not <c>new Arena(...)</c> or
    /// <c>default</c>).
    /// </summary>
    private static bool CopiesExistingArena(IOperation? value, INamedTypeSymbol arenaType)
    {
        if (value is null)
            return false;
        if (!IsArenaType(value.Type, arenaType))
            return false;

        value = Unwrap(value);

        switch (value)
        {
            case IObjectCreationOperation:
            case IDefaultValueOperation:
                return false;

            case ILocalReferenceOperation:
            case IParameterReferenceOperation:
            case IFieldReferenceOperation:
            case IPropertyReferenceOperation:
            case IArrayElementReferenceOperation:
                return true;

            case IConditionalOperation cond:
                return CopiesExistingArena(cond.WhenTrue, arenaType)
                    || CopiesExistingArena(cond.WhenFalse, arenaType);

            case ICoalesceOperation co:
                return CopiesExistingArena(co.Value, arenaType)
                    || CopiesExistingArena(co.WhenNull, arenaType);

            case ISwitchExpressionOperation sw:
                foreach (var arm in sw.Arms)
                {
                    if (CopiesExistingArena(arm.Value, arenaType))
                        return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static IOperation Unwrap(IOperation op)
    {
        while (true)
        {
            switch (op)
            {
                case IConversionOperation c:
                    op = c.Operand;
                    continue;
                case IParenthesizedOperation p:
                    op = p.Operand;
                    continue;
                default:
                    return op;
            }
        }
    }
}
