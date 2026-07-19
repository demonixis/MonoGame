// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#pragma once

#include <stdbool.h>
#include <stdint.h>

#if defined(__cplusplus)
extern "C" {
#endif

#if defined(__GNUC__)
#define MGV_EXPORT __attribute__((visibility("default")))
#else
#define MGV_EXPORT
#endif

enum
{
    MGV_HAND_JOINT_COUNT = 27,
    MGV_MAX_INTERACTION_COUNT = 32,
    MGV_MAX_VIEW_COUNT = 2,
};

typedef enum MGV_TrackingState
{
    MGV_TrackingState_Unavailable = 0,
    MGV_TrackingState_OrientationOnly = 1,
    MGV_TrackingState_Tracked = 2,
} MGV_TrackingState;

typedef enum MGV_Handedness
{
    MGV_Handedness_None = 0,
    MGV_Handedness_Left = 1,
    MGV_Handedness_Right = 2,
} MGV_Handedness;

typedef enum MGV_HandFidelity
{
    MGV_HandFidelity_Unavailable = 0,
    MGV_HandFidelity_Nominal = 1,
    MGV_HandFidelity_High = 2,
} MGV_HandFidelity;

typedef enum MGV_InteractionKind
{
    MGV_InteractionKind_Unknown = 0,
    MGV_InteractionKind_DirectPinch = 1,
    MGV_InteractionKind_IndirectPinch = 2,
} MGV_InteractionKind;

typedef enum MGV_InteractionPhase
{
    MGV_InteractionPhase_Active = 0,
    MGV_InteractionPhase_Ended = 1,
    MGV_InteractionPhase_Cancelled = 2,
} MGV_InteractionPhase;

typedef struct MGV_Vector3
{
    float X;
    float Y;
    float Z;
} MGV_Vector3;

// Apple matrices use column vectors. Keeping the columns in this field order
// transposes them for MonoGame's row-vector Matrix convention without changing
// the represented transform.
typedef struct MGV_Matrix
{
    float M11;
    float M12;
    float M13;
    float M14;
    float M21;
    float M22;
    float M23;
    float M24;
    float M31;
    float M32;
    float M33;
    float M34;
    float M41;
    float M42;
    float M43;
    float M44;
} MGV_Matrix;

typedef struct MGV_Rectangle
{
    int32_t X;
    int32_t Y;
    int32_t Width;
    int32_t Height;
} MGV_Rectangle;

typedef struct MGV_HandJoint
{
    int32_t Kind;
    bool IsTracked;
    uint8_t Reserved[3];
    MGV_Matrix OriginFromJoint;
} MGV_HandJoint;

typedef struct MGV_HandState
{
    int32_t Handedness;
    int32_t TrackingState;
    int32_t Fidelity;
    int32_t JointCount;
    MGV_Matrix OriginFromHand;
} MGV_HandState;

typedef struct MGV_ViewState
{
    int32_t Eye;
    bool IsTracked;
    uint8_t Reserved[3];
    MGV_Matrix OriginFromEye;
    MGV_Matrix View;
    MGV_Matrix Projection;
    MGV_Rectangle Viewport;
    int32_t TextureIndex;
    int32_t TextureSlice;
} MGV_ViewState;

typedef struct MGV_Interaction
{
    uint64_t Id;
    double Timestamp;
    int32_t Kind;
    int32_t Phase;
    int32_t Handedness;
    bool HasSelectionRay;
    bool HasManipulatorPose;
    bool HasTarget;
    uint8_t Reserved;
    MGV_Vector3 SelectionRayOrigin;
    MGV_Vector3 SelectionRayDirection;
    MGV_Matrix OriginFromManipulator;
    uint64_t TargetId;
} MGV_Interaction;

typedef struct MGV_InputCapabilities
{
    bool IsSupported;
    bool SupportsHeadTracking;
    bool SupportsHandTracking;
    bool SupportsInteractions;
    int32_t ViewCount;
    int32_t HandJointCount;
    int32_t MaximumInteractionCount;
} MGV_InputCapabilities;

typedef struct MGV_InputState
{
    uint64_t FrameId;
    double Timestamp;
    double PredictedDisplayTime;
    int32_t HeadTrackingState;
    int32_t ViewCount;
    int32_t InteractionCount;
    int32_t Reserved;
    MGV_Matrix OriginFromHead;
    MGV_Matrix HeadView;
    MGV_ViewState Views[MGV_MAX_VIEW_COUNT];
    MGV_HandState Hands[2];
    MGV_HandJoint HandJoints[2][MGV_HAND_JOINT_COUNT];
    MGV_Interaction Interactions[MGV_MAX_INTERACTION_COUNT];
} MGV_InputState;

// Starts the ARKit world- and hand-tracking providers. The application must
// include NSHandsTrackingUsageDescription before requesting hand tracking.
MGV_EXPORT bool MGV_Input_Start(void);
MGV_EXPORT void MGV_Input_Stop(void);
MGV_EXPORT MGV_InputCapabilities MGV_Input_GetCapabilities(void);

// Samples head and hand tracking at the compositor prediction time. The time
// values use mach absolute time expressed in seconds.
MGV_EXPORT bool MGV_Input_UpdateTracking(
    uint64_t frameId,
    double timestamp,
    double predictedDisplayTime);

// Publishes per-eye matrices supplied by the Compositor Services drawable.
MGV_EXPORT void MGV_Input_PublishViews(const MGV_ViewState* views, int32_t viewCount);

// Replaces the active SwiftUI spatial-event collection atomically.
MGV_EXPORT void MGV_Input_PublishInteractions(
    const MGV_Interaction* interactions,
    int32_t interactionCount);

// Copies one complete snapshot. The destination is never partially updated.
MGV_EXPORT bool MGV_Input_CopyState(MGV_InputState* state);

#if defined(__cplusplus)
}
#endif
