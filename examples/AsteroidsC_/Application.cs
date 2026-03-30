using System.Diagnostics;
using System.Runtime;
using Raylib_cs;

namespace AsteroidsC_;

/// <summary>Entry and Raylib lifetime. <see cref="Main"/> is exempt; <see cref="PresentFrame"/> and <see cref="Game"/> sim/draw stay hot (C_0017).</summary>
public static class Application
{
    [C_.Exempt(Reason = "Raylib InitWindow/CloseWindow; hosts frame loop; GC latency hint")]
    public static void Main()
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        Raylib.InitWindow(Game.ScreenW, Game.ScreenH, "Asteroids C_");

        Game.InitializeAfterWindow();

        while (!Raylib.WindowShouldClose())
            PresentFrame();

        Game.UnloadSprites();
        Raylib.CloseWindow();
    }

    private static void PresentFrame()
    {
        if (Game.RestartRequested)
        {
            Game.RestartRequested = false;
            Game.RestartFromGameOver();
        }

        Game.RunSimulation();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Game.BackgroundColor);
        Game.DrawScene();
        Game.DrawHud();
        Raylib.EndDrawing();
    }
}
