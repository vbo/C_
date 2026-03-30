# C_ Core Language Specification

**Version:** 0.15  
**Date:** March 2026  

C_ is a strict dialect of C# designed for high-performance, latency-critical applications such as games and servers. It enforces zero heap allocations in hot paths and strictly bounded memory usage.

The dialect is enforced by a custom Roslyn analyzer. Violations in hot-path code are compile-time errors unless explicitly exempted.

See also: **`C_.SDK`** library (attributes, useful primitives) is documented in **`docs/sdk.md`**.

## 1. Language Principles

1. **Hot-Path Default**  
   - All code is considered hot-path unless explicitly exempted. Hot-path code must execute without any heap allocation.

2. **Zero-Allocation Guarantee**  
   - Hot-path code must not perform any heap allocation.

3. **Static Memory Bounding**  
   - All persistent data structures must be sized at startup or load time. Dynamic growth in hot paths is forbidden.

4. **Static-Only, Value-Oriented Model**  
   - Only `public static partial class` declarations are permitted as top-level containers.  
   - All methods are `static`.  
   - Dynamic dispatch is prohibited.  
   - Reference types (`class`) may be used only in exempted code.

5. **Predictable Latency**  
   Hot-path execution is guaranteed to be free of GC pauses and hidden runtime overhead.

Async/await, asynchronous patterns, and detailed memory-management rules are defined in a separate specification and are out of scope for this document.

## 2. Program Structure

- The sole allowed top-level construct is `public static partial class`.  
- These classes serve purely as namespaces/modules.  
- A program must contain exactly one entry point: `public static void Main()` or a method marked with `[EntryPoint]`.

## 3. Type System

### 3.1 Permitted Types in Hot Paths

- Primitive types (`bool`, integral types, `float`, `double`, `char`)
- `struct` and `ref struct` (including `readonly struct`)
- `Span<T>`, `ReadOnlySpan<T>`
- Arrays of the above types **only if pre-allocated** at startup or load time
- `unsafe` pointers when `Span<T>` is insufficient

### 3.2 Restricted Types

- `class` instantiation is forbidden in hot paths.

### 3.3 Generics

- Unconstrained type parameters (`T` with no `where` clause) are forbidden in hot-path declarations.
- Interface constraints (`where T : ISomeInterface`) are forbidden: they imply interface dispatch on `T`. (**Relaxation** — see **TODO** at end of document.)
- Permitted constraints are those that preserve fully static resolution, such as `where T : struct`, `where T : unmanaged`, or `where T : SomeStruct` where `SomeStruct` is a concrete struct type (not substitutable via an interface call on `T`).

## 4. Hot-Path Semantics

### 4.1 Allocation Rules

Hot-path code must not execute any of the following:

- `new` on any reference type
- `string` concatenation, interpolation (`$"..."`), `string.Format`, `ToString()`
- `ArrayPool<T>.Shared.Rent` or any operation that allocates
- LINQ queries (today: query/comprehension syntax in the analyzer; fluent `Enumerable.*` — see **TODO** at end of document)
- `yield return`
- `foreach` on any collection that allocates an enumerator
- Any operation that results in a heap allocation (including hidden boxing)

### 4.2 Exceptions

- `throw`, throw expressions, and bare rethrow (`throw;`) are forbidden in hot paths (throwing allocates and implies exceptional control flow).
- `catch` clauses (including `catch when (...)`) are forbidden in hot paths: exception handling is not part of the hot-path model. Use **`[C_.Exempt]`** (or **`[C_.DebugExempt]`** in Debug) for recovery or boundary code, or handle failures outside the frame loop.
- **`finally` remains allowed** on hot paths. A `finally` block schedules **deterministic cleanup when the `try` region is left**; it does not express “recover from a failure in flight” the way `catch` does. Hot-path code must still not `throw` (§4.2, first bullet), so in compliant programs control reaches `finally` through **normal exits** from `try`, not through unwinding. Keeping `finally` permits idioms such as **`using`** and **`lock`**, which lower to `try`/`finally`, without forcing those whole call sites into **`[C_.Exempt]`**. The analyzer diagnoses **`catch`** only (**C_0018**); it does **not** flag `finally`.

### 4.3 Reflection

Hot-path code must not use reflection or runtime type discovery, including but not limited to:

- `Type.GetType`, `typeof` used to obtain `Type` for dispatch, `GetType()`, `object.GetType()`
- `System.Reflection` APIs (`MethodInfo`, `FieldInfo`, `Invoke`, `MakeGenericType`, `Activator.CreateInstance`, etc.)
- `Marshal`, `RuntimeHelpers` patterns that perform dynamic allocation or layout discovery, unless the analyzer whitelists specific non-allocating idioms

### 4.4 Closures

- Lambdas, anonymous methods, and local functions **must not capture** variables from an enclosing scope in hot paths.
- Non-capturing `static` anonymous functions are permitted only if the analyzer proves they introduce no allocation (e.g. cached delegate where applicable).

### 4.5 Dynamic Dispatch and Interfaces

The following constructs are forbidden in hot paths:

- `virtual`, `abstract`, `override`
- **Any call through an interface type** (no `IMyService` dispatch, including on generic type parameters constrained to an interface—see §3.3)
- Interface default method implementations as a dispatch target
- Delegates used for dynamic dispatch
- `dynamic` keyword

All calls must be statically resolvable to a concrete `static` method and inlinable.

### 4.6 Parameter Passing

Prefer `ref`, `in`, `out` for zero-copy semantics. Return values should be small value types or spans.

## 5. Exemption Mechanism

Hot-path rules (allocations, exceptions, reflection, closures, generics, dispatch, I/O, and other analyzer checks) may be relaxed only by annotating code with **`[C_.Exempt]`**:

```c
[C_.Exempt(Reason = "One-time asset loading")]
```

The attribute may be applied to:

- A method
- A `class` or `struct` (including nested types)

Code inside an exempted scope is not subject to hot-path restrictions enforced by the analyzer.

### 5.1 `[C_.DebugExempt]`

The same targets and `Reason` property apply as for `[C_.Exempt]`, but the analyzer treats the scope as exempt **only when the compilation defines the `DEBUG` preprocessor symbol** (typically **Debug** configuration). In **Release** (production builds and most CI), `[DebugExempt]` has **no** effect: the code is analyzed like ordinary hot-path code.

Use `[DebugExempt]` for debug-only assertions, logging, or diagnostics that must not weaken Release guarantees.

**C_0017** (hot path calling into exempt startup code) applies only to **`[Exempt]`**, not to `[DebugExempt]`: you may call a `[DebugExempt]` helper from the hot path in Debug builds; in Release builds that helper’s body must satisfy hot-path rules (or the call site must be compiled out, e.g. with `#if DEBUG`).

For **printf-style** helpers, combine **`[Conditional("DEBUG")]`** with **`[DebugExempt]`**: the compiler strips calls in non-`DEBUG` builds, and the analyzer treats the method body as exempt in **Debug** via `[DebugExempt]` and in **non-Debug** via `[Conditional("DEBUG")]` when `DEBUG` is undefined. See **`docs/analyzer.md`** (DebugExempt section).

### 5.2 Third-party libraries (non–C_ code)

You may depend on libraries that are not written for C_. The analyzer only inspects **your** compilation; behavior inside a referenced **assembly** (throws, allocations) is not visible unless that code is compiled **with** the analyzer in the same project.

Do **not** use **`[C_.Exempt]`** on a wrapper solely to “bless” calls from the hot path: **C_0017** forbids unexempt hot-path code from calling into **`[Exempt]`** scopes (except from the lexical entry point). A pattern of “`[Exempt]` adapter called from `Tick`” conflicts with that rule.

**Recommended approach:** treat the dependency as a **vetted boundary**—prefer referencing it as a **binary** (or a separate project built **without** C_.Analyzer), keep **your** hot-path call sites clean (types, dispatch, arguments), and **document** the review (library id/version, what was checked, acceptance of residual risk). For third-party **source** compiled into the same analyzer run, see **`docs/analyzer.md`** (Third-party dependencies).

### 5.3 Hot-path trust for external assemblies

A library can be **appropriate for cold paths** (startup, tooling, background work) while still being **undesirable on the hot path**—not only because it may allocate or throw, but because **you do not want a precedent** where any developer may call it from frame-critical code without review.

**Policy (intent):** treat code that lives **outside** your C_-disciplined application assemblies as **untrusted for the hot path by default**. Using it on the hot path should require an **explicit blessing** (team-approved allowlist, attribute, or equivalent) that records *what* may be called and *why*, rather than relying on “the compiler didn’t complain.”

**Today:** **`C_.Analyzer` does not yet enforce** an external-call allowlist; cross-assembly invocations from hot-path code are not diagnosed as such. Until tooling catches up, use **code review**, **architectural boundaries** (e.g. hot-path code lives in dedicated assemblies with narrow references), and the **vetted boundary** practices in §5.2. **Future** tooling is described in **`docs/analyzer.md`** (Third-party dependencies — untrusted-by-default).

## 6. Tooling and Compilation Requirements

- Target: .NET 9+ with Native AOT (`PublishAot=true`)
- Language version: C# 12 or higher
- Required MSBuild properties:
  ```xml
  <Nullable>enable</Nullable>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  ```
- The C_ Roslyn analyzer must be enabled and must treat unmarked code as hot-path.
- CI pipeline must include allocation profiling (`dotnet-trace`) and fail on any Gen0+ allocations in hot paths.

## 7. Runtime Configuration (Mandatory)

```c
GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
```

## 8. Minimal Valid Program

```c
public static partial class Application
{
    private static PreallocatedArray<GameState> _state = new(1);

    [C_.Exempt(Reason = "Startup only")]
    public static void Initialize() { }

    public static void Main()
    {
        Initialize();

        while (Platform.IsRunning())
        {
            Tick();
        }
    }

    public static void Tick()
    {
        // All code here is hot-path: zero allocations enforced
        ref var s = ref _state[0];
        UpdateSystems(ref s);
    }
}
```

## TODO (tooling / specification follow-ups)

The following items are **not** fully specified or implemented in the current toolchain; they are tracked for a future revision of this document and the analyzer.

- **Fluent LINQ:** The analyzer today diagnoses **LINQ query/comprehension** syntax only (**C_0007**). **Method-chained** `Enumerable.*` (e.g. `.Select`, `.Where`) remains out of scope; extending the rule set or tightening §4.1 wording to match is **TODO**.
- **C_0013 / generic constraints:** §3.3 and **C_0013** reject **any** interface in a type parameter’s constraint list. **Relaxation** (e.g. allowing `where T : struct, IDisposable`, separate diagnostic ids for unconstrained vs. interface-only, or per-parameter locations) is **TODO** and would require spec + analyzer + tests.
