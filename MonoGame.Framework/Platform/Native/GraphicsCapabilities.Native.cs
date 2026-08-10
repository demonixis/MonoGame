// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using MonoGame.Interop;

namespace Microsoft.Xna.Framework.Graphics;

internal partial class GraphicsCapabilities
{
    private void PlatformInitialize(GraphicsDevice device)
    {
        var isNativeOpenGL = device.NativeCapabilitiesAbiVersion >= 2 &&
            (GraphicsDevice.ShaderProfile == 82 || GraphicsDevice.ShaderProfile == 84);
        SupportsNonPowerOfTwo = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsTextureFilterAnisotropic =
            (device.NativeFeatures & NativeGraphicsFeatures.AnisotropicFiltering) != 0;

        SupportsDepth24 = true;
        SupportsPackedDepthStencil = true;
        SupportsDepthNonLinear = false;
        SupportsTextureMaxLevel = true;

        // Texture compression
        var textureCompression = device.TextureCompressionCapabilities;
        SupportsDxt1 = textureCompression.HasFlag(TextureCompressionCapabilities.S3tc);
        SupportsS3tc = SupportsDxt1;
        SupportsPvrtc = textureCompression.HasFlag(TextureCompressionCapabilities.Pvrtc);
        SupportsEtc2 = textureCompression.HasFlag(TextureCompressionCapabilities.Etc2);
        SupportsAstc = textureCompression.HasFlag(TextureCompressionCapabilities.Astc);

        SupportsSRgb = true;

        SupportsTextureArrays = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsDepthClamp = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsVertexTextures = device.GraphicsProfile == GraphicsProfile.HiDef;
        SupportsFloatTextures = true;
        SupportsHalfFloatTextures = true;
        SupportsNormalized = true;

        SupportsInstancing = true;
        SupportsBaseIndexInstancing = SupportsBaseIndexInstancingForShaderProfile(GraphicsDevice.ShaderProfile);
        SupportsSeparateBlendStates = !isNativeOpenGL;

        MaxTextureAnisotropy = SupportsTextureFilterAnisotropic
            ? Math.Max(1, (int)Math.Floor(device.NativeMaxAnisotropy))
            : 1;
    }

    internal static bool SupportsBaseIndexInstancingForShaderProfile(int shaderProfile)
    {
        return shaderProfile != 82;
    }

}
