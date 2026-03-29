using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace C_.Memory;

/// <summary>
/// Bump allocator over a caller-provided <see cref="Span{T}"/> of bytes (e.g. <c>stackalloc byte[n]</c> or a
/// pre-allocated pool slice). Intended for <b>mixed</b> sequences in one slab (e.g. some <c>Alloc&lt;Vector3&gt;</c>,
/// then <c>Alloc&lt;YourStruct&gt;</c>): with the default constructor, <b>each</b> <c>Alloc&lt;T&gt;</c> realigns the
/// cursor for that <c>T</c> (smallest power of two &gt;= <c>sizeof(T)</c>), then bumps by <c>count * sizeof(T)</c>
/// bytes. Use <see cref="Arena(Span{byte}, int)"/> only when every allocation in that arena should share the
/// same fixed byte step (e.g. an all-16-byte SIMD region); a single forced step is a poor fit for arbitrary
/// mixed types. Elements in one <c>Alloc&lt;T&gt;(count)</c> are packed with no inter-element padding; for SIMD
/// stride or tail padding, use a wider or explicitly laid out struct (or a dedicated forced-alignment arena).
/// Use <see cref="Scope(ref Arena)"/> (or <c>arena.Scope()</c> via <see cref="ArenaMemoryExtensions.Scope(ref Arena)"/>)
/// for a lexical scope that rolls back the bump cursor on exit. Reset with <see cref="Reset"/> for the whole arena.
/// Do not copy an <see cref="Arena"/> by value while an active scope holds a <see cref="ScopeGuard"/> tied to that instance’s cursor.
/// The C_ analyzer reports by-value copies as <c>C_SDK0001</c> and forbids fields/properties as <c>C_SDK0002</c> (see <c>docs/sdk.md</c>).
/// <see cref="TryAlloc{T}"/> never throws. <see cref="Alloc{T}"/> throws in <c>DEBUG</c> builds when the
/// allocation fails; in non-<c>DEBUG</c> builds failed <see cref="Alloc{T}"/> returns <see cref="Span{T}.Empty"/>.
/// </summary>
public ref struct Arena
{
    private Span<byte> _bytes;
    private int _position;
    /// <summary>Zero: default power-of-two-from-size; positive: forced alignment in bytes for every <c>Alloc&lt;T&gt;</c>.</summary>
    private int _byteAlignment;

    public Arena(Span<byte> backing)
    {
        _bytes = backing;
        _position = 0;
        _byteAlignment = 0;
    }

    /// <param name="backing">Byte span backing the arena.</param>
    /// <param name="byteAlignment">
    /// If greater than zero, the bump cursor is advanced to a multiple of this value before <b>every</b>
    /// allocation, regardless of <c>T</c>. Prefer the default constructor for heterogeneous <c>Alloc&lt;T&gt;</c>
    /// sequences. If zero or negative, the per-<c>T</c> default applies.
    /// </param>
    public Arena(Span<byte> backing, int byteAlignment)
    {
        _bytes = backing;
        _position = 0;
        _byteAlignment = byteAlignment;
    }

    /// <summary>Bytes from the current bump cursor to the end of the backing span.</summary>
    public readonly int Remaining => _bytes.Length - _position;

    /// <summary>Total length of the backing span.</summary>
    public readonly int Capacity => _bytes.Length;

    /// <summary>Resets the bump cursor to the start of the backing span.</summary>
    public void Reset() => _position = 0;

    /// <summary>
    /// Saves the current bump cursor; <see cref="ScopeGuard.Dispose"/> restores it. Nested scopes are supported.
    /// Must be static so the guard can hold a <see langword="ref"/> to the arena's position (ref-struct instance
    /// methods cannot return this pattern). Call <c>Arena.Scope(ref arena)</c> or <c>arena.Scope()</c> from
    /// <see cref="ArenaMemoryExtensions"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScopeGuard Scope(ref Arena arena) => new ScopeGuard(ref arena);

    /// <summary>
    /// Rolls back the parent <see cref="Arena"/> bump cursor to the value recorded at construction when disposed.
    /// Holds a <see langword="ref"/> to the arena's position field (not the arena itself) so the type stays valid
    /// under ref-struct rules.
    /// </summary>
    public ref struct ScopeGuard : IDisposable
    {
        private ref int _position;
        private readonly int _checkpoint;

        internal ScopeGuard(ref Arena arena)
        {
            _checkpoint = arena._position;
            _position = ref arena._position;
        }

        /// <summary>Restores the arena bump cursor to the saved position.</summary>
        public void Dispose()
        {
            _position = _checkpoint;
        }
    }

    /// <summary>
    /// Allocates <paramref name="count"/> contiguous <typeparamref name="T"/> elements.
    /// </summary>
    /// <returns>
    /// A <see cref="Span{T}"/> view over the allocated region; <see cref="Span{T}.Empty"/> if
    /// <paramref name="count"/> is 0. If the request fails, returns <see cref="Span{T}.Empty"/> in
    /// non-<c>DEBUG</c> builds. For a definitive success signal, use <see cref="TryAlloc{T}"/>.
    /// </returns>
#if DEBUG
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Not enough space (including alignment) or size overflow.</exception>
#endif
    public Span<T> Alloc<T>(int count = 1) where T : unmanaged
    {
        if (!TryAlloc(count, out Span<T> span))
        {
#if DEBUG
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            throw new InvalidOperationException("Arena backing span does not have enough space for the allocation.");
#else
            return Span<T>.Empty;
#endif
        }

        return span;
    }

    /// <summary>
    /// Attempts to allocate <paramref name="count"/> contiguous <typeparamref name="T"/> elements.
    /// </summary>
    /// <returns><see langword="true"/> if the allocation succeeded; otherwise <see langword="false"/>.</returns>
    public bool TryAlloc<T>(int count, out Span<T> span) where T : unmanaged
    {
        span = default;
        if (count < 0)
            return false;
        if (count == 0)
            return true;

        if (!TryGetAlignmentStep<T>(_byteAlignment, out var align))
            return false;

        if (!TryAlignForward(_position, align, out var pos))
            return false;
        var elemSize = Unsafe.SizeOf<T>();
        if (elemSize != 0 && count > int.MaxValue / elemSize)
            return false;

        var byteLen = count * elemSize;
        if (byteLen > int.MaxValue - pos)
            return false;

        var newPos = pos + byteLen;
        if (newPos > _bytes.Length)
            return false;

        var slice = _bytes.Slice(pos, byteLen);
        _position = newPos;
        span = MemoryMarshal.Cast<byte, T>(slice);
        return true;
    }

    private static bool TryAlignForward(int offset, int alignment, out int aligned)
    {
        if (alignment <= 0)
        {
            aligned = 0;
            return false;
        }

        if (alignment == 1)
        {
            aligned = offset;
            return true;
        }

        long o = offset;
        long a = alignment;
        var r = (o + a - 1) / a * a;
        if (r > int.MaxValue)
        {
            aligned = 0;
            return false;
        }

        aligned = (int)r;
        return true;
    }

    private static bool TryGetAlignmentStep<T>(int byteAlignment, out int alignment) where T : unmanaged
    {
        if (byteAlignment > 0)
        {
            alignment = byteAlignment;
            return true;
        }

        return TryDefaultPowerOfTwoAlignment<T>(out alignment);
    }

    /// <summary>
    /// Smallest power of two &gt;= <see cref="Unsafe.SizeOf{T}"/> (at least 1). Not a portable <c>alignof(T)</c>,
    /// but a simple conservative default.
    /// </summary>
    private static bool TryDefaultPowerOfTwoAlignment<T>(out int alignment) where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        if (size <= 1)
        {
            alignment = 1;
            return true;
        }

        var pow2 = BitOperations.RoundUpToPowerOf2((uint)size);
        if (pow2 > int.MaxValue)
        {
            alignment = 0;
            return false;
        }

        alignment = (int)pow2;
        return true;
    }
}
