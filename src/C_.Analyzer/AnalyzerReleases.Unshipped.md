; Unshipped analyzer release
; Move rule entries to Shipped when releasing a stable analyzer package.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
C_.0001 | C_ | Error | Throw is not allowed on the hot path (docs/lang.md §4.2).
C_.0002 | C_ | Error | Heap-allocating `new` (reference type, array, anonymous type) on the hot path (§4.1, §3.2).
C_.0003 | C_ | Error | String interpolation on the hot path (§4.1).
C_.0004 | C_ | Error | String concatenation with `+` on the hot path (§4.1).
C_.0005 | C_ | Error | `string.Format` on the hot path (§4.1).
C_.0006 | C_ | Error | `ArrayPool<>.Shared.Rent` on the hot path (§4.1).
C_.0007 | C_ | Error | LINQ query syntax on the hot path (§4.1).
C_.0008 | C_ | Error | `yield return` on the hot path (§4.1).
C_.0009 | C_ | Error | `await` on the hot path (§4.1).
C_.0010 | C_ | Error | Reflection / `GetType()`-style calls on the hot path (§4.3).
C_.0011 | C_ | Error | Lambda / anonymous function captures on the hot path (§4.4).
C_.0012 | C_ | Error | Call through interface dispatch on the hot path (§4.5).
C_.0013 | C_ | Error | Unconstrained or interface-constrained type parameter in hot-path declaration (§3.3).
C_.0014 | C_ | Error | Implicit boxing on the hot path (§4.1).
C_.0015 | C_ | Error | `ToString` on the hot path (§4.1).
C_.0016 | C_ | Error | I/O on the hot path: `System.Net.*`, filesystem/pipes/ports, `Console` read/write (§4.1).
C_.0017 | C_ | Error | Hot path must not call code marked `[Exempt]` (`[DebugExempt]` ignored for C_.0017; docs/lang.md sec. 8).
C_.0018 | C_ | Error | `catch` (including `catch when`) on the hot path (§4.2).
