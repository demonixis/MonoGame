// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NUnit.Framework;

namespace MonoGame.Tests.Framework
{
    [TestFixture]
    [NonParallelizable]
    class VisionOSInputTest
    {
        [TearDown]
        public void TearDown()
        {
            VisionOSInput.Source = null;
        }

        [Test]
        public void UnsupportedPlatformsReturnNoState()
        {
            var left = new VisionOSHandJoint[VisionOSInput.HandJointCount];
            var right = new VisionOSHandJoint[VisionOSInput.HandJointCount];
            var interactions = new VisionOSInteraction[VisionOSInput.MaximumInteractionCount];

            Assert.False(VisionOSInput.GetCapabilities().IsSupported);
            Assert.False(VisionOSInput.TryGetState(left, right, interactions, out var state));
            Assert.AreEqual(0, state.FrameId);
            Assert.AreEqual(VisionOSTrackingState.Unavailable, state.HeadTrackingState);
        }

        [Test]
        public void HandJointIdentifiersMatchTheNativeContract()
        {
            Assert.AreEqual(27, VisionOSInput.HandJointCount);
            Assert.AreEqual(8760, NativeVisionOSInputSource.NativeStateSize);
            Assert.AreEqual(0, (int)VisionOSHandJointKind.Wrist);
            Assert.AreEqual(4, (int)VisionOSHandJointKind.ThumbTip);
            Assert.AreEqual(9, (int)VisionOSHandJointKind.IndexFingerTip);
            Assert.AreEqual(14, (int)VisionOSHandJointKind.MiddleFingerTip);
            Assert.AreEqual(19, (int)VisionOSHandJointKind.RingFingerTip);
            Assert.AreEqual(24, (int)VisionOSHandJointKind.LittleFingerTip);
            Assert.AreEqual(26, (int)VisionOSHandJointKind.ForearmArm);
        }

        [Test]
        public void CopiesOneCoherentSnapshotIntoCallerOwnedBuffers()
        {
            VisionOSInput.Source = new TestVisionOSInputSource();
            var left = new VisionOSHandJoint[VisionOSInput.HandJointCount];
            var right = new VisionOSHandJoint[VisionOSInput.HandJointCount];
            var interactions = new VisionOSInteraction[VisionOSInput.MaximumInteractionCount];

            Assert.True(VisionOSInput.TryGetState(left, right, interactions, out var state));

            Assert.AreEqual(42, state.FrameId);
            Assert.AreEqual(2, state.ViewCount);
            Assert.AreEqual(VisionOSTrackingState.Tracked, state.HeadTrackingState);
            Assert.AreEqual(new Vector3(1, 2, 3), state.OriginFromHead.Translation);
            Assert.AreEqual(VisionOSHandJointKind.Wrist, left[0].Kind);
            Assert.AreEqual(VisionOSHandJointKind.ForearmArm, right[26].Kind);
            Assert.AreEqual(VisionOSHandedness.Left, state.LeftHand.Handedness);
            Assert.AreEqual(VisionOSHandedness.Right, state.RightHand.Handedness);
            Assert.AreEqual(1, state.InteractionCount);
            Assert.AreEqual(7, interactions[0].Id);
            Assert.True(interactions[0].HasSelectionRay);
            Assert.AreEqual(Vector3.Forward, interactions[0].SelectionRayDirection);
        }

        [Test]
        public void RequiresCompleteHandAndInteractionStorage()
        {
            var joints = new VisionOSHandJoint[VisionOSInput.HandJointCount];
            var shortJoints = new VisionOSHandJoint[VisionOSInput.HandJointCount - 1];
            var interactions = new VisionOSInteraction[VisionOSInput.MaximumInteractionCount];
            var shortInteractions = new VisionOSInteraction[VisionOSInput.MaximumInteractionCount - 1];

            Assert.Throws<ArgumentException>(() => VisionOSInput.TryGetState(shortJoints, joints, interactions, out _));
            Assert.Throws<ArgumentException>(() => VisionOSInput.TryGetState(joints, shortJoints, interactions, out _));
            Assert.Throws<ArgumentException>(() => VisionOSInput.TryGetState(joints, joints, shortInteractions, out _));
        }

        private sealed class TestVisionOSInputSource : IVisionOSInputSource
        {
            public VisionOSInputCapabilities GetCapabilities()
            {
                return new VisionOSInputCapabilities(true, true, true, true, 2, 27, 32);
            }

            public bool TryCopyState(
                Span<VisionOSHandJoint> leftHandJoints,
                Span<VisionOSHandJoint> rightHandJoints,
                Span<VisionOSInteraction> interactions,
                out VisionOSInputState state)
            {
                for (var index = 0; index < VisionOSInput.HandJointCount; ++index)
                {
                    var kind = (VisionOSHandJointKind)index;
                    leftHandJoints[index] = new VisionOSHandJoint(kind, true, Matrix.CreateTranslation(index, 0, 0));
                    rightHandJoints[index] = new VisionOSHandJoint(kind, true, Matrix.CreateTranslation(-index, 0, 0));
                }

                interactions.Clear();
                interactions[0] = new VisionOSInteraction(
                    7,
                    1.25,
                    VisionOSInteractionKind.IndirectPinch,
                    VisionOSInteractionPhase.Active,
                    VisionOSHandedness.Right,
                    true,
                    Vector3.Zero,
                    Vector3.Forward,
                    true,
                    Matrix.Identity,
                    true,
                    99);

                var leftView = new VisionOSViewState(
                    VisionOSEye.Left,
                    true,
                    Matrix.CreateTranslation(-0.032f, 0, 0),
                    Matrix.Identity,
                    Matrix.Identity,
                    new Rectangle(0, 0, 1024, 1024),
                    0,
                    0);
                var rightView = new VisionOSViewState(
                    VisionOSEye.Right,
                    true,
                    Matrix.CreateTranslation(0.032f, 0, 0),
                    Matrix.Identity,
                    Matrix.Identity,
                    new Rectangle(0, 0, 1024, 1024),
                    0,
                    1);
                var leftHand = new VisionOSHandState(
                    VisionOSHandedness.Left,
                    VisionOSTrackingState.Tracked,
                    VisionOSHandFidelity.High,
                    Matrix.Identity,
                    27);
                var rightHand = new VisionOSHandState(
                    VisionOSHandedness.Right,
                    VisionOSTrackingState.Tracked,
                    VisionOSHandFidelity.Nominal,
                    Matrix.Identity,
                    27);
                var originFromHead = Matrix.CreateTranslation(1, 2, 3);
                Matrix.Invert(ref originFromHead, out var headView);
                state = new VisionOSInputState(
                    42,
                    1.0,
                    1.02,
                    VisionOSTrackingState.Tracked,
                    originFromHead,
                    headView,
                    leftView,
                    rightView,
                    2,
                    leftHand,
                    rightHand,
                    1);
                return true;
            }
        }
    }
}
