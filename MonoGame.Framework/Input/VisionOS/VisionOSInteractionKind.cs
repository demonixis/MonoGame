// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Identifies a visionOS spatial interaction.
    /// </summary>
    public enum VisionOSInteractionKind
    {
        /// <summary>
        /// An interaction kind not recognized by this version of MonoGame.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A direct pinch on content at the hand position.
        /// </summary>
        DirectPinch = 1,

        /// <summary>
        /// An indirect gaze-and-pinch selection resolved by visionOS.
        /// </summary>
        IndirectPinch = 2
    }
}
