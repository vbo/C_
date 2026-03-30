using System;
using Raylib_cs;

namespace AsteroidsC_;

public static partial class Game
{
    /// <summary>Hot path: GC snapshot, input, integration, collisions (no exempt calls).</summary>
    public static void RunSimulation()
    {
        _gcAtFrameStart = GC.GetAllocatedBytesForCurrentThread();

        var dt = Raylib.GetFrameTime();
        if (float.IsNaN(dt) || dt <= 0f)
            dt = 1f / SimRefHz;
        if (dt > 0.1f)
            dt = 0.1f;

        PollDebugToggle();

        if (_gameOver)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Enter))
                RestartRequested = true;
        }
        else
        {
            PollDrive(dt);
            TryFire(dt);
            TickInvulnerability(dt);

            var rem = dt;
            while (rem > MaxPhysicsStepSec)
            {
                SimStep(MaxPhysicsStepSec);
                rem -= MaxPhysicsStepSec;
            }

            if (rem > 1e-6f)
                SimStep(rem);

            if (CountActiveAsteroids() == 0)
                NextWave();
        }

        if (_showDebugHud)
            RefreshDebugHudStats(dt);
    }

    /// <summary>Collects GC / FPS counters for the F3 panel after sim work (draw path stays allocation-free for these reads).</summary>
    private static void RefreshDebugHudStats(float frameDt)
    {
        _debugHudFps = Raylib.GetFPS();

        var delta = unchecked(GC.GetAllocatedBytesForCurrentThread() - _gcAtFrameStart);
        _allocRateAccBytes += delta;
        _allocRateAccSec += frameDt;
        if (_allocRateAccSec >= DebugAllocRateWindowSec)
        {
            _debugHudAllocPerSec = (long)(_allocRateAccBytes / _allocRateAccSec);
            _allocRateAccBytes = 0;
            _allocRateAccSec = 0f;
        }

        _debugHudGc0 = GC.CollectionCount(0);
        _debugHudGc1 = GC.CollectionCount(1);
        _debugHudGc2 = GC.CollectionCount(2);

        if (_heapSampleFrame++ >= 30)
        {
            _heapSampleFrame = 0;
            _lastHeapBytes = GC.GetTotalMemory(false);
        }

        _debugHudHeapKb = _lastHeapBytes / 1024;
    }

    private static void SimStep(float h)
    {
        IntegrateShip(h);
        IntegrateBullets(h);
        IntegrateAsteroids(h);
        ResolveCollisions();
    }

    private static void PollDebugToggle()
    {
        if (!Raylib.IsKeyPressed(KeyboardKey.F3))
            return;
        _showDebugHud = !_showDebugHud;
        if (_showDebugHud)
        {
            _allocRateAccBytes = 0;
            _allocRateAccSec = 0f;
            _debugHudAllocPerSec = 0;
        }
    }

    private static void PollDrive(float dt)
    {
        if (Raylib.IsKeyDown(KeyboardKey.Right))
            _ship.AngleDeg += TurnDegPerSec * dt;
        if (Raylib.IsKeyDown(KeyboardKey.Left))
            _ship.AngleDeg -= TurnDegPerSec * dt;

        var rad = _ship.AngleDeg * (MathF.PI / 180f);
        var c = MathF.Cos(rad);
        var s = MathF.Sin(rad);

        var thrustHeld = Raylib.IsKeyDown(KeyboardKey.Up);
        if (thrustHeld)
        {
            _ship.Vx += c * ThrustAccelPerSec * dt;
            _ship.Vy += s * ThrustAccelPerSec * dt;
        }
        else
        {
            var dragMul = MathF.Pow(DragPerRefFrame, dt * SimRefHz);
            _ship.Vx *= dragMul;
            _ship.Vy *= dragMul;
        }

        var spd = MathF.Sqrt(_ship.Vx * _ship.Vx + _ship.Vy * _ship.Vy);
        if (spd > MaxSpeed)
        {
            var k = MaxSpeed / spd;
            _ship.Vx *= k;
            _ship.Vy *= k;
        }

        _playerThrustOn = thrustHeld;
    }

    private static void TryFire(float dt)
    {
        if (_fireCooldownRemainSec > 0f)
            _fireCooldownRemainSec -= dt;

        if (!Raylib.IsKeyDown(KeyboardKey.Space) || _fireCooldownRemainSec > 0f)
            return;

        var rad = _ship.AngleDeg * (MathF.PI / 180f);
        var c = MathF.Cos(rad);
        var s = MathF.Sin(rad);
        var muzzle = 20f;
        var bx = _ship.X + c * muzzle;
        var by = _ship.Y + s * muzzle;
        var vx = c * BulletSpeed + _ship.Vx;
        var vy = s * BulletSpeed + _ship.Vy;

        if (TrySpawnBullet(bx, by, vx, vy))
            _fireCooldownRemainSec = FireCooldownSec;
    }

    private static void TickInvulnerability(float dt)
    {
        if (_ship.InvulnSec > 0f)
            _ship.InvulnSec -= dt;
    }

    private static void IntegrateShip(float dt)
    {
        _ship.X += _ship.Vx * dt;
        _ship.Y += _ship.Vy * dt;
        Wrap(ref _ship.X, ref _ship.Y);
    }

    private static void IntegrateBullets(float dt)
    {
        for (var i = 0; i < _bullets.Length; i++)
        {
            ref var b = ref _bullets[i];
            if (!b.Active)
                continue;

            b.X += b.Vx * dt;
            b.Y += b.Vy * dt;
            Wrap(ref b.X, ref b.Y);
            b.AgeSec += dt;
            if (b.AgeSec >= BulletLifetimeSec)
                b.Active = false;
        }
    }

    private static void IntegrateAsteroids(float dt)
    {
        for (var i = 0; i < _asteroids.Length; i++)
        {
            ref var a = ref _asteroids[i];
            if (!a.Active)
                continue;

            a.X += a.Vx * dt;
            a.Y += a.Vy * dt;
            Wrap(ref a.X, ref a.Y);

            var spin = a.Tier switch
            {
                0 => AsteroidSpinDegPerSec0,
                1 => AsteroidSpinDegPerSec1,
                _ => AsteroidSpinDegPerSec2,
            };
            a.SpinDeg += spin * dt;
            if (a.SpinDeg >= 360f)
                a.SpinDeg -= 360f;
        }
    }

    private static void Wrap(ref float x, ref float y)
    {
        if (x < 0f)
            x += ScreenW;
        else if (x >= ScreenW)
            x -= ScreenW;

        if (y < 0f)
            y += ScreenH;
        else if (y >= ScreenH)
            y -= ScreenH;
    }

    private static bool Circles(float ax, float ay, float ar, float bx, float by, float br)
    {
        var dx = ax - bx;
        var dy = ay - by;
        var r = ar + br;
        return dx * dx + dy * dy <= r * r;
    }

    private static float AsteroidRadius(in AsteroidSlot a) => a.HalfExtent * 0.82f;

    private static void ResolveCollisions()
    {
        for (var bi = 0; bi < _bullets.Length; bi++)
        {
            ref var b = ref _bullets[bi];
            if (!b.Active)
                continue;

            for (var ai = 0; ai < _asteroids.Length; ai++)
            {
                ref var a = ref _asteroids[ai];
                if (!a.Active)
                    continue;

                var ar = AsteroidRadius(a);
                if (!Circles(b.X, b.Y, BulletHitRadius, a.X, a.Y, ar))
                    continue;

                b.Active = false;
                var tier = a.Tier;
                var ax = a.X;
                var ay = a.Y;
                a.Active = false;

                _score += tier switch
                {
                    0 => 20,
                    1 => 50,
                    _ => 100,
                };

                if (tier < 2)
                {
                    var sp = 1.05f + tier * 0.35f;
                    TrySpawnAsteroid(ax - 10f, ay, (byte)(tier + 1), sp);
                    TrySpawnAsteroid(ax + 10f, ay, (byte)(tier + 1), sp);
                }

                break;
            }
        }

        if (_ship.InvulnSec > 0f)
            return;

        for (var ai = 0; ai < _asteroids.Length; ai++)
        {
            ref var a = ref _asteroids[ai];
            if (!a.Active)
                continue;

            var ar = AsteroidRadius(a);
            if (!Circles(_ship.X, _ship.Y, ShipHitRadius, a.X, a.Y, ar))
                continue;

            HitShip();
            break;
        }
    }

    private static void HitShip()
    {
        _lives--;
        if (_lives <= 0)
        {
            _gameOver = true;
            return;
        }

        ResetShip();
        ClearBullets();
    }

    private static int CountActiveAsteroids()
    {
        var n = 0;
        for (var i = 0; i < _asteroids.Length; i++)
        {
            if (_asteroids[i].Active)
                n++;
        }

        return n;
    }
}
