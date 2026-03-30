# C_.SDK

`C_.SDK` (`src/C_.SDK`) is the small **runtime library** that ships with the C_ dialect: attributes used by **`C_.Analyzer`**, plus optional reference implementation for basic **primitives**.

Normative language rules and hot-path semantics are in **`docs/lang.md`**. Analyzer behavior (**`C_SDK0001`** / **`C_SDK0002`** for **`Arena`**, and hot-path **`C_0001`**–**`C_0018`**) is in **`docs/analyzer.md`**. Scratch and bump-allocator **patterns** are in **`docs/guide_memory.md`**.

---

## 1. What builds where

| TFM | Typical consumers | Contents |
|-----|-------------------|----------|
| **`netstandard2.0`** | Analyzer attribute resolution, older runtimes | **`C_.Exempt`**, **`C_.DebugExempt`**, **`C_.HotPath`** (under namespace **`C_`**) |
| **`net10.0`** | Applications and libraries on modern .NET | Same attributes **plus** **`C_.Memory`** types (e.g. **`Arena`**) |

The **`C_.Analyzer`** project links **attribute source files only** from the SDK so the analyzer does not load the full SDK assembly as an analyzer dependency; **`C_SDK*`** rules are implemented under **`src/C_.Analyzer/SDK/`**. Application projects reference **`C_.SDK`** normally.

---

## 2. Memory: `C_.Memory.Arena`

**`Arena`** is a **`ref struct`** bump allocator over a caller-provided **`Span<byte>`** (e.g. **`stackalloc byte[n]`**, a slice of a pre-allocated buffer, or **`Memory<byte>.Span`** from cold-path setup).

### 2.1 API sketch

- **Constructors** — `Arena(Span<byte> backing)` and optional fixed byte alignment for homogeneous regions.
- **`Alloc<T>` / `TryAlloc<T>`** — bump with per-`T` or forced alignment; see XML comments on **`Arena.cs`** for semantics and **`DEBUG`** vs non-**`DEBUG`** behavior.
- **`Reset()`** — cursor to start of backing.
- **Scopes** — **`Arena.Scope(ref Arena arena)`** or **`arena.Scope()`** (**`ArenaMemoryExtensions`**) returns **`Arena.ScopeGuard`**: **`Dispose`** restores the bump cursor to the value at scope entry (nested scopes supported).

### 2.2 Why you must not copy `Arena` by value

`Arena` stores the bump **cursor** alongside a **`Span<byte>`** to the backing storage. A **by-value copy** duplicates the cursor while **sharing** the same span. An active **`ScopeGuard`** holds a **`ref`** into **one** copy’s cursor; another copy can advance a **different** cursor, so rollback and allocation bookkeeping no longer match reality.

**Mitigation:** use **one** logical **`Arena`** variable per slab, pass **`ref Arena`** into helpers, and rely on **`C_SDK0001`** / **`C_SDK0002`** (see below). Deliberate suppression or code the analyzer does not see is **unsupported** for this type.

### 2.3 Analyzer rule `C_SDK0001` (by-value copy)

**`C_SDK0001`** is reported by **`ArenaCopyAnalyzer`** (same NuGet package as **`HotPathAnalyzer`**). It reports **by-value** copies of **`C_.Memory.Arena`**, including:

- Local initialization and assignment (`var b = a`, `b = a`)
- By-value arguments (`void Take(Arena x)` … `Take(a)`)
- By-value returns (`Arena Echo(Arena a) => a`)
- **`??`**, **`?:`**, and **switch expressions** when any branch copies an existing arena
- Single-field initializers that copy from another arena

It does **not** run only on the C_ hot path: if the compilation resolves **`C_.Memory.Arena`**, the rule applies anywhere in that project’s source (subject to normal Roslyn suppression / generated-code settings).

**Not diagnosed (non-exhaustive):** copies hidden inside **generic** or **dynamic** code, **transitive** embedding (e.g. a struct field whose type is another struct that contains an **`Arena`**), or values produced only through **invocations** the analyzer treats as opaque. Treat **`C_SDK0001`** as **strong lint**, not a proof of no copies.

### 2.4 Analyzer rule `C_SDK0002` (no fields or properties)

**`C_SDK0002`** is reported by **`ArenaFieldAnalyzer`**. Do not declare **`Arena`** as an **instance or static field** on a **`class`**, **`struct`**, or **`ref struct`**. Do not declare a **property** of type **`Arena`** unless it is the usual **auto-property** (the analyzer diagnoses the compiler-generated backing field once, at the property). **Expression-bodied** properties such as `public Arena A => …` are diagnosed on the property. Rationale: copying or moving the **enclosing** object duplicates embedded **`Arena`** state while **`Span`** usage can still alias the same backing memory.

**Note:** For **`class`**, the language usually forbids **`Arena`** instance fields (**`ref struct`** rules); the rule still states intent and covers **`static`** fields and any future surface the compiler allows.

Configure severity in **`.editorconfig`**, for example:

```ini
dotnet_diagnostic.C_SDK0001.severity = error
dotnet_diagnostic.C_SDK0002.severity = error
```

---

## 3. Attributes (`C_.Exempt`, `C_.DebugExempt`, `C_.HotPath`)

These are consumed by **`C_.Analyzer`** for hot-path vs exempt classification. Full semantics are in **`docs/lang.md`** and **`docs/analyzer.md`**.

---

## 4. Examples in this repo

- **`examples/HelloC_SDK`** — references **`C_.SDK`** and uses **`C_.Memory.Arena`** for frame scratch (see **`Application.cs`**).

---

## 5. Alternatives (outside the SDK)

For contexts where **reference** semantics are required (e.g. sharing one bump cursor through many layers without `ref`), a **heap-owned** allocator is the usual C# shape: one **class** holds **`byte[]`** and the cursor; aliases are **reference** copies. That trades **GC / indirection** for **no split cursor on assignment**. The SDK **`Arena`** stays **`ref struct`** to keep hot-path scratch **heap-free**; **`C_SDK0001`** and **`C_SDK0002`** back that contract in tooling.

---

## 6. Versioning and packaging

Application projects typically add:

```xml
<ProjectReference Include="path/to/C_.SDK.csproj" />
```

or consume a packed **`C_.SDK`** package from your feed. Keep **`C_.Analyzer`** aligned with the attribute definitions your code references (**`C_AnalyzerPackageVersion`** in **`Directory.Build.props`** in this repo).
