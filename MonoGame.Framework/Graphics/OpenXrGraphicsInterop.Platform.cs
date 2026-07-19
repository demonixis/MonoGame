// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;

#if OPENGL
using Microsoft.Xna.Framework;
using MonoGame.OpenGL;
#elif NATIVE || NATIVE_GRAPHICS
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
#elif OPENGL
    private static partial void PlatformConfigureVulkanBootstrap(IntPtr userData, IntPtr createInstance, IntPtr getPhysicalDevice, IntPtr createDevice) =>
        throw new PlatformNotSupportedException("Vulkan bootstrap is unavailable in a DesktopGL build.");

    private static partial void PlatformConfigureDirect3D12Adapter(long adapterLuid, uint minimumFeatureLevel) =>
        throw new PlatformNotSupportedException("D3D12 bootstrap is unavailable in a DesktopGL build.");

    private static partial void PlatformConfigureMetalDevice(IntPtr device) =>
        throw new PlatformNotSupportedException("Metal bootstrap is unavailable in a DesktopGL build.");

    private static partial IntPtr PlatformGetVulkanGetInstanceProcAddr() => IntPtr.Zero;

    private static partial OpenXrGraphicsBinding PlatformGetBinding(GraphicsDevice graphicsDevice)
    {
        var window = SdlGameWindow.Instance.Handle;
        var info = new Sdl.Window.SDL_SysWMinfo { version = Sdl.version };
        if (!Sdl.Window.GetWindowWMInfo(window, ref info))
            throw new InvalidOperationException("SDL_GetWindowWMInfo failed while creating the OpenXR OpenGL binding.");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (info.subsystem != Sdl.Window.SysWMType.Windows)
                throw new PlatformNotSupportedException("OpenXR OpenGL on Windows requires an SDL Win32 window.");
            return new OpenXrGraphicsBinding
            {
                Api = OpenXrGraphicsApi.OpenGL,
                NativeWindow = info.info0,
                Display = info.info1,
                Context = Sdl.GL.GetCurrentContext()
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (info.subsystem == Sdl.Window.SysWMType.Wayland)
                throw new PlatformNotSupportedException("OpenXR OpenGL requires X11/GLX. Select Vulkan or start the player with SDL_VIDEODRIVER=x11.");
            if (info.subsystem != Sdl.Window.SysWMType.X11)
                throw new PlatformNotSupportedException("OpenXR OpenGL on Linux requires an SDL X11 window.");

            var display = GlxGetCurrentDisplay();
            var context = Sdl.GL.GetCurrentContext();
            var drawable = GlxGetCurrentDrawable();
            if (display == IntPtr.Zero || context == IntPtr.Zero || drawable == UIntPtr.Zero)
                throw new InvalidOperationException("The current GLX display, context, or drawable is unavailable.");
            if (GlxQueryContext(display, context, GlxFbConfigId, out var configId) != 0)
                throw new InvalidOperationException("glXQueryContext could not resolve the current GLX framebuffer configuration.");

            var attributes = new[] { GlxFbConfigId, configId, 0 };
            var configs = GlxChooseFbConfig(display, XDefaultScreen(display), attributes, out var count);
            if (configs == IntPtr.Zero || count <= 0)
                throw new InvalidOperationException("No GLX framebuffer configuration matches the current OpenGL context.");
            var configuration = Marshal.ReadIntPtr(configs);
            XFree(configs);
            if (GlxGetFbConfigAttrib(display, configuration, GlxVisualId, out var visualId) != 0)
                throw new InvalidOperationException("glXGetFBConfigAttrib could not resolve the current X11 visual.");

            return new OpenXrGraphicsBinding
            {
                Api = OpenXrGraphicsApi.OpenGL,
                NativeWindow = window,
                Display = display,
                Context = context,
                Drawable = (IntPtr)drawable,
                Configuration = configuration,
                VisualId = unchecked((uint)visualId)
            };
        }

        throw new PlatformNotSupportedException("The OpenXR OpenGL binding is maintained only on Windows/WGL and Linux/X11/GLX.");
    }

    private static partial RenderTarget2D PlatformWrapExternalRenderTarget(
        GraphicsDevice graphicsDevice,
        IntPtr image,
        int width,
        int height,
        SurfaceFormat format,
        DepthFormat depthFormat) =>
        new RenderTarget2D(graphicsDevice, image.ToInt32(), width, height, format, depthFormat);

    private static partial void PlatformSubmitWithoutPresent(GraphicsDevice graphicsDevice) => GL.Flush();
    private static partial void PlatformPrepareForRuntimeRelease(RenderTarget2D renderTarget) => GL.Flush();

    private const int GlxVisualId = 0x800B;
    private const int GlxFbConfigId = 0x8013;

    [DllImport("libGL.so.1", EntryPoint = "glXGetCurrentDisplay")]
    private static extern IntPtr GlxGetCurrentDisplay();
    [DllImport("libGL.so.1", EntryPoint = "glXGetCurrentDrawable")]
    private static extern UIntPtr GlxGetCurrentDrawable();
    [DllImport("libGL.so.1", EntryPoint = "glXQueryContext")]
    private static extern int GlxQueryContext(IntPtr display, IntPtr context, int attribute, out int value);
    [DllImport("libGL.so.1", EntryPoint = "glXChooseFBConfig")]
    private static extern IntPtr GlxChooseFbConfig(IntPtr display, int screen, int[] attributes, out int count);
    [DllImport("libGL.so.1", EntryPoint = "glXGetFBConfigAttrib")]
    private static extern int GlxGetFbConfigAttrib(IntPtr display, IntPtr config, int attribute, out int value);
    [DllImport("libX11.so.6", EntryPoint = "XDefaultScreen")]
    private static extern int XDefaultScreen(IntPtr display);
    [DllImport("libX11.so.6", EntryPoint = "XFree")]
    private static extern int XFree(IntPtr data);
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
