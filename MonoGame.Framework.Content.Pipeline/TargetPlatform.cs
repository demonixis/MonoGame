// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.ComponentModel;
using System.Globalization;

namespace Microsoft.Xna.Framework.Content.Pipeline
{
    /// <summary>
    /// Identifiers for the target platform.
    /// </summary>
    [TypeConverter(typeof(TargetPlatformTypeConverter))]
    public enum TargetPlatform
    {
        /// <summary>
        /// All desktop versions of Windows using DirectX.
        /// </summary>
        Windows,

        /// <summary>
        /// Xbox 360 video game and entertainment system
        /// </summary>
        Xbox360,

        // MonoGame-specific platforms listed below

        /// <summary>
        /// Apple iOS-based devices (iPod Touch, iPhone, iPad)
        /// (MonoGame)
        /// </summary>
        iOS,

        /// <summary>
        /// Android-based devices
        /// (MonoGame)
        /// </summary>
        Android,

        /// <summary>
        /// All desktop versions using OpenGL.
        /// (MonoGame)
        /// </summary>
        DesktopGL,

        /// <summary>
        /// Apple Mac OSX-based devices (iMac, MacBook, MacBook Air, etc)
        /// (MonoGame)
        /// </summary>
        MacOSX,

        /// <summary>
        /// Google Chrome Native Client
        /// (MonoGame)
        /// </summary>
        NativeClient,

        /// <summary>
        /// Raspberry Pi
        /// (MonoGame)
        /// </summary>
        RaspberryPi,

        /// <summary>
        /// Sony PlayStation4
        /// </summary>
        PlayStation4,

        /// <summary>
        /// Sony PlayStation5
        /// </summary>
        PlayStation5,

        /// <summary>
        /// Xbox One
        /// </summary>
        XboxOne,

        /// <summary>
        /// Nintendo Switch
        /// </summary>
        Switch,

        /// <summary>
        /// WebAssembly and Bridge.NET
        /// </summary>
        Web,

        /// <summary>
        /// All desktop versions using Vulkan.
        /// </summary>
        DesktopVK,

        /// <summary>
        /// Windows using DirectX 12
        /// </summary>
        WindowsDX12,

        /// <summary>
        /// Xbox Series S|X
        /// </summary>
        XboxSeries,
        /// <summary>
        /// Nintendo Switch 2
        /// </summary>
        Switch2 = 16,

        /// <summary>
        /// Apple macOS devices using Metal.
        /// </summary>
        MacOSMetal = 17,

        /// <summary>
        /// Apple iOS and iPadOS devices using Metal.
        /// </summary>
        iOSMetal = 18,

        /// <summary>
        /// Android-based devices using Vulkan.
        /// </summary>
        AndroidVK = 19,

        /// <summary>
        /// Android devices using the additive Native OpenGL ES 3.0 backend.
        /// </summary>
        AndroidNativeGLES = 20,

        /// <summary>
        /// Apple iOS and iPadOS devices using the additive Native OpenGL ES 3.0 backend.
        /// </summary>
        iOSNativeGLES = 21,

        /// <summary>
        /// Desktop systems using the additive Native OpenGL 4.1 backend.
        /// </summary>
        DesktopNativeGL = 22,
    }

    /// <summary>
    /// Deserialize legacy Platforms from .MGCB files.
    /// </summary>
    internal class TargetPlatformTypeConverter(Type type) : EnumConverter(type)
    {
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {   
            try
            {
                return base.ConvertFrom(context, culture, value);
            }
            catch (FormatException)
            { 
                // convert legacy Platforms
                if (value.Equals("Linux") || value.Equals("WindowsGL"))
                    return TargetPlatform.DesktopGL;
                else
                    throw;
            }
        }
    }
}
