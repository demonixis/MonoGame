// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#if NATIVE

using System;
using MonoGame.Interop;

namespace Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Specifies how an attachment is initialized when a render pass begins.
/// </summary>
public enum RenderPassLoadAction
{
    /// <summary>Preserve the attachment contents.</summary>
    Load = 0,

    /// <summary>Clear the attachment before drawing.</summary>
    Clear = 1,

    /// <summary>The previous attachment contents are not required.</summary>
    DontCare = 2,
}

/// <summary>
/// Specifies whether an attachment must be preserved when a render pass ends.
/// </summary>
public enum RenderPassStoreAction
{
    /// <summary>Preserve the attachment contents.</summary>
    Store = 0,

    /// <summary>The attachment contents are not required after the pass.</summary>
    DontCare = 1,
}

/// <summary>
/// Describes one color attachment in an explicit native render pass.
/// </summary>
public readonly struct RenderPassColorAttachment
{
    /// <summary>Gets the render-target binding.</summary>
    public RenderTargetBinding Binding { get; }

    /// <summary>Gets the attachment load action.</summary>
    public RenderPassLoadAction LoadAction { get; }

    /// <summary>Gets the attachment store action.</summary>
    public RenderPassStoreAction StoreAction { get; }

    /// <summary>Gets the color used when <see cref="LoadAction"/> is <see cref="RenderPassLoadAction.Clear"/>.</summary>
    public Vector4 ClearColor { get; }

    /// <summary>Creates a color attachment descriptor.</summary>
    public RenderPassColorAttachment(
        RenderTargetBinding binding,
        RenderPassLoadAction loadAction,
        RenderPassStoreAction storeAction,
        Vector4 clearColor)
    {
        if (binding.RenderTarget == null)
            throw new ArgumentException("The render-target binding must contain a render target.", nameof(binding));

        Binding = binding;
        LoadAction = loadAction;
        StoreAction = storeAction;
        ClearColor = clearColor;
    }

    /// <summary>Creates a color attachment descriptor for a two-dimensional render target.</summary>
    public RenderPassColorAttachment(
        RenderTarget2D renderTarget,
        RenderPassLoadAction loadAction,
        RenderPassStoreAction storeAction,
        Vector4 clearColor)
        : this(new RenderTargetBinding(renderTarget), loadAction, storeAction, clearColor)
    {
    }
}

/// <summary>
/// Describes the depth and stencil attachment in an explicit native render pass.
/// </summary>
public readonly struct RenderPassDepthStencilAttachment
{
    /// <summary>Gets the independent depth/stencil target.</summary>
    public DepthStencilTarget2D Target { get; }

    /// <summary>Gets the depth load action.</summary>
    public RenderPassLoadAction DepthLoadAction { get; }

    /// <summary>Gets the depth store action.</summary>
    public RenderPassStoreAction DepthStoreAction { get; }

    /// <summary>Gets the stencil load action.</summary>
    public RenderPassLoadAction StencilLoadAction { get; }

    /// <summary>Gets the stencil store action.</summary>
    public RenderPassStoreAction StencilStoreAction { get; }

    /// <summary>Gets the depth clear value.</summary>
    public float ClearDepth { get; }

    /// <summary>Gets the stencil clear value.</summary>
    public int ClearStencil { get; }

    /// <summary>Creates a depth/stencil attachment descriptor.</summary>
    public RenderPassDepthStencilAttachment(
        DepthStencilTarget2D target,
        RenderPassLoadAction depthLoadAction,
        RenderPassStoreAction depthStoreAction,
        RenderPassLoadAction stencilLoadAction,
        RenderPassStoreAction stencilStoreAction,
        float clearDepth = 1.0f,
        int clearStencil = 0)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        DepthLoadAction = depthLoadAction;
        DepthStoreAction = depthStoreAction;
        StencilLoadAction = stencilLoadAction;
        StencilStoreAction = stencilStoreAction;
        ClearDepth = clearDepth;
        ClearStencil = clearStencil;
    }
}

/// <summary>
/// A sampleable depth/stencil texture that can be shared by several explicit render passes.
/// </summary>
/// <remarks>
/// The texture cannot be sampled while it is attached to the active render pass.
/// Multisampled independent depth targets are not supported by this API.
/// </remarks>
public sealed class DepthStencilTarget2D : Texture2D
{
    /// <summary>Gets the depth/stencil format.</summary>
    public DepthFormat DepthStencilFormat { get; }

    /// <summary>Creates a sampleable independent depth/stencil target.</summary>
    public unsafe DepthStencilTarget2D(
        GraphicsDevice graphicsDevice,
        int width,
        int height,
        DepthFormat depthStencilFormat)
        : base(
            graphicsDevice,
            width,
            height,
            false,
            SurfaceFormat.Single,
            SurfaceType.SwapChainRenderTarget,
            false,
            1)
    {
        if (depthStencilFormat == DepthFormat.None)
            throw new ArgumentOutOfRangeException(nameof(depthStencilFormat), "An independent depth target requires a depth format.");
        if (!graphicsDevice.SupportsDepthStencilTargetFormat(depthStencilFormat))
            throw new NotSupportedException($"The active native graphics backend does not support a compatible sampleable {depthStencilFormat} target.");

        DepthStencilFormat = depthStencilFormat;
        Handle = MGG.DepthStencilTarget_Create(graphicsDevice.Handle, width, height, depthStencilFormat);
        if (Handle == null)
            throw new InvalidOperationException($"The native graphics backend failed to create its advertised sampleable {depthStencilFormat} target.");
    }
}

#endif
