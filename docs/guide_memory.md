# Memory allocation strategy in C_

C_ enforces a strict rule: **hot-path code must not allocate on the heap**.  
Any unmarked method is treated as **hot path**; **`C_.Analyzer`** reports heap allocations there as compile-time errors. That discipline is what backs predictable, low-GC, low-latency behavior.

Normative rules and exemptions are in **`docs/lang.md`**; what the analyzer flags is in **`docs/analyzer.md`**. This guide is **patterns and ergonomics**, not a substitute for those documents.

A future **optional SDK** doc may introduce reusable buffer and string helpers. **This guide stays self-contained** using ordinary C#, **`Span<T>`**, arrays, and small types you can define locally.

---

### 1. Core principle

- **Hot path** — Unmarked methods (and types that contain them) → **no heap allocations** (and the rest of the C_ hot-path rules).
- **Cold / exempt code** — Marked with **`[C_.Exempt]`** or, in Debug builds, **`[C_.DebugExempt]`** → allocations and other relaxed rules are allowed there. Use for startup, loading, tooling, and other non–frame-critical work only.

Persistent data should be allocated **once** at program start or during load phases. Everything that varies per frame should use **stack**, **pre-allocated** storage, or **`Span<T>`** views into that storage—not **`new`** of reference types on the hot path.

---

### 2. General guidelines

- **Allocate once, reuse** for data that survives across frames.
- Keep per-frame temporaries on the **stack** or in **pre-allocated** scratch, with **`Span<T>`** / **`ref`** for slicing.
- Prefer **`ref`**, **`in`**, and **`Span<T>`** at boundaries between hot-path functions.
- Do not allocate in the main loop unless the enclosing scope is **`[C_.Exempt]`** / **`[C_.DebugExempt]`** (and you understand the Release vs Debug semantics—see **`docs/lang.md`**).
- Use **`[C_.Exempt]`** sparingly and always with a clear **`Reason`**.
- Do not use **`ArrayPool<T>.Shared.Rent`** on the hot path (**C_0006**). If you need a **large** reusable scratch region, allocate a **`byte[]`** (or similar) **once** during startup and slice it each frame—see §3.3.

---

### 3. Recommended patterns

#### 3.1 Arrays and persistent data

Keep long-lived data in an **array** (or similar) whose **backing allocation happens during startup or load**, not inside **`Tick`**. The snippet below only shows the hot path; the field must be assigned **once** in cold-path code (constructor, **`[C_.Exempt]`** initializer, `Main`, etc.):

```csharp
// Assigned once during startup — not from Tick.
private static EntityState[] s_entities = null!;

public static void Tick()
{
    for (int i = 0; i < s_entities.Length; i++)
    {
        ref EntityState e = ref s_entities[i];
        // work with e
    }
}
```

`EntityState` is your **`struct`** (or primitive) element type.

#### 3.2 `stackalloc` for small temporary buffers

For scratch space that **fits comfortably under your thread’s stack limit** and lives in **one** method:

```csharp
public static void Tick()
{
    Span<float> tempPositions = stackalloc float[1024];
    // use tempPositions...
}
```

##### Stack budget (important)

`stackalloc` consumes **stack** space for the lifetime of the containing invocation. Large buffers, **deep call stacks**, or **many** nested `stackalloc`s can cause **stack overflow**. Size scratch regions to your platform (typical main-thread stacks are often on the order of **1 MiB**, worker threads may be smaller). When a single frame needs **more** scratch than is safe on the stack, use a **pre-allocated pool buffer** (§3.3) instead of a giant **`stackalloc`**.

#### 3.3 Frame-scoped scratch passed between functions

When several functions need temporary space in the same frame, use a **`Span<byte>`** plus a running **`offset`** (bump pointer). The **only** difference below is **where the bytes live**: stack vs a **one-time heap allocation** of a backing array (done in cold-path code, not in **`Tick`**).

**A — Scratch on the stack (`stackalloc`)**

Good when the total scratch size per **`Tick`** is small enough for your thread stack:

```csharp
public static void Tick()
{
    Span<byte> scratch = stackalloc byte[16384];
    var offset = 0;

    RunPhysics(ref scratch, ref offset);
    RunAi(ref scratch, ref offset);
}

private static void RunPhysics(ref Span<byte> scratch, ref int offset)
{
    int need = 256;
    var block = scratch.Slice(offset, need);
    offset += need;
    // use block...
}
```

**B — Scratch from a pre-allocated pool (`byte[]` filled once)**

Good for **large** frame arenas. The array is allocated **during startup** (or under **`[C_.Exempt]`**); **`Tick`** only takes a **`Span<byte>`** over it—**no** heap allocation in the hot path:

```csharp
// Filled once during startup — not in Tick.
private static byte[] s_frameScratch = new byte[512 * 1024];

public static void Tick()
{
    Span<byte> scratch = s_frameScratch.AsSpan();
    var offset = 0;

    RunPhysics(ref scratch, ref offset);
    RunAi(ref scratch, ref offset);
}
```

Use the **same** callee shape (**`ref Span<byte>`**, **`ref int offset`**) so subsystems do not care whether the backing storage was stack or pooled.

Keep **`offset`** in bounds each frame. For the pool variant, you may reuse the **entire** buffer every frame (reset **`offset`** to **`0`** at the start of **`Tick`**) or partition it if multiple threads need scratch (e.g. one slab per worker, sized at startup).

Do **not** substitute **`ArrayPool<T>.Shared.Rent`** in **`Tick`** for this pattern—that API is disallowed on the hot path (**C_0006**).

If you prefer a single value type to pass around, a **`struct`** holding **`Span<byte>`** and **`int`** (cursor) is your own local type—not prescribed by this repo.

#### 3.4 `ref struct` for temporary views

Use **`ref struct`** for short-lived types that must **not** escape to the heap or be stored on the heap:

```csharp
public ref struct TransformView
{
    public System.Numerics.Vector3 Position;
    public System.Numerics.Quaternion Rotation;
}
```

Pass by **`ref`**; they live on the stack (subject to C# **`ref struct`** rules).

#### 3.5 Fixed-size inline data (e.g. names)

For small text or blobs that must live inside a **`struct`** without a separate heap allocation, use a **fixed-capacity** layout. One portable option is an **`unsafe`** fixed buffer; another is C# **inline arrays** (when your language version supports them). Example with **`unsafe`** (your project must allow unsafe blocks—see **`docs/lang.md`**):

```csharp
public unsafe struct Name64
{
    private fixed char _chars[64];
    public byte Length;
}
```

Encoding, validation, and how you expose **`Span<char>`** / **`ReadOnlySpan<char>`** are up to your codebase.

---

### 4. Example: tick with shared scratch

**Stack-backed** (same idea as §3.3 A):

```csharp
public static void Tick()
{
    Span<byte> scratch = stackalloc byte[16384];
    var offset = 0;

    Input.Update();
    RunPhysics(ref scratch, ref offset);
    RunAi(ref scratch, ref offset);
    RunRender(ref scratch, ref offset);
}
```

For a **large** scratch budget, replace the first two lines with a **`Span<byte>`** over **`s_frameScratch.AsSpan()`** as in §3.3 B.

These patterns keep **your** hot-path code allocation-free as far as **`C_.Analyzer`** can see; you still own **correctness** (no buffer overruns, stack sizing, and behavior inside external libraries—see **`docs/analyzer.md`**, Third-party dependencies).

Together they are a **common way to structure** simulation-style C_ code; the rest of the dialect (dispatch, exceptions, I/O, etc.) still follows **`docs/lang.md`**.
