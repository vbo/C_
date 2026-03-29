using System.Runtime.CompilerServices;

namespace C_.Memory;

/// <summary>
/// Syntax sugar for <see cref="Arena.Scope(ref Arena)"/> on a local <see cref="Arena"/> variable.
/// </summary>
public static class ArenaMemoryExtensions
{
    /// <summary>
    /// Equivalent to <see cref="Arena.Scope(ref Arena)"/>. The receiver must be a variable (e.g.
    /// <c>using (arena.Scope())</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Arena.ScopeGuard Scope(this ref Arena arena) => Arena.Scope(ref arena);
}
