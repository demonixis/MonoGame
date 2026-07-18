// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Audio;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using MonoGame.Effect;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline
{
    [TestFixture]
    public class AndroidVulkanProfileTests
    {
        [Test]
        public void AndroidVulkanPlatformIsAppendOnly()
        {
            Assert.AreEqual(17, (int)TargetPlatform.iOSMetal);
            Assert.AreEqual(18, (int)TargetPlatform.AndroidVK);
        }

        [Test]
        public void AndroidVulkanPlatformHasUniqueStableXnbIdentifier()
        {
            var writerField = typeof(ContentWriter).GetField(
                "targetPlatformIdentifiers",
                BindingFlags.NonPublic | BindingFlags.Static);
            var writerIdentifiers = (char[])writerField.GetValue(null);

            var readerField = typeof(ContentManager).GetField(
                "targetPlatformIdentifiers",
                BindingFlags.NonPublic | BindingFlags.Static);
            var readerIdentifiers = (List<char>)readerField.GetValue(null);

            Assert.AreEqual('K', writerIdentifiers[(int)TargetPlatform.AndroidVK]);
            Assert.AreEqual('K', readerIdentifiers[(int)TargetPlatform.AndroidVK]);
            Assert.AreEqual(writerIdentifiers.Length, writerIdentifiers.Distinct().Count());
            Assert.AreEqual(readerIdentifiers.Count, readerIdentifiers.Distinct().Count());
        }

        [Test]
        public void DefaultProfilesSupportAndroidVulkan()
        {
            var texture = new DefaultTextureProfile();
            var audio = new DefaultAudioProfile();

            Assert.IsTrue(texture.Supports(TargetPlatform.AndroidVK));
            Assert.IsTrue(audio.Supports(TargetPlatform.AndroidVK));
        }

        [Test]
        public void AndroidVulkanSelectsVulkanShaderProfile()
        {
            var profile = ShaderProfile.GetProfileForPlatform(TargetPlatform.AndroidVK);

            Assert.AreEqual("Vulkan", profile.Name);
            Assert.AreEqual(80, profile.FormatId);
        }

        [Test]
        public void AndroidVulkanAllowsAstcButRejectsDxt()
        {
            var selector = typeof(DefaultTextureProfile).GetMethod(
                "GetTextureFormatForPlatform",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.AreEqual(
                TextureProcessorOutputFormat.EtcCompressed,
                selector.Invoke(null, new object[]
                {
                    TextureProcessorOutputFormat.Compressed,
                    TargetPlatform.AndroidVK,
                }));

            Assert.AreEqual(
                TextureProcessorOutputFormat.AstcCompressed,
                selector.Invoke(null, new object[]
                {
                    TextureProcessorOutputFormat.AstcCompressed,
                    TargetPlatform.AndroidVK,
                }));

            var exception = Assert.Throws<TargetInvocationException>(() => selector.Invoke(
                null,
                new object[]
                {
                    TextureProcessorOutputFormat.DxtCompressed,
                    TargetPlatform.AndroidVK,
                }));
            Assert.IsInstanceOf<System.PlatformNotSupportedException>(exception.InnerException);
        }
    }
}
