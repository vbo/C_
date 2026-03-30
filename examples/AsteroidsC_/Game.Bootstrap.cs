using System;
using System.IO;
using Raylib_cs;

namespace AsteroidsC_;

public static partial class Game
{
    [C_.Exempt(Reason = "Allocates fixed slot arrays; seeds RNG; LoadTexture path string; starts first wave")]
    public static void InitializeAfterWindow()
    {
        _asteroids = new AsteroidSlot[MaxAsteroids];
        _bullets = new BulletSlot[MaxBullets];
        SeedRng((ulong)Environment.TickCount64);
        LoadSprites();
        StartNewGame();
    }

    [C_.Exempt(Reason = "LoadTexture / path combine")]
    private static void LoadSprites()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "sprites.png");
        _sprites = Raylib.LoadTexture(path);
        Raylib.SetTextureFilter(_sprites, TextureFilter.Point);
    }

    [C_.Exempt(Reason = "UnloadTexture at shutdown")]
    internal static void UnloadSprites()
    {
        if (_sprites.Id != 0)
        {
            Raylib.UnloadTexture(_sprites);
            _sprites = default;
        }
    }

    private static void StartNewGame()
    {
        _score = 0;
        _lives = 3;
        _wave = 0;
        _gameOver = false;
        _fireCooldownRemainSec = 0f;
        _playerThrustOn = false;
        ClearAsteroids();
        ClearBullets();
        ResetShip();
        NextWave();
    }

    internal static void RestartFromGameOver() => StartNewGame();

    private static void SeedRng(ulong s) => _rng = s == 0 ? 1UL : s;

    private static uint NextU32()
    {
        var x = _rng;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        _rng = x;
        return (uint)x;
    }

    private static float Rng01() => (NextU32() & 0xFFFFFF) / 16777216f;

    private static float RngRange(float a, float b) => a + Rng01() * (b - a);

    private static void ClearAsteroids()
    {
        for (var i = 0; i < _asteroids.Length; i++)
            _asteroids[i].Active = false;
    }

    private static void ClearBullets()
    {
        for (var i = 0; i < _bullets.Length; i++)
            _bullets[i].Active = false;
    }

    private static void ResetShip()
    {
        _ship.X = ScreenW * 0.5f;
        _ship.Y = ScreenH * 0.5f;
        _ship.Vx = 0f;
        _ship.Vy = 0f;
        _ship.AngleDeg = -90f;
        _ship.InvulnSec = RespawnInvulnSec;
        _playerThrustOn = false;
    }

    private static void NextWave()
    {
        _wave++;
        var count = 3 + _wave;
        if (count > 12)
            count = 12;

        var cx = ScreenW * 0.5f;
        var cy = ScreenH * 0.5f;

        for (var n = 0; n < count; n++)
        {
            float x = 0f, y = 0f, dx, dy;
            var guard = 0;
            do
            {
                x = RngRange(LargeHalf + 8f, ScreenW - LargeHalf - 8f);
                y = RngRange(LargeHalf + 8f, ScreenH - LargeHalf - 8f);
                dx = x - cx;
                dy = y - cy;
                guard++;
            } while (dx * dx + dy * dy < 220f * 220f && guard < 48);

            TrySpawnAsteroid(x, y, 0, 0.55f);
        }
    }

    private static (float vx, float vy) RndVelocity(float scale)
    {
        var a = RngRange(0f, MathF.PI * 2f);
        var sp = RngRange(0.7f, 2.1f) * scale;
        return (MathF.Cos(a) * sp, MathF.Sin(a) * sp);
    }

    private static bool TrySpawnAsteroid(float x, float y, byte tier, float speedScale)
    {
        var slot = FirstFreeAsteroid();
        if (slot < 0)
            return false;

        var half = tier switch
        {
            0 => LargeHalf,
            1 => SmallRockHalf,
            _ => TinyRockHalf,
        };

        var variant = tier == 0
            ? (byte)(NextU32() % 3u)
            : (byte)(NextU32() & 3u);

        var (vx, vy) = RndVelocity(speedScale);
        vx *= SimRefHz;
        vy *= SimRefHz;

        _asteroids[slot] = new AsteroidSlot
        {
            X = x,
            Y = y,
            Vx = vx,
            Vy = vy,
            HalfExtent = half,
            SpinDeg = RngRange(0f, 360f),
            Tier = tier,
            SpriteVariant = variant,
            Active = true,
        };
        return true;
    }

    private static int FirstFreeAsteroid()
    {
        for (var i = 0; i < _asteroids.Length; i++)
        {
            if (!_asteroids[i].Active)
                return i;
        }

        return -1;
    }

    private static bool TrySpawnBullet(float x, float y, float vx, float vy)
    {
        for (var i = 0; i < _bullets.Length; i++)
        {
            if (_bullets[i].Active)
                continue;
            _bullets[i] = new BulletSlot
            {
                X = x,
                Y = y,
                Vx = vx,
                Vy = vy,
                AgeSec = 0f,
                Active = true,
            };
            return true;
        }

        return false;
    }
}
