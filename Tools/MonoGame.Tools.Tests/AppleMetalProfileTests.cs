// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Audio;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using NUnit.Framework;
using MonoGame.Effect;

namespace MonoGame.Tests.ContentPipeline
{
    [TestFixture]
    public class AppleMetalProfileTests
    {
        [Test]
        public void AppleMetalPlatformsAreAppendOnly()
        {
            Assert.AreEqual(15, (int)TargetPlatform.XboxSeries);
            Assert.AreEqual(16, (int)TargetPlatform.MacOSMetal);
            Assert.AreEqual(17, (int)TargetPlatform.iOSMetal);
        }

        [Test]
        public void AppleMetalPlatformsHaveStableXnbIdentifiers()
        {
            var writerField = typeof(ContentWriter).GetField(
                "targetPlatformIdentifiers",
                BindingFlags.NonPublic | BindingFlags.Static);
            var writerIdentifiers = (char[])writerField.GetValue(null);

            var readerField = typeof(ContentManager).GetField(
                "targetPlatformIdentifiers",
                BindingFlags.NonPublic | BindingFlags.Static);
            var readerIdentifiers = (List<char>)readerField.GetValue(null);

            Assert.AreEqual('A', writerIdentifiers[(int)TargetPlatform.MacOSMetal]);
            Assert.AreEqual('I', writerIdentifiers[(int)TargetPlatform.iOSMetal]);
            Assert.AreEqual('A', readerIdentifiers[(int)TargetPlatform.MacOSMetal]);
            Assert.AreEqual('I', readerIdentifiers[(int)TargetPlatform.iOSMetal]);
        }

        [Test]
        public void DefaultProfilesSupportAppleMetalPlatforms()
        {
            var texture = new DefaultTextureProfile();
            var audio = new DefaultAudioProfile();

            Assert.IsTrue(texture.Supports(TargetPlatform.MacOSMetal));
            Assert.IsTrue(texture.Supports(TargetPlatform.iOSMetal));
            Assert.IsTrue(audio.Supports(TargetPlatform.MacOSMetal));
            Assert.IsTrue(audio.Supports(TargetPlatform.iOSMetal));
        }

        [Test]
        public void AppleMetalPlatformsSelectOsSpecificShaderProfiles()
        {
            var macOS = ShaderProfile.GetProfileForPlatform(TargetPlatform.MacOSMetal);
            var iOS = ShaderProfile.GetProfileForPlatform(TargetPlatform.iOSMetal);

            Assert.AreEqual("MetalMacOS", macOS.Name);
            Assert.AreEqual("MetaliOS", iOS.Name);
            Assert.AreEqual(81, macOS.FormatId);
            Assert.AreEqual(81, iOS.FormatId);
        }
    }
}
