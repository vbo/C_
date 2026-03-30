using System;
using C_.Memory;

namespace HelloC_SDK;

// Companion to ../HelloC_/: that sample uses the SDK for attributes only and a manual bump offset over stackalloc.
// This executable dogfoods C_.Memory.Arena for the same style of per-frame scratch (see docs/guide_memory.md).

/// <summary>
/// Minimal loop: one <see cref="Arena"/> over <c>stackalloc</c> backing per <see cref="Tick"/>; no heap on the hot path.
/// </summary>
public static partial class Application
{
    private static int _frame;

    [C_.Exempt(Reason = "Demo entry + console")]
    public static void Main()
    {
        while (_frame < 3)
            Tick();
        Console.WriteLine("HelloC_SDK: Arena scratch OK.");
    }

    public static void Tick()
    {
        var arena = new Arena(stackalloc byte[64]);
        if (!arena.TryAlloc<int>(4, out var ints))
            return;
        ints[0] = _frame;
        _frame++;
    }
}
