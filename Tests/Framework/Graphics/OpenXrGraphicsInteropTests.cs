// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using NUnit.Framework;

namespace MonoGame.Tests.Graphics
{
    internal class OpenXrGraphicsInteropTests
    {
        [Test]
        public void BindingLayoutIsStableForTheNativeAbi()
        {
            Assert.AreEqual(IntPtr.Size == 8 ? 104 : 56, Marshal.SizeOf<OpenXrGraphicsBinding>());
            Assert.AreEqual(1, (int)OpenXrGraphicsApi.OpenGL);
            Assert.AreEqual(2, (int)OpenXrGraphicsApi.Vulkan);
            Assert.AreEqual(3, (int)OpenXrGraphicsApi.Direct3D12);
            Assert.AreEqual(4, (int)OpenXrGraphicsApi.Metal);
        }

        [Test]
        public void ManagedBoundaryRejectsInvalidOwnershipInputsBeforeNativeCalls()
        {
            Assert.Throws<ArgumentNullException>(() => OpenXrGraphicsInterop.GetBinding(null));
            Assert.Throws<ArgumentNullException>(() => OpenXrGraphicsInterop.SubmitWithoutPresent(null));
            Assert.Throws<ArgumentNullException>(() => OpenXrGraphicsInterop.PrepareForRuntimeRelease(null));
            Assert.Throws<ArgumentNullException>(() => OpenXrGraphicsInterop.WrapExternalRenderTarget(null, new IntPtr(1), 1, 1, SurfaceFormat.Color));
        }
    }
}
