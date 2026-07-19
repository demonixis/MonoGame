// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Contains the compositor-provided state for one visionOS view.
    /// </summary>
    public readonly struct VisionOSViewState
    {
        internal VisionOSViewState(
            VisionOSEye eye,
            bool isTracked,
            Matrix originFromEye,
            Matrix view,
            Matrix projection,
            Rectangle viewport,
            int textureIndex,
            int textureSlice)
        {
            Eye = eye;
            IsTracked = isTracked;
            OriginFromEye = originFromEye;
            View = view;
            Projection = projection;
            Viewport = viewport;
            TextureIndex = textureIndex;
            TextureSlice = textureSlice;
        }

        /// <summary>
        /// Gets the eye represented by this view.
        /// </summary>
        public VisionOSEye Eye { get; }

        /// <summary>
        /// Gets whether the compositor supplied a valid pose for this view.
        /// </summary>
        public bool IsTracked { get; }

        /// <summary>
        /// Gets the transform from eye space to the current session-origin space.
        /// </summary>
        public Matrix OriginFromEye { get; }

        /// <summary>
        /// Gets the right-handed view matrix for MonoGame row-vector multiplication.
        /// </summary>
        public Matrix View { get; }

        /// <summary>
        /// Gets the Metal depth-range projection matrix supplied for this drawable.
        /// </summary>
        public Matrix Projection { get; }

        /// <summary>
        /// Gets the viewport inside the compositor texture.
        /// </summary>
        public Rectangle Viewport { get; }

        /// <summary>
        /// Gets the compositor color/depth texture index for dedicated layouts.
        /// </summary>
        public int TextureIndex { get; }

        /// <summary>
        /// Gets the texture-array slice for layered layouts.
        /// </summary>
        public int TextureSlice { get; }

        /// <summary>
        /// Gets the combined view-projection matrix.
        /// </summary>
        public Matrix ViewProjection
        {
            get { return View * Projection; }
        }
    }
}
