// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Input
{
    internal sealed unsafe class NativeVisionOSInputSource : IVisionOSInputSource
    {
        internal static int NativeStateSize
        {
            get { return sizeof(NativeInputState); }
        }

        public VisionOSInputCapabilities GetCapabilities()
        {
            var capabilities = NativeMethods.GetCapabilities();
            return new VisionOSInputCapabilities(
                capabilities.IsSupported != 0,
                capabilities.SupportsHeadTracking != 0,
                capabilities.SupportsHandTracking != 0,
                capabilities.SupportsInteractions != 0,
                capabilities.ViewCount,
                capabilities.HandJointCount,
                capabilities.MaximumInteractionCount);
        }

        public bool TryCopyState(
            Span<VisionOSHandJoint> leftHandJoints,
            Span<VisionOSHandJoint> rightHandJoints,
            Span<VisionOSInteraction> interactions,
            out VisionOSInputState state)
        {
            NativeInputState native;
            if (!NativeMethods.CopyState(out native))
            {
                leftHandJoints.Clear();
                rightHandJoints.Clear();
                interactions.Clear();
                state = default;
                return false;
            }

            var joints = (NativeHandJoint*)native.HandJoints;
            for (var index = 0; index < VisionOSInput.HandJointCount; ++index)
            {
                leftHandJoints[index] = ToHandJoint(joints[index]);
                rightHandJoints[index] = ToHandJoint(joints[VisionOSInput.HandJointCount + index]);
            }

            interactions.Clear();
            var interactionCount = Math.Clamp(
                native.InteractionCount,
                0,
                VisionOSInput.MaximumInteractionCount);
            var nativeInteractions = (NativeInteraction*)native.Interactions;
            for (var index = 0; index < interactionCount; ++index)
            {
                interactions[index] = ToInteraction(nativeInteractions[index]);
            }

            var leftHand = ToHandState(native.Hand0);
            var rightHand = ToHandState(native.Hand1);
            var leftView = ToViewState(native.View0);
            var rightView = ToViewState(native.View1);
            state = new VisionOSInputState(
                native.FrameId,
                native.Timestamp,
                native.PredictedDisplayTime,
                (VisionOSTrackingState)native.HeadTrackingState,
                ToMatrix(native.OriginFromHead),
                ToMatrix(native.HeadView),
                leftView,
                rightView,
                Math.Clamp(native.ViewCount, 0, VisionOSInput.MaximumViewCount),
                leftHand,
                rightHand,
                interactionCount);
            return true;
        }

        private static VisionOSHandJoint ToHandJoint(NativeHandJoint value)
        {
            return new VisionOSHandJoint(
                (VisionOSHandJointKind)value.Kind,
                value.IsTracked != 0,
                ToMatrix(value.OriginFromJoint));
        }

        private static VisionOSHandState ToHandState(NativeHandState value)
        {
            return new VisionOSHandState(
                (VisionOSHandedness)value.Handedness,
                (VisionOSTrackingState)value.TrackingState,
                (VisionOSHandFidelity)value.Fidelity,
                ToMatrix(value.OriginFromHand),
                Math.Clamp(value.JointCount, 0, VisionOSInput.HandJointCount));
        }

        private static VisionOSViewState ToViewState(NativeViewState value)
        {
            return new VisionOSViewState(
                (VisionOSEye)value.Eye,
                value.IsTracked != 0,
                ToMatrix(value.OriginFromEye),
                ToMatrix(value.View),
                ToMatrix(value.Projection),
                new Rectangle(
                    value.Viewport.X,
                    value.Viewport.Y,
                    value.Viewport.Width,
                    value.Viewport.Height),
                value.TextureIndex,
                value.TextureSlice);
        }

        private static VisionOSInteraction ToInteraction(NativeInteraction value)
        {
            return new VisionOSInteraction(
                value.Id,
                value.Timestamp,
                (VisionOSInteractionKind)value.Kind,
                (VisionOSInteractionPhase)value.Phase,
                (VisionOSHandedness)value.Handedness,
                value.HasSelectionRay != 0,
                ToVector3(value.SelectionRayOrigin),
                ToVector3(value.SelectionRayDirection),
                value.HasManipulatorPose != 0,
                ToMatrix(value.OriginFromManipulator),
                value.HasTarget != 0,
                value.TargetId);
        }

        private static Vector3 ToVector3(NativeVector3 value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Matrix ToMatrix(NativeMatrix value)
        {
            return new Matrix(
                value.M11,
                value.M12,
                value.M13,
                value.M14,
                value.M21,
                value.M22,
                value.M23,
                value.M24,
                value.M31,
                value.M32,
                value.M33,
                value.M34,
                value.M41,
                value.M42,
                value.M43,
                value.M44);
        }

        private static class NativeMethods
        {
            private const string NativeLibrary = "__Internal";

            [DllImport(NativeLibrary, EntryPoint = "MGV_Input_GetCapabilities", ExactSpelling = true)]
            internal static extern NativeInputCapabilities GetCapabilities();

            [DllImport(NativeLibrary, EntryPoint = "MGV_Input_CopyState", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool CopyState(out NativeInputState state);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeVector3
        {
            public float X;
            public float Y;
            public float Z;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMatrix
        {
            public float M11;
            public float M12;
            public float M13;
            public float M14;
            public float M21;
            public float M22;
            public float M23;
            public float M24;
            public float M31;
            public float M32;
            public float M33;
            public float M34;
            public float M41;
            public float M42;
            public float M43;
            public float M44;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRectangle
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeHandJoint
        {
            public int Kind;
            public byte IsTracked;
            public byte Reserved0;
            public byte Reserved1;
            public byte Reserved2;
            public NativeMatrix OriginFromJoint;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeHandState
        {
            public int Handedness;
            public int TrackingState;
            public int Fidelity;
            public int JointCount;
            public NativeMatrix OriginFromHand;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeViewState
        {
            public int Eye;
            public byte IsTracked;
            public byte Reserved0;
            public byte Reserved1;
            public byte Reserved2;
            public NativeMatrix OriginFromEye;
            public NativeMatrix View;
            public NativeMatrix Projection;
            public NativeRectangle Viewport;
            public int TextureIndex;
            public int TextureSlice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInteraction
        {
            public ulong Id;
            public double Timestamp;
            public int Kind;
            public int Phase;
            public int Handedness;
            public byte HasSelectionRay;
            public byte HasManipulatorPose;
            public byte HasTarget;
            public byte Reserved;
            public NativeVector3 SelectionRayOrigin;
            public NativeVector3 SelectionRayDirection;
            public NativeMatrix OriginFromManipulator;
            public ulong TargetId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInputCapabilities
        {
            public byte IsSupported;
            public byte SupportsHeadTracking;
            public byte SupportsHandTracking;
            public byte SupportsInteractions;
            public int ViewCount;
            public int HandJointCount;
            public int MaximumInteractionCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeInputState
        {
            public ulong FrameId;
            public double Timestamp;
            public double PredictedDisplayTime;
            public int HeadTrackingState;
            public int ViewCount;
            public int InteractionCount;
            public int Reserved;
            public NativeMatrix OriginFromHead;
            public NativeMatrix HeadView;
            public NativeViewState View0;
            public NativeViewState View1;
            public NativeHandState Hand0;
            public NativeHandState Hand1;
            public fixed byte HandJoints[54 * 72];
            public fixed byte Interactions[32 * 128];
        }
    }
}
