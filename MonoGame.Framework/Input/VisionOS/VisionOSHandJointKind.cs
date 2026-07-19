// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

namespace Microsoft.Xna.Framework.Input
{
    /// <summary>
    /// Identifies one of the 27 joints in an ARKit visionOS hand skeleton.
    /// Values are append-only and match the native bridge contract.
    /// </summary>
    public enum VisionOSHandJointKind
    {
        /// <summary>The wrist joint.</summary>
        Wrist = 0,
        /// <summary>The thumb knuckle joint.</summary>
        ThumbKnuckle = 1,
        /// <summary>The base of the thumb intermediate segment.</summary>
        ThumbIntermediateBase = 2,
        /// <summary>The tip of the thumb intermediate segment.</summary>
        ThumbIntermediateTip = 3,
        /// <summary>The thumb tip joint.</summary>
        ThumbTip = 4,
        /// <summary>The index-finger metacarpal joint.</summary>
        IndexFingerMetacarpal = 5,
        /// <summary>The index-finger knuckle joint.</summary>
        IndexFingerKnuckle = 6,
        /// <summary>The base of the index-finger intermediate segment.</summary>
        IndexFingerIntermediateBase = 7,
        /// <summary>The tip of the index-finger intermediate segment.</summary>
        IndexFingerIntermediateTip = 8,
        /// <summary>The index-finger tip joint.</summary>
        IndexFingerTip = 9,
        /// <summary>The middle-finger metacarpal joint.</summary>
        MiddleFingerMetacarpal = 10,
        /// <summary>The middle-finger knuckle joint.</summary>
        MiddleFingerKnuckle = 11,
        /// <summary>The base of the middle-finger intermediate segment.</summary>
        MiddleFingerIntermediateBase = 12,
        /// <summary>The tip of the middle-finger intermediate segment.</summary>
        MiddleFingerIntermediateTip = 13,
        /// <summary>The middle-finger tip joint.</summary>
        MiddleFingerTip = 14,
        /// <summary>The ring-finger metacarpal joint.</summary>
        RingFingerMetacarpal = 15,
        /// <summary>The ring-finger knuckle joint.</summary>
        RingFingerKnuckle = 16,
        /// <summary>The base of the ring-finger intermediate segment.</summary>
        RingFingerIntermediateBase = 17,
        /// <summary>The tip of the ring-finger intermediate segment.</summary>
        RingFingerIntermediateTip = 18,
        /// <summary>The ring-finger tip joint.</summary>
        RingFingerTip = 19,
        /// <summary>The little-finger metacarpal joint.</summary>
        LittleFingerMetacarpal = 20,
        /// <summary>The little-finger knuckle joint.</summary>
        LittleFingerKnuckle = 21,
        /// <summary>The base of the little-finger intermediate segment.</summary>
        LittleFingerIntermediateBase = 22,
        /// <summary>The tip of the little-finger intermediate segment.</summary>
        LittleFingerIntermediateTip = 23,
        /// <summary>The little-finger tip joint.</summary>
        LittleFingerTip = 24,
        /// <summary>The forearm joint adjacent to the wrist.</summary>
        ForearmWrist = 25,
        /// <summary>The forearm joint adjacent to the arm.</summary>
        ForearmArm = 26
    }
}
