// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal sealed class AppleMetalSmokeGame : Game
{
    internal const int RequiredFrameCount = 3;

    private readonly GraphicsDeviceManager _graphics;
    private RenderTarget2D? _multisampledTarget;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _spriteTexture;
    private bool _completionSignaled;

    internal AppleMetalSmokeGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 320,
            PreferredBackBufferHeight = 180,
            SynchronizeWithVerticalRetrace = false
        };
        IsFixedTimeStep = false;
        Window.Title = "MonoGame Apple Metal Smoke";
    }

    internal int PresentedFrameCount { get; private set; }

    internal event EventHandler? Completed;

    protected override void LoadContent()
    {
        ValidateDisplayModes();
        ValidateTextureUploadAndReadback();
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _spriteTexture = new Texture2D(GraphicsDevice, 1, 1);
        _spriteTexture.SetData(new[] { Color.Red });
        _multisampledTarget = new RenderTarget2D(
            GraphicsDevice,
            32,
            32,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24Stencil8,
            4,
            RenderTargetUsage.PreserveContents);
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (PresentedFrameCount >= RequiredFrameCount)
        {
#if IOS
            if (!_completionSignaled)
            {
                _completionSignaled = true;
                Completed?.Invoke(this, EventArgs.Empty);
            }
#else
            Exit();
#endif
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (PresentedFrameCount == 0)
            ValidateRenderTargetAndBackBufferReadback();

        GraphicsDevice.Clear(Color.CornflowerBlue);
        ++PresentedFrameCount;
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _spriteTexture?.Dispose();
        _spriteBatch?.Dispose();
        _multisampledTarget?.Dispose();
        base.UnloadContent();
    }

    private static void ValidateDisplayModes()
    {
        var adapter = GraphicsAdapter.DefaultAdapter;
        var current = adapter.CurrentDisplayMode;
        if (current.Width <= 1 || current.Height <= 1)
            throw new InvalidOperationException($"Metal reported an invalid display mode: {current}.");

        foreach (var mode in adapter.SupportedDisplayModes)
        {
            if (mode == current)
                return;
        }

        throw new InvalidOperationException("Metal's current display mode is missing from SupportedDisplayModes.");
    }

    private void ValidateTextureUploadAndReadback()
    {
        using var texture = new Texture2D(GraphicsDevice, 2, 2);
        Color[] expected =
        {
            Color.Red,
            Color.Green,
            Color.Blue,
            Color.White
        };
        var actual = new Color[expected.Length];

        texture.SetData(expected);
        texture.GetData(actual);

        if (!expected.AsSpan().SequenceEqual(actual))
            throw new InvalidOperationException("Metal texture upload/readback did not preserve RGBA data.");
    }

    private void ValidateRenderTargetAndBackBufferReadback()
    {
        GraphicsDevice.SetRenderTarget(_multisampledTarget);
        GraphicsDevice.Clear(Color.OrangeRed);
        GraphicsDevice.SetRenderTarget(null);

        var clearPixel = new Color[1];
        _multisampledTarget!.GetData(0, new Rectangle(0, 0, 1, 1), clearPixel, 0, 1);
        AssertColorNear(Color.OrangeRed, clearPixel[0], "MSAA clear/resolve/readback");

        GraphicsDevice.SetRenderTarget(_multisampledTarget);
        GraphicsDevice.Clear(Color.OrangeRed);
        _spriteBatch!.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
        _spriteBatch.Draw(_spriteTexture!, new Rectangle(0, 0, 32, 32), Color.White);
        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);

        var targetPixel = new Color[1];
        _multisampledTarget.GetData(0, new Rectangle(0, 0, 1, 1), targetPixel, 0, 1);
        AssertColorNear(Color.Red, targetPixel[0], "converted SpriteEffect MSAA render/readback");

        GraphicsDevice.Clear(Color.MediumPurple);
        var backBufferPixel = new Color[1];
        GraphicsDevice.GetBackBufferData(new Rectangle(0, 0, 1, 1), backBufferPixel, 0, 1);
        AssertColorNear(Color.MediumPurple, backBufferPixel[0], "backbuffer readback");
    }

    private static void AssertColorNear(Color expected, Color actual, string operation)
    {
        const int tolerance = 1;
        if (Math.Abs(expected.R - actual.R) > tolerance ||
            Math.Abs(expected.G - actual.G) > tolerance ||
            Math.Abs(expected.B - actual.B) > tolerance ||
            Math.Abs(expected.A - actual.A) > tolerance)
        {
            throw new InvalidOperationException(
                $"Metal {operation} returned {actual} instead of {expected}.");
        }
    }
}
