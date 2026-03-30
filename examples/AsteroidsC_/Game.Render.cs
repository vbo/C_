using System;
using System.Numerics;
using Raylib_cs;

namespace AsteroidsC_;

public static partial class Game
{
    /// <summary>Sprite draws via Raylib; coordinates match window pixels (<see cref="ScreenW"/>×<see cref="ScreenH"/>).</summary>
    internal static void DrawScene()
    {
        if (_sprites.Id == 0)
            return;

        DrawAsteroids();
        DrawBullets();
        DrawShip();
    }

    public static void DrawHud()
    {
        Span<int> buf = stackalloc int[HudCodepointCapacity];
        var len = 0;
        AppendAsciiUtf8(buf, ref len, "Score "u8);
        AppendUInt32(buf, ref len, (uint)_score);
        AppendAsciiUtf8(buf, ref len, "  Lives "u8);
        AppendUInt32(buf, ref len, (uint)_lives);
        AppendAsciiUtf8(buf, ref len, "  Wave "u8);
        AppendUInt32(buf, ref len, (uint)_wave);
        DrawCodepoints(buf, len, 12, 8, FontSizeHud, Color.RayWhite);

        var y = 8 + LineStepHud + 4;
        if (_gameOver)
        {
            len = 0;
            AppendAsciiUtf8(buf, ref len, "GAME OVER - Enter to restart"u8);
            DrawCodepoints(buf, len, 12, y, FontSizeHud, new Color(255, 180, 120, 255));
        }

        const int hintLineStep = 20;
        const float hintFont = 16f;
        var hintY = ScreenH - 8 - hintLineStep * 2;
        len = 0;
        AppendAsciiUtf8(buf, ref len, "Left/Right: turn  Up: thrust  Space: fire"u8);
        DrawCodepoints(buf, len, 12, hintY, hintFont, Color.DarkGray);
        hintY += hintLineStep;
        len = 0;
        AppendAsciiUtf8(buf, ref len, "F3: stats  Esc: quit"u8);
        DrawCodepoints(buf, len, 12, hintY, hintFont, Color.DarkGray);

        if (!_showDebugHud)
            return;

        y = 8;
        var x = ScreenW - 500;
        if (x < 12)
            x = 12;

        len = 0;
        AppendAsciiUtf8(buf, ref len, "FPS (raylib): "u8);
        AppendInt32(buf, ref len, _debugHudFps);
        DrawCodepoints(buf, len, x, y, FontSizeHud, Color.LightGray);
        y += LineStepHud;

        len = 0;
        AppendAsciiUtf8(buf, ref len, "Alloc/sec (thread): "u8);
        AppendInt64(buf, ref len, _debugHudAllocPerSec);
        DrawCodepoints(buf, len, x, y, FontSizeHud, Color.LightGray);
        y += LineStepHud;

        len = 0;
        AppendAsciiUtf8(buf, ref len, "GC gen 0/1/2: "u8);
        AppendUInt32(buf, ref len, (uint)_debugHudGc0);
        AppendAsciiUtf8(buf, ref len, " / "u8);
        AppendUInt32(buf, ref len, (uint)_debugHudGc1);
        AppendAsciiUtf8(buf, ref len, " / "u8);
        AppendUInt32(buf, ref len, (uint)_debugHudGc2);
        DrawCodepoints(buf, len, x, y, FontSizeHud, Color.LightGray);
        y += LineStepHud;

        len = 0;
        AppendAsciiUtf8(buf, ref len, "Heap (sampled): "u8);
        AppendInt64(buf, ref len, _debugHudHeapKb);
        AppendAsciiUtf8(buf, ref len, " KB"u8);
        DrawCodepoints(buf, len, x, y, FontSizeHud, Color.LightGray);
        y += LineStepHud;

        len = 0;
        AppendAsciiUtf8(buf, ref len, "F3 toggles this panel"u8);
        DrawCodepoints(buf, len, x, y, 16f, Color.DarkGray);
    }

    private static Rectangle Src(SpriteSheet.Region r) => new(r.X, r.Y, r.W, r.H);

    private static void DrawShip()
    {
        var blink = _ship.InvulnSec > 0f && (((int)(_ship.InvulnSec * SimRefHz) >> 3) & 1) == 0;
        if (blink)
            return;

        var reg = _playerThrustOn ? SpriteSheet.PlayerThrust : SpriteSheet.PlayerIdle;
        var src = Src(reg);
        var sz = ShipSpriteSize;
        var half = sz * 0.5f;
        // DrawTexturePro places origin at (dest.x, dest.y); quad top-left is dest - origin.
        var dest = new Rectangle(_ship.X, _ship.Y, sz, sz);
        var origin = new Vector2(half, half);
        Raylib.DrawTexturePro(_sprites, src, dest, origin, _ship.AngleDeg + 90f, Color.RayWhite);
    }

    private static void DrawBullets()
    {
        var src = Src(SpriteSheet.LaserFrame0);
        var sz = BulletDrawSize;
        var half = sz * 0.5f;

        for (var i = 0; i < _bullets.Length; i++)
        {
            ref var b = ref _bullets[i];
            if (!b.Active)
                continue;

            var dest = new Rectangle(b.X, b.Y, sz, sz);
            var deg = MathF.Atan2(b.Vy, b.Vx) * (180f / MathF.PI);
            Raylib.DrawTexturePro(_sprites, src, dest, new Vector2(half, half), deg, Color.RayWhite);
        }
    }

    private static void DrawAsteroids()
    {
        for (var i = 0; i < _asteroids.Length; i++)
        {
            ref var a = ref _asteroids[i];
            if (!a.Active)
                continue;

            var reg = a.Tier == 0
                ? SpriteSheet.AsteroidLarge(a.SpriteVariant % 3)
                : SpriteSheet.AsteroidSmall(a.SpriteVariant & 3);

            var src = Src(reg);
            var dim = a.HalfExtent * 2f;
            var halfDim = dim * 0.5f;
            var dest = new Rectangle(a.X, a.Y, dim, dim);
            var origin = new Vector2(halfDim, halfDim);
            Raylib.DrawTexturePro(_sprites, src, dest, origin, a.SpinDeg, Color.RayWhite);
        }
    }

    private static void DrawCodepoints(Span<int> buf, int count, int posX, int posY, float fontSize, Color color)
    {
        if (count <= 0 || count > buf.Length)
            return;

        unsafe
        {
            fixed (int* p = buf)
                Raylib.DrawTextCodepoints(Raylib.GetFontDefault(), p, count, new Vector2(posX, posY), fontSize, 1f, color);
        }
    }

    private static void AppendAsciiUtf8(Span<int> buf, ref int len, ReadOnlySpan<byte> asciiUtf8)
    {
        if (len + asciiUtf8.Length > buf.Length)
            return;

        for (var i = 0; i < asciiUtf8.Length; i++)
            buf[len++] = asciiUtf8[i];
    }

    private static void AppendUInt32(Span<int> buf, ref int len, uint v)
    {
        if (len >= buf.Length)
            return;

        if (v == 0)
        {
            buf[len++] = '0';
            return;
        }

        var n = 0;
        for (var t = v; t > 0; t /= 10)
            n++;

        if (len + n > buf.Length)
            return;

        var end = len + n;
        var w = end;
        while (v > 0)
        {
            buf[--w] = '0' + (int)(v % 10);
            v /= 10;
        }

        len = end;
    }

    private static void AppendInt32(Span<int> buf, ref int len, int v) =>
        AppendInt64(buf, ref len, v);

    private static void AppendInt64(Span<int> buf, ref int len, long v)
    {
        if (v < 0)
        {
            if (len >= buf.Length)
                return;
            buf[len++] = '-';
            if (v == long.MinValue)
            {
                AppendAsciiUtf8(buf, ref len, "9223372036854775808"u8);
                return;
            }

            v = -v;
        }

        AppendUInt64(buf, ref len, (ulong)v);
    }

    private static void AppendUInt64(Span<int> buf, ref int len, ulong v)
    {
        if (len >= buf.Length)
            return;

        if (v == 0)
        {
            buf[len++] = '0';
            return;
        }

        var n = 0;
        for (var t = v; t > 0; t /= 10)
            n++;

        if (len + n > buf.Length)
            return;

        var end = len + n;
        var w = end;
        while (v > 0)
        {
            buf[--w] = '0' + (int)(v % 10);
            v /= 10;
        }

        len = end;
    }
}
