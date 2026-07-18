// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace MonoGame.Tests.AndroidVulkanPackageSmoke;

[Activity(
    Label = "MonoGame Android Vulkan package smoke",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : AndroidGameActivity
{
    private SmokeGame _game;

    protected override void OnCreate(Bundle bundle)
    {
        base.OnCreate(bundle);

        _game = new SmokeGame();
        SetContentView((View)_game.Services.GetService(typeof(View)));
        _game.Run();
    }
}

internal sealed class SmokeGame : Game
{
    private readonly GraphicsDeviceManager _graphics;

    internal SmokeGame()
    {
        _graphics = new GraphicsDeviceManager(this);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        base.Draw(gameTime);
    }
}
