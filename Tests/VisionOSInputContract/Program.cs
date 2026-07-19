// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Input;

if (!ContractConstantsAreValid())
{
    Console.Error.WriteLine("The managed visionOS input ABI constants changed unexpectedly.");
    return 1;
}

Span<VisionOSHandJoint> leftHand = stackalloc VisionOSHandJoint[VisionOSInput.HandJointCount];
Span<VisionOSHandJoint> rightHand = stackalloc VisionOSHandJoint[VisionOSInput.HandJointCount];
Span<VisionOSInteraction> interactions = stackalloc VisionOSInteraction[VisionOSInput.MaximumInteractionCount];

if (VisionOSInput.TryGetState(leftHand, rightHand, interactions, out _))
{
    Console.Error.WriteLine("The non-visionOS validation host unexpectedly supplied a tracking snapshot.");
    return 1;
}

if (VisionOSInput.GetCapabilities().IsSupported)
{
    Console.Error.WriteLine("The non-visionOS validation host unexpectedly advertised visionOS input.");
    return 1;
}

Console.WriteLine("VISIONOS_INPUT_CONTRACT pass");
return 0;

static bool ContractConstantsAreValid()
{
    return VisionOSInput.HandJointCount == 27 &&
        VisionOSInput.MaximumInteractionCount == 32 &&
        VisionOSInput.MaximumViewCount == 2;
}
