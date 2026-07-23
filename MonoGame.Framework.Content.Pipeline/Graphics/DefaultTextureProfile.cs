// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Graphics;


namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    internal class DefaultTextureProfile : TextureProfile
    {
        public override bool Supports(TargetPlatform platform)
        {
            return  platform == TargetPlatform.Android ||
                    platform == TargetPlatform.AndroidVK ||
                    platform == TargetPlatform.AndroidNativeGLES ||
                    platform == TargetPlatform.DesktopGL ||
                    platform == TargetPlatform.DesktopVK ||
                    platform == TargetPlatform.DesktopNativeGL ||
                    platform == TargetPlatform.MacOSX ||
                    platform == TargetPlatform.MacOSMetal ||
                    platform == TargetPlatform.NativeClient ||
                    platform == TargetPlatform.RaspberryPi ||
                    platform == TargetPlatform.Windows ||
                    platform == TargetPlatform.WindowsDX12 ||
                    platform == TargetPlatform.iOS ||
                    platform == TargetPlatform.iOSMetal ||
                    platform == TargetPlatform.iOSNativeGLES ||
                    platform == TargetPlatform.Web;
        }

        private static bool IsCompressedTextureFormat(TextureProcessorOutputFormat format)
        {
            switch (format)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                case TextureProcessorOutputFormat.Etc1Compressed:
#pragma warning restore CS0618 // Type or member is obsolete

                case TextureProcessorOutputFormat.AtcCompressed:
                case TextureProcessorOutputFormat.DxtCompressed:
                case TextureProcessorOutputFormat.EtcCompressed:
                case TextureProcessorOutputFormat.PvrCompressed:
                case TextureProcessorOutputFormat.AstcCompressed:
                    return true;
            }
            return false;
        }

        private static TextureProcessorOutputFormat GetTextureFormatForPlatform(TextureProcessorOutputFormat format, TargetPlatform platform)
        {
            // Select the default texture compression format for the target platform
            if (format == TextureProcessorOutputFormat.Compressed)
            {
                if (platform == TargetPlatform.iOS || platform == TargetPlatform.iOSNativeGLES)
                    format = TextureProcessorOutputFormat.PvrCompressed;
                else if (platform == TargetPlatform.iOSMetal)
                    format = TextureProcessorOutputFormat.AstcCompressed;
                else if (platform == TargetPlatform.Android ||
                         platform == TargetPlatform.AndroidVK ||
                         platform == TargetPlatform.AndroidNativeGLES)
                    format = TextureProcessorOutputFormat.EtcCompressed;
                else
                    format = TextureProcessorOutputFormat.DxtCompressed;
            }

            if (IsCompressedTextureFormat(format))
            {
                // Make sure the target platform supports the selected texture compression format
                if (platform == TargetPlatform.iOS || platform == TargetPlatform.iOSNativeGLES)
                {
                    if (format != TextureProcessorOutputFormat.PvrCompressed)
                        throw new PlatformNotSupportedException("iOS platform only supports PVR texture compression");
                }
                else if (platform == TargetPlatform.iOSMetal)
                {
                    if (format != TextureProcessorOutputFormat.AstcCompressed &&
                        format != TextureProcessorOutputFormat.EtcCompressed)
                    {
                        throw new PlatformNotSupportedException(
                            "iOS Metal supports ASTC and ETC2 texture compression");
                    }
                }
                else if (platform == TargetPlatform.AndroidVK)
                {
                    if (format != TextureProcessorOutputFormat.EtcCompressed &&
                        format != TextureProcessorOutputFormat.AstcCompressed)
                    {
                        throw new PlatformNotSupportedException(
                            "Android Vulkan supports ETC2 and ASTC texture compression");
                    }
                }
                else if (platform == TargetPlatform.AndroidNativeGLES)
                {
                    if (format != TextureProcessorOutputFormat.EtcCompressed &&
                        format != TextureProcessorOutputFormat.AstcCompressed)
                    {
                        throw new PlatformNotSupportedException(
                            "Android Native OpenGL ES supports ETC2 and ASTC texture compression");
                    }
                }
                else if (   platform == TargetPlatform.Windows ||
                            platform == TargetPlatform.WindowsDX12 ||
                            platform == TargetPlatform.DesktopGL ||
                            platform == TargetPlatform.DesktopVK ||
                            platform == TargetPlatform.DesktopNativeGL ||
                            platform == TargetPlatform.MacOSX ||
                            platform == TargetPlatform.MacOSMetal ||
                            platform == TargetPlatform.NativeClient ||
                            platform == TargetPlatform.Web)
                {
                    if (format != TextureProcessorOutputFormat.DxtCompressed)
                        throw new PlatformNotSupportedException(platform + " platform only supports DXT texture compression");
                }
            }

            return format;
        }

        public override void Requirements(ContentProcessorContext context, TextureProcessorOutputFormat format, out bool requiresPowerOfTwo, out bool requiresSquare)
        {
            if (format == TextureProcessorOutputFormat.Compressed)
                format = GetTextureFormatForPlatform(format, context.TargetPlatform);

            // Does it require POT textures?
            switch (format)
            {
                default:
                    requiresPowerOfTwo = false;
                    break;

                case TextureProcessorOutputFormat.DxtCompressed:
                    requiresPowerOfTwo = context.TargetProfile == GraphicsProfile.Reach;
                    break;

#pragma warning disable CS0618 // Type or member is obsolete
                case TextureProcessorOutputFormat.Etc1Compressed:
#pragma warning restore CS0618 // Type or member is obsolete

                case TextureProcessorOutputFormat.PvrCompressed:
                case TextureProcessorOutputFormat.EtcCompressed:
                    requiresPowerOfTwo = true;
                    break;
            }

            // Does it require square textures?
            switch (format)
            {
                default:
                    requiresSquare = false;
                    break;

                case TextureProcessorOutputFormat.PvrCompressed:
                    requiresSquare = true;
                    break;
            }
        }

        protected override void PlatformCompressTexture(ContentProcessorContext context, TextureContent content, TextureProcessorOutputFormat format, bool isSpriteFont)
        {
            format = GetTextureFormatForPlatform(format, context.TargetPlatform);

            // Make sure we're in a floating point format
            content.ConvertBitmapType(typeof(PixelBitmapContent<Vector4>));

            switch (format)
            {
                case TextureProcessorOutputFormat.AtcCompressed:
                    GraphicsUtil.CompressAti(context, content, isSpriteFont);
                    break;

                case TextureProcessorOutputFormat.AstcCompressed:
                case TextureProcessorOutputFormat.AstcCompressed4x4:
                case TextureProcessorOutputFormat.AstcCompressed5x5:
                case TextureProcessorOutputFormat.AstcCompressed6x6:
                case TextureProcessorOutputFormat.AstcCompressed8x8:
                case TextureProcessorOutputFormat.AstcCompressed10x10:
                case TextureProcessorOutputFormat.AstcCompressed12x12:
                    GraphicsUtil.CompressAstc(context, content, isSpriteFont, format);
                    break;

                case TextureProcessorOutputFormat.Color16Bit:
                    GraphicsUtil.CompressColor16Bit(context, content);
                    break;

                case TextureProcessorOutputFormat.DxtCompressed:
                    GraphicsUtil.CompressDxt(context, content, isSpriteFont);
                    break;

#pragma warning disable CS0618 // Type or member is obsolete
                case TextureProcessorOutputFormat.Etc1Compressed:
#pragma warning restore CS0618 // Type or member is obsolete
                    GraphicsUtil.CompressEtc1(context, content, isSpriteFont);
                    break;

                case TextureProcessorOutputFormat.EtcCompressed:
                    GraphicsUtil.CompressEtc(context, content, isSpriteFont);
                    break;

                case TextureProcessorOutputFormat.PvrCompressed:
                    GraphicsUtil.CompressPvrtc(context, content, isSpriteFont);
                    break;
            }
        }
    }
}
