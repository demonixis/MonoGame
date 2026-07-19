// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Contains one atomic visionOS tracking and interaction snapshot.
    /// </summary>
    public readonly struct VisionOSInputState
    {
        internal VisionOSInputState(
            ulong frameId,
            double timestamp,
            double predictedDisplayTime,
            VisionOSTrackingState headTrackingState,
            Matrix originFromHead,
            Matrix headView,
            VisionOSViewState leftView,
            VisionOSViewState rightView,
            int viewCount,
            VisionOSHandState leftHand,
            VisionOSHandState rightHand,
            int interactionCount)
        {
            FrameId = frameId;
            Timestamp = timestamp;
            PredictedDisplayTime = predictedDisplayTime;
            HeadTrackingState = headTrackingState;
            OriginFromHead = originFromHead;
            HeadView = headView;
            LeftView = leftView;
            RightView = rightView;
            ViewCount = viewCount;
            LeftHand = leftHand;
            RightHand = rightHand;
            InteractionCount = interactionCount;
        }

        /// <summary>
        /// Gets the monotonically increasing native frame identifier.
        /// </summary>
        public ulong FrameId { get; }

        /// <summary>
        /// Gets the snapshot timestamp in seconds on the platform monotonic clock.
        /// </summary>
        public double Timestamp { get; }

        /// <summary>
        /// Gets the compositor-predicted presentation time on the same monotonic clock.
        /// </summary>
        public double PredictedDisplayTime { get; }

        /// <summary>
        /// Gets the current head-tracking state.
        /// </summary>
        public VisionOSTrackingState HeadTrackingState { get; }

        /// <summary>
        /// Gets the transform from head/device space to the current ARKit session-origin space.
        /// </summary>
        public Matrix OriginFromHead { get; }

        /// <summary>
        /// Gets the inverse of <see cref="OriginFromHead"/>.
        /// </summary>
        public Matrix HeadView { get; }

        /// <summary>
        /// Gets the current left-eye compositor view.
        /// </summary>
        public VisionOSViewState LeftView { get; }

        /// <summary>
        /// Gets the current right-eye compositor view.
        /// </summary>
        public VisionOSViewState RightView { get; }

        /// <summary>
        /// Gets the number of valid compositor views in this snapshot.
        /// </summary>
        public int ViewCount { get; }

        /// <summary>
        /// Gets the left-hand tracking metadata.
        /// </summary>
        public VisionOSHandState LeftHand { get; }

        /// <summary>
        /// Gets the right-hand tracking metadata.
        /// </summary>
        public VisionOSHandState RightHand { get; }

        /// <summary>
        /// Gets the number of interactions copied to the destination span.
        /// </summary>
        public int InteractionCount { get; }
    }
}
