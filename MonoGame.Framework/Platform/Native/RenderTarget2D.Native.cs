// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Interop;
namespace Microsoft.Xna.Framework.Graphics;

public partial class RenderTarget2D
{
    internal unsafe RenderTarget2D(
        GraphicsDevice graphicsDevice,
        MGG_Texture* handle,
        int width,
        int height,
        SurfaceFormat format,
        DepthFormat depthFormat)
        : base(graphicsDevice, width, height, false, format, SurfaceType.SwapChainRenderTarget, false, 1)
    {
        DepthStencilFormat = depthFormat;
        MultiSampleCount = 0;
        RenderTargetUsage = RenderTargetUsage.DiscardContents;
        Handle = handle;
        Owned = true;
    }

    internal unsafe void OpenXrPrepareForRuntimeRelease() =>
        MGG.OpenXR_PrepareForRuntimeRelease(GraphicsDevice.Handle, Handle);

    private unsafe void PlatformConstruct(GraphicsDevice graphicsDevice, int width, int height, bool mipMap, DepthFormat preferredDepthFormat, int preferredMultiSampleCount, RenderTargetUsage usage, bool shared)
    {
        Handle = MGG.RenderTarget_Create(
            GraphicsDevice.Handle,
            TextureType._2D,
            _format,
            width,
            height,
            1,
            _levelCount,
            ArraySize,
            preferredDepthFormat,
            preferredMultiSampleCount,
            usage);
    }

    private unsafe void PlatformGraphicsDeviceResetting()
    {
        if (Handle != null && Owned)
        {
            MGG.Texture_Destroy(GraphicsDevice.Handle, Handle);
            Handle = null;
        }
    }
}
