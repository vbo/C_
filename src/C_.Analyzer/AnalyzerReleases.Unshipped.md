; Unshipped analyzer release
; Move rule entries to Shipped when releasing a stable analyzer package.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
C_0001 | C_ | Error | Throw is not allowed on the hot path (docs/lang.md §4.2).
C_0002 | C_ | Error | Heap-allocating `new` (reference type, array, anonymous type) on the hot path (§4.1, §3.2).
C_0003 | C_ | Error | String interpolation on the hot path (§4.1).
C_0004 | C_ | Error | String concatenation with `+` on the hot path (§4.1).
C_0005 | C_ | Error | `string.Format` on the hot path (§4.1).
C_0006 | C_ | Error | `ArrayPool<>.Shared.Rent` on the hot path (§4.1).
C_0007 | C_ | Error | LINQ query syntax on the hot path (§4.1).
C_0008 | C_ | Error | `yield return` on the hot path (§4.1).
C_0009 | C_ | Error | `await` on the hot path (§4.1).
C_0010 | C_ | Error | Reflection / `GetType()`-style calls on the hot path (§4.3).
C_0011 | C_ | Error | Lambda / anonymous function captures on the hot path (§4.4).
C_0012 | C_ | Error | Call through interface dispatch on the hot path (§4.5).
C_0013 | C_ | Error | Unconstrained or interface-constrained type parameter in hot-path declaration (§3.3).
C_0014 | C_ | Error | Implicit boxing on the hot path (§4.1).
C_0015 | C_ | Error | `ToString` on the hot path (§4.1).
C_0016 | C_ | Error | I/O on the hot path: `System.Net.*`, filesystem/pipes/ports, `Console` read/write (§4.1).
C_0017 | C_ | Error | Hot path must not call code marked `[Exempt]` (`[DebugExempt]` ignored for C_0017; docs/lang.md sec. 8).
C_0018 | C_ | Error | `catch` (including `catch when`) on the hot path (§4.2).
