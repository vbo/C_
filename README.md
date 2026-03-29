# C_

`C_` is a strict C# _dialect_ designed for real-time applications - games, servers, simulations, robotics, and any system that demands low, predictable latency and stable memory footprint.

It enforces zero heap allocations, no I/O, no exceptions, and other sources of jitter in **hot-path code** through a Roslyn analyzer. When an engineer (or an AI agent) adds code like that, `C_` immediately fails the build so you can catch the degradation early and rethink the approach. For startup, loading, or debug-only work, you explicitly mark the code with `[C_.Exempt]` and provide a clear `Reason`.

Writing code in this style may feel restrictive at first, so `C_` comes with practical recipes for common challenges and a lightweight SDK with decent implementation for common primitives. `C_` follows the same disciplined philosophy used in Unity DOTS, BepuPhysics, Flecs.NET, and production systems for high-frequency trading, real-time audio, and robotics. The payoff is hot-path code that is easier to review, reason about, and ship with confidence.

---

## Documentation

| Document | What it is |
|----------|------------|
| [docs/lang.md](docs/lang.md) | Specification: principles, hot-path semantics, exemptions, tooling expectations. |
| [docs/analyzer.md](docs/analyzer.md) | Analyzer reference: rules (`C_0001`–`C_0018`), exemptions, BCL and third-party notes. |
| [docs/guide_memory.md](docs/guide_memory.md) | Memory patterns: pre-allocated data, `stackalloc`, frame scratch, `ref struct`. |

---

## Repository

| Path | Role |
|------|------|
| `src/C_.SDK` | Basic primitives to get you started. |
| `src/C_.Analyzer` | Roslyn analyzer; packaged as **`C_.Analyzer`** for NuGet-style feeds. |
| `src/C_.Analyzer.Tests` | xUnit tests: in-process **`CompilationWithAnalyzers`** over **`C_.SDK`** (see **`docs/analyzer.md`**). |
| `examples/` | Runnable examples using C_. |
| `C_.sln` | **Main solution:** SDK, analyzer, tests, and **`HelloC_`**. In **VS Code / Cursor**, open this solution (palette: **“.NET: Open Solution”** / pick **`C_.sln`**) so **`HelloC_`** is in the language-server workspace. |
| `examples/Examples.sln` | **`HelloC_`** only—skip for full IDE analyzer context (see **`C_.sln`**). |
| `Directory.Build.props` / `Directory.Build.targets` | Good defaults; **`HelloC_`** restores **`C_.Analyzer`** from **`feed/analyzers`** (packed before restore; see **Consuming**). |

---

## Consuming `C_.Analyzer`

**In this repo,** **`HelloC_`** uses a **`PackageReference`** to **`C_.Analyzer`** at **`$(C_AnalyzerPackageVersion)`** with **`RestoreAdditionalProjectSources`** pointing at **`feed/analyzers`**. **`Directory.Build.targets`** runs **`dotnet pack`** on **`src/C_.Analyzer`** into that folder **before** **`HelloC_`** **`Restore`**, so a normal **`dotnet build`** / **`dotnet restore`** refreshes the local nupkg. This matches what **Cursor’s C# language server** expects for third-party analyzers better than a live **`ProjectReference`**. Bump **`C_AnalyzerPackageVersion`** in **`Directory.Build.props`** when you change the analyzer; nupkg files under **`feed/`** are gitignored.

**Publishing / other feeds:** run **`dotnet pack src/C_.Analyzer`** (or your pipeline); the nupkg uses **`analyzers/dotnet/cs`** (see **`src/C_.Analyzer/C_.Analyzer.csproj`**).

---

## Running Examples

```bash
dotnet build C_.sln -c Release
dotnet test C_.sln -c Release --no-build
dotnet run --project examples/HelloC_/HelloC_.csproj -c Release
```

**Note:** **`examples/Examples.sln`** does not include **`src`** projects; use it only for a minimal **`HelloC_`** build. For analyzer + example together, prefer **`C_.sln`**.

---
