// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#if OPENGL && NATIVE
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using MonoGame.Interop;
using NUnit.Framework;

namespace MonoGame.Tests.Graphics
{
    [NonParallelizable]
    [RunOnUiTestFixture]
    internal sealed class NativeOpenGLValidationTest : GraphicsDeviceTestFixtureBase
    {
        [Test]
        public void Strict41ContextAndPixelReadback()
        {
            gd.Clear(Color.MonoGameOrange);
            var pixel = new Color[1];
            gd.GetBackBufferData(new Rectangle(gd.Viewport.Width / 2, gd.Viewport.Height / 2, 1, 1), pixel, 0, 1);
            Assert.AreEqual(Color.MonoGameOrange, pixel[0]);

            var capturePath = Environment.GetEnvironmentVariable("MONOGAME_OPENGL_CAPTURE_PATH");
            if (!string.IsNullOrEmpty(capturePath))
            {
                var directory = Path.GetDirectoryName(capturePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var pixels = new Color[gd.Viewport.Width * gd.Viewport.Height];
                gd.GetBackBufferData(pixels);
                using var capture = new Texture2D(gd, gd.Viewport.Width, gd.Viewport.Height);
                capture.SetData(pixels);
                using var stream = File.Create(capturePath);
                capture.SaveAsPng(stream, capture.Width, capture.Height);
            }

            Assert.DoesNotThrow(() => gd.Present());

        }

        [Test]
        public unsafe void CapabilityAbiAndShaderProfileRejectionAreStrict()
        {
            var caps = new MGG_GraphicsDevice_CapsV2
            {
                StructSize = (uint)Marshal.SizeOf<MGG_GraphicsDevice_CapsV2>(),
            };
            Assert.AreEqual(
                MonoGame.Interop.GraphicsDeviceStatus.Success,
                MGG.GraphicsDevice_GetCapsV2(gd.Handle, ref caps, caps.StructSize));
            var undersizedCaps = new MGG_GraphicsDevice_CapsV2 { StructSize = 4 };
            Assert.AreEqual(
                MonoGame.Interop.GraphicsDeviceStatus.InsufficientSize,
                MGG.GraphicsDevice_GetCapsV2(gd.Handle, ref undersizedCaps, undersizedCaps.StructSize));
            Assert.AreEqual(3u, caps.AbiVersion);
            Assert.AreEqual(4, caps.ApiMajor);
            Assert.GreaterOrEqual(caps.ApiMinor, 1);
            Assert.AreEqual(84, caps.ShaderProfile);
            Assert.GreaterOrEqual(caps.MaxMultiSampleCount, 1);
            Assert.GreaterOrEqual(caps.MaxRenderTargets, 4);
            Assert.GreaterOrEqual(caps.MaxDrawBuffers, 4);
            Assert.GreaterOrEqual(caps.MaxColorAttachments, 4);
            Assert.IsTrue(caps.Features.HasFlag(NativeGraphicsFeatures.ExplicitRenderPass));
            Assert.IsTrue(gd.SupportsExplicitRenderPass);
            Assert.IsFalse(gd.SupportsDepthStencilTargetFormat(DepthFormat.None));
            Assert.IsTrue(
                gd.SupportsDepthStencilTargetFormat(DepthFormat.Depth32Float) ||
                gd.SupportsDepthStencilTargetFormat(DepthFormat.Depth24));
            Assert.IsFalse(gd.GraphicsCapabilities.SupportsSeparateBlendStates);
            Assert.IsTrue(gd.GraphicsCapabilities.SupportsBaseIndexInstancing);
            Assert.IsFalse(GraphicsCapabilities.SupportsBaseIndexInstancingForShaderProfile(82));
            Assert.AreEqual(
                caps.Features.HasFlag(NativeGraphicsFeatures.AnisotropicFiltering),
                gd.GraphicsCapabilities.SupportsTextureFilterAnisotropic);
            Assert.AreEqual(
                gd.GraphicsCapabilities.SupportsTextureFilterAnisotropic
                    ? Math.Max(1, (int)Math.Floor(caps.MaxAnisotropy))
                    : 1,
                gd.GraphicsCapabilities.MaxTextureAnisotropy);
            Assert.Throws<InvalidOperationException>(() =>
                GraphicsDevice.ThrowForNativeRenderTargetStatus(
                    MonoGame.Interop.GraphicsDeviceStatus.FramebufferIncomplete));
            var incompatiblePayload = new byte[20];
            BitConverter.GetBytes(0x314C474Du).CopyTo(incompatiblePayload, 0); // MGL1
            BitConverter.GetBytes(1u).CopyTo(incompatiblePayload, 4);
            BitConverter.GetBytes(82u).CopyTo(incompatiblePayload, 8); // Mobile GLES, not desktop GL.
            BitConverter.GetBytes(0u).CopyTo(incompatiblePayload, 12);
            fixed (byte* payload = incompatiblePayload)
            {
                var shader = MGG.Shader_Create(gd.Handle, ShaderStage.Vertex, payload, incompatiblePayload.Length);
                Assert.AreEqual(IntPtr.Zero, (IntPtr)shader);
            }
        }

        [Test]
        public void ExplicitRenderPassSharesAndPreservesSampleableDepth()
        {
            var depthFormat = gd.SupportsDepthStencilTargetFormat(DepthFormat.Depth32Float)
                ? DepthFormat.Depth32Float
                : DepthFormat.Depth24;
            Assert.IsTrue(gd.SupportsDepthStencilTargetFormat(depthFormat));

            using var color = new RenderTarget2D(
                gd,
                16,
                16,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);
            using var depth = new DepthStencilTarget2D(gd, 16, 16, depthFormat);
            using var mismatchedDepth = new DepthStencilTarget2D(gd, 8, 8, depthFormat);
            using var effect = new BasicEffect(gd)
            {
                World = Matrix.Identity,
                View = Matrix.Identity,
                Projection = Matrix.Identity,
                VertexColorEnabled = true,
            };

            Assert.IsInstanceOf<Texture2D>(depth);
            Assert.AreEqual(depthFormat, depth.DepthStencilFormat);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DepthStencilTarget2D(gd, 1, 1, DepthFormat.None));

            var colors = new[]
            {
                new RenderPassColorAttachment(
                    color,
                    RenderPassLoadAction.Clear,
                    RenderPassStoreAction.Store,
                    Color.Red.ToVector4()),
            };
            var depthAttachment = new RenderPassDepthStencilAttachment(
                depth,
                RenderPassLoadAction.Clear,
                RenderPassStoreAction.Store,
                RenderPassLoadAction.DontCare,
                RenderPassStoreAction.DontCare,
                0.25f);
            var vertices = new[]
            {
                new VertexPositionColor(new Vector3(-1, -1, 0.5f), Color.Red),
                new VertexPositionColor(new Vector3(-1,  3, 0.5f), Color.Red),
                new VertexPositionColor(new Vector3( 3, -1, 0.5f), Color.Red),
            };

            gd.BlendState = BlendState.Opaque;
            gd.RasterizerState = RasterizerState.CullNone;
            gd.DepthStencilState = DepthStencilState.None;
            gd.SetRenderPass(colors, in depthAttachment);
            DrawExplicitDepthTriangle(effect, vertices);
            for (var index = 0; index < vertices.Length; ++index)
                vertices[index].Color = Color.Blue;

            colors[0] = new RenderPassColorAttachment(
                color,
                RenderPassLoadAction.Load,
                RenderPassStoreAction.Store,
                Vector4.Zero);
            depthAttachment = new RenderPassDepthStencilAttachment(
                depth,
                RenderPassLoadAction.Load,
                RenderPassStoreAction.Store,
                RenderPassLoadAction.DontCare,
                RenderPassStoreAction.DontCare);
            gd.DepthStencilState = DepthStencilState.Default;
            gd.SetRenderPass(colors, in depthAttachment);
            DrawExplicitDepthTriangle(effect, vertices);
            gd.SetRenderTarget(null);

            var pixel = new Color[1];
            color.GetData(0, new Rectangle(8, 8, 1, 1), pixel, 0, 1);
            Assert.AreEqual(Color.Red, pixel[0], "The second pass must load the stored 0.25 depth and reject z=0.5.");

            depthAttachment = new RenderPassDepthStencilAttachment(
                depth,
                RenderPassLoadAction.Clear,
                RenderPassStoreAction.Store,
                RenderPassLoadAction.DontCare,
                RenderPassStoreAction.DontCare,
                1.0f);
            gd.SetRenderPass(colors, in depthAttachment);
            DrawExplicitDepthTriangle(effect, vertices);
            gd.SetRenderTarget(null);

            color.GetData(0, new Rectangle(8, 8, 1, 1), pixel, 0, 1);
            Assert.AreEqual(Color.Blue, pixel[0], "Clearing shared depth to one must admit z=0.5.");

            var wrongDepth = new RenderPassDepthStencilAttachment(
                mismatchedDepth,
                RenderPassLoadAction.Clear,
                RenderPassStoreAction.Store,
                RenderPassLoadAction.DontCare,
                RenderPassStoreAction.DontCare);
            Assert.Throws<ArgumentException>(() => gd.SetRenderPass(colors, in wrongDepth));

            colors[0] = new RenderPassColorAttachment(
                color,
                (RenderPassLoadAction)99,
                RenderPassStoreAction.Store,
                Vector4.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => gd.SetRenderPass(colors));
        }

        private void DrawExplicitDepthTriangle(BasicEffect effect, VertexPositionColor[] vertices)
        {
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);
            }
        }

        [Test]
        public void LegacyCapabilityFallbackDoesNotInventMrtSupport()
        {
            var legacy = new MGG_GraphicsDevice_Caps
            {
                MaxTextureSlots = 16,
                MaxVertexTextureSlots = 4,
                MaxVertexBufferSlots = 8,
                ShaderProfile = 80,
                MaxMultiSampleCount = 4,
                TextureCompression = TextureCompressionCapabilities.S3tc,
            };

            var fallback = GraphicsDevice.CreateLegacyCapabilitiesFallback(legacy);
            Assert.AreEqual(0u, fallback.AbiVersion);
            Assert.AreEqual(legacy.ShaderProfile, fallback.ShaderProfile);
            Assert.AreEqual(1, fallback.MaxRenderTargets);
            Assert.AreEqual(1, fallback.MaxDrawBuffers);
            Assert.AreEqual(1, fallback.MaxColorAttachments);
            Assert.AreEqual(NativeGraphicsFeatures.None, fallback.Features);
        }

        [Test]
        public void CompletedGpuTimingDoesNotCallAnUnsupportedNativeBackend()
        {
            Assert.IsFalse(gd.SupportsCompletedGpuFrameTiming);
            Assert.IsFalse(gd.TryDequeueCompletedGpuFrameTiming(out var timing));
            Assert.AreEqual(0ul, timing.SubmissionId);
            Assert.AreEqual(0ul, timing.DurationNanoseconds);
            Assert.AreEqual(0ul, timing.DroppedTimingCount);
        }

        [Test]
        public void BuffersTexture3DAndMrtShaderReadback()
        {
            using (var vertexBuffer = new VertexBuffer(gd, typeof(VertexPositionColor), 3, BufferUsage.None))
            {
                var source = new[]
                {
                    new VertexPositionColor(Vector3.Zero, Color.Red),
                    new VertexPositionColor(Vector3.One, Color.Green),
                    new VertexPositionColor(Vector3.Up, Color.Blue),
                };
                var result = new VertexPositionColor[source.Length];
                vertexBuffer.SetData(source);
                vertexBuffer.GetData(result);
                CollectionAssert.AreEqual(source, result);
            }

            using (var texture = new Texture3D(gd, 2, 2, 2, false, SurfaceFormat.Color))
            {
                var source = new[]
                {
                    Color.Red, Color.Green, Color.Blue, Color.White,
                    Color.Yellow, Color.Cyan, Color.Magenta, Color.Black,
                };
                var result = new Color[source.Length];
                texture.SetData(source);
                texture.GetData(result);
                CollectionAssert.AreEqual(source, result);
            }

            using (var first = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8))
            using (var second = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.None))
            using (var third = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.None))
            using (var fourth = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.None))
            using (var effect = new Effect(gd, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "NativeOpenGLMrt.mgfxo"))))
            {
                gd.SetRenderTargets(
                    new RenderTargetBinding(first),
                    new RenderTargetBinding(second),
                    new RenderTargetBinding(third),
                    new RenderTargetBinding(fourth));
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.None;
                gd.RasterizerState = RasterizerState.CullNone;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        new[]
                        {
                            new VertexPosition(new Vector3(-1, -1, 0)),
                            new VertexPosition(new Vector3(-1,  3, 0)),
                            new VertexPosition(new Vector3( 3, -1, 0)),
                        },
                        0,
                        1);
                }
                gd.SetRenderTarget(null);

                var expected = new[] { Color.Red, Color.Lime, Color.Blue, Color.Yellow };
                var targets = new[] { first, second, third, fourth };
                for (var index = 0; index < targets.Length; ++index)
                {
                    var pixel = new Color[1];
                    targets[index].GetData(0, new Rectangle(4, 4, 1, 1), pixel, 0, 1);
                    Assert.AreEqual(expected[index], pixel[0], $"MRT output {index}");
                }
            }

            using (var first = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.None))
            using (var wrongSize = new RenderTarget2D(gd, 16, 8, false, SurfaceFormat.Color, DepthFormat.None))
            using (var depthOnSecond = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.Depth24))
            using (var fifth = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.None))
            using (var nonColor = new RenderTarget2D(gd, 8, 8, false, SurfaceFormat.HalfVector4, DepthFormat.None))
            using (var cube = new RenderTargetCube(gd, 8, false, SurfaceFormat.Color, DepthFormat.None))
            using (var multisampled = new RenderTarget2D(
                gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.None,
                2, RenderTargetUsage.DiscardContents))
            {
                Assert.Throws<ArgumentException>(() => gd.SetRenderTargets(first, wrongSize));
                Assert.Throws<NotSupportedException>(() => gd.SetRenderTargets(first, depthOnSecond));
                Assert.Throws<ArgumentException>(() => gd.SetRenderTargets(first, first, first, first, fifth));
                Assert.DoesNotThrow(() => gd.SetRenderTarget(nonColor));
                gd.SetRenderTarget(null);
                Assert.Throws<NotSupportedException>(() => gd.SetRenderTargets(first, nonColor));
                Assert.DoesNotThrow(() => gd.SetRenderTargets(
                    new RenderTargetBinding(cube, CubeMapFace.PositiveX)));
                gd.SetRenderTarget(null);
                Assert.Greater(multisampled.MultiSampleCount, 1, "The GL 4.1 validation context must expose MSAA for the rejection gate.");
                Assert.DoesNotThrow(() => gd.SetRenderTarget(multisampled));
                gd.SetRenderTarget(null);
                Assert.Throws<NotSupportedException>(() => gd.SetRenderTargets(first, multisampled));
            }

            var marker = Encoding.UTF8.GetBytes("MONOGAME_OPENGL_VALIDATION pass api=4.1 mrt=4 format=Color msaa=0\n");
            Console.OpenStandardOutput().Write(marker, 0, marker.Length);
        }

        [Test]
        public void InstancingAndResizeRemainRenderable()
        {
            using (var renderTarget = new RenderTarget2D(gd, 32, 32, false, SurfaceFormat.Color, DepthFormat.None))
            using (var vertices = new VertexBuffer(gd, VertexPositionColor.VertexDeclaration, 3, BufferUsage.None))
            using (var indices = new IndexBuffer(gd, IndexElementSize.SixteenBits, 3, BufferUsage.None))
            using (var effect = new BasicEffect(gd))
            {
                vertices.SetData(new[]
                {
                    new VertexPositionColor(new Vector3(-0.75f, -0.75f, 0), Color.White),
                    new VertexPositionColor(new Vector3( 0.00f,  0.75f, 0), Color.White),
                    new VertexPositionColor(new Vector3( 0.75f, -0.75f, 0), Color.White),
                });
                indices.SetData(new ushort[] { 0, 1, 2 });

                effect.World = Matrix.Identity;
                effect.View = Matrix.Identity;
                effect.Projection = Matrix.Identity;
                effect.VertexColorEnabled = true;

                gd.SetRenderTarget(renderTarget);
                gd.Clear(Color.Black);
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.None;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.SetVertexBuffer(vertices);
                gd.Indices = indices;

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 1, 2);
                }

                gd.SetRenderTarget(null);
                var center = new Color[1];
                renderTarget.GetData(0, new Rectangle(16, 16, 1, 1), center, 0, 1);
                Assert.AreEqual(Color.White, center[0]);
            }

            var original = gd.PresentationParameters.Clone();
            var resized = original.Clone();
            resized.BackBufferWidth += 17;
            resized.BackBufferHeight += 11;

            try
            {
                gd.Reset(resized);
                Assert.AreEqual(resized.BackBufferWidth, gd.PresentationParameters.BackBufferWidth);
                Assert.AreEqual(resized.BackBufferHeight, gd.PresentationParameters.BackBufferHeight);
                Assert.AreEqual(resized.BackBufferWidth, gd.Viewport.Width);
                Assert.AreEqual(resized.BackBufferHeight, gd.Viewport.Height);

                gd.Clear(Color.CornflowerBlue);
                var pixel = new Color[1];
                gd.GetBackBufferData(new Rectangle(gd.Viewport.Width / 2, gd.Viewport.Height / 2, 1, 1), pixel, 0, 1);
                Assert.AreEqual(Color.CornflowerBlue, pixel[0]);
                Assert.DoesNotThrow(() => gd.Present());
            }
            finally
            {
                gd.Reset(original);
            }
        }

        [Test]
        public void NativeWindowClientResizeUpdatesPresentation()
        {
            var originalWidth = gd.PresentationParameters.BackBufferWidth;
            var originalHeight = gd.PresentationParameters.BackBufferHeight;
            var resizedWidth = originalWidth + 17;
            var resizedHeight = originalHeight + 11;
            var clientSizeChangedCount = 0;
            EventHandler<EventArgs> handler = (_, _) => ++clientSizeChangedCount;
            game.Window.ClientSizeChanged += handler;

            try
            {
                ((NativeGameWindow)game.Window).ClientResize(resizedWidth, resizedHeight);

                Assert.AreEqual(1, clientSizeChangedCount);
                Assert.AreEqual(resizedWidth, game.Window.ClientBounds.Width);
                Assert.AreEqual(resizedHeight, game.Window.ClientBounds.Height);
                Assert.AreEqual(resizedWidth, gd.PresentationParameters.BackBufferWidth);
                Assert.AreEqual(resizedHeight, gd.PresentationParameters.BackBufferHeight);
                Assert.AreEqual(resizedWidth, gd.Viewport.Width);
                Assert.AreEqual(resizedHeight, gd.Viewport.Height);

                gd.Clear(Color.CornflowerBlue);
                var pixel = new Color[1];
                gd.GetBackBufferData(
                    new Rectangle(resizedWidth / 2, resizedHeight / 2, 1, 1),
                    pixel,
                    0,
                    1);
                Assert.AreEqual(Color.CornflowerBlue, pixel[0]);
                Assert.DoesNotThrow(() => gd.Present());
            }
            finally
            {
                game.Window.ClientSizeChanged -= handler;
                ((NativeGameWindow)game.Window).ClientResize(originalWidth, originalHeight);
            }
        }

        [Test]
        public void SpriteBatchPreservesTopLeftOrientationAndPremultipliedAlpha()
        {
            var expected = new[]
            {
                Color.Red, Color.Lime,
                Color.Blue, Color.Yellow,
            };

            using (var source = new Texture2D(gd, 2, 2, false, SurfaceFormat.Color))
            using (var target = new RenderTarget2D(
                gd, 2, 2, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents))
            using (var spriteBatch = new SpriteBatch(gd))
            {
                source.SetData(expected);

                gd.SetRenderTarget(target);
                gd.Clear(Color.Black);
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise);
                spriteBatch.Draw(source, new Rectangle(0, 0, 2, 2), Color.White);
                spriteBatch.End();
                gd.SetRenderTarget(null);

                var targetPixels = new Color[expected.Length];
                target.GetData(targetPixels);
                CollectionAssert.AreEqual(expected, targetPixels, "render-target SpriteBatch orientation");

                gd.Clear(Color.Black);
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise);
                spriteBatch.Draw(source, new Rectangle(0, 0, 2, 2), Color.White);
                spriteBatch.End();

                var backBufferPixels = new Color[expected.Length];
                gd.GetBackBufferData(new Rectangle(0, 0, 2, 2), backBufferPixels, 0, backBufferPixels.Length);
                CollectionAssert.AreEqual(expected, backBufferPixels, "backbuffer SpriteBatch orientation");
            }

            using (var source = new Texture2D(gd, 2, 1, false, SurfaceFormat.Color))
            using (var target = new RenderTarget2D(
                gd, 2, 1, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents))
            using (var spriteBatch = new SpriteBatch(gd))
            {
                source.SetData(new[] { new Color(128, 0, 0, 128), Color.Transparent });
                gd.SetRenderTarget(target);
                gd.Clear(Color.Lime);
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise);
                spriteBatch.Draw(source, new Rectangle(0, 0, 2, 1), Color.White);
                spriteBatch.End();
                gd.SetRenderTarget(null);

                var pixels = new Color[2];
                target.GetData(pixels);
                Assert.That(pixels[0].R, Is.EqualTo(128).Within(1));
                Assert.That(pixels[0].G, Is.EqualTo(127).Within(1));
                Assert.That(pixels[0].B, Is.Zero);
                Assert.That(pixels[0].A, Is.EqualTo(255).Within(1));
                Assert.AreEqual(Color.Lime, pixels[1], "fully transparent texel must preserve the destination");
            }
        }

        [Test]
        public void HalfFloatMsaaResolvePreservesHdrColor()
        {
            using (var source = new Texture2D(gd, 2, 1, false, SurfaceFormat.Color))
            using (var hdr = new RenderTarget2D(
                gd, 4, 2, false, SurfaceFormat.HdrBlendable, DepthFormat.Depth24,
                4, RenderTargetUsage.DiscardContents))
            using (var presentation = new RenderTarget2D(
                gd, 4, 2, false, SurfaceFormat.Color, DepthFormat.None,
                0, RenderTargetUsage.PreserveContents))
            using (var spriteBatch = new SpriteBatch(gd))
            {
                Assert.Greater(hdr.MultiSampleCount, 1);
                source.SetData(new[]
                {
                    new Color(48, 255, 64),
                    new Color(240, 32, 160),
                });

                gd.SetRenderTarget(hdr);
                gd.Clear(Color.Black);
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);
                spriteBatch.Draw(source, new Rectangle(0, 0, 4, 2), Color.White);
                spriteBatch.End();

                gd.SetRenderTarget(presentation);
                gd.Clear(Color.Black);
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);
                spriteBatch.Draw(hdr, new Rectangle(0, 0, 4, 2), Color.White);
                spriteBatch.End();
                gd.SetRenderTarget(null);

                var pixels = new Color[8];
                presentation.GetData(pixels);
                for (var y = 0; y < 2; ++y)
                {
                    Assert.That(pixels[y * 4 + 0].R, Is.EqualTo(48).Within(1));
                    Assert.That(pixels[y * 4 + 0].G, Is.EqualTo(255).Within(1));
                    Assert.That(pixels[y * 4 + 0].B, Is.EqualTo(64).Within(1));
                    Assert.That(pixels[y * 4 + 3].R, Is.EqualTo(240).Within(1));
                    Assert.That(pixels[y * 4 + 3].G, Is.EqualTo(32).Within(1));
                    Assert.That(pixels[y * 4 + 3].B, Is.EqualTo(160).Within(1));
                }
            }
        }

        [Test]
        public void TextureCubeUploadsAndSamplesAllFaces()
        {
            var faces = new[]
            {
                CubeMapFace.PositiveX,
                CubeMapFace.NegativeX,
                CubeMapFace.PositiveY,
                CubeMapFace.NegativeY,
                CubeMapFace.PositiveZ,
                CubeMapFace.NegativeZ,
            };
            var expected = new[]
            {
                Color.Red,
                Color.Lime,
                Color.Blue,
                Color.Yellow,
                Color.Cyan,
                Color.Magenta,
            };

            using (var cube = new TextureCube(gd, 2, false, SurfaceFormat.Color))
            using (var target = new RenderTarget2D(
                gd, 6, 1, false, SurfaceFormat.Color, DepthFormat.None,
                0, RenderTargetUsage.PreserveContents))
            using (var effect = new Effect(
                gd,
                File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "NativeOpenGLCube.mgfxo"))))
            {
                for (var faceIndex = 0; faceIndex < faces.Length; ++faceIndex)
                {
                    var source = new Color[4];
                    Array.Fill(source, expected[faceIndex]);
                    cube.SetData(faces[faceIndex], source);

                    var uploaded = new Color[4];
                    cube.GetData(faces[faceIndex], uploaded);
                    CollectionAssert.AreEqual(source, uploaded, $"cube upload {faces[faceIndex]}");
                }

                effect.Parameters["CubeTexture"].SetValue(cube);
                gd.SetRenderTarget(target);
                gd.Clear(Color.Black);
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.None;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        new[]
                        {
                            new VertexPosition(new Vector3(-1, -1, 0)),
                            new VertexPosition(new Vector3(-1,  3, 0)),
                            new VertexPosition(new Vector3( 3, -1, 0)),
                        },
                        0,
                        1);
                }
                gd.SetRenderTarget(null);

                var actual = new Color[6];
                target.GetData(actual);
                CollectionAssert.AreEqual(expected, actual, "cube sampling order");
            }
        }

        [Test]
        public void HdrRenderTargetCubeRendersAndSamplesAllFaces()
        {
            var faces = new[]
            {
                CubeMapFace.PositiveX,
                CubeMapFace.NegativeX,
                CubeMapFace.PositiveY,
                CubeMapFace.NegativeY,
                CubeMapFace.PositiveZ,
                CubeMapFace.NegativeZ,
            };
            var expected = new[]
            {
                Color.Red,
                Color.Lime,
                Color.Blue,
                Color.Yellow,
                Color.Cyan,
                Color.Magenta,
            };

            using (var cube = new RenderTargetCube(
                gd, 2, false, SurfaceFormat.HdrBlendable, DepthFormat.None))
            using (var target = new RenderTarget2D(
                gd, 6, 1, false, SurfaceFormat.Color, DepthFormat.None,
                0, RenderTargetUsage.PreserveContents))
            using (var effect = new Effect(
                gd,
                File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "NativeOpenGLCube.mgfxo"))))
            {
                for (var faceIndex = 0; faceIndex < faces.Length; ++faceIndex)
                {
                    gd.SetRenderTarget(cube, faces[faceIndex]);
                    gd.Clear(expected[faceIndex]);

                    var rendered = new HalfVector4[4];
                    cube.GetData(faces[faceIndex], rendered);
                    foreach (var pixel in rendered)
                    {
                        var renderedValue = pixel.ToVector4();
                        var wanted = expected[faceIndex].ToVector4();
                        Assert.That(renderedValue.X, Is.EqualTo(wanted.X).Within(0.002f));
                        Assert.That(renderedValue.Y, Is.EqualTo(wanted.Y).Within(0.002f));
                        Assert.That(renderedValue.Z, Is.EqualTo(wanted.Z).Within(0.002f));
                        Assert.That(renderedValue.W, Is.EqualTo(wanted.W).Within(0.002f));
                    }
                }

                effect.Parameters["CubeTexture"].SetValue(cube);
                gd.SetRenderTarget(target);
                gd.Clear(Color.Black);
                gd.BlendState = BlendState.Opaque;
                gd.DepthStencilState = DepthStencilState.None;
                gd.RasterizerState = RasterizerState.CullNone;
                gd.SamplerStates[1] = SamplerState.LinearClamp;
                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawUserPrimitives(
                        PrimitiveType.TriangleList,
                        new[]
                        {
                            new VertexPosition(new Vector3(-1, -1, 0)),
                            new VertexPosition(new Vector3(-1,  3, 0)),
                            new VertexPosition(new Vector3( 3, -1, 0)),
                        },
                        0,
                        1);
                }
                gd.SetRenderTarget(null);

                var actual = new Color[6];
                target.GetData(actual);
                CollectionAssert.AreEqual(expected, actual, "HDR render-target cube sampling order");
            }
        }

        [Test]
        public void ClearIgnoresAndRestoresWriteMasksAndScissorState()
        {
            using (var target = new RenderTarget2D(
                gd, 8, 8, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8,
                0, RenderTargetUsage.PreserveContents))
            using (var source = new Texture2D(gd, 1, 1, false, SurfaceFormat.Color))
            using (var spriteBatch = new SpriteBatch(gd))
            using (var redOnly = new BlendState { ColorWriteChannels = ColorWriteChannels.Red })
            using (var scissored = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true })
            {
                source.SetData(new[] { Color.White });
                gd.SetRenderTarget(target);
                gd.Clear(Color.Black);
                gd.ScissorRectangle = new Rectangle(0, 0, 2, 2);

                // Force the restricted masks and scissor into native GL state.
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    redOnly,
                    SamplerState.PointClamp,
                    DepthStencilState.DepthRead,
                    scissored);
                spriteBatch.Draw(source, new Rectangle(0, 0, 8, 8), Color.White);
                spriteBatch.End();

                for (var frame = 0; frame < 32; ++frame)
                {
                    var clearColor = (frame & 1) == 0 ? Color.CornflowerBlue : Color.MonoGameOrange;
                    gd.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, clearColor, 1f, 0);

                    var pixels = new Color[64];
                    target.GetData(pixels);
                    var expectedClear = new Color[64];
                    Array.Fill(expectedClear, clearColor);
                    CollectionAssert.AreEqual(
                        expectedClear,
                        pixels,
                        $"full clear on frame {frame}");
                }

                // Clear must also restore the state that the managed device
                // still considers current.  Only the red channel inside the
                // 2x2 scissor may change on this draw.
                spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    redOnly,
                    SamplerState.PointClamp,
                    DepthStencilState.DepthRead,
                    scissored);
                spriteBatch.Draw(source, new Rectangle(0, 0, 8, 8), Color.White);
                spriteBatch.End();

                var restoredPixels = new Color[64];
                target.GetData(restoredPixels);
                var background = Color.MonoGameOrange;
                var changedPixels = 0;
                foreach (var pixel in restoredPixels)
                {
                    if (pixel == background)
                        continue;

                    ++changedPixels;
                    Assert.AreEqual(
                        new Color(255, (int)background.G, (int)background.B, (int)background.A),
                        pixel,
                        "restored red-only write mask");
                }
                Assert.AreEqual(4, changedPixels, "restored 2x2 scissor");
                gd.SetRenderTarget(null);
            }
        }
    }
}
#endif
