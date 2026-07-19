// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Describes the lifecycle of a visionOS spatial interaction.
    /// </summary>
    public enum VisionOSInteractionPhase
    {
        /// <summary>
        /// The interaction is active.
        /// </summary>
        Active = 0,

        /// <summary>
        /// The interaction completed normally.
        /// </summary>
        Ended = 1,

        /// <summary>
        /// The system cancelled the interaction.
        /// </summary>
        Cancelled = 2
    }
}
