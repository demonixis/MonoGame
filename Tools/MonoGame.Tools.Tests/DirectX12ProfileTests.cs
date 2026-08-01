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
            ";       float ScreenSpaceGlobalIlluminationEnabled;; Offset:    0",
            "float",
            "ScreenSpaceGlobalIlluminationEnabled",
            0)]
        [TestCase(
            ";       float4 ScreenSpaceGlobalIlluminationData;; Offset:   16",
            "float4",
            "ScreenSpaceGlobalIlluminationData",
            16)]
        [TestCase(
            ";       column_major float4x4 ScreenSpaceGlobalIlluminationPreviousView;; Offset:   32",
            "float4x4",
            "ScreenSpaceGlobalIlluminationPreviousView",
            32)]
        [TestCase(
            ";       row_major float4x4 ScreenSpaceGlobalIlluminationPreviousProjection; ; Offset:   96",
            "float4x4",
            "ScreenSpaceGlobalIlluminationPreviousProjection",
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
