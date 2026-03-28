# C_

C_ is a strict, performance-oriented dialect of C# for developers who care about predictability in low-latency servers, games, and real-time simulations. A Roslyn analyzer treats unmarked code as **hot path** and turns easy mistakes - surprise allocations, throws, interface dispatch where you didn’t mean it - into **compile-time errors**, so critical paths are less likely to feed the GC for no good reason. That isn’t a promise of zero GC in the entire process; it’s a guardrail on the code you mark as important. When startup, I/O, or debug-only work needs to break those rules, you use **`[C_.Exempt]`** or **`[C_.DebugExempt]`** with a **`Reason`** (see [docs/lang.md](docs/lang.md)).

You keep the productivity of C# and .NET. **`C_.Analyzer`** and the small **`C_.SDK`** library are the extra pieces; optional **Native AOT** defaults in this repo are a deployment choice, not a requirement of the dialect. The payoff is hot-path code that is easier to review and ship with confidence.

---

## Documentation

| Document | What it is |
|----------|------------|
| [docs/lang.md](docs/lang.md) | Specification: principles, hot-path semantics, exemptions, tooling expectations. |
| [docs/analyzer.md](docs/analyzer.md) | Analyzer reference: rules (`C_.0001`–`C_.0018`), exemptions, BCL and third-party notes. |
| [docs/guide_memory.md](docs/guide_memory.md) | Memory patterns: pre-allocated data, `stackalloc`, frame scratch, `ref struct`. |

Read **lang** for the contract, **analyzer** while you code, **guide_memory** when you design buffers and lifetimes.

---

## Repository

| Path | Role |
|------|------|
| `src/C_.SDK` | Attributes referenced from app code (`C_.Exempt`, `C_.DebugExempt`). |
| `src/C_.Analyzer` | Roslyn analyzer; packaged as **`C_.Analyzer`** for NuGet-style feeds. |
| `examples/HelloC_` | Runnable sample: exemptions, frame scratch, small simulation. |
| `src/C_.sln` | Build SDK + analyzer. |
| `examples/Examples.sln` | Build the example host. |
| `Directory.Build.props` / `Directory.Build.targets` | Shared language, nullable, AOT/trim defaults for `Exe` projects. |

---

## Build and run

```bash
dotnet build src/C_.sln -c Release
dotnet build examples/Examples.sln -c Release
dotnet run --project examples/HelloC_/HelloC_.csproj -c Release
```

The example restores **`C_.Analyzer`** from `feed/analyzers`. If the package is missing, MSBuild packs `src/C_.Analyzer` into that folder on restore.

---

## Who it is for

Teams that want **C# productivity** for the bulk of a codebase but **hard guarantees** (or hard failures) on the frame-critical slice without leaving the ecosystem or maintaining a private language fork.
