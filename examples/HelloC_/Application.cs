using System;
using System.Diagnostics;
using System.Runtime;

namespace HelloC_;

/// <summary>
/// Self-contained sketch aligned with <c>docs/lang.md</c>, <c>docs/analyzer.md</c>, and <c>docs/guide_memory.md</c>:
/// <list type="bullet">
/// <item><description><b>[C_.Exempt]</b> — startup allocates the backing array, console I/O, and the final ASCII snapshot.</description></item>
/// <item><description><b>Pre-allocated array</b> — <see cref="Entity"/> state filled once in <see cref="Initialize"/>.</description></item>
/// <item><description><b><c>stackalloc</c> frame scratch</b> — per-entity telemetry slices (bump <c>offset</c>) each <see cref="Tick"/>.</description></item>
/// <item><description><b>[C_.DebugExempt]</b> + <b>[Conditional("DEBUG")]</b> — printf-style trace on the hot path in Debug only.</description></item>
/// </list>
/// Simulation: four dots spawn at random grid positions and directions (cold path only); they bounce, swap velocity on overlap, then <see cref="ReportFinalState"/> draws the map.
/// </summary>
public static partial class Application
{
    private const short GridSize = 20;

    /// <summary>Axis-aligned particle with integer physics (no heap).</summary>
    private readonly struct Entity(short x, short y, sbyte vx, sbyte vy)
    {
        public readonly short X = x;
        public readonly short Y = y;
        public readonly sbyte Vx = vx;
        public readonly sbyte Vy = vy;

        public Entity Step() => IntegrateMotion(this);
    }

    /// <summary>Backing store allocated only from <see cref="Initialize"/> (cold path).</summary>
    private static Entity[] _entities = null!;

    private static int _simulationTick;

    private static class Platform
    {
        private const int MaxFrames = 17;
        private static int _frame;

        public static bool IsRunning() => _frame++ < MaxFrames;
    }

    [C_.Exempt(Reason = "Startup: heap array + console + Random; seeds simulation")]
    public static void Initialize()
    {
        // System.Random allocates and uses virtual methods — fine on [Exempt] startup only.
        // For entropy inside Tick, use a struct PRNG (xorshift, etc.) with no heap.
        var rng = new Random(unchecked((int)Environment.TickCount64));

        _entities = new Entity[4];
        for (var i = 0; i < _entities.Length; i++)
        {
            sbyte vx, vy;
            do
            {
                vx = (sbyte)rng.Next(-1, 2);
                vy = (sbyte)rng.Next(-1, 2);
            } while (vx == 0 && vy == 0);

            _entities[i] = new(
                (short)rng.Next(0, GridSize),
                (short)rng.Next(0, GridSize),
                vx,
                vy);
        }

        Console.WriteLine("Hello, C_ — tiny grid simulation (see final map).");
    }

    public static void Main()
    {
        // Recommended for C_: reduce GC pauses and jitter during hot paths.
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        Initialize();

        while (Platform.IsRunning())
            Tick();

        ReportFinalState();
    }

    public static void Tick()
    {
        DebugTrace("tick");

        Span<byte> scratch = stackalloc byte[32];
        var offset = 0;

        for (var i = 0; i < _entities.Length; i++)
        {
            ref var e = ref _entities[i];
            StepEntity(ref e, ref scratch, ref offset);
        }

        ResolveOverlaps();

        _simulationTick++;
    }

    [C_.Exempt(Reason = "Post-run visualization; string work and console I/O")]
    private static void ReportFinalState()
    {
        Console.WriteLine();
        Console.WriteLine($"Ticks simulated: {_simulationTick} (grid {GridSize}x{GridSize})");

        Span<char> row = stackalloc char[GridSize];
        for (var y = 0; y < GridSize; y++)
        {
            for (var x = 0; x < GridSize; x++)
                row[x] = '.';

            for (var i = 0; i < _entities.Length; i++)
            {
                var e = _entities[i];
                if (e.Y == y && (uint)e.X < (uint)GridSize)
                    row[e.X] = (char)('A' + i);
            }

            Console.WriteLine(row);
        }
    }

    /// <summary>Printf hook: see <c>docs/analyzer.md</c> (DebugExempt + Conditional).</summary>
    [Conditional("DEBUG")]
    [C_.DebugExempt(Reason = "Printf debugging; calls stripped when DEBUG undefined")]
    private static void DebugTrace(string message) => Console.WriteLine(message);

    /// <summary>Hot path: copy a snapshot into scratch (bump allocator), then integrate motion.</summary>
    private static void StepEntity(ref Entity e, ref Span<byte> scratch, ref int offset)
    {
        const int need = 4;
        var block = scratch.Slice(offset, need);
        offset += need;

        block[0] = (byte)e.X;
        block[1] = (byte)e.Y;
        block[2] = (byte)e.Vx;
        block[3] = (byte)e.Vy;

        e = e.Step();
    }

    /// <summary>Hot path: if two entities land on the same tile, exchange velocity (cheap “collision”).</summary>
    private static void ResolveOverlaps()
    {
        for (var i = 0; i < _entities.Length; i++)
        {
            for (var j = i + 1; j < _entities.Length; j++)
            {
                ref var a = ref _entities[i];
                ref var b = ref _entities[j];
                if (a.X != b.X || a.Y != b.Y)
                    continue;

                var avx = a.Vx;
                var avy = a.Vy;
                a = new Entity(a.X, a.Y, b.Vx, b.Vy);
                b = new Entity(b.X, b.Y, avx, avy);
            }
        }
    }

    /// <summary>Axis-aligned bounce against <see cref="GridSize"/>; kept at file bottom so <see cref="Entity"/> stays small.</summary>
    private static Entity IntegrateMotion(Entity e)
    {
        var nx = (short)(e.X + e.Vx);
        var ny = (short)(e.Y + e.Vy);
        var vx = e.Vx;
        var vy = e.Vy;

        if (nx < 0)
        {
            nx = 0;
            vx = (sbyte)-vx;
        }
        else if (nx >= GridSize)
        {
            nx = (short)(GridSize - 1);
            vx = (sbyte)-vx;
        }

        if (ny < 0)
        {
            ny = 0;
            vy = (sbyte)-vy;
        }
        else if (ny >= GridSize)
        {
            ny = (short)(GridSize - 1);
            vy = (sbyte)-vy;
        }

        return new Entity(nx, ny, vx, vy);
    }
}
