// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Foundation;
using Microsoft.Xna.Framework;
using UIKit;

[Register("AppDelegate")]
internal sealed class AppDelegate : UIApplicationDelegate
{
    private AppleMetalSmokeGame? _game;

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        try
        {
            _game = new AppleMetalSmokeGame();
            _game.Completed += OnGameCompleted;
            _game.Run(GameRunBehavior.Asynchronous);
            return true;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Environment.Exit(1);
            return false;
        }
    }

    private void OnGameCompleted(object? sender, EventArgs eventArgs)
    {
        var frameCount = _game?.PresentedFrameCount ?? 0;
        if (frameCount < AppleMetalSmokeGame.RequiredFrameCount)
        {
            Console.Error.WriteLine($"Apple Metal iOS smoke test presented only {frameCount} frames.");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine($"Apple Metal iOS smoke test presented {frameCount} frames.");
        Environment.Exit(0);
    }
}
