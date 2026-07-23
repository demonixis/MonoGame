// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;
using Android.Content;
using Android.Runtime;

namespace Microsoft.Xna.Framework
{
    internal sealed class MonoGameAndroidOpenGlesGameView : MonoGameAndroidGameView
    {
        private IntPtr _nativeWindow;
        internal override IntPtr NativeWindowHandle => _nativeWindow;

        internal MonoGameAndroidOpenGlesGameView(Context context, AndroidGameWindow gameWindow, Game game)
            : base(context, gameWindow, game) { }

        public override void MakeCurrent() { }
        public override void ClearCurrent() { }
        public override void SwapBuffers() { }

        protected override void CreateGLContext()
        {
            lostglContext = false;
            glContextAvailable = true;
        }

        protected override void DestroyGLContext() { glContextAvailable = false; }

        protected override void CreateGLSurface()
        {
            if (glSurfaceAvailable)
                return;
            var surface = Holder.Surface;
            if (surface == null || !surface.IsValid)
                return;
            _nativeWindow = ANativeWindow_fromSurface(JNIEnv.Handle, surface.Handle);
            if (_nativeWindow == IntPtr.Zero)
                throw new InvalidOperationException("ANativeWindow_fromSurface failed for Native GLES.");
            glSurfaceAvailable = true;
            if (_game.GraphicsDevice != null)
            {
                _game.GraphicsDevice.ResumePresentation(_nativeWindow);
                _game.graphicsDeviceManager.ResetClientBounds();
            }
        }

        protected override void DestroyGLSurface()
        {
            if (!glSurfaceAvailable && _nativeWindow == IntPtr.Zero)
                return;
            if (_game.GraphicsDevice != null)
                _game.GraphicsDevice.SuspendPresentation();
            glSurfaceAvailable = false;
            if (_nativeWindow != IntPtr.Zero)
            {
                ANativeWindow_release(_nativeWindow);
                _nativeWindow = IntPtr.Zero;
            }
        }

        [DllImport("android", EntryPoint = "ANativeWindow_fromSurface", ExactSpelling = true)]
        private static extern IntPtr ANativeWindow_fromSurface(IntPtr environment, IntPtr surface);
        [DllImport("android", EntryPoint = "ANativeWindow_release", ExactSpelling = true)]
        private static extern void ANativeWindow_release(IntPtr window);
    }
}
