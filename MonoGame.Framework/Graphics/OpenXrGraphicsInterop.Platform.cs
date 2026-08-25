// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;

#if NATIVE || NATIVE_GRAPHICS
using MonoGame.Interop;
#endif

namespace Microsoft.Xna.Framework.Graphics;

public static partial class OpenXrGraphicsInterop
{
#if NATIVE || NATIVE_GRAPHICS
    private static partial void PlatformConfigureVulkanBootstrap(IntPtr userData, IntPtr createInstance, IntPtr getPhysicalDevice, IntPtr createDevice)
    {
        unsafe { MGG.OpenXR_ConfigureVulkanBootstrap(userData, createInstance, getPhysicalDevice, createDevice); }
    }

    private static partial void PlatformConfigureDirect3D12Adapter(long adapterLuid, uint minimumFeatureLevel)
    {
        unsafe { MGG.OpenXR_ConfigureDirect3D12Adapter(adapterLuid, minimumFeatureLevel); }
    }

    private static partial void PlatformConfigureMetalDevice(IntPtr device)
    {
        unsafe { MGG.OpenXR_ConfigureMetalDevice(device); }
    }

    private static partial IntPtr PlatformGetVulkanGetInstanceProcAddr()
    {
        unsafe { return MGG.OpenXR_GetVulkanGetInstanceProcAddr(); }
    }

    private static partial OpenXrGraphicsBinding PlatformGetBinding(GraphicsDevice graphicsDevice)
    {
        MGG_OpenXrGraphicsBinding binding;
        unsafe { MGG.OpenXR_GetGraphicsBinding(graphicsDevice.Handle, out binding); }
        return new OpenXrGraphicsBinding
        {
            Api = (OpenXrGraphicsApi)binding.Api,
            Instance = binding.Instance,
            PhysicalDevice = binding.PhysicalDevice,
            Device = binding.Device,
            Queue = binding.Queue,
            QueueFamilyIndex = binding.QueueFamilyIndex,
            QueueIndex = binding.QueueIndex,
            NativeWindow = binding.NativeWindow,
            Display = binding.Display,
            Context = binding.Context,
            Drawable = binding.Drawable,
            Visual = binding.Visual,
            Configuration = binding.Configuration,
            VisualId = binding.VisualId
        };
    }

    private static partial RenderTarget2D PlatformWrapExternalRenderTarget(
        GraphicsDevice graphicsDevice,
        IntPtr image,
        int width,
        int height,
        SurfaceFormat format,
        DepthFormat depthFormat)
    {
        unsafe
        {
            var handle = MGG.OpenXR_WrapRenderTarget(graphicsDevice.Handle, image, format, width, height, depthFormat);
            if (handle == null)
                throw new InvalidOperationException("The selected native backend rejected the OpenXR swapchain image.");
            return new RenderTarget2D(graphicsDevice, handle, width, height, format, depthFormat);
        }
    }

    private static partial void PlatformSubmitWithoutPresent(GraphicsDevice graphicsDevice) =>
        graphicsDevice.OpenXrSubmitWithoutPresent();

    private static partial void PlatformPrepareForRuntimeRelease(RenderTarget2D renderTarget) =>
        renderTarget.OpenXrPrepareForRuntimeRelease();
#else
    private static partial void PlatformConfigureVulkanBootstrap(IntPtr userData, IntPtr createInstance, IntPtr getPhysicalDevice, IntPtr createDevice) =>
        throw new PlatformNotSupportedException("OpenXR graphics interop is unavailable on this MonoGame platform.");
    private static partial void PlatformConfigureDirect3D12Adapter(long adapterLuid, uint minimumFeatureLevel) =>
        throw new PlatformNotSupportedException("OpenXR graphics interop is unavailable on this MonoGame platform.");
    private static partial void PlatformConfigureMetalDevice(IntPtr device) =>
        throw new PlatformNotSupportedException("OpenXR graphics interop is unavailable on this MonoGame platform.");
    private static partial IntPtr PlatformGetVulkanGetInstanceProcAddr() => IntPtr.Zero;
    private static partial OpenXrGraphicsBinding PlatformGetBinding(GraphicsDevice graphicsDevice) =>
        throw new PlatformNotSupportedException("OpenXR graphics interop is unavailable on this MonoGame platform.");
    private static partial RenderTarget2D PlatformWrapExternalRenderTarget(GraphicsDevice graphicsDevice, IntPtr image, int width, int height, SurfaceFormat format, DepthFormat depthFormat) =>
        throw new PlatformNotSupportedException("OpenXR graphics interop is unavailable on this MonoGame platform.");
    private static partial void PlatformSubmitWithoutPresent(GraphicsDevice graphicsDevice) =>
        throw new PlatformNotSupportedException("OpenXR graphics interop is unavailable on this MonoGame platform.");
    private static partial void PlatformPrepareForRuntimeRelease(RenderTarget2D renderTarget) =>
        throw new PlatformNotSupportedException("OpenXR graphics interop is unavailable on this MonoGame platform.");
#endif
}
