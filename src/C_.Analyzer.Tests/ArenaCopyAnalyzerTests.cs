using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Xunit;

namespace C_.Analyzer.Tests;

public sealed class ArenaCopyAnalyzerTests
{
    private const string Usings = """
        using System;
        using C_.Memory;

        """;

    [Fact]
    public async Task C0019_local_copy_from_local()
    {
        var src = Usings + """
            public class C
            {
                void M()
                {
                    Span<byte> s = stackalloc byte[16];
                    var a = new Arena(s);
                    var b = a;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0001", 1);
    }

    [Fact]
    public async Task C0019_assignment_copy()
    {
        var src = Usings + """
            public class C
            {
                void M()
                {
                    Span<byte> s = stackalloc byte[16];
                    var a = new Arena(s);
                    Arena b = default;
                    b = a;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0001", 1);
    }

    [Fact]
    public async Task C0019_by_value_argument()
    {
        var src = Usings + """
            public class C
            {
                static void Take(Arena a) { }

                void M()
                {
                    Span<byte> s = stackalloc byte[16];
                    var a = new Arena(s);
                    Take(a);
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0001", 1);
    }

    [Fact]
    public async Task C0019_return_copy()
    {
        var src = Usings + """
            public class C
            {
                Arena Echo(Arena a) => a;

                void M()
                {
                    Span<byte> s = stackalloc byte[16];
                    _ = Echo(new Arena(s));
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0001", 1);
    }

    [Fact]
    public async Task C0019_conditional_branch_copy()
    {
        var src = Usings + """
            public class C
            {
                void M(bool f)
                {
                    Span<byte> s = stackalloc byte[16];
                    var a = new Arena(s);
                    var b = new Arena(s);
                    var c = f ? a : b;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0001", 1);
    }

    [Fact]
    public async Task No_diagnostic_new_default_ref_param_scope()
    {
        var src = Usings + """
            public class C
            {
                static void TakeRef(ref Arena a) { }

                void M()
                {
                    Span<byte> s = stackalloc byte[16];
                    var a = new Arena(s);
                    var z = default(Arena);
                    TakeRef(ref a);
                    using (Arena.Scope(ref a)) { }
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        Assert.Empty(d.Where(x => x.Id == "C_SDK0001"));
    }

    [Fact]
    public async Task No_diagnostic_conditional_both_new()
    {
        var src = Usings + """
            public class C
            {
                void M(bool f)
                {
                    Span<byte> s = stackalloc byte[16];
                    Span<byte> t = stackalloc byte[8];
                    var c = f ? new Arena(s) : new Arena(t);
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        Assert.Empty(d.Where(x => x.Id == "C_SDK0001"));
    }

    [Fact]
    public async Task C0019_null_coalescing_copy_branch()
    {
        var src = Usings + """
            public class C
            {
                void M()
                {
                    Span<byte> s = stackalloc byte[16];
                    var a = new Arena(s);
                    Arena? n = null;
                    var c = n ?? a;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0001", 1);
    }

    [Fact]
    public async Task C0019_switch_expression_copy_arm()
    {
        var src = Usings + """
            public class C
            {
                void M(bool f)
                {
                    Span<byte> s = stackalloc byte[16];
                    var a = new Arena(s);
                    var b = new Arena(s);
                    var c = f switch { true => a, false => b };
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0001", 1);
    }
}
