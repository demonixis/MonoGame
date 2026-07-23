// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Audio;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using MonoGame.Effect;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline
{
    [TestFixture]
    public sealed class NativeOpenGLProfileTests
    {
        [Test]
        public void PlatformsAndXnbIdentifiersAreAppendOnlyAndUnique()
        {
            Assert.AreEqual(19, (int)TargetPlatform.AndroidNativeGLES);
            Assert.AreEqual(20, (int)TargetPlatform.iOSNativeGLES);
            Assert.AreEqual(21, (int)TargetPlatform.DesktopNativeGL);

            var writerIdentifiers = (char[])typeof(ContentWriter).GetField(
                "targetPlatformIdentifiers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var readerIdentifiers = (List<char>)typeof(ContentManager).GetField(
                "targetPlatformIdentifiers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            Assert.AreEqual('E', writerIdentifiers[(int)TargetPlatform.AndroidNativeGLES]);
            Assert.AreEqual('U', writerIdentifiers[(int)TargetPlatform.iOSNativeGLES]);
            Assert.AreEqual('L', writerIdentifiers[(int)TargetPlatform.DesktopNativeGL]);
            Assert.AreEqual('E', readerIdentifiers[(int)TargetPlatform.AndroidNativeGLES]);
            Assert.AreEqual('U', readerIdentifiers[(int)TargetPlatform.iOSNativeGLES]);
            Assert.AreEqual('L', readerIdentifiers[(int)TargetPlatform.DesktopNativeGL]);
            Assert.AreEqual(writerIdentifiers.Length, writerIdentifiers.Distinct().Count());
            Assert.AreEqual(readerIdentifiers.Count, readerIdentifiers.Distinct().Count());
        }

        [Test]
        public void PlatformsSelectStrictAppendOnlyMgfxProfiles()
        {
            var android = ShaderProfile.GetProfileForPlatform(TargetPlatform.AndroidNativeGLES);
            var iOS = ShaderProfile.GetProfileForPlatform(TargetPlatform.iOSNativeGLES);
            var desktop = ShaderProfile.GetProfileForPlatform(TargetPlatform.DesktopNativeGL);

            Assert.Multiple(() =>
            {
                Assert.AreEqual("OpenGLES_3_0", android.Name);
                Assert.AreEqual(82, android.FormatId);
                Assert.AreEqual("OpenGLES_3_0", iOS.Name);
                Assert.AreEqual(82, iOS.FormatId);
                Assert.AreEqual("OpenGL_4_1", desktop.Name);
                Assert.AreEqual(84, desktop.FormatId);
                Assert.AreEqual(0, ShaderProfile.OpenGL.FormatId, "The historical DesktopGL profile must remain unchanged.");
            });
        }

        [Test]
        public void DefaultContentProfilesSupportAllNativeOpenGLTargets()
        {
            var texture = new DefaultTextureProfile();
            var audio = new DefaultAudioProfile();
            foreach (var platform in new[]
            {
                TargetPlatform.AndroidNativeGLES,
                TargetPlatform.iOSNativeGLES,
                TargetPlatform.DesktopNativeGL,
            })
            {
                Assert.IsTrue(texture.Supports(platform), platform.ToString());
                Assert.IsTrue(audio.Supports(platform), platform.ToString());
            }
        }

        [Test]
        public void MobileNativeOpenGLContentUsesPlatformBundleItems()
        {
            var path = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "MonoGame.Content.Builder.Task.targets");
            var document = XDocument.Load(path);

            var androidAssetCondition = document.Descendants()
                .Single(element =>
                    element.Name.LocalName == "Output" &&
                    (string)element.Attribute("ItemName") == "AndroidAsset")
                .Attribute("Condition")?.Value;
            var bundleResourceCondition = document.Descendants()
                .Single(element =>
                    element.Name.LocalName == "Output" &&
                    (string)element.Attribute("ItemName") == "BundleResource")
                .Attribute("Condition")?.Value;

            Assert.That(androidAssetCondition, Does.Contain("AndroidNativeGLES"));
            Assert.That(bundleResourceCondition, Does.Contain("iOSNativeGLES"));
        }

        [TestCase(TargetPlatform.iOSNativeGLES, 82, "#version 300 es")]
        [TestCase(TargetPlatform.AndroidNativeGLES, 82, "#version 300 es")]
        [TestCase(TargetPlatform.DesktopNativeGL, 84, "#version 410 core")]
        public void CompiledPayloadContainsStrictGlslAndDeterministicBindings(
            TargetPlatform platform,
            int expectedFormat,
            string expectedVersion)
        {
            var effectCode = CompileSpriteEffect(platform);
            Assert.AreEqual(expectedFormat, effectCode[5]);

            var shaders = ReadShaders(effectCode);
            Assert.AreEqual(2, shaders.Count);
            foreach (var shader in shaders)
            {
                using var stream = new MemoryStream(shader.Payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
                Assert.AreEqual(0x314C474Du, reader.ReadUInt32());
                Assert.AreEqual(1u, reader.ReadUInt32());
                Assert.AreEqual((uint)expectedFormat, reader.ReadUInt32());
                reader.ReadUInt32();
                Assert.That(ReadPayloadString(reader), Does.StartWith(expectedVersion));

                var uniformCount = reader.ReadUInt32();
                for (var index = 0; index < uniformCount; ++index)
                {
                    var name = ReadPayloadString(reader);
                    Assert.That(name, Does.StartWith("mg_").And.Contain("_block_"));
                    Assert.AreEqual((uint)index, reader.ReadUInt32());
                }

                var samplerCount = reader.ReadUInt32();
                for (var index = 0; index < samplerCount; ++index)
                {
                    var name = ReadPayloadString(reader);
                    Assert.That(name, Does.StartWith("mg_").And.Contain("_texture_"));
                    Assert.AreEqual(0, reader.ReadInt32());
                    Assert.AreEqual(0, reader.ReadInt32());
                }
                Assert.AreEqual(stream.Length, stream.Position);
            }

            var vertexShader = shaders.Single(shader => shader.IsVertexShader);
            CollectionAssert.AreEqual(new short[] { 0, 1, 2 }, vertexShader.AttributeLocations);
            var vertexSource = ReadGlslSource(vertexShader.Payload);
            Assert.That(vertexSource, Does.Contain("uniform vec4 mg_posFixup;"));
            Assert.That(vertexSource, Does.Contain("gl_Position.y = gl_Position.y * mg_posFixup.y;"));
            Assert.That(vertexSource, Does.Contain("gl_Position.xy += mg_posFixup.zw * gl_Position.ww;"));
            Assert.That(vertexSource, Does.Not.Match(@"\b([A-Za-z_][A-Za-z0-9_]*)\.y\s*=\s*-\s*\1\.y"));
            Assert.That(shaders.Single(shader => !shader.IsVertexShader).SamplerCount, Is.EqualTo(1));
        }

        [Test]
        public void NativeOpenGLCompilationIsDeterministic()
        {
            CollectionAssert.AreEqual(
                CompileSpriteEffect(TargetPlatform.DesktopNativeGL),
                CompileSpriteEffect(TargetPlatform.DesktopNativeGL));
        }

        [Test]
        public void NativeOpenGLEffectsTrackTheSpirvCrossConverterAsAContentDependency()
        {
            var path = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "Assets", "Effects", "Stock", "SpriteEffect.fx");
            using var context = new TestProcessorContext(
                TargetPlatform.DesktopNativeGL,
                Path.ChangeExtension(path, ".xnb"));

            var processor = new EffectProcessor();
            processor.Process(new EffectContent
            {
                EffectCode = File.ReadAllText(path),
                Identity = new ContentIdentity(path),
            }, context);

            var converter = context.Dependencies.Single(dependency =>
                string.Equals(
                    Path.GetFileName(dependency),
                    OperatingSystem.IsWindows() ? "mgfx-spvc.exe" : "mgfx-spvc",
                    StringComparison.OrdinalIgnoreCase));
            Assert.That(Path.IsPathRooted(converter), Is.True);
            Assert.That(File.Exists(converter), Is.True);
        }

        [TestCase(TargetPlatform.AndroidNativeGLES, "#version 300 es")]
        [TestCase(TargetPlatform.DesktopNativeGL, "#version 410 core")]
        public void MrtOutputsHaveExplicitLocationsAndCompileDeterministically(
            TargetPlatform platform,
            string expectedVersion)
        {
            var first = CompileEffect(platform, "NativeOpenGLMrt.fx");
            var second = CompileEffect(platform, "NativeOpenGLMrt.fx");
            CollectionAssert.AreEqual(first, second);

            var pixelShader = ReadShaders(first).Single(shader => !shader.IsVertexShader);
            var source = ReadGlslSource(pixelShader.Payload);
            Assert.That(source, Does.StartWith(expectedVersion));
            for (var location = 0; location < 4; ++location)
                Assert.That(source, Does.Contain($"layout(location = {location})"));
        }

        private static byte[] CompileSpriteEffect(TargetPlatform platform)
        {
            return CompileEffect(platform, Path.Combine("Stock", "SpriteEffect.fx"));
        }

        private static byte[] CompileEffect(TargetPlatform platform, string relativePath)
        {
            var path = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "Assets", "Effects", relativePath);
            using var context = new TestProcessorContext(platform, Path.ChangeExtension(path, ".xnb"));
            var processor = new EffectProcessor();
            var output = processor.Process(new EffectContent
            {
                EffectCode = File.ReadAllText(path),
                Identity = new ContentIdentity(path),
            }, context);
            return output.GetEffectCode();
        }

        private static List<CompiledShader> ReadShaders(byte[] effectCode)
        {
            using var stream = new MemoryStream(effectCode, 10, effectCode.Length - 10, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            var constantBufferCount = reader.ReadInt32();
            for (var index = 0; index < constantBufferCount; ++index)
            {
                reader.ReadString();
                reader.ReadUInt16();
                var parameterCount = reader.ReadInt32();
                for (var parameter = 0; parameter < parameterCount; ++parameter)
                {
                    reader.ReadInt32();
                    reader.ReadUInt16();
                }
            }

            var result = new List<CompiledShader>();
            var shaderCount = reader.ReadInt32();
            for (var index = 0; index < shaderCount; ++index)
            {
                var shader = new CompiledShader { IsVertexShader = reader.ReadBoolean() };
                reader.ReadString();
                reader.ReadString();
                shader.Payload = reader.ReadBytes(reader.ReadInt32());

                shader.SamplerCount = reader.ReadByte();
                for (var sampler = 0; sampler < shader.SamplerCount; ++sampler)
                {
                    reader.ReadByte();
                    reader.ReadByte();
                    reader.ReadByte();
                    if (reader.ReadBoolean())
                    {
                        reader.ReadBytes(3);
                        reader.ReadBytes(4);
                        reader.ReadByte();
                        reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadSingle();
                    }
                    reader.ReadString();
                    reader.ReadByte();
                }

                reader.ReadBytes(reader.ReadByte());
                var attributeCount = reader.ReadByte();
                shader.AttributeLocations = new short[attributeCount];
                for (var attribute = 0; attribute < attributeCount; ++attribute)
                {
                    reader.ReadString();
                    reader.ReadByte();
                    reader.ReadByte();
                    shader.AttributeLocations[attribute] = reader.ReadInt16();
                }
                result.Add(shader);
            }
            return result;
        }

        private static string ReadPayloadString(BinaryReader reader)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes(checked((int)reader.ReadUInt32())));
        }

        private static string ReadGlslSource(byte[] payload)
        {
            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
            Assert.AreEqual(0x314C474Du, reader.ReadUInt32());
            Assert.AreEqual(1u, reader.ReadUInt32());
            reader.ReadUInt32();
            reader.ReadUInt32();
            return ReadPayloadString(reader);
        }

        private sealed class CompiledShader
        {
            public bool IsVertexShader;
            public byte[] Payload;
            public int SamplerCount;
            public short[] AttributeLocations;
        }
    }
}
