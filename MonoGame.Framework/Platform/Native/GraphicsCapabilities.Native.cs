// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Interop;

namespace Microsoft.Xna.Framework.Graphics;

internal partial class GraphicsCapabilities
{
    private void PlatformInitialize(GraphicsDevice device)
    {
        SupportsNonPowerOfTwo = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsTextureFilterAnisotropic = true;

        SupportsDepth24 = true;
        SupportsPackedDepthStencil = true;
        SupportsDepthNonLinear = false;
        SupportsTextureMaxLevel = true;

        // Texture compression
#if METAL
        var textureCompression = device.TextureCompressionCapabilities;
        SupportsDxt1 = textureCompression.HasFlag(TextureCompressionCapabilities.S3tc);
        SupportsS3tc = SupportsDxt1;
        SupportsPvrtc = textureCompression.HasFlag(TextureCompressionCapabilities.Pvrtc);
        SupportsEtc2 = textureCompression.HasFlag(TextureCompressionCapabilities.Etc2);
        SupportsAstc = textureCompression.HasFlag(TextureCompressionCapabilities.Astc);
#else
        SupportsDxt1 = true;
        SupportsS3tc = true;
#endif

        SupportsSRgb = true;

        SupportsTextureArrays = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsDepthClamp = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsVertexTextures = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsFloatTextures = true;
        SupportsHalfFloatTextures = true;
        SupportsNormalized = true;

        SupportsInstancing = true;
        SupportsBaseIndexInstancing = true;
        SupportsSeparateBlendStates = true;

        MaxTextureAnisotropy = (device.GraphicsProfile == GraphicsProfile.Reach) ? 2 : 16;
    }

}
