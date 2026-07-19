// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

namespace Microsoft.Xna.Framework.Graphics;

/// <summary>Experimental native graphics API selected for an OpenXR binding.</summary>
public enum OpenXrGraphicsApi
{
    Unknown,
    OpenGL,
    Vulkan,
    Direct3D12,
    Metal
}

/// <summary>Non-owning native handles required to create an OpenXR graphics binding.</summary>
public struct OpenXrGraphicsBinding
{
    public OpenXrGraphicsApi Api { get; set; }
    public IntPtr Instance { get; set; }
    public IntPtr PhysicalDevice { get; set; }
    public IntPtr Device { get; set; }
    public IntPtr Queue { get; set; }
    public uint QueueFamilyIndex { get; set; }
    public uint QueueIndex { get; set; }
    public IntPtr NativeWindow { get; set; }
    public IntPtr Display { get; set; }
    public IntPtr Context { get; set; }
    public IntPtr Drawable { get; set; }
    public IntPtr Visual { get; set; }
    public IntPtr Configuration { get; set; }
    public uint VisualId { get; set; }
}

/// <summary>
/// Experimental OpenXR graphics bootstrap and external-image bridge. External resources remain
/// owned by the OpenXR runtime; the returned render target owns only backend views/FBOs and its
/// MonoGame depth attachment.
/// </summary>
public static partial class OpenXrGraphicsInterop
{
    public static void ConfigureVulkanBootstrap(
        IntPtr userData,
        IntPtr createInstance,
        IntPtr getPhysicalDevice,
        IntPtr createDevice) =>
        PlatformConfigureVulkanBootstrap(userData, createInstance, getPhysicalDevice, createDevice);

    public static void ConfigureDirect3D12Adapter(long adapterLuid, uint minimumFeatureLevel) =>
        PlatformConfigureDirect3D12Adapter(adapterLuid, minimumFeatureLevel);

    public static void ConfigureMetalDevice(IntPtr device) => PlatformConfigureMetalDevice(device);

    public static IntPtr GetVulkanGetInstanceProcAddr() => PlatformGetVulkanGetInstanceProcAddr();

    public static OpenXrGraphicsBinding GetBinding(GraphicsDevice graphicsDevice)
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));
        return PlatformGetBinding(graphicsDevice);
    }

    public static RenderTarget2D WrapExternalRenderTarget(
        GraphicsDevice graphicsDevice,
        IntPtr image,
        int width,
        int height,
        SurfaceFormat format,
        DepthFormat depthFormat = DepthFormat.Depth24)
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));
        if (image == IntPtr.Zero)
            throw new ArgumentException("The external image handle is null.", nameof(image));
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        return PlatformWrapExternalRenderTarget(graphicsDevice, image, width, height, format, depthFormat);
    }

    /// <summary>Commits the current GPU work without presenting the desktop swapchain.</summary>
    public static void SubmitWithoutPresent(GraphicsDevice graphicsDevice)
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));
        PlatformSubmitWithoutPresent(graphicsDevice);
    }

    public static void PrepareForRuntimeRelease(RenderTarget2D renderTarget)
    {
        if (renderTarget == null)
            throw new ArgumentNullException(nameof(renderTarget));
        PlatformPrepareForRuntimeRelease(renderTarget);
    }

    private static partial void PlatformConfigureVulkanBootstrap(IntPtr userData, IntPtr createInstance, IntPtr getPhysicalDevice, IntPtr createDevice);
    private static partial void PlatformConfigureDirect3D12Adapter(long adapterLuid, uint minimumFeatureLevel);
    private static partial void PlatformConfigureMetalDevice(IntPtr device);
    private static partial IntPtr PlatformGetVulkanGetInstanceProcAddr();
    private static partial OpenXrGraphicsBinding PlatformGetBinding(GraphicsDevice graphicsDevice);
    private static partial RenderTarget2D PlatformWrapExternalRenderTarget(GraphicsDevice graphicsDevice, IntPtr image, int width, int height, SurfaceFormat format, DepthFormat depthFormat);
    private static partial void PlatformSubmitWithoutPresent(GraphicsDevice graphicsDevice);
    private static partial void PlatformPrepareForRuntimeRelease(RenderTarget2D renderTarget);
}
