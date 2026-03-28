using System;
using System.Diagnostics;
using System.Runtime;

namespace HelloC_;

/// <summary>Example shape from docs/lang.md §8: static partial module, startup vs hot path.</summary>
public static partial class Application
{
    /// <summary>Allocations live here; [Exempt] on this type exempts nested ctor bodies. Initialize is also attributed so it may call into this type from outside.</summary>
    [C_.Exempt(Reason = "Startup buffers (constructed only from Initialize)")]
    private static class StartupBuffers
    {
        internal struct PreallocatedArray<T> where T : struct
        {
            private readonly T[] _items;

            public PreallocatedArray(int length)
            {
                _items = new T[length];
            }

            public ref T this[int index] => ref _items[index];
        }
    }

    private readonly struct GameState
    {
        public readonly int Tick;

        public GameState(int tick) => Tick = tick;

        public GameState WithNextTick() => new(Tick + 1);
    }

    private static class Platform
    {
        private const int MaxFrames = 3;
        private static int s_frame;

        public static bool IsRunning() => s_frame++ < MaxFrames;
    }

    private static StartupBuffers.PreallocatedArray<GameState> s_state;

    [C_.Exempt(Reason = "Startup: backing store and console I/O")]
    public static void Initialize()
    {
        s_state = new StartupBuffers.PreallocatedArray<GameState>(1);
        Console.WriteLine("Hello, C_");
    }

    public static void Main()
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        Initialize();

        while (Platform.IsRunning())
        {
            Tick();
        }
    }

    public static void Tick()
    {
        DebugTrace("tick");
        ref var s = ref s_state[0];
        UpdateSystems(ref s);
    }

    /// <summary>Printf hook: <c>[Conditional("DEBUG")]</c> + <c>[C_.DebugExempt]</c> (see docs/analyzer.md).</summary>
    [Conditional("DEBUG")]
    [C_.DebugExempt(Reason = "Hot-path printf; calls stripped when DEBUG undefined")]
    private static void DebugTrace(string message) => Console.WriteLine(message);

    private static void UpdateSystems(ref GameState s)
    {
        s = s.WithNextTick();
    }
}
