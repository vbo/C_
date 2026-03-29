using Xunit;

namespace C_.Analyzer.Tests;

/// <summary>C_0013 on type and method declarations under the hot path.</summary>
public sealed class GenericConstraintRuleTests
{
    [Fact]
    public async Task C0013_unconstrained_type_parameter_on_class()
    {
        var src = """
            public class C<T>
            {
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0013");
    }

    [Fact]
    public async Task C0013_interface_only_constraint()
    {
        var src = """
            using System;
            public class C<T> where T : IDisposable
            {
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0013");
    }

    [Fact]
    public async Task C0013_unconstrained_method_type_parameter()
    {
        var src = """
            public class C
            {
                void M<T>() { }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0013");
    }

    [Fact]
    public async Task C0013_struct_constraint_ok()
    {
        var src = """
            public class C<T> where T : struct
            {
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertEmpty(d);
    }

    [Fact]
    public async Task C0013_class_constraint_ok()
    {
        var src = """
            public class C<T> where T : class
            {
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertEmpty(d);
    }

    [Fact]
    public async Task C0013_not_applied_when_default_scope_exempt_without_hotpath()
    {
        var src = """
            public class C<T>
            {
            }
            """;
        var provider = new UniformAnalyzerConfigOptionsProvider([
            new KeyValuePair<string, string>("c_.default_scope", "exempt"),
        ]);
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, optionsProvider: provider);
        DiagnosticAssert.AssertEmpty(d);
    }

    [Fact]
    public async Task C0013_applied_under_exempt_default_with_HotPath_on_type()
    {
        var src = """
            [HotPath(Reason = "strict")]
            public class C<T>
            {
            }
            """;
        var provider = new UniformAnalyzerConfigOptionsProvider([
            new KeyValuePair<string, string>("c_.default_scope", "exempt"),
        ]);
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, optionsProvider: provider);
        DiagnosticAssert.AssertContainsIds(d, "C_0013");
    }
}
