// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Contains tracking metadata for one visionOS hand.
    /// </summary>
    public readonly struct VisionOSHandState
    {
        internal VisionOSHandState(
            VisionOSHandedness handedness,
            VisionOSTrackingState trackingState,
            VisionOSHandFidelity fidelity,
            Matrix originFromHand,
            int jointCount)
        {
            Handedness = handedness;
            TrackingState = trackingState;
            Fidelity = fidelity;
            OriginFromHand = originFromHand;
            JointCount = jointCount;
        }

        /// <summary>
        /// Gets the hand represented by this state.
        /// </summary>
        public VisionOSHandedness Handedness { get; }

        /// <summary>
        /// Gets the current tracking state.
        /// </summary>
        public VisionOSTrackingState TrackingState { get; }

        /// <summary>
        /// Gets the current hand-tracking fidelity.
        /// </summary>
        public VisionOSHandFidelity Fidelity { get; }

        /// <summary>
        /// Gets the transform from the ARKit hand anchor to the session-origin space.
        /// Translation is expressed in meters.
        /// </summary>
        public Matrix OriginFromHand { get; }

        /// <summary>
        /// Gets the number of joints copied to the corresponding destination span.
        /// </summary>
        public int JointCount { get; }
    }
}
