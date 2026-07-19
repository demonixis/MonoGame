// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Contains one system-resolved visionOS spatial interaction.
    /// </summary>
    public readonly struct VisionOSInteraction
    {
        internal VisionOSInteraction(
            ulong id,
            double timestamp,
            VisionOSInteractionKind kind,
            VisionOSInteractionPhase phase,
            VisionOSHandedness handedness,
            bool hasSelectionRay,
            Vector3 selectionRayOrigin,
            Vector3 selectionRayDirection,
            bool hasManipulatorPose,
            Matrix originFromManipulator,
            bool hasTarget,
            ulong targetId)
        {
            Id = id;
            Timestamp = timestamp;
            Kind = kind;
            Phase = phase;
            Handedness = handedness;
            HasSelectionRay = hasSelectionRay;
            SelectionRayOrigin = selectionRayOrigin;
            SelectionRayDirection = selectionRayDirection;
            HasManipulatorPose = hasManipulatorPose;
            OriginFromManipulator = originFromManipulator;
            HasTarget = hasTarget;
            TargetId = targetId;
        }

        /// <summary>
        /// Gets the interaction identifier. It remains stable for the interaction lifecycle.
        /// </summary>
        public ulong Id { get; }

        /// <summary>
        /// Gets the event timestamp in seconds on the platform monotonic clock.
        /// </summary>
        public double Timestamp { get; }

        /// <summary>
        /// Gets the kind of spatial interaction.
        /// </summary>
        public VisionOSInteractionKind Kind { get; }

        /// <summary>
        /// Gets the current interaction phase.
        /// </summary>
        public VisionOSInteractionPhase Phase { get; }

        /// <summary>
        /// Gets the hand associated with this interaction, when known.
        /// </summary>
        public VisionOSHandedness Handedness { get; }

        /// <summary>
        /// Gets whether the system supplied a privacy-preserving selection ray.
        /// </summary>
        public bool HasSelectionRay { get; }

        /// <summary>
        /// Gets the selection-ray origin in session-origin space, in meters.
        /// </summary>
        public Vector3 SelectionRayOrigin { get; }

        /// <summary>
        /// Gets the normalized selection-ray direction in session-origin space.
        /// </summary>
        public Vector3 SelectionRayDirection { get; }

        /// <summary>
        /// Gets whether a tracked manipulator pose is available.
        /// </summary>
        public bool HasManipulatorPose { get; }

        /// <summary>
        /// Gets the transform from manipulator space to session-origin space.
        /// </summary>
        public Matrix OriginFromManipulator { get; }

        /// <summary>
        /// Gets whether the compositor resolved the interaction to a registered tracking area.
        /// </summary>
        public bool HasTarget { get; }

        /// <summary>
        /// Gets the application-defined identifier of the resolved target.
        /// </summary>
        public ulong TargetId { get; }
    }
}
