// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Framework.Utilities;
using MonoGame.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace Microsoft.Xna.Framework.Graphics;


public partial class GraphicsDevice
{
    internal unsafe MGG_GraphicsDevice* Handle;

    internal Texture2D DefaultTexture;

    private int _currentFrame = -1;

    private readonly Dictionary<int, DynamicVertexBuffer> _userVertexBuffers = new Dictionary<int, DynamicVertexBuffer>();
    private DynamicIndexBuffer _userIndexBuffer16;
    private DynamicIndexBuffer _userIndexBuffer32;

    private unsafe readonly MGG_Texture*[] _curRenderTargets = new MGG_Texture*[4];
    private readonly int[] _currentRenderTargetArraySlices = new int[4];
    private readonly MGG_RenderPassColorAttachment[] _nativeRenderPassColorAttachments = new MGG_RenderPassColorAttachment[4];
    private bool _explicitRenderPassActive;
    private DepthFormat _explicitDepthFormat = DepthFormat.None;

    internal static int ShaderProfile
    {
        get; private set;
    }

    internal TextureCompressionCapabilities TextureCompressionCapabilities
    {
        get; private set;
    }

    internal uint NativeCapabilitiesAbiVersion { get; private set; }

    internal NativeGraphicsFeatures NativeFeatures { get; private set; }

    internal float NativeMaxAnisotropy { get; private set; }

    internal int NativeApiMajor { get; private set; }

    internal int NativeApiMinor { get; private set; }

    internal int NativeMaxRenderTargets { get; private set; } = 1;

    internal int NativeMaxDrawBuffers { get; private set; } = 1;

    internal int NativeMaxColorAttachments { get; private set; } = 1;

    /// <summary>
    /// Gets whether the native graphics backend supports explicit render passes with a
    /// separate sampleable depth/stencil attachment.
    /// </summary>
    public bool SupportsExplicitRenderPass =>
        NativeCapabilitiesAbiVersion >= 3 &&
        (NativeFeatures & NativeGraphicsFeatures.ExplicitRenderPass) != 0;

    /// <summary>
    /// Gets whether a compatible sampleable independent depth/stencil format is supported.
    /// </summary>
    /// <remarks>
    /// This method never reduces the requested depth precision or adds or removes stencil.
    /// A backend may use greater depth precision for a packed depth/stencil request.
    /// </remarks>
    public unsafe bool SupportsDepthStencilTargetFormat(DepthFormat depthFormat)
    {
        if (!SupportsExplicitRenderPass || depthFormat == DepthFormat.None)
            return false;

        return MGG.GraphicsDevice_SupportsDepthStencilTargetFormatV3(Handle, depthFormat) != 0;
    }

    private void PlatformValidateRenderTargets(RenderTargetBinding[] renderTargets)
    {
        if (renderTargets == null)
            return;

        var maximum = Math.Min(4, NativeMaxRenderTargets);
        if (renderTargets.Length > maximum)
            throw new ArgumentException($"The native graphics backend supports at most {maximum} simultaneous render targets.", nameof(renderTargets));

        if (ShaderProfile != 82 && ShaderProfile != 84)
            return;

        // The MRT v1 restrictions apply only when several color attachments
        // are bound.  A single cube face is an existing MonoGame render-target
        // contract used by reflection probes and sky captures.
        if (renderTargets.Length == 1)
        {
            var renderTarget = renderTargets[0].RenderTarget;
            if (renderTarget == null)
                throw new ArgumentException("The native OpenGL render-target binding must contain a render target.", nameof(renderTargets));
            if (renderTarget is RenderTarget2D or RenderTargetCube)
                return;
            throw new NotSupportedException("Native OpenGL supports a single RenderTarget2D or RenderTargetCube binding.");
        }

        RenderTarget2D first = null;
        for (var index = 0; index < renderTargets.Length; ++index)
        {
            var texture = renderTargets[index].RenderTarget;
            if (texture == null)
                throw new ArgumentException("Every native OpenGL render-target binding must contain a render target.", nameof(renderTargets));
            if (texture is not RenderTarget2D target)
                throw new NotSupportedException("Native OpenGL MRT v1 supports only RenderTarget2D bindings.");
            if (target.Format != SurfaceFormat.Color)
                throw new NotSupportedException("Native OpenGL MRT v1 supports only SurfaceFormat.Color render targets.");
            if (first != null && (target.Width != first.Width || target.Height != first.Height))
                throw new ArgumentException("Native OpenGL MRT v1 requires render targets with identical dimensions.", nameof(renderTargets));

            if (target.MultiSampleCount > 1)
                throw new NotSupportedException("Native OpenGL MRT v1 does not support multisampled render targets.");
            if (index > 0 && renderTargets[index].DepthFormat != DepthFormat.None)
                throw new NotSupportedException("Native OpenGL MRT v1 permits a depth attachment only on render target 0.");

            first ??= target;
        }
    }

    private bool _supportsNativeGraphicsAbiV2;

    private int _maxMultiSampleCount;

    internal static MGG_GraphicsDevice_CapsV2 CreateLegacyCapabilitiesFallback(MGG_GraphicsDevice_Caps caps)
    {
        return new MGG_GraphicsDevice_CapsV2
        {
            StructSize = (uint)Marshal.SizeOf<MGG_GraphicsDevice_CapsV2>(),
            AbiVersion = 0,
            MaxTextureSlots = caps.MaxTextureSlots,
            MaxVertexTextureSlots = caps.MaxVertexTextureSlots,
            MaxVertexBufferSlots = caps.MaxVertexBufferSlots,
            ShaderProfile = caps.ShaderProfile,
            MaxMultiSampleCount = caps.MaxMultiSampleCount,
            TextureCompression = caps.TextureCompression,
            Features = NativeGraphicsFeatures.None,
            MaxAnisotropy = 1.0f,
            MaxRenderTargets = 1,
            MaxDrawBuffers = 1,
            MaxColorAttachments = 1,
        };
    }

    private void ApplyNativeCapabilities(MGG_GraphicsDevice_CapsV2 caps)
    {
        MaxTextureSlots = caps.MaxTextureSlots;
        MaxVertexTextureSlots = caps.MaxVertexTextureSlots;
        _maxVertexBufferSlots = caps.MaxVertexBufferSlots;
        ShaderProfile = caps.ShaderProfile;
        _maxMultiSampleCount = caps.MaxMultiSampleCount;
        TextureCompressionCapabilities = caps.TextureCompression;
        NativeCapabilitiesAbiVersion = caps.AbiVersion;
        NativeApiMajor = caps.ApiMajor;
        NativeApiMinor = caps.ApiMinor;
        NativeFeatures = caps.Features;
        NativeMaxAnisotropy = caps.MaxAnisotropy;
        NativeMaxRenderTargets = caps.MaxRenderTargets;
        NativeMaxDrawBuffers = caps.MaxDrawBuffers;
        NativeMaxColorAttachments = caps.MaxColorAttachments;
    }

    private unsafe void PlatformSetup()
    {
        // Creates the device, but no swap chain yet.
#if ANDROID && (VULKAN || NATIVE_GLES)
        var surface = MGG_PresentationSurface.FromAndroidNativeWindow(PresentationParameters.DeviceWindowHandle);
        Handle = MGG.GraphicsDevice_CreateWithSurface(NativeGraphicsSystem.Handle, Adapter.Handle, ref surface);
#elif IOS && NATIVE_GLES
        var surface = MGG_PresentationSurface.FromOpenGlesLayer(PresentationParameters.DeviceWindowHandle);
        Handle = MGG.GraphicsDevice_CreateWithSurface(NativeGraphicsSystem.Handle, Adapter.Handle, ref surface);
#else
        if (PlatformInfo.GraphicsBackend == GraphicsBackend.OpenGL)
        {
            var surface = MGG_PresentationSurface.FromWindowHandle(PresentationParameters.DeviceWindowHandle);
            Handle = MGG.GraphicsDevice_CreateWithSurface(NativeGraphicsSystem.Handle, Adapter.Handle, ref surface);
        }
        else
            Handle = MGG.GraphicsDevice_Create(NativeGraphicsSystem.Handle, Adapter.Handle);
#endif
        if (Handle == null)
        {
            throw new NoSuitableGraphicsDeviceException(
                "The selected native graphics device does not satisfy the requested API and presentation requirements.");
        }

        // Get the device caps.
        try
        {
            var caps = new MGG_GraphicsDevice_CapsV2
            {
                StructSize = (uint)sizeof(MGG_GraphicsDevice_CapsV2),
            };
            var status = MGG.GraphicsDevice_GetCapsV2(Handle, ref caps, caps.StructSize);
            if (status != MonoGame.Interop.GraphicsDeviceStatus.Success)
                throw new InvalidOperationException($"The native graphics capability query failed with status {status}.");

            ApplyNativeCapabilities(caps);
            _supportsNativeGraphicsAbiV2 = true;
        }
        catch (EntryPointNotFoundException)
        {
            MGG.GraphicsDevice_GetCaps(Handle, out var caps);
            ApplyNativeCapabilities(CreateLegacyCapabilitiesFallback(caps));
            _supportsNativeGraphicsAbiV2 = false;
        }
        UseHalfPixelOffset = false;
    }

    private unsafe void PlatformInitialize()
    {
        PresentationParameters.MultiSampleCount =
                GetClampedMultisampleCount(PresentationParameters.BackBufferFormat, PresentationParameters.MultiSampleCount);

#if ANDROID && (VULKAN || NATIVE_GLES)
        var surface = MGG_PresentationSurface.FromAndroidNativeWindow(PresentationParameters.DeviceWindowHandle);
#elif IOS && METAL
        var surface = MGG_PresentationSurface.FromMetalLayer(PresentationParameters.DeviceWindowHandle);
#elif IOS && NATIVE_GLES
        var surface = MGG_PresentationSurface.FromOpenGlesLayer(PresentationParameters.DeviceWindowHandle);
#else
        var surface = MGG_PresentationSurface.FromWindowHandle(PresentationParameters.DeviceWindowHandle);
#endif
        MGG.GraphicsDevice_ResizeSwapchain(
                Handle,
                ref surface,
                PresentationParameters.BackBufferWidth,
                PresentationParameters.BackBufferHeight,
                PresentationParameters.BackBufferFormat,
                PresentationParameters.DepthStencilFormat,
                PresentationParameters.MultiSampleCount,
                PresentationParameters.PresentationInterval.GetSyncInterval());

        // Setup the default texture.
        DefaultTexture = new Texture2D(this, 2, 2);
        DefaultTexture.SetData(new[] { Color.Black, Color.Black, Color.Black, Color.Black });
    }

    internal int PlatformGetMaxMultiSampleCount(SurfaceFormat format)
    {
        // Older native runtimes predate this capability field. Preserve their
        // established behavior while Metal reports the selected device value.
        return _maxMultiSampleCount > 0 ? _maxMultiSampleCount : 4;
    }

    private unsafe void OnPresentationChanged()
    {
        // Clamp MultiSampleCount
        PresentationParameters.MultiSampleCount =
                GetClampedMultisampleCount(PresentationParameters.BackBufferFormat, PresentationParameters.MultiSampleCount);

        // Finish any frame that is currently rendering.
        if (_currentFrame > -1)
        {
            var syncInterval = PresentationParameters.PresentationInterval.GetSyncInterval();
            MGG.GraphicsDevice_Present(Handle, _currentFrame, syncInterval);
        }

        // Now resize the back buffer.
#if ANDROID && (VULKAN || NATIVE_GLES)
        var surface = MGG_PresentationSurface.FromAndroidNativeWindow(PresentationParameters.DeviceWindowHandle);
#elif IOS && METAL
        var surface = MGG_PresentationSurface.FromMetalLayer(PresentationParameters.DeviceWindowHandle);
#elif IOS && NATIVE_GLES
        var surface = MGG_PresentationSurface.FromOpenGlesLayer(PresentationParameters.DeviceWindowHandle);
#else
        var surface = MGG_PresentationSurface.FromWindowHandle(PresentationParameters.DeviceWindowHandle);
#endif
        MGG.GraphicsDevice_ResizeSwapchain(
            Handle,
            ref surface,
            PresentationParameters.BackBufferWidth,
            PresentationParameters.BackBufferHeight,
            PresentationParameters.BackBufferFormat,
            PresentationParameters.DepthStencilFormat,
            PresentationParameters.MultiSampleCount,
            PresentationParameters.PresentationInterval.GetSyncInterval());

        _viewport = new Viewport(
            0,
            0,
            PresentationParameters.BackBufferWidth,
            PresentationParameters.BackBufferHeight,
            _viewport.MinDepth,
            _viewport.MaxDepth);

        _scissorRectangle = new Rectangle(
            0,
            0,
            PresentationParameters.BackBufferWidth,
            PresentationParameters.BackBufferHeight);

        // Begin a new frame it if was previously rendering.
        if (_currentFrame > -1)
        {
            _currentFrame = -1;
            BeginFrame();
        }
    }

    private unsafe void BeginFrame()
    {
        if (_currentFrame > -1)
            return;

        // Start the command buffer now.
        _currentFrame = MGG.GraphicsDevice_BeginFrame(Handle);

        // Metal can temporarily have no drawable while the window is hidden,
        // minimized, or moving between screens.  Keep the frame idle in that
        // case; applying the default target would recurse into BeginFrame.
        if (_currentFrame < 0)
            return;

        // We must reapply all the state on a new command buffer.
        _scissorRectangleDirty = true;
        _blendFactorDirty = true;
        _blendStateDirty = true;
        _pixelShaderDirty = true;
        _vertexShaderDirty = true;
        _depthStencilStateDirty = true;
        _indexBufferDirty = true;
        _rasterizerStateDirty = true;
        _vertexBuffersDirty = true;
        Textures.Dirty();
        SamplerStates.Dirty();

        MGG.GraphicsDevice_SetViewport(
            Handle,
            _viewport.X,
            _viewport.Y,
            _viewport.Width,
            _viewport.Height,
            _viewport.MinDepth,
            _viewport.MaxDepth);

        PlatformApplyDefaultRenderTarget();
    }

    internal unsafe void SuspendPresentation()
    {
        if (Handle == null)
            return;

        _currentFrame = -1;
        MGG.GraphicsDevice_SuspendPresentation(Handle);
    }

    internal unsafe void ResumePresentation(IntPtr nativeWindow)
    {
        if (Handle == null || nativeWindow == IntPtr.Zero)
            return;

        PresentationParameters.DeviceWindowHandle = nativeWindow;
        OnPresentationChanged();
    }

    private unsafe void PlatformClear(ClearOptions options, Vector4 color, float depth, int stencil)
    {
        BeginFrame();

        PlatformBeginApplyState();
        MGG.GraphicsDevice_Clear(Handle, options, ref color, depth, stencil);
    }

    private unsafe void PlatformDispose()
    {
        if (Handle != null)
        {
            MGG.GraphicsDevice_Destroy(Handle);
            Handle = null;
        }
    }

    private unsafe void PlatformPresent()
    {
        if (_currentFrame < 0)
            return;

        var syncInterval = PresentationParameters.PresentationInterval.GetSyncInterval();
        MGG.GraphicsDevice_Present(Handle, _currentFrame, syncInterval);
        _currentFrame = -1;
    }

    internal unsafe void OpenXrSubmitWithoutPresent()
    {
        if (_currentFrame < 0)
            return;
        MGG.GraphicsDevice_SubmitWithoutPresent(Handle);
        _scissorRectangleDirty = true;
        _blendFactorDirty = true;
        _blendStateDirty = true;
        _pixelShaderDirty = true;
        _vertexShaderDirty = true;
        _depthStencilStateDirty = true;
        _indexBufferDirty = true;
        _rasterizerStateDirty = true;
        _vertexBuffersDirty = true;
        Textures.Dirty();
        SamplerStates.Dirty();
    }

    private unsafe void PlatformSetViewport(ref Viewport viewport)
    {
        BeginFrame();

        MGG.GraphicsDevice_SetViewport(
            Handle,
            viewport.X,
            viewport.Y,
            viewport.Width,
            viewport.Height,
            viewport.MinDepth,
            viewport.MaxDepth);
    }

    private unsafe void PlatformApplyDefaultRenderTarget()
    {
        BeginFrame();

        _explicitRenderPassActive = false;
        _explicitDepthFormat = DepthFormat.None;

        _viewport = new Viewport(
            0,
            0,
            PresentationParameters.BackBufferWidth,
            PresentationParameters.BackBufferHeight,
            _viewport.MinDepth,
            _viewport.MaxDepth);

        _scissorRectangle = new Rectangle(
            0,
            0,
            PresentationParameters.BackBufferWidth,
            PresentationParameters.BackBufferHeight);

        SetNativeRenderTargets(null, null, 0);
    }

    private unsafe void PlatformResolveRenderTargets()
    {
        MGG.GraphicsDevice_ResolveRenderTargets(Handle);
    }

    private unsafe IRenderTarget PlatformApplyRenderTargets()
    {
        BeginFrame();

        _explicitRenderPassActive = false;
        _explicitDepthFormat = DepthFormat.None;

        Array.Clear(_curRenderTargets, 0, 4);

        IRenderTarget first = null;

        for (var i = 0; i < _currentRenderTargetCount; i++)
        {
            var binding = _currentRenderTargetBindings[i];
            var target = binding.RenderTarget;
            _curRenderTargets[i] = target.Handle;
            _currentRenderTargetArraySlices[i] = binding.ArraySlice;
            if (i == 0)
                first = target as IRenderTarget;
        }

        fixed (MGG_Texture** targets = _curRenderTargets)
        fixed (int* arraySlices = _currentRenderTargetArraySlices)
            SetNativeRenderTargets(targets, arraySlices, _currentRenderTargetCount);
        
        return first;
    }

    /// <summary>
    /// Begins an explicit native render pass with color attachments and no depth/stencil attachment.
    /// </summary>
    /// <remarks>
    /// Reuse the attachment array across frames to keep this call allocation-free.
    /// </remarks>
    public void SetRenderPass(RenderPassColorAttachment[] colorAttachments)
    {
        var unusedDepth = default(RenderPassDepthStencilAttachment);
        SetRenderPassCore(colorAttachments, false, in unusedDepth);
    }

    /// <summary>
    /// Begins an explicit native render pass with independent color and depth/stencil attachments.
    /// </summary>
    /// <remarks>
    /// Reuse the attachment array across frames to keep this call allocation-free. The depth/stencil
    /// target must not also be bound as a shader resource while this pass is active.
    /// </remarks>
    public void SetRenderPass(
        RenderPassColorAttachment[] colorAttachments,
        in RenderPassDepthStencilAttachment depthStencilAttachment)
    {
        SetRenderPassCore(colorAttachments, true, in depthStencilAttachment);
    }

    private unsafe void SetRenderPassCore(
        RenderPassColorAttachment[] colorAttachments,
        bool hasDepthStencilAttachment,
        in RenderPassDepthStencilAttachment depthStencilAttachment)
    {
        if (!SupportsExplicitRenderPass)
            throw new NotSupportedException("The active native graphics backend does not support explicit render passes.");
        if (colorAttachments == null)
            throw new ArgumentNullException(nameof(colorAttachments));
        if (colorAttachments.Length == 0)
            throw new ArgumentException("An explicit render pass requires at least one color attachment.", nameof(colorAttachments));

        var maximum = Math.Min(_nativeRenderPassColorAttachments.Length, NativeMaxRenderTargets);
        if (colorAttachments.Length > maximum)
            throw new ArgumentException($"The native graphics backend supports at most {maximum} color attachments.", nameof(colorAttachments));

        var width = 0;
        var height = 0;
        for (var index = 0; index < colorAttachments.Length; ++index)
        {
            ref readonly var attachment = ref colorAttachments[index];
            ValidateRenderPassActions(attachment.LoadAction, attachment.StoreAction, nameof(colorAttachments));

            var binding = attachment.Binding;
            if (binding.RenderTarget is not RenderTarget2D target)
                throw new NotSupportedException("Explicit native render passes currently support RenderTarget2D color attachments only.");
            if (target.GraphicsDevice != this)
                throw new ArgumentException("Every render-pass attachment must belong to this graphics device.", nameof(colorAttachments));
            if (target.MultiSampleCount > 1)
                throw new NotSupportedException("Explicit native render passes do not support multisampled color attachments.");
            if (index == 0)
            {
                width = target.Width;
                height = target.Height;
            }
            else if (target.Width != width || target.Height != height)
            {
                throw new ArgumentException("Every render-pass attachment must have identical dimensions.", nameof(colorAttachments));
            }

            _nativeRenderPassColorAttachments[index] = new MGG_RenderPassColorAttachment
            {
                Target = (nint)target.Handle,
                ArraySlice = binding.ArraySlice,
                LoadAction = attachment.LoadAction,
                StoreAction = attachment.StoreAction,
                ClearColor = attachment.ClearColor,
            };
        }

        MGG_RenderPassDepthStencilAttachment nativeDepthStencilAttachment = default;
        if (hasDepthStencilAttachment)
        {
            var target = depthStencilAttachment.Target;
            if (target == null)
                throw new ArgumentException("The depth/stencil attachment must contain a target.", nameof(depthStencilAttachment));
            if (target.GraphicsDevice != this)
                throw new ArgumentException("The depth/stencil attachment must belong to this graphics device.", nameof(depthStencilAttachment));
            if (target.Width != width || target.Height != height)
                throw new ArgumentException("The depth/stencil attachment must match the color attachment dimensions.", nameof(depthStencilAttachment));
            if (depthStencilAttachment.ClearDepth < 0.0f || depthStencilAttachment.ClearDepth > 1.0f)
                throw new ArgumentOutOfRangeException(nameof(depthStencilAttachment), "The depth clear value must be between zero and one.");
            if (depthStencilAttachment.ClearStencil < 0 || depthStencilAttachment.ClearStencil > byte.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(depthStencilAttachment), "The stencil clear value must be between zero and 255.");

            ValidateRenderPassActions(
                depthStencilAttachment.DepthLoadAction,
                depthStencilAttachment.DepthStoreAction,
                nameof(depthStencilAttachment));
            ValidateRenderPassActions(
                depthStencilAttachment.StencilLoadAction,
                depthStencilAttachment.StencilStoreAction,
                nameof(depthStencilAttachment));

            nativeDepthStencilAttachment = new MGG_RenderPassDepthStencilAttachment
            {
                Target = (nint)target.Handle,
                DepthLoadAction = depthStencilAttachment.DepthLoadAction,
                DepthStoreAction = depthStencilAttachment.DepthStoreAction,
                StencilLoadAction = depthStencilAttachment.StencilLoadAction,
                StencilStoreAction = depthStencilAttachment.StencilStoreAction,
                ClearDepth = depthStencilAttachment.ClearDepth,
                ClearStencil = depthStencilAttachment.ClearStencil,
            };
        }

        PlatformResolveRenderTargets();
        BeginFrame();

        fixed (MGG_RenderPassColorAttachment* nativeColors = _nativeRenderPassColorAttachments)
        {
            var nativeDepth = hasDepthStencilAttachment ? &nativeDepthStencilAttachment : null;
            var status = MGG.GraphicsDevice_SetRenderPassV3(
                Handle,
                nativeColors,
                colorAttachments.Length,
                nativeDepth);
            ThrowForNativeRenderTargetStatus(status);
        }

        Array.Clear(_currentRenderTargetBindings, 0, _currentRenderTargetBindings.Length);
        for (var index = 0; index < colorAttachments.Length; ++index)
            _currentRenderTargetBindings[index] = colorAttachments[index].Binding;
        _currentRenderTargetCount = colorAttachments.Length;
        _explicitRenderPassActive = true;
        _explicitDepthFormat = hasDepthStencilAttachment
            ? depthStencilAttachment.Target.DepthStencilFormat
            : DepthFormat.None;

        Viewport = new Viewport(0, 0, width, height);
        ScissorRectangle = new Rectangle(0, 0, width, height);
        unchecked
        {
            _graphicsMetrics._targetCount += colorAttachments.Length;
        }
    }

    private static void ValidateRenderPassActions(
        RenderPassLoadAction loadAction,
        RenderPassStoreAction storeAction,
        string parameterName)
    {
        if (loadAction < RenderPassLoadAction.Load || loadAction > RenderPassLoadAction.DontCare)
            throw new ArgumentOutOfRangeException(parameterName, $"Unknown render-pass load action {loadAction}.");
        if (storeAction < RenderPassStoreAction.Store || storeAction > RenderPassStoreAction.DontCare)
            throw new ArgumentOutOfRangeException(parameterName, $"Unknown render-pass store action {storeAction}.");
    }

    private unsafe void SetNativeRenderTargets(MGG_Texture** targets, int* arraySlices, int count)
    {
        if (!_supportsNativeGraphicsAbiV2)
        {
            MGG.GraphicsDevice_SetRenderTargets(Handle, targets, arraySlices, count);
            return;
        }

        var status = MGG.GraphicsDevice_SetRenderTargetsV2(Handle, targets, arraySlices, count);
        ThrowForNativeRenderTargetStatus(status);
    }

    internal static void ThrowForNativeRenderTargetStatus(MonoGame.Interop.GraphicsDeviceStatus status)
    {
        switch (status)
        {
            case MonoGame.Interop.GraphicsDeviceStatus.Success:
                return;
            case MonoGame.Interop.GraphicsDeviceStatus.InvalidArgument:
            case MonoGame.Interop.GraphicsDeviceStatus.InvalidRenderTargetCount:
            case MonoGame.Interop.GraphicsDeviceStatus.InvalidRenderTarget:
            case MonoGame.Interop.GraphicsDeviceStatus.RenderTargetDimensionsMismatch:
                throw new ArgumentException($"The native graphics backend rejected the render-target set ({status}).");
            case MonoGame.Interop.GraphicsDeviceStatus.RenderTargetFormatNotSupported:
            case MonoGame.Interop.GraphicsDeviceStatus.RenderTargetMultisamplingNotSupported:
            case MonoGame.Interop.GraphicsDeviceStatus.RenderTargetDepthAttachmentNotSupported:
            case MonoGame.Interop.GraphicsDeviceStatus.Unsupported:
                throw new NotSupportedException($"The native graphics backend does not support the render-target set ({status}).");
            case MonoGame.Interop.GraphicsDeviceStatus.DeviceUnavailable:
            case MonoGame.Interop.GraphicsDeviceStatus.FramebufferIncomplete:
                throw new InvalidOperationException($"The native graphics backend could not bind the render-target set ({status}).");
            default:
                throw new InvalidOperationException($"The native graphics backend failed to bind render targets ({status}).");
        }
    }

    private void PlatformBeginApplyState()
    {
        BeginFrame();
    }

    private void PlatformApplyBlend()
    {
        if (_blendStateDirty)
        {
            _actualBlendState.PlatformApplyState(this);
            _blendStateDirty = false;
        }

        if (_blendFactorDirty)
        {
            // TODO?
            _blendFactorDirty = false;
        }
    }

    private unsafe void PlatformApplyState(bool applyShaders)
    {
        if (_scissorRectangleDirty)
        {
            MGG.GraphicsDevice_SetScissorRectangle(
                Handle,
                _scissorRectangle.X,
                _scissorRectangle.Y,
                _scissorRectangle.Width,
                _scissorRectangle.Height);

            _scissorRectangleDirty = false;
        }

        // If we're not applying shaders then early out now.
        if (!applyShaders)
            return;

        if (_vertexShader == null)
            throw new InvalidOperationException("A vertex shader must be set!");
        if (_pixelShader == null)
            throw new InvalidOperationException("A pixel shader must be set!");

        bool layoutChanged = false;

        // Change the vertex shader first as it can cause changes
        // to other states that the shader depends on.
        if (_vertexShaderDirty)
        {
            MGG.GraphicsDevice_SetShader(Handle, ShaderStage.Vertex, _vertexShader.Handle);
            _vertexBuffersDirty = true;
            unchecked { _graphicsMetrics._vertexShaderCount++; }
        }

        if (_pixelShaderDirty)
        {
            MGG.GraphicsDevice_SetShader(Handle, ShaderStage.Pixel, _pixelShader.Handle);
            unchecked { _graphicsMetrics._pixelShaderCount++; }
        }

        if (_indexBufferDirty)
        {
            if (_indexBuffer != null)
                MGG.GraphicsDevice_SetIndexBuffer(Handle, _indexBuffer.IndexElementSize, _indexBuffer.Handle);
        }

        if (layoutChanged || _vertexBuffersDirty)
        {
            var layout = _vertexShader.GetOrCreateLayout(_vertexBuffers);
            MGG.GraphicsDevice_SetInputLayout(Handle, layout);
        }

        if (_vertexBuffersDirty)
        {
            for (var slot = 0; slot < _vertexBuffers.Count; slot++)
            {
                var vertexBufferBinding = _vertexBuffers.Get(slot);
                var buffer = vertexBufferBinding.VertexBuffer;

                MGG.GraphicsDevice_SetVertexBuffer(Handle, slot, buffer.Handle, vertexBufferBinding.VertexOffset);
            }
        }
        _vertexConstantBuffers.SetConstantBuffers(this);
        _pixelConstantBuffers.SetConstantBuffers(this);

        VertexTextures.SetTextures(this);
        VertexSamplerStates.PlatformSetSamplers(this);

        Textures.SetTextures(this);
        SamplerStates.PlatformSetSamplers(this);

        _indexBufferDirty = false;
        _vertexBuffersDirty = false;
        _vertexShaderDirty = false;
        _pixelShaderDirty = false;
    }

    private int SetUserVertexBuffer<T>(T[] vertexData, int vertexOffset, int vertexCount, VertexDeclaration vertexDecl)
        where T : struct
    {
        DynamicVertexBuffer buffer;

        if (!_userVertexBuffers.TryGetValue(vertexDecl.GetHashCode(), out buffer) || buffer.VertexCount < vertexCount)
        {
            // Dispose the previous buffer if we have one.
            if (buffer != null)
                buffer.Dispose();

            buffer = new DynamicVertexBuffer(this, vertexDecl, Math.Max(vertexCount, 2000), BufferUsage.WriteOnly);
            _userVertexBuffers[vertexDecl.GetHashCode()] = buffer;
        }

        var startVertex = buffer.UserOffset;

        if ((vertexCount + buffer.UserOffset) < buffer.VertexCount)
        {
            buffer.UserOffset += vertexCount;
            buffer.SetData(startVertex * vertexDecl.VertexStride, vertexData, vertexOffset, vertexCount, vertexDecl.VertexStride, SetDataOptions.NoOverwrite);
        }
        else
        {
            buffer.UserOffset = vertexCount;
            buffer.SetData(vertexData, vertexOffset, vertexCount, SetDataOptions.Discard);
            startVertex = 0;
        }

        SetVertexBuffer(buffer);

        return startVertex;
    }

    private int SetUserIndexBuffer<T>(T[] indexData, int indexOffset, int indexCount)
        where T : struct
    {
        DynamicIndexBuffer buffer;

        var indexSize = ReflectionHelpers.FastSizeOf<T>();
        var indexElementSize = indexSize == 2 ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits;

        var requiredIndexCount = Math.Max(indexCount, 6000);
        if (indexElementSize == IndexElementSize.SixteenBits)
        {
            if (_userIndexBuffer16 == null || _userIndexBuffer16.IndexCount < requiredIndexCount)
            {
                if (_userIndexBuffer16 != null)
                    _userIndexBuffer16.Dispose();

                _userIndexBuffer16 = new DynamicIndexBuffer(this, indexElementSize, requiredIndexCount, BufferUsage.WriteOnly);
            }

            buffer = _userIndexBuffer16;
        }
        else
        {
            if (_userIndexBuffer32 == null || _userIndexBuffer32.IndexCount < requiredIndexCount)
            {
                if (_userIndexBuffer32 != null)
                    _userIndexBuffer32.Dispose();

                _userIndexBuffer32 = new DynamicIndexBuffer(this, indexElementSize, requiredIndexCount, BufferUsage.WriteOnly);
            }

            buffer = _userIndexBuffer32;
        }

        var startIndex = buffer.UserOffset;

        if ((indexCount + buffer.UserOffset) < buffer.IndexCount)
        {
            buffer.UserOffset += indexCount;
            buffer.SetData(startIndex * indexSize, indexData, indexOffset, indexCount, SetDataOptions.NoOverwrite);
        }
        else
        {
            startIndex = 0;
            buffer.UserOffset = indexCount;
            buffer.SetData(indexData, indexOffset, indexCount, SetDataOptions.Discard);
        }

        Indices = buffer;

        return startIndex;
    }

    private unsafe void PlatformDrawIndexedPrimitives(PrimitiveType primitiveType, int baseVertex, int startIndex, int primitiveCount)
    {
        ApplyState(true);
        if (baseVertex < 0)
            baseVertex = 0;
        if (startIndex < 0)
            startIndex = 0;

        MGG.GraphicsDevice_DrawIndexed(Handle, primitiveType, primitiveCount, startIndex, baseVertex);
    }

    private unsafe  void PlatformDrawUserPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, VertexDeclaration vertexDeclaration, int vertexCount) where T : struct
    {
        var startVertex = SetUserVertexBuffer(vertexData, vertexOffset, vertexCount, vertexDeclaration);
        ApplyState(true);

        MGG.GraphicsDevice_Draw(Handle, primitiveType, startVertex, vertexCount);
    }

    private unsafe void PlatformDrawPrimitives(PrimitiveType primitiveType, int vertexStart, int vertexCount)
    {
        ApplyState(true);
        if (vertexStart < 0)
            vertexStart = 0;

        MGG.GraphicsDevice_Draw(Handle, primitiveType, vertexStart, vertexCount);
    }

    private unsafe void PlatformDrawUserIndexedPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int numVertices, short[] indexData, int indexOffset, int primitiveCount, VertexDeclaration vertexDeclaration) where T : struct
    {
        var indexCount = GetElementCountArray(primitiveType, primitiveCount);
        var startVertex = SetUserVertexBuffer(vertexData, vertexOffset, numVertices, vertexDeclaration);
        var startIndex = SetUserIndexBuffer(indexData, indexOffset, indexCount);
        ApplyState(true);

        MGG.GraphicsDevice_DrawIndexed(Handle, primitiveType, primitiveCount, startIndex, startVertex);
    }

    private unsafe void PlatformDrawUserIndexedPrimitives<T>(PrimitiveType primitiveType, T[] vertexData, int vertexOffset, int numVertices, int[] indexData, int indexOffset, int primitiveCount, VertexDeclaration vertexDeclaration) where T : struct
    {
        var indexCount = GetElementCountArray(primitiveType, primitiveCount);
        var startVertex = SetUserVertexBuffer(vertexData, vertexOffset, numVertices, vertexDeclaration);
        var startIndex = SetUserIndexBuffer(indexData, indexOffset, indexCount);
        ApplyState(true);

        MGG.GraphicsDevice_DrawIndexed(Handle, primitiveType, primitiveCount, startIndex, startVertex);
    }

    private unsafe void PlatformDrawInstancedPrimitives(PrimitiveType primitiveType, int baseVertex, int startIndex, int primitiveCount, int baseInstance, int instanceCount)
    {
        ApplyState(true);

        MGG.GraphicsDevice_DrawIndexedInstanced(Handle, primitiveType, primitiveCount, startIndex, baseVertex, instanceCount);
    }

    private unsafe void PlatformGetBackBufferData<T>(Rectangle? rect, T[] data, int startIndex, int count) where T : struct
    {
        var rectangle = rect ?? new Rectangle(0, 0, PresentationParameters.BackBufferWidth, PresentationParameters.BackBufferHeight);
        var tSize = Marshal.SizeOf<T>();
        GCHandle dataHandle = default;
        try
        {
            dataHandle = GCHandle.Alloc(
                data, GCHandleType.Pinned);
            IntPtr pData = dataHandle.AddrOfPinnedObject();
            MGG.GraphicsDevice_GetBackBufferData(
                Handle,
                rectangle.X,
                rectangle.Y,
                rectangle.Width,
                rectangle.Height,
                pData + startIndex * tSize,
                count,
                tSize);
        }
        finally
        {
            if (dataHandle.IsAllocated)
            {
                dataHandle.Free();
            }
        }
    }

    private static unsafe Rectangle PlatformGetTitleSafeArea(int x, int y, int width, int height)
    {
        MGG.GraphicsDevice_GetTitleSafeArea(ref x, ref y, ref width, ref height);

        return new Rectangle(x, y, width, height);
    }
}
