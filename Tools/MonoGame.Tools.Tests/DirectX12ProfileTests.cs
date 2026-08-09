// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Effect;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline
{
    [TestFixture]
    public sealed class DirectX12ProfileTests
    {
        [TestCase(
            ";       float LightingEnabled;; Offset:    0",
            "float",
            "LightingEnabled",
            0)]
        [TestCase(
            ";       float4 SurfaceData;; Offset:   16",
            "float4",
            "SurfaceData",
            16)]
        [TestCase(
            ";       column_major float4x4 PreviousView;; Offset:   32",
            "float4x4",
            "PreviousView",
            32)]
        [TestCase(
            ";       row_major float4x4 PreviousProjection; ; Offset:   96",
            "float4x4",
            "PreviousProjection",
            96)]
        public void ConstantBufferParametersAllowAdjacentMetadataSeparator(
            string line,
            string expectedType,
            string expectedName,
            int expectedOffset)
        {
            var parsed = DirectX12ShaderProfile.TryParseConstantBufferParameter(
                line,
                out var type,
                out var name,
                out var offset);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(parsed);
                Assert.AreEqual(expectedType, type);
                Assert.AreEqual(expectedName, name);
                Assert.AreEqual(expectedOffset, offset);
            });
        }
    }
}
