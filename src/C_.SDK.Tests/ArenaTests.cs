using System.Numerics;
using System.Runtime.InteropServices;
using C_.Memory;
using Xunit;

namespace C_.SDK.Tests;

public class ArenaTests
{
    [Fact]
    public void TryAlloc_negative_count_fails()
    {
        Span<byte> mem = stackalloc byte[64];
        var arena = new Arena(mem);
        Assert.False(arena.TryAlloc<int>(-1, out var span));
        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void TryAlloc_zero_count_succeeds_empty()
    {
        Span<byte> mem = stackalloc byte[64];
        var arena = new Arena(mem);
        Assert.True(arena.TryAlloc<int>(0, out var span));
        Assert.True(span.IsEmpty);
        Assert.Equal(64, arena.Remaining);
    }

    [Fact]
    public void TryAlloc_advances_remaining()
    {
        Span<byte> mem = stackalloc byte[64];
        var arena = new Arena(mem);
        Assert.True(arena.TryAlloc<int>(2, out var a));
        Assert.Equal(2, a.Length);
        Assert.Equal(64 - 8, arena.Remaining);
    }

    [Fact]
    public void TryAlloc_insufficient_space_fails()
    {
        Span<byte> mem = stackalloc byte[4];
        var arena = new Arena(mem);
        Assert.False(arena.TryAlloc<int>(2, out _));
        Assert.Equal(4, arena.Remaining);
    }

    [Fact]
    public void Reset_restores_full_slack()
    {
        Span<byte> mem = stackalloc byte[32];
        var arena = new Arena(mem);
        Assert.True(arena.TryAlloc<long>(1, out _));
        Assert.True(arena.Remaining < 32);
        arena.Reset();
        Assert.Equal(32, arena.Remaining);
    }

    [Fact]
    public void Default_alignment_inserts_padding_between_misaligned_types()
    {
        Span<byte> mem = stackalloc byte[64];
        var arena = new Arena(mem);
        Assert.True(arena.TryAlloc<byte>(1, out _));
        // Cursor at 1; int needs 4-byte alignment -> next slot at 4, then 4 bytes -> position 8
        Assert.True(arena.TryAlloc<int>(1, out _));
        Assert.Equal(64 - 8, arena.Remaining);
    }

    [Fact]
    public void Forced_byte_alignment_used_for_every_alloc()
    {
        Span<byte> mem = stackalloc byte[64];
        var arena = new Arena(mem, byteAlignment: 16);
        Assert.True(arena.TryAlloc<byte>(1, out _));
        // After one byte, cursor 1; next alloc aligns to 16 -> start at 16
        Assert.True(arena.TryAlloc<byte>(1, out _));
        Assert.Equal(64 - 17, arena.Remaining);
    }

    [Fact]
    public void Scope_restores_cursor_on_dispose()
    {
        Span<byte> mem = stackalloc byte[64];
        var arena = new Arena(mem);
        var before = arena.Remaining;
        using (Arena.Scope(ref arena))
        {
            Assert.True(arena.TryAlloc<int>(3, out _));
            Assert.True(arena.Remaining < before);
        }

        Assert.Equal(before, arena.Remaining);
    }

    [Fact]
    public void Scope_extension_restores_cursor()
    {
        Span<byte> mem = stackalloc byte[64];
        var arena = new Arena(mem);
        var before = arena.Remaining;
        using (arena.Scope())
        {
            Assert.True(arena.TryAlloc<int>(1, out _));
        }

        Assert.Equal(before, arena.Remaining);
    }

    [Fact]
    public void Nested_scopes_restore_in_order()
    {
        Span<byte> mem = stackalloc byte[128];
        var arena = new Arena(mem);
        Assert.True(arena.TryAlloc<byte>(1, out _));
        var mid = arena.Remaining;

        using (Arena.Scope(ref arena))
        {
            Assert.True(arena.TryAlloc<long>(2, out _));
            using (Arena.Scope(ref arena))
            {
                Assert.True(arena.TryAlloc<short>(5, out _));
            }

            // Inner disposed: back to after the two longs
            Assert.True(arena.Remaining < mid);
        }

        // Outer disposed: back to mid (after initial single byte)
        Assert.Equal(mid, arena.Remaining);
    }

    [Fact]
    public void Large_Vector3_run_packed_no_per_element_align_padding()
    {
        const int n = 1000;
        var need = n * Marshal.SizeOf<Vector3>();
        var mem = new byte[need + 32];
        var arena = new Arena(mem);
        Assert.True(arena.TryAlloc<Vector3>(n, out var span));
        Assert.Equal(n, span.Length);
        Assert.Equal(mem.Length - need, arena.Remaining);
    }

    [Fact]
    public void Alloc_returns_slices_over_backing_memory()
    {
        Span<byte> mem = stackalloc byte[16];
        var arena = new Arena(mem);
        var s = arena.Alloc<int>(2);
        s[0] = 0x11223344;
        s[1] = unchecked((int)0xAABBCCDD);
        Assert.Equal(0x44, mem[0]);
    }
}
