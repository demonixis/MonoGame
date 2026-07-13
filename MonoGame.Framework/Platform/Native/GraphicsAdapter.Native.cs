// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Interop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
#if IOS && METAL
using UIKit;
#endif

namespace Microsoft.Xna.Framework.Graphics;

partial class GraphicsAdapter
{
    internal unsafe MGG_GraphicsAdapter* Handle;

    private static unsafe void PlatformInitializeAdapters(out ReadOnlyCollection<GraphicsAdapter> adapters)
    {
        var found = new List<GraphicsAdapter>();
        
        while (true)
        {
            var handle = MGG.GraphicsAdapter_Get(NativeGraphicsSystem.Handle, found.Count);
            if (handle == null)
                break;

            MGG_GraphicsAdaptor_Info info;
            MGG.GraphicsAdapter_GetInfo(handle, out info);

            var adapter = new GraphicsAdapter();
            adapter.Handle = handle;
            adapter.DeviceName = Marshal.PtrToStringUTF8(info.DeviceName);
            adapter.Description = Marshal.PtrToStringUTF8(info.Description);
            adapter.DeviceId = info.DeviceId;
            adapter.Revision = info.Revision;
            adapter.VendorId = info.VendorId;
            adapter.SubSystemId = info.SubSystemId;
            adapter.MonitorHandle = info.MonitorHandle;
            
            // Assume the first adapter is the default for now.
            adapter.IsDefaultAdapter = found.Count == 0;

            var modes = new List<DisplayMode>();

#if IOS && METAL
            // The shared native renderer deliberately has no UIKit dependency.
            // Query the host-owned screen here and expose its physical pixel
            // dimensions through the existing GraphicsAdapter contract.
            var screen = UIScreen.MainScreen;
            var width = (int)Math.Round(screen.Bounds.Width * screen.Scale);
            var height = (int)Math.Round(screen.Bounds.Height * screen.Scale);
            adapter._currentDisplayMode = new DisplayMode(width, height, SurfaceFormat.Color);
            modes.Add(adapter._currentDisplayMode);
#else
            adapter._currentDisplayMode = new DisplayMode(
                info.CurrentDisplayMode.width,
                info.CurrentDisplayMode.height,
                info.CurrentDisplayMode.format);

            for (int i=0; i < info.DisplayModeCount; i++)
            {
                var mode = new DisplayMode(
                    info.DisplayModes[i].width,
                    info.DisplayModes[i].height,
                    info.DisplayModes[i].format);

                modes.Add(mode);
            }
#endif

            adapter._supportedDisplayModes = new DisplayModeCollection(modes);

            found.Add(adapter);
        }

        adapters = new ReadOnlyCollection<GraphicsAdapter>(found);
    }

    private bool PlatformIsProfileSupported(GraphicsProfile graphicsProfile)
    {
        // This isn't needed in 2024!
        return true;
    }
}
