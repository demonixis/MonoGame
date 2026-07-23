// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Xna.Framework.Graphics;
using ObjCRuntime;
using OpenGLES;
using UIKit;

namespace Microsoft.Xna.Framework
{
    [Register("iOSGameView")]
    partial class iOSGameView : UIView
    {
        private readonly iOSGamePlatform _platform;
        private bool _isDisposed;
        private int _drawableWidth;
        private int _drawableHeight;

        public iOSGameView(iOSGamePlatform platform, CGRect frame)
            : base(frame)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
#if !TVOS
            MultipleTouchEnabled = true;
#endif
            Opaque = true;
            ContentScaleFactor = UIScreen.MainScreen.Scale;
            OpenGlesLayer.ContentsScale = ContentScaleFactor;
            OpenGlesLayer.DrawableProperties = NSDictionary.FromObjectsAndKeys(
                new NSObject[] { NSNumber.FromBoolean(false), EAGLColorFormat.RGBA8 },
                new NSObject[] { EAGLDrawableProperty.RetainedBacking, EAGLDrawableProperty.ColorFormat });
        }

        [Export("layerClass")]
        public static Class GetLayerClass()
        {
            return new Class(typeof(CAEAGLLayer));
        }

        internal CAEAGLLayer OpenGlesLayer => (CAEAGLLayer)base.Layer;

        public bool IsDisposed => _isDisposed;

        public override bool CanBecomeFirstResponder => true;

        [Export("doTick")]
        private void DoTick()
        {
            _platform.Tick();
        }

        // Presentation and context ownership are both in the native C++ host.
        public void Present()
        {
        }

        public void MakeCurrent()
        {
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            var scale = Window?.Screen?.Scale ?? UIScreen.MainScreen.Scale;
            ContentScaleFactor = scale;
            OpenGlesLayer.ContentsScale = scale;

            var width = Math.Max((int)Math.Round(Bounds.Width * scale), 1);
            var height = Math.Max((int)Math.Round(Bounds.Height * scale), 1);
            var drawableSizeChanged = width != _drawableWidth || height != _drawableHeight;
            _drawableWidth = width;
            _drawableHeight = height;

            var service = _platform.Game.Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;
            if (service?.GraphicsDevice == null)
                return;

            var presentation = service.GraphicsDevice.PresentationParameters;
            if (presentation.BackBufferWidth == width && presentation.BackBufferHeight == height)
            {
                if (drawableSizeChanged)
                    _platform.Game.Window.OnClientSizeChanged();
                return;
            }

            presentation.BackBufferWidth = width;
            presentation.BackBufferHeight = height;
            presentation.DeviceWindowHandle = OpenGlesLayer.Handle;
            service.GraphicsDevice.Reset(presentation);
            _platform.Game.Window.OnClientSizeChanged();
        }

        protected override void Dispose(bool disposing)
        {
            _isDisposed = true;
            base.Dispose(disposing);
        }
    }
}
