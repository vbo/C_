**C_**

C_ is a strict, performance-first dialect of C# designed for high-performance games and low-latency servers. It enforces zero heap allocations, statically bounded memory, and predictable execution in hot paths by treating every unmarked method as hot-path code. The language eliminates dynamic dispatch, exceptions, and any operation that could introduce GC pauses or jitter, while still providing full access to modern C# tooling and Native AOT compilation. By requiring developers to pre-allocate all persistent data and use stack-based or frame-scoped temporary memory, C_ delivers C-like control and predictability within the .NET ecosystem.

The project consists of a precise language specification, a Roslyn analyzer that enforces the rules at compile time, and a lightweight SDK containing essential zero-allocation primitives. Developers organize code using `public static partial class` modules and use explicit exemption attributes (`[C_.Exempt]` and `[C_.DebugExempt]`) only where allocations or side effects are truly necessary. C_ is intended for developers who want maximum performance and minimum surprise - code that is obviously correct, easy to reason about, and ships as small, fast native executables with no hidden runtime costs.

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

Teams that want **C# productivity** for the bulk of a codebase but **hard guarantees** (or hard failures) on the frame-critical slice - without leaving the ecosystem or maintaining a private language fork.
