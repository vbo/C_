# C_.Analyzer

`C_.Analyzer` is a Roslyn **diagnostic analyzer** shipped from `src/C_.Analyzer`. It enforces **hot-path** rules for the C_ language discipline: code that is not explicitly exempt must avoid heap allocations, reflection, interface dispatch, async/iterator patterns, **`catch`** on the hot path, and I/O that the rule set recognizes.

Language background and rationale live in **`docs/lang.md`**; this document describes **what the analyzer checks** and **how** exemptions work.

Note on **BCL**s: Short for **Base Class Library** — the core .NET libraries that ship **with the runtime** (framework assemblies such as `System.Runtime`, `System.Collections`, `System.Net.*`, `System.Console`, and the rest of the shared-framework surface you get with the SDK). It does **not** mean “any NuGet package” or your own assemblies. In this doc, **“BCL patterns”** (e.g. for **C_.0016**) means the concrete namespaces and types the analyzer’s blocklists target in those libraries; **“non–BCL”** means other assemblies (your projects, third-party packages, private DLLs).

---

## Hot path vs exempt code

The analyzer treats a piece of code as **on the hot path** when the enclosing symbol (method, accessor, etc.) and its containing types **do not** carry an effective exemption (see below). Operations inside **exempt** scopes skip the allocation, I/O, and most behavioral rules; **declarations** can still be checked (for example generic constraints, **C_.0013**).

If **both** `ExemptAttribute` and `DebugExemptAttribute` are missing from the compilation (metadata not referenced), hot-path detection does not run and those rules do not apply.

---

## `[Exempt]` (`C_.Exempt`)

Defined in **`C_.SDK`** as `ExemptAttribute` (use as `[C_.Exempt(...)]` in source):

- **Targets:** `Method`, `Class`, `Struct`
- **Property:** `Reason` (string, for documentation and review)

**Exemption** applies to a symbol if **that symbol or any ancestor** (walking the containing chain, considering methods and named types) has `[Exempt]`. Nested types and their members are exempt when an outer type is marked.

Use `[Exempt]` for startup, tooling, or other code that is **not** expected to run on the frame hot path. Hot-path frames should stay unmarked.

---

## `[DebugExempt]` (`C_.DebugExempt`)

Defined in **`C_.SDK`** as `DebugExemptAttribute` — same targets and `Reason` as `[Exempt]`.

The analyzer treats `[DebugExempt]` as an exemption **only when at least one syntax tree was parsed with the `DEBUG` conditional compilation symbol** (Roslyn `CSharpParseOptions.PreprocessorSymbolNames`, case-sensitive `DEBUG`). That matches typical **Debug** vs **Release** MSBuild configurations.

In **Release** (and any configuration that does not define `DEBUG`), `[DebugExempt]` is ignored for exemption purposes **except** as paired with `[Conditional("DEBUG")]` (see below).

**C_.0017** considers only **`[Exempt]`**, not `[DebugExempt]`. Hot-path code may invoke `[DebugExempt]` members without **C_.0017**.

### `[System.Diagnostics.Conditional("DEBUG")]` (aligned with DebugExempt)

The analyzer treats a **method** as exempt when **all** of the following hold:

1. The method has **`[System.Diagnostics.Conditional("DEBUG")]`** (condition string must be exactly `DEBUG`, case-sensitive, matching the preprocessor symbol used for `[DebugExempt]`).
2. The compilation **does not** define the `DEBUG` preprocessor symbol (same detection as `[DebugExempt]` — see above).

So in **Release**, the **body** of such a method is **not** analyzed as hot-path code, even though the method is still emitted. That matches the intent that call sites are stripped and the implementation is debug-only. You can use **`Console.WriteLine`**, allocations, etc. in the body **without** wrapping it in `#if DEBUG`, as long as `[Conditional("DEBUG")]` is present.

In a **Debug** configuration, `[Conditional("DEBUG")]` does **not** by itself exempt the body; use **`[C_.DebugExempt]`** on the same method so the body is exempt while `DEBUG` is defined.

### Printf-style debugging (recommended)

| Mechanism | Effect |
|-----------|--------|
| **`[Conditional("DEBUG")]`** | Non-`DEBUG` builds: compiler removes **calls** (arguments not evaluated). Analyzer: **body** treated as exempt when `DEBUG` is undefined. |
| **`[C_.DebugExempt]`** | When `DEBUG` **is** defined, analyzer exempts the **body** from C_ rules. |

**Recommended shape:**

```csharp
[System.Diagnostics.Conditional("DEBUG")]
[C_.DebugExempt(Reason = "Printf debugging; calls stripped in Release")]
private static void DebugTrace(string message) =>
    System.Console.WriteLine(message);
```

**Notes:**

- `Conditional` applies only to methods that return **`void`** (or `void`-like rules per C# language).
- Use the **same** `DEBUG` symbol for `Conditional`, MSBuild `DefineConstants`, and the analyzer’s `[DebugExempt]` / parse options.
- **`[Conditional("TRACE")]`** (or other symbols) is **not** handled by this analyzer rule; only **`DEBUG`** is recognized for parity with `[DebugExempt]`.
- **Reflection:** **C_.0010** forbids reflection and similar APIs on the **hot path**, so compliant code cannot reach this helper from hot-path frames via `MethodInfo.Invoke`, `Activator`, etc. In practice only normal static calls matter, and those are removed when `DEBUG` is undefined. (Reflection from **`[Exempt]`** or other non–hot-path code is a separate concern and is not what the hot-path model optimizes for.)

---

## Rule C_.0017: no calls from hot path into exempt code

**C_.0017** fires when **unexempt** (hot-path) code:

- **Invokes** a method that is `[Exempt]` or lies under a **class/struct** that is `[Exempt]`, or
- **`new`**s a type whose constructor is in such a scope (same attribute walk from the callee method upward).

`[DebugExempt]` does **not** participate in **C_.0017**. This blocks the pattern of marking a helper with `[Exempt]` and calling it from `Tick()` or any other hot-path method.

### Entry-point exception

Calls that appear **inside the compilation entry point** (Roslyn `Compilation.GetEntryPoint` — typically `Main` or the synthetic entry for top-level statements) **do not** trigger **C_.0017**. That allows `Main` to call startup routines such as `Initialize()` that are marked `[Exempt]`.

Calls from **other** unexempt methods into `[Exempt]` code still report **C_.0017**. Keep `Main` thin if you rely on this exception.

---

## Rule C_.0016: I/O on the hot path

**C_.0016** reports invocations and object creations whose resolved method is classified as **I/O** by `HotPathIoRules`. The goal is to keep **console, network, filesystem, pipe, and serial** traffic out of hot-path frames, independent of whether a specific call site allocates on the heap.

### Covered APIs (current implementation)

| Area | What is flagged |
|------|------------------|
| **Network** | Any instance or static member on types in namespaces starting with `System.Net.` (for example `HttpClient`, `Socket`, `Dns`, `SslStream`). |
| **Pipes** | Namespaces starting with `System.IO.Pipes`. |
| **Serial** | Namespaces starting with `System.IO.Ports`. |
| **Isolated storage** | Namespaces starting with `System.IO.IsolatedStorage`. |
| **Memory-mapped files** | Namespaces starting with `System.IO.MemoryMappedFiles`. |
| **Console** | `System.Console`: `Write`, `WriteLine`, `Read`, `ReadLine`, `ReadKey`, `OpenStandardOutput`, `OpenStandardError`, `OpenStandardInput`. |
| **Filesystem (blocklist in `System.IO` only)** | Types named `File`, `Directory`, `FileStream`, `FileInfo`, `DirectoryInfo`, `StreamReader`, `StreamWriter`, `BinaryReader`, `BinaryWriter`, `MemoryMappedFile`, `RandomAccess`, `FileSystemWatcher`, `DriveInfo`. |

### Explicitly not flagged by C_.0016 (examples)

- **`MemoryStream`, `UnmanagedMemoryStream`** — in-memory use.
- **`Path`** — primarily string operations (some members still touch the OS; the rule set does not special-case them).
- **`StringReader` / `StringWriter`** — in-memory text.
- **`System.IO.Compression`** — not included (often used with memory streams; would need finer rules to avoid false positives).

Third-party libraries, `Process`, waits, and many other blocking or I/O-like APIs are **not** in the list unless they surface as the BCL patterns above.

---

## Rules C_.0001–C_.0015 & C_.0018 (summary)

These apply on the **hot path** unless the enclosing scope is exempt via `[Exempt]` or (in DEBUG builds) `[DebugExempt]`. **C_.0013** is evaluated on **declarations** (types and generic methods) and does not use the same “operation in exempt body” shortcut for the type-parameter rules.

| ID | Scope | What it catches |
|----|--------|------------------|
| **C_.0001** | Operation | `throw` / throw expression / rethrow on the hot path. |
| **C_.0002** | Operation | Reference-type `new`, arrays (`new[]` / `new T[n]`), anonymous types. (Struct `new` is not reported as **C_.0002**.) |
| **C_.0003** | Operation | String interpolation. |
| **C_.0004** | Syntax | String concatenation with `+` when either operand is `string`. |
| **C_.0005** | Invocation | `string.Format`. |
| **C_.0006** | Invocation | `ArrayPool<>.Shared.Rent` (`System.Buffers`). |
| **C_.0007** | Syntax | LINQ query syntax. |
| **C_.0008** | Syntax | `yield return`. |
| **C_.0009** | Operation | `await`. |
| **C_.0010** | Invocation | `object.GetType()`; any invocation on `System.Reflection.*`; `System.Activator`; `System.Runtime.InteropServices.Marshal`. |
| **C_.0011** | Syntax | Lambdas / local functions that capture outer variables. |
| **C_.0012** | Invocation | Instance call when the static type of the receiver is an **interface** (interface dispatch). |
| **C_.0013** | Symbol | Generic **type** or **method** type parameters: must not be unconstrained; must not be constrained **only** to interfaces (see `docs/lang.md`, section 3.3). |
| **C_.0014** | Operation | Implicit boxing (value type to reference). |
| **C_.0015** | Invocation | Instance `ToString()` with no parameters. |
| **C_.0018** | Syntax | `catch` / `catch when` on the hot path (`try`/`finally` without `catch` is allowed). |

**`finally`:** The analyzer does **not** report `finally`. Rationale: `finally` is for **deterministic cleanup** on leaving `try`, not for **exception recovery**; with **no `throw`** on the hot path, compliant code uses it for normal-exit teardown and for language sugar (`using`, `lock`). See **`docs/lang.md` §4.2** for the full decision.

Messages and titles may cite **`docs/lang.md`** sections for the language spec.

---

## Consuming the analyzer

- Reference the **`C_.Analyzer`** package (or project) as an **analyzer** so MSBuild and the IDE load it. For Visual Studio Code / C# Dev Kit, a **NuGet-style** package placing the DLL under `analyzers/dotnet/cs` is the usual path (see `examples/HelloC_` and `src/C_.Analyzer` packaging in the repo).
- **`C_.Exempt`** and **`C_.DebugExempt`** come from **`C_.SDK`**; application code must reference the SDK assembly (or equivalent) so the attributes exist at compile time.

---

## Third-party dependencies (non–C_ libraries)

A library can be high quality and still violate C_ in places (occasional allocation, possible `throw`). If you have reviewed it and accept that behavior on your hot path, handle it as follows.

### What the analyzer sees

- **Referenced as a normal assembly:** Only **your** source is analyzed with **`C_.Analyzer`**. The compiler does not surface method bodies from that DLL to the analyzer, so internal **`throw`**, heap **`new`**, and similar patterns there are **not** reported as diagnostics in **your** project. Your **call sites** still must satisfy the rules (e.g. no extra allocations you introduce, respect **C_.0012** if you call through an **interface** type you control).
- **Same compilation (source package, submodule, copied `.cs`):** Those methods are analyzed like your code. Violations inside that source are reported unless you change how it is built or suppress.

### Cold path OK, hot path not automatic

A dependency may be **fine for cold paths** (initialization, I/O threads, editors) but **not** something you want developers to call from **`Tick`**, simulation steps, or other frame-critical code—whether because of hidden cost or simply to **avoid unchecked sprawl** into the hot path.

**Intended policy** (see **`docs/lang.md` §5.3**): external / non-application assemblies are **untrusted on the hot path by default**. Calling into them from hot-path code should require an **explicit blessing** (documented allowlist, a dedicated attribute, MSBuild item, or similar) so “I imported a nice library” does not silently become “it’s on the hot path.”

**Current tooling gap:** this analyzer **does not** yet report “hot path calls non-blessed external assembly.” Only **your** source is checked for C_ rules; **BCL** calls are not classified as external for this purpose, and **NuGet** / private DLLs are **not** blocked by default. Treat that as a **known limitation** until an allowlist-based rule exists.

### Why not `[Exempt]` on a wrapper?

**C_.0017** blocks hot-path code from calling into **`[Exempt]`** members (except from the **lexical** entry point). You cannot fix “we trust this DLL” by marking a wrapper **`[Exempt]`** and calling it from **`Tick()`** or other hot-path methods—that pattern is exactly what **C_.0017** rejects. Normative wording lives in **`docs/lang.md` §5.2**.

### Recommended practice

1. **Prefer a binary reference** (or a **separate** project built **without** this analyzer) so the dependency’s IL stays **outside** the analyzed compilation. Tighten **your** usage at the boundary (concrete types where possible, minimal arguments, no redundant hot-path anti-patterns).
2. **Document the decision:** short comment at the boundary plus a durable note (version, what was reviewed, residual risk). Treat it as an explicit engineering acceptance, not an analyzer loophole.
3. If the library **must** compile as **source** alongside your app: **build it in another project** without **`C_.Analyzer`**, reference the output assembly; or use **narrow** `#pragma` / **`.editorconfig`** suppressions on specific files or lines with the same **mandatory** audit trail; or **fork** / maintain a thin façade if you need C_-clean source in-tree.

### Possible future direction (blessing / allowlist)

A practical design is **untrusted-by-default** for hot-path **call sites** whose **callee** is declared in another assembly (with a configurable **allowlist**: your own assemblies, optionally **BCL**, and explicitly **blessed** third-party references). A compile-time error would fire when hot-path code invokes anything outside that set unless the call is covered by a **blessing** (e.g. **`[HotPathTrustedLibrary(...)]`** on a thin façade method, or an MSBuild/`editorconfig` map from assembly name to approval id).

That complements (and differs from) **`[TrustedExternal(...)]`** on **symbols**, which could mark **audited** entry points without using **`[Exempt]`** (so **C_.0017** does not apply). Either way, spec + analyzer work is required.

Until then: **code review**, **thin hot-path assemblies** with minimal project references, **binary boundaries + documentation** (§5.2), and **scoped suppressions** when source must live in the same solution.

---

## Limitations

- **Blocklists are incomplete:** APIs outside the BCL patterns above are not flagged by **C_.0016**; third-party networking and I/O need manual discipline or future rules.
- **False positives:** e.g. `StreamReader` over a `MemoryStream` is still flagged because the rule keys off the **declaring type**, not the runtime target of the stream.
- **False negatives:** indirect calls (delegates, unresolved virtual targets) may bypass **C_.0012** and **C_.0017** in edge cases.
- **External assemblies:** hot-path calls into **non–BCL** referenced assemblies are **not** rejected by default; see **Third-party dependencies** and **`docs/lang.md` §5.3** for the intended **untrusted-by-default / blessing** model and current gap.
- **C_.0017 / Main:** only the **lexical** entry point method is exempt for calls **into** `[Exempt]` code; nested helpers called from `Main` are not special-cased beyond their own exemption.

Rule IDs and notes are also tracked in **`src/C_.Analyzer/AnalyzerReleases.Unshipped.md`** until a release is shipped.
