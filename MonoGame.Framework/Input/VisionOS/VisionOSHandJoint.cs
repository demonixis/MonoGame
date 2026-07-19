// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Contains one tracked visionOS hand joint in the session-origin space.
    /// </summary>
    public readonly struct VisionOSHandJoint
    {
        internal VisionOSHandJoint(VisionOSHandJointKind kind, bool isTracked, Matrix originFromJoint)
        {
            Kind = kind;
            IsTracked = isTracked;
            OriginFromJoint = originFromJoint;
        }

        /// <summary>
        /// Gets the joint identifier.
        /// </summary>
        public VisionOSHandJointKind Kind { get; }

        /// <summary>
        /// Gets whether this joint has a valid tracked pose.
        /// </summary>
        public bool IsTracked { get; }

        /// <summary>
        /// Gets the transform from joint space to the current ARKit session-origin space.
        /// Translation is expressed in meters.
        /// </summary>
        public Matrix OriginFromJoint { get; }
    }
}
