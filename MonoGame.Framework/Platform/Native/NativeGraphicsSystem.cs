// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using Microsoft.Xna.Framework.Graphics;
using MonoGame.Interop;

namespace Microsoft.Xna.Framework;

internal static unsafe class NativeGraphicsSystem
{
    private static readonly object _sync = new object();
    private static MGG_GraphicsSystem* _handle;

    internal static MGG_GraphicsSystem* Handle
    {
        get
        {
            lock (_sync)
            {
                if (_handle == null)
                {
                    _handle = MGG.GraphicsSystem_Create();
                    if (_handle == null)
                    {
                        throw new NoSuitableGraphicsDeviceException(
                            "Failed to initialize the native graphics system.");
                    }
                }

                return _handle;
            }
        }
    }

    internal static void Shutdown()
    {
        lock (_sync)
        {
            if (_handle == null)
                return;

            MGG.GraphicsSystem_Destroy(_handle);
            _handle = null;
            GraphicsAdapter._adapters = null;
        }
    }
}
