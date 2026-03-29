extern alias SdkOut;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace C_.Analyzer.Tests;

/// <summary>
/// Builds compilations with platform references + C_.SDK, runs <see cref="HotPathAnalyzer"/>,
/// <see cref="ArenaCopyAnalyzer"/>, and <see cref="ArenaFieldAnalyzer"/>, and optional per-tree editorconfig.
/// </summary>
internal static class AnalyzerTestHarness
{
    private static readonly ImmutableArray<MetadataReference> s_platformRefs = LoadPlatformReferences();

    internal static ImmutableArray<MetadataReference> LoadPlatformReferences()
    {
        var raw = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(raw))
            throw new InvalidOperationException("TRUSTED_PLATFORM_ASSEMBLIES is not set; run tests on .NET (dotnet test).");

        return raw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(static path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return !string.Equals(name, "C_.Analyzer", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, "C_.Analyzer.Tests", StringComparison.OrdinalIgnoreCase);
            })
            .Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    internal static MetadataReference SdkReference { get; } =
        MetadataReference.CreateFromFile(typeof(SdkOut::C_.ExemptAttribute).Assembly.Location);

    internal static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        IReadOnlyList<(string fileName, string text)> sources,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary,
        AnalyzerConfigOptionsProvider? optionsProvider = null,
        bool defineDebug = false)
    {
        var symbols = defineDebug
            ? ImmutableArray.Create("DEBUG")
            : ImmutableArray<string>.Empty;

        var parse = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(symbols);

        var trees = new List<SyntaxTree>();
        foreach (var (fileName, text) in sources)
            trees.Add(CSharpSyntaxTree.ParseText("using C_;\n" + text, parse, path: fileName));

        var refs = s_platformRefs.Add(SdkReference);
        var comp = CSharpCompilation.Create(
            assemblyName: "TestAsm",
            syntaxTrees: trees,
            references: refs,
            options: new CSharpCompilationOptions(outputKind)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var analyzerOptions = optionsProvider is null
            ? new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty)
            : new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, optionsProvider);

        var withAnalyzers = comp.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(
                new HotPathAnalyzer(),
                new ArenaCopyAnalyzer(),
                new ArenaFieldAnalyzer()),
            analyzerOptions);

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    internal static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary,
        AnalyzerConfigOptionsProvider? optionsProvider = null,
        bool defineDebug = false) =>
        GetAnalyzerDiagnosticsAsync([("Test.cs", source)], outputKind, optionsProvider, defineDebug);
}

/// <summary>Editorconfig-style key/value pairs applied to every syntax tree.</summary>
internal sealed class UniformAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly TestAnalyzerConfigOptions _options;

    internal UniformAnalyzerConfigOptionsProvider(IEnumerable<KeyValuePair<string, string>> pairs) =>
        _options = new TestAnalyzerConfigOptions(pairs);

    public override AnalyzerConfigOptions GlobalOptions => TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText text) => TestAnalyzerConfigOptions.Empty;
}

internal sealed class PerTreeAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly ImmutableDictionary<string, AnalyzerConfigOptions> _byFileName;

    internal PerTreeAnalyzerConfigOptionsProvider(
        IReadOnlyDictionary<string, IEnumerable<KeyValuePair<string, string>>> optionsByFileName)
    {
        var b = ImmutableDictionary.CreateBuilder<string, AnalyzerConfigOptions>(StringComparer.OrdinalIgnoreCase);
        foreach (var (file, pairs) in optionsByFileName)
            b[file] = new TestAnalyzerConfigOptions(pairs);
        _byFileName = b.ToImmutable();
    }

    public override AnalyzerConfigOptions GlobalOptions => TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
    {
        var name = Path.GetFileName(tree.FilePath);
        return _byFileName.TryGetValue(name, out var o) ? o : TestAnalyzerConfigOptions.Empty;
    }

    public override AnalyzerConfigOptions GetOptions(AdditionalText text) => TestAnalyzerConfigOptions.Empty;
}

internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    internal static readonly TestAnalyzerConfigOptions Empty = new([]);

    private readonly ImmutableDictionary<string, string> _values;

    internal TestAnalyzerConfigOptions(IEnumerable<KeyValuePair<string, string>> pairs) =>
        _values = pairs.ToImmutableDictionary(pair => pair.Key, pair => pair.Value, KeyComparer);

    public override bool TryGetValue(string key, out string value) =>
        _values.TryGetValue(key, out value!);

    public override IEnumerable<string> Keys => _values.Keys;
}
