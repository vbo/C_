using Xunit;

namespace C_.Analyzer.Tests;

/// <summary>EditorConfig default scope, DebugExempt, and Conditional("DEBUG").</summary>
public sealed class ScopeAndDebugExemptTests
{
    [Fact]
    public async Task Default_scope_exempt_suppresses_without_hotpath_opt_in()
    {
        var src = """
            public class C
            {
                void M()
                {
                    _ = new object();
                }
            }
            """;
        var provider = new UniformAnalyzerConfigOptionsProvider([
            new KeyValuePair<string, string>("c_.default_scope", "exempt"),
        ]);
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, optionsProvider: provider);
        DiagnosticAssert.AssertEmpty(d);
    }

    [Fact]
    public async Task Default_scope_exempt_HotPath_on_method_restores_rules()
    {
        var src = """
            public class C
            {
                [HotPath(Reason = "opt-in")]
                void M()
                {
                    _ = new object();
                }
            }
            """;
        var provider = new UniformAnalyzerConfigOptionsProvider([
            new KeyValuePair<string, string>("c_.default_scope", "exempt"),
        ]);
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, optionsProvider: provider);
        DiagnosticAssert.AssertContainsIds(d, "C_0002");
    }

    [Fact]
    public async Task Default_scope_hot_explicit_same_as_strict()
    {
        var src = """
            public class C
            {
                void M() { _ = new object(); }
            }
            """;
        var provider = new UniformAnalyzerConfigOptionsProvider([
            new KeyValuePair<string, string>("c_.default_scope", "hot"),
        ]);
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, optionsProvider: provider);
        DiagnosticAssert.AssertContainsIds(d, "C_0002");
    }

    [Fact]
    public async Task Partial_type_one_part_hot_file_triggers_on_hot_part()
    {
        var sources = new (string fileName, string text)[]
        {
            ("exempt.cs", """
            partial class Box
            {
            }
            """),
            ("hot.cs", """
            partial class Box
            {
                void M() { _ = new object(); }
            }
            """),
        };
        var provider = new PerTreeAnalyzerConfigOptionsProvider(new Dictionary<string, IEnumerable<KeyValuePair<string, string>>>
        {
            ["exempt.cs"] = [new KeyValuePair<string, string>("c_.default_scope", "exempt")],
            ["hot.cs"] = [],
        });
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(
            sources,
            optionsProvider: provider);
        DiagnosticAssert.AssertContainsIds(d, "C_0002");
    }

    [Fact]
    public async Task DebugExempt_allows_heap_when_DEBUG_defined()
    {
        var src = """
            public class C
            {
                [DebugExempt(Reason = "test")]
                void M()
                {
                    _ = new object();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, defineDebug: true);
        DiagnosticAssert.AssertEmpty(d);
    }

    [Fact]
    public async Task DebugExempt_ignored_when_DEBUG_not_defined()
    {
        var src = """
            public class C
            {
                [DebugExempt(Reason = "test")]
                void M()
                {
                    _ = new object();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, defineDebug: false);
        DiagnosticAssert.AssertContainsIds(d, "C_0002");
    }

    [Fact]
    public async Task Conditional_DEBUG_method_body_skipped_when_DEBUG_undefined()
    {
        var src = """
            using System;
            using System.Diagnostics;
            public class C
            {
                [Conditional("DEBUG")]
                void Trace()
                {
                    Console.WriteLine("x");
                }

                void M() { Trace(); }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, defineDebug: false);
        DiagnosticAssert.AssertEmpty(d);
    }

    [Fact]
    public async Task Conditional_DEBUG_does_not_skip_when_DEBUG_defined()
    {
        var src = """
            using System;
            using System.Diagnostics;
            public class C
            {
                [Conditional("DEBUG")]
                void Trace()
                {
                    Console.WriteLine("x");
                }

                void M() { Trace(); }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, defineDebug: true);
        DiagnosticAssert.AssertContainsIds(d, "C_0016");
    }

    [Fact]
    public async Task DebugExempt_plus_Conditional_when_DEBUG_allows_printf_body()
    {
        var src = """
            using System;
            using System.Diagnostics;
            public class C
            {
                [Conditional("DEBUG")]
                [DebugExempt(Reason = "printf")]
                void Trace(string m)
                {
                    Console.WriteLine(m);
                }

                void M() { Trace("x"); }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src, defineDebug: true);
        DiagnosticAssert.AssertEmpty(d);
    }
}
