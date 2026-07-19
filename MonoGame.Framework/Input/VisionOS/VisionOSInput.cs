// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Threading;

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Provides atomic, allocation-free access to visionOS head, hand, view, and interaction input.
    /// </summary>
    public static class VisionOSInput
    {
        /// <summary>
        /// The number of joints in each visionOS hand skeleton.
        /// </summary>
        public const int HandJointCount = 27;

        /// <summary>
        /// The maximum number of interactions copied in one snapshot.
        /// </summary>
        public const int MaximumInteractionCount = 32;

        /// <summary>
        /// The maximum number of compositor views in one snapshot.
        /// </summary>
        public const int MaximumViewCount = 2;

        private static readonly IVisionOSInputSource _unsupportedSource = new UnsupportedVisionOSInputSource();
#if VISIONOS
        private static IVisionOSInputSource _source = new NativeVisionOSInputSource();
#else
        private static IVisionOSInputSource _source = _unsupportedSource;
#endif

        /// <summary>
        /// Gets the capabilities of the current visionOS input source.
        /// </summary>
        public static VisionOSInputCapabilities GetCapabilities()
        {
            return Volatile.Read(ref _source).GetCapabilities();
        }

        /// <summary>
        /// Copies one coherent platform snapshot into caller-owned storage.
        /// </summary>
        /// <param name="leftHandJoints">Storage for all 27 left-hand joints.</param>
        /// <param name="rightHandJoints">Storage for all 27 right-hand joints.</param>
        /// <param name="interactions">Storage for up to 32 current spatial interactions.</param>
        /// <param name="state">The metadata, head pose, and stereo view state for the copied frame.</param>
        /// <returns><see langword="true"/> when a complete snapshot is available.</returns>
        public static bool TryGetState(
            Span<VisionOSHandJoint> leftHandJoints,
            Span<VisionOSHandJoint> rightHandJoints,
            Span<VisionOSInteraction> interactions,
            out VisionOSInputState state)
        {
            if (leftHandJoints.Length < HandJointCount)
                throw new ArgumentException($"The destination must hold at least {HandJointCount} joints.", nameof(leftHandJoints));
            if (rightHandJoints.Length < HandJointCount)
                throw new ArgumentException($"The destination must hold at least {HandJointCount} joints.", nameof(rightHandJoints));
            if (interactions.Length < MaximumInteractionCount)
                throw new ArgumentException($"The destination must hold at least {MaximumInteractionCount} interactions.", nameof(interactions));

            return Volatile.Read(ref _source).TryCopyState(
                leftHandJoints,
                rightHandJoints,
                interactions,
                out state);
        }

        internal static IVisionOSInputSource Source
        {
            get { return Volatile.Read(ref _source); }
            set { Volatile.Write(ref _source, value ?? _unsupportedSource); }
        }
    }

    internal interface IVisionOSInputSource
    {
        VisionOSInputCapabilities GetCapabilities();

        bool TryCopyState(
            Span<VisionOSHandJoint> leftHandJoints,
            Span<VisionOSHandJoint> rightHandJoints,
            Span<VisionOSInteraction> interactions,
            out VisionOSInputState state);
    }

    internal sealed class UnsupportedVisionOSInputSource : IVisionOSInputSource
    {
        public VisionOSInputCapabilities GetCapabilities()
        {
            return default;
        }

        public bool TryCopyState(
            Span<VisionOSHandJoint> leftHandJoints,
            Span<VisionOSHandJoint> rightHandJoints,
            Span<VisionOSInteraction> interactions,
            out VisionOSInputState state)
        {
            leftHandJoints.Clear();
            rightHandJoints.Clear();
            interactions.Clear();
            state = default;
            return false;
        }
    }
}
