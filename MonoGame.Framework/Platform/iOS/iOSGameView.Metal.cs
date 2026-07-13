// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Xna.Framework.Graphics;
using ObjCRuntime;
using UIKit;

namespace Microsoft.Xna.Framework
{
    [Register("iOSGameView")]
    partial class iOSGameView : UIView
    {
        private readonly iOSGamePlatform _platform;
        private bool _isDisposed;

        public iOSGameView(iOSGamePlatform platform, CGRect frame)
            : base(frame)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
#if !TVOS
            MultipleTouchEnabled = true;
#endif
            Opaque = true;
            ContentScaleFactor = UIScreen.MainScreen.Scale;
            MetalLayer.ContentsScale = ContentScaleFactor;
            MetalLayer.FramebufferOnly = false;
        }

        [Export("layerClass")]
        public static Class GetLayerClass()
        {
            return new Class(typeof(CAMetalLayer));
        }

        internal CAMetalLayer MetalLayer
        {
            get { return (CAMetalLayer)base.Layer; }
        }

        public bool IsDisposed
        {
            get { return _isDisposed; }
        }

        public override bool CanBecomeFirstResponder
        {
            get { return true; }
        }

        [Export("doTick")]
        private void DoTick()
        {
            _platform.Tick();
        }

        // Metal command buffers are presented by GraphicsDevice.
        public void Present()
        {
        }

        // Kept as a compatibility hook for the existing UIKit run loop.
        public void MakeCurrent()
        {
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            var scale = Window?.Screen?.Scale ?? UIScreen.MainScreen.Scale;
            ContentScaleFactor = scale;
            MetalLayer.ContentsScale = scale;
            MetalLayer.DrawableSize = new CGSize(
                Math.Max(Bounds.Width * scale, 1),
                Math.Max(Bounds.Height * scale, 1));

            var service = _platform.Game.Services.GetService(typeof(IGraphicsDeviceService)) as IGraphicsDeviceService;
            if (service?.GraphicsDevice == null)
                return;

            var presentation = service.GraphicsDevice.PresentationParameters;
            var width = (int)Math.Round(MetalLayer.DrawableSize.Width);
            var height = (int)Math.Round(MetalLayer.DrawableSize.Height);
            if (presentation.BackBufferWidth == width && presentation.BackBufferHeight == height)
                return;

            presentation.BackBufferWidth = width;
            presentation.BackBufferHeight = height;
            presentation.DeviceWindowHandle = MetalLayer.Handle;
            service.GraphicsDevice.Reset(presentation);
        }

        protected override void Dispose(bool disposing)
        {
            _isDisposed = true;
            base.Dispose(disposing);
        }
    }
}
