using Microsoft.CodeAnalysis;
using Xunit;

namespace C_.Analyzer.Tests;

/// <summary>C_0017: hot path must not call [Exempt] code; entry Main exception.</summary>
public sealed class HotPathCallsExemptTests
{
    [Fact]
    public async Task C0017_hot_path_calls_exempt_method()
    {
        var src = """
            public static class P
            {
                public static void Main()
                {
                    Help();
                }

                [Exempt(Reason = "startup")]
                static void Help() { }

                public static void Tick()
                {
                    Help();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(
            src,
            outputKind: OutputKind.ConsoleApplication);
        DiagnosticAssert.AssertContainsIds(d, "C_0017");
        DiagnosticAssert.AssertContainsIdCount(d, "C_0017", 1);
    }

    [Fact]
    public async Task C0017_Main_may_call_exempt()
    {
        var src = """
            public static class P
            {
                public static void Main()
                {
                    Init();
                }

                [Exempt(Reason = "startup")]
                static void Init()
                {
                    _ = new object();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(
            src,
            outputKind: OutputKind.ConsoleApplication);
        DiagnosticAssert.AssertEmpty(d);
    }

    [Fact]
    public async Task C0017_hot_path_new_exempt_type()
    {
        var src = """
            [Exempt(Reason = "cold")]
            class D
            {
                public D() { }
            }

            public static class P
            {
                public static void Main() { }

                public static void Tick()
                {
                    _ = new D();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(
            src,
            outputKind: OutputKind.ConsoleApplication);
        DiagnosticAssert.AssertContainsIds(d, "C_0017");
    }

    [Fact]
    public async Task C0017_no_entry_point_reports_all_hot_call_sites()
    {
        var src = """
            public static class P
            {
                [Exempt(Reason = "x")]
                static void Help() { }

                public static void Tick() { Help(); }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(
            src,
            outputKind: OutputKind.DynamicallyLinkedLibrary);
        DiagnosticAssert.AssertContainsIds(d, "C_0017");
    }
}
