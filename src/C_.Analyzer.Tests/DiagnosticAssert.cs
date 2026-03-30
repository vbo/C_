using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Xunit;

namespace C_.Analyzer.Tests;

internal static class DiagnosticAssert
{
    internal static void AssertContainsIds(ImmutableArray<Diagnostic> diags, params string[] expectedIds)
    {
        var set = diags.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in expectedIds)
            Assert.Contains(id, set);
    }

    internal static void AssertContainsIdCount(ImmutableArray<Diagnostic> diags, string id, int count) =>
        Assert.Equal(count, diags.Count(d => d.Id == id));

    internal static void AssertEmpty(ImmutableArray<Diagnostic> diags) =>
        Assert.Empty(diags);

    internal static void AssertAny(ImmutableArray<Diagnostic> diags) =>
        Assert.NotEmpty(diags);
}
