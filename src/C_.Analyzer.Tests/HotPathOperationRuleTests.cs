using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Xunit;

namespace C_.Analyzer.Tests;

/// <summary>Operation and syntax rules on the default (strict) hot path: C_0001–C_0012, C_0014–C_0016, C_0018.</summary>
public sealed class HotPathOperationRuleTests
{
    [Fact]
    public async Task C0001_throw_on_hot_path()
    {
        var src = """
            public class C
            {
                void M() { throw new System.Exception(); }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0001");
    }

    [Fact]
    public async Task C0018_catch_on_hot_path_try_finally_ok()
    {
        var catchSrc = """
            public class C
            {
                void M()
                {
                    try { }
                    catch { }
                }
            }
            """;
        var dCatch = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(catchSrc);
        DiagnosticAssert.AssertContainsIds(dCatch, "C_0018");

        var finallySrc = """
            public class C
            {
                void M()
                {
                    try { }
                    finally { }
                }
            }
            """;
        var dFinally = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(finallySrc);
        DiagnosticAssert.AssertEmpty(dFinally);
    }

    [Fact]
    public async Task C0002_heap_reference_array_anonymous_struct_new_ok()
    {
        var src = """
            public class C
            {
                void M()
                {
                    _ = new object();
                    _ = new int[1];
                    _ = new { X = 1 };
                }

                struct S { public int X; }

                void N()
                {
                    _ = new S();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_0002", 3);

        var structOnly = """
            public class C
            {
                struct S { public int X; }
                void M() { _ = new S(); }
            }
            """;
        var d2 = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(structOnly);
        DiagnosticAssert.AssertEmpty(d2);
    }

    [Fact]
    public async Task C0003_interpolation()
    {
        var src = """
            public class C
            {
                void M(int x) { _ = $"{x}"; }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0003");
    }

    [Fact]
    public async Task C0004_string_concat_with_plus()
    {
        var src = """
            public class C
            {
                void M() { _ = "a" + "b"; }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0004");
    }

    [Fact]
    public async Task C0005_string_Format()
    {
        var src = """
            public class C
            {
                void M() { _ = string.Format("{0}", 1); }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0005");
    }

    [Fact]
    public async Task C0006_ArrayPool_Rent()
    {
        var src = """
            using System.Buffers;
            public class C
            {
                void M()
                {
                    var a = ArrayPool<byte>.Shared.Rent(4);
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0006");
    }

    [Fact]
    public async Task C0007_query_syntax()
    {
        var src = """
            using System.Linq;
            public class C
            {
                void M(int[] xs)
                {
                    _ = from x in xs select x;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0007");
    }

    [Fact]
    public async Task C0008_yield_return()
    {
        var src = """
            using System.Collections.Generic;
            public class C
            {
                IEnumerable<int> M()
                {
                    yield return 1;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0008");
    }

    [Fact]
    public async Task C0009_await()
    {
        var src = """
            using System.Threading.Tasks;
            public class C
            {
                async Task M()
                {
                    await Task.CompletedTask;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0009");
    }

    [Fact]
    public async Task C0010_reflection_GetType_System_Reflection_Activator_Marshal()
    {
        var src = """
            using System;
            using System.Reflection;
            using System.Runtime.InteropServices;
            public class C
            {
                void M()
                {
                    _ = ((object)1).GetType();
                    _ = Activator.CreateInstance(typeof(C));
                    _ = Marshal.SizeOf(typeof(int));
                    _ = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.ObsoleteAttribute>(typeof(string));
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_0010", 4);
    }

    [Fact]
    public async Task C0011_closure_capture_lambda_and_local_function()
    {
        var src = """
            public class C
            {
                void M()
                {
                    var x = 1;
                    System.Func<int, int> f = a => a + x;
                    int Local() => x;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_0011", 2);

        var noCapture = """
            public class C
            {
                void M()
                {
                    System.Func<int, int> f = a => a + 1;
                    int Local(int a) => a + 1;
                }
            }
            """;
        var d2 = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(noCapture);
        DiagnosticAssert.AssertEmpty(d2);
    }

    [Fact]
    public async Task C0012_interface_dispatch()
    {
        var src = """
            using System;
            public class C
            {
                void M()
                {
                    IDisposable d = null!;
                    d.Dispose();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0012");
    }

    [Fact]
    public async Task C0014_implicit_boxing()
    {
        var src = """
            public class C
            {
                void M()
                {
                    object o = 1;
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0014");
    }

    [Fact]
    public async Task C0015_ToString()
    {
        var src = """
            public class C
            {
                void M()
                {
                    _ = 1.ToString();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0015");
    }

    [Fact]
    public async Task C0016_console_and_file_io()
    {
        var src = """
            using System;
            using System.IO;
            public class C
            {
                void M()
                {
                    Console.WriteLine("x");
                    _ = File.OpenRead("a.txt");
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIdCount(d, "C_0016", 2);
    }

    [Fact]
    public async Task C0016_network_namespace()
    {
        var src = """
            public class C
            {
                void M()
                {
                    _ = new System.Net.Sockets.TcpClient();
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertContainsIds(d, "C_0016");
    }

    [Fact]
    public async Task Exempt_suppresses_operation_rules()
    {
        var src = """
            public class C
            {
                [Exempt(Reason = "test")]
                void M()
                {
                    throw new System.Exception();
                    _ = new object();
                    _ = $"{1}";
                }
            }
            """;
        var d = await AnalyzerTestHarness.GetAnalyzerDiagnosticsAsync(src);
        DiagnosticAssert.AssertEmpty(d);
    }
}
