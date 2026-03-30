using Raylib_cs;

namespace AsteroidsC_;

/// <summary>Fixed slots, sim state, and frame-start GC snapshot (hot-path fields).</summary>
public static partial class Game
{
    private struct ShipState
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;

        /// <summary>Degrees; 0 = east, 90 = south (screen Y down), ship nose is “up” at -90.</summary>
        public float AngleDeg;

        public float InvulnSec;
    }

    private struct AsteroidSlot
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float HalfExtent;
        public float SpinDeg;
        public byte Tier;

        /// <summary>Large: 0..2 → <see cref="SpriteSheet.AsteroidLarge"/>; small/tiny: 0..3 → <see cref="SpriteSheet.AsteroidSmall"/>.</summary>
        public byte SpriteVariant;

        public bool Active;
    }

    private struct BulletSlot
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float AgeSec;
        public bool Active;
    }

    private static ShipState _ship;
    private static AsteroidSlot[] _asteroids = null!;
    private static BulletSlot[] _bullets = null!;

    private static ulong _rng;
    private static Texture2D _sprites;

    /// <summary>Visual thrust on (mirrors last <c>Up</c> in <see cref="PollDrive"/>).</summary>
    private static bool _playerThrustOn;

    private static int _score;
    private static int _lives;
    private static int _wave;
    private static float _fireCooldownRemainSec;
    private static bool _gameOver;
    private static long _gcAtFrameStart;
    private static bool _showDebugHud;
    private static int _heapSampleFrame;
    private static long _lastHeapBytes;

    /// <summary>Last frame’s debug overlay metrics; filled in <see cref="RefreshDebugHudStats"/> when F3 panel is on.</summary>
    private static int _debugHudFps;
    private static long _debugHudAllocPerSec;
    private static long _allocRateAccBytes;
    private static float _allocRateAccSec;
    private static int _debugHudGc0;
    private static int _debugHudGc1;
    private static int _debugHudGc2;
    private static long _debugHudHeapKb;

    /// <summary>Set from hot sim when Enter is pressed on game over; consumed next frame in <see cref="Application.PresentFrame"/>.</summary>
    internal static bool RestartRequested;
}
