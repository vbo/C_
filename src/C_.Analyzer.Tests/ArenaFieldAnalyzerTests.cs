using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Xunit;

namespace C_.Analyzer.Tests;

public sealed class ArenaFieldAnalyzerTests
{
    private const string Usings = """
        using System;
        using C_.Memory;

        """;

    [Fact]
    public async Task C_SDK0002_ref_struct_instance_field()
    {
        var src = Usings + """
            public ref struct R
            {
                private Arena _arena;
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0002", 1);
    }

    [Fact]
    public async Task C_SDK0002_ref_struct_static_field()
    {
        var src = Usings + """
            public ref struct R
            {
                private static Arena _staticArena;
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0002", 1);
    }

    [Fact]
    public async Task C_SDK0002_expression_bodied_property_without_arena_field()
    {
        var src = Usings + """
            public ref struct R
            {
                private int _x;
                public Arena A => default;
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_SDK0002", 1);
    }

    [Fact]
    public async Task No_SDK0002_local_arena_only()
    {
        var src = Usings + """
            public class C
            {
                void M()
                {
                    Span<byte> s = stackalloc byte[16];
                    var arena = new Arena(s);
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        Assert.Empty(d.Where(x => x.Id == "C_SDK0002"));
    }
}
