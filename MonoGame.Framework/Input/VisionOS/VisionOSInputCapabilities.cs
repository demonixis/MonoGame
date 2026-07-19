// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Describes the visionOS spatial input features available to the current host.
    /// </summary>
    public readonly struct VisionOSInputCapabilities
    {
        internal VisionOSInputCapabilities(
            bool isSupported,
            bool supportsHeadTracking,
            bool supportsHandTracking,
            bool supportsInteractions,
            int viewCount,
            int handJointCount,
            int maximumInteractionCount)
        {
            IsSupported = isSupported;
            SupportsHeadTracking = supportsHeadTracking;
            SupportsHandTracking = supportsHandTracking;
            SupportsInteractions = supportsInteractions;
            ViewCount = viewCount;
            HandJointCount = handJointCount;
            MaximumInteractionCount = maximumInteractionCount;
        }

        /// <summary>
        /// Gets whether the current platform host supplies visionOS spatial input.
        /// </summary>
        public bool IsSupported { get; }

        /// <summary>
        /// Gets whether head poses are available.
        /// </summary>
        public bool SupportsHeadTracking { get; }

        /// <summary>
        /// Gets whether articulated hand skeletons are available.
        /// </summary>
        public bool SupportsHandTracking { get; }

        /// <summary>
        /// Gets whether system-resolved spatial interactions are available.
        /// </summary>
        public bool SupportsInteractions { get; }

        /// <summary>
        /// Gets the number of compositor views supplied by the host.
        /// </summary>
        public int ViewCount { get; }

        /// <summary>
        /// Gets the number of joints supplied for each hand.
        /// </summary>
        public int HandJointCount { get; }

        /// <summary>
        /// Gets the maximum number of interactions in one snapshot.
        /// </summary>
        public int MaximumInteractionCount { get; }
    }
}
