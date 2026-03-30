using Raylib_cs;

namespace AsteroidsC_;

/// <summary>Tuning constants, HUD layout, clear color, and sprite atlas layout for <c>Assets/sprites.png</c> (256×256).</summary>
public static partial class Game
{
    public const int ScreenW = 960;
    public const int ScreenH = 540;

    public const int MaxAsteroids = 64;
    public const int MaxBullets = 32;

    public static readonly Color BackgroundColor = new(12, 14, 28, 255);

    /// <summary>Original per-frame tuning assumed ~this many steps per second.</summary>
    private const float SimRefHz = 60f;

    private const float TurnDegPerSec = 3.8f * SimRefHz;
    private const float ThrustAccelPerSec = 2f * SimRefHz;

    /// <summary>Per-reference-frame velocity multiplier when coasting (no thrust): <c>Pow(..., dt * SimRefHz)</c>.</summary>
    private const float DragPerRefFrame = 0.987f;

    private const float MaxSpeed = 12f * SimRefHz;
    private const float BulletSpeed = 15f * SimRefHz;
    private const float BulletLifetimeSec = 100f / SimRefHz;
    private const float FireCooldownSec = 20f / SimRefHz;
    private const float RespawnInvulnSec = 120f / SimRefHz;

    private const float AsteroidSpinDegPerSec0 = 0.45f * SimRefHz;
    private const float AsteroidSpinDegPerSec1 = 0.7f * SimRefHz;
    private const float AsteroidSpinDegPerSec2 = 1f * SimRefHz;

    /// <summary>Caps integration/collision step so fast bullets cannot tunnel past small asteroids at large <c>dt</c>.</summary>
    private const float MaxPhysicsStepSec = 1f / 240f;

    /// <summary>Player sprite is 32×32; art faces up; rotation = <see cref="ShipState.AngleDeg"/> + 90°.</summary>
    private const float ShipSpriteSize = 32f;

    private const float ShipHitRadius = 10f;
    private const float BulletDrawSize = 14f;
    private const float BulletHitRadius = 6f;

    /// <summary>Tier 0 = 64×64 art; tier 1 = 32×32; tier 2 = same art scaled down.</summary>
    private const float LargeHalf = 32f;

    private const float SmallRockHalf = 16f;
    private const float TinyRockHalf = 9f;

    private const int HudCodepointCapacity = 288;
    private const int FontSizeHud = 20;
    private const int LineStepHud = 22;

    /// <summary>Wall time over which per-frame thread allocs are summed for the F3 <c>alloc/sec</c> line.</summary>
    private const float DebugAllocRateWindowSec = 1f;
}

/// <summary>
/// Pixel source rectangles for <c>Assets/sprites.png</c> (256×256, top-left origin).
/// Layout: 32×32 small rocks and UFO row; 16×16 laser cells under the UFO; player idle + thrust on bottom row.
/// </summary>
public static class SpriteSheet
{
    public const int TextureSize = 256;

    public const int AsteroidSmallVariantCount = 4;
    public const int AsteroidLargeVariantCount = 3;
    public const int LaserFrameCount = 3;

    /// <summary>Integer pixel rect inside the texture (for <see cref="Raylib_cs.Raylib.DrawTexturePro"/> source).</summary>
    public readonly struct Region
    {
        public readonly short X;
        public readonly short Y;
        public readonly short W;
        public readonly short H;

        public Region(short x, short y, short w, short h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }

    // --- 32 × 32 : small asteroids (top row) ---
    public static readonly Region AsteroidSmall0 = new(0, 0, 32, 32);
    public static readonly Region AsteroidSmall1 = new(32, 0, 32, 32);
    public static readonly Region AsteroidSmall2 = new(64, 0, 32, 32);
    public static readonly Region AsteroidSmall3 = new(96, 0, 32, 32);

    public static readonly Region EnemyShipSmall = new(128, 0, 32, 32);

    public static readonly Region LaserFrame0 = new(128, 32, 16, 16);
    public static readonly Region LaserFrame1 = new(144, 32, 16, 16);
    public static readonly Region LaserFrame2 = new(128, 48, 16, 16);

    public static readonly Region AsteroidLarge0 = new(0, 64, 64, 64);
    public static readonly Region AsteroidLarge1 = new(64, 64, 64, 64);
    public static readonly Region AsteroidLarge2 = new(128, 64, 64, 64);

    public static readonly Region BackgroundStars = new(0, 144, 128, 32);

    /// <summary>Player ship, no thrust (bottom row).</summary>
    public static readonly Region PlayerIdle = new(0, 192, 32, 32);

    /// <summary>Player ship with thrust (single pose for binary on/off).</summary>
    public static readonly Region PlayerThrust = new(96, 192, 32, 32);

    public static Region AsteroidSmall(int variant) => (variant & 3) switch
    {
        0 => AsteroidSmall0,
        1 => AsteroidSmall1,
        2 => AsteroidSmall2,
        _ => AsteroidSmall3,
    };

    public static Region AsteroidLarge(int variant) => (uint)variant switch
    {
        0 => AsteroidLarge0,
        1 => AsteroidLarge1,
        _ => AsteroidLarge2,
    };

    public static Region Laser(int frame) => (uint)frame switch
    {
        0 => LaserFrame0,
        1 => LaserFrame1,
        _ => LaserFrame2,
    };
}
