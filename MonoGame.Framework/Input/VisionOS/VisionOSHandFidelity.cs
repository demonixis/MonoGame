// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Describes the fidelity of a visionOS hand sample.
    /// </summary>
    public enum VisionOSHandFidelity
    {
        /// <summary>
        /// No fidelity is available because the hand is not tracked.
        /// </summary>
        Unavailable = 0,

        /// <summary>
        /// The sample has nominal fidelity.
        /// </summary>
        Nominal = 1,

        /// <summary>
        /// The sample has high fidelity.
        /// </summary>
        High = 2
    }
}
