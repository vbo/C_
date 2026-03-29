# C_

`C_` is a strict C# _dialect_ designed for real-time applications - games, servers, simulations, robotics, and any system that demands low, predictable latency and stable memory footprint.

It enforces zero heap allocations, no I/O, no exceptions, and other sources of jitter in **hot-path code** through a Roslyn analyzer. When an engineer (or an AI agent) adds code like that, `C_` immediately fails the build so you can catch the degradation early and rethink the approach. For startup, loading, or debug-only work, you explicitly mark the code with `[C_.Exempt]` and provide a clear `Reason`.

Writing code in this style may feel restrictive at first, so `C_` comes with practical recipes for common challenges and a lightweight SDK with decent implementation for common primitives. `C_` follows the same disciplined philosophy used in Unity DOTS, BepuPhysics, Flecs.NET, and production systems for high-frequency trading, real-time audio, and robotics. The payoff is hot-path code that is easier to review, reason about, and ship with confidence.

---

## Documentation

| Document | What it is |
|----------|------------|
| [docs/lang.md](docs/lang.md) | Specification: principles, hot-path semantics, exemptions, tooling expectations. |
| [docs/analyzer.md](docs/analyzer.md) | Analyzer reference: rules (`C_.0001`–`C_.0018`), exemptions, BCL and third-party notes. |
| [docs/guide_memory.md](docs/guide_memory.md) | Memory patterns: pre-allocated data, `stackalloc`, frame scratch, `ref struct`. |

---

## Repository

| Path | Role |
|------|------|
| `src/C_.SDK` | Basic primitives to get you started. |
| `src/C_.Analyzer` | Roslyn analyzer; packaged as **`C_.Analyzer`** for NuGet-style feeds. |
| `examples/` | Runnable examples using C_. |
| `Directory.Build.props` / `Directory.Build.targets` | Good defaults for your build. |

---

## Running Examples

```bash
dotnet build src/C_.sln -c Release
dotnet build examples/Examples.sln -c Release
dotnet run --project examples/HelloC_/HelloC_.csproj -c Release
```

The example restores **`C_.Analyzer`** from `feed/analyzers`. If the package is missing, MSBuild packs `src/C_.Analyzer` into that folder on restore.

---
