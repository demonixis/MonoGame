// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Describes the quality of a tracked spatial pose on visionOS.
    /// </summary>
    public enum VisionOSTrackingState
    {
        /// <summary>
        /// Tracking is unavailable.
        /// </summary>
        Unavailable = 0,

        /// <summary>
        /// Only orientation is currently tracked.
        /// </summary>
        OrientationOnly = 1,

        /// <summary>
        /// Position and orientation are currently tracked.
        /// </summary>
        Tracked = 2
    }
}
