// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "include/MGV_Input.h"

#include <ARKit/ARKit.h>
#include <os/lock.h>
#include <simd/simd.h>

#include <algorithm>
#include <cstring>

namespace
{
    static_assert(sizeof(MGV_Matrix) == sizeof(simd_float4x4));
    static_assert(sizeof(MGV_InputState) == 8760);

    os_unfair_lock gLock = OS_UNFAIR_LOCK_INIT;
    MGV_InputState gState = {};
    ar_session_t gSession = nullptr;
    ar_world_tracking_provider_t gWorldProvider = nullptr;
    ar_hand_tracking_provider_t gHandProvider = nullptr;
    ar_device_anchor_t gDeviceAnchor = nullptr;
    ar_hand_anchor_t gLeftHandAnchor = nullptr;
    ar_hand_anchor_t gRightHandAnchor = nullptr;

    MGV_Matrix CopyMatrix(simd_float4x4 matrix)
    {
        MGV_Matrix result;
        std::memcpy(&result, &matrix, sizeof(result));
        return result;
    }

    MGV_Matrix IdentityMatrix()
    {
        return CopyMatrix(matrix_identity_float4x4);
    }

    void ResetHand(MGV_InputState& state, int index, MGV_Handedness handedness)
    {
        MGV_HandState& hand = state.Hands[index];
        hand.Handedness = handedness;
        hand.TrackingState = MGV_TrackingState_Unavailable;
        hand.Fidelity = MGV_HandFidelity_Unavailable;
        hand.JointCount = MGV_HAND_JOINT_COUNT;
        hand.OriginFromHand = IdentityMatrix();
        for (int jointIndex = 0; jointIndex < MGV_HAND_JOINT_COUNT; ++jointIndex)
        {
            MGV_HandJoint& joint = state.HandJoints[index][jointIndex];
            joint.Kind = jointIndex;
            joint.IsTracked = false;
            joint.OriginFromJoint = IdentityMatrix();
        }
    }

    void ResetTrackingState(MGV_InputState& state)
    {
        state.HeadTrackingState = MGV_TrackingState_Unavailable;
        state.OriginFromHead = IdentityMatrix();
        state.HeadView = IdentityMatrix();
        ResetHand(state, 0, MGV_Handedness_Left);
        ResetHand(state, 1, MGV_Handedness_Right);
    }

    MGV_TrackingState GetDeviceTrackingState(ar_device_anchor_t anchor)
    {
        if (anchor == nullptr || !ar_device_anchor_is_tracked(anchor))
            return MGV_TrackingState_Unavailable;

        switch (ar_device_anchor_get_tracking_state(anchor))
        {
            case ar_device_anchor_tracking_state_orientation_tracked:
                return MGV_TrackingState_OrientationOnly;
            case ar_device_anchor_tracking_state_tracked:
                return MGV_TrackingState_Tracked;
            default:
                return MGV_TrackingState_Unavailable;
        }
    }

    void CopyHand(
        MGV_InputState& state,
        int index,
        MGV_Handedness handedness,
        ar_hand_anchor_t anchor)
    {
        ResetHand(state, index, handedness);
        if (anchor == nullptr || !ar_hand_anchor_is_tracked(anchor))
            return;

        ar_hand_skeleton_t skeleton = ar_hand_anchor_get_hand_skeleton(anchor);
        if (skeleton == nullptr)
            return;

        MGV_HandState& hand = state.Hands[index];
        const simd_float4x4 originFromHand = ar_hand_anchor_get_origin_from_anchor_transform(anchor);
        hand.TrackingState = MGV_TrackingState_Tracked;
        hand.Fidelity = MGV_HandFidelity_Nominal;
        hand.OriginFromHand = CopyMatrix(originFromHand);
        if (@available(visionOS 26.0, *))
        {
            hand.Fidelity = ar_hand_anchor_get_fidelity(anchor) == ar_hand_fidelity_high
                ? MGV_HandFidelity_High
                : MGV_HandFidelity_Nominal;
        }

        for (int jointIndex = 0; jointIndex < MGV_HAND_JOINT_COUNT; ++jointIndex)
        {
            ar_skeleton_joint_t source = ar_hand_skeleton_get_joint_named(
                skeleton,
                static_cast<ar_hand_skeleton_joint_name_t>(jointIndex));
            if (source == nullptr)
                continue;

            MGV_HandJoint& destination = state.HandJoints[index][jointIndex];
            destination.IsTracked = ar_skeleton_joint_is_tracked(source);
            destination.OriginFromJoint = CopyMatrix(
                simd_mul(originFromHand, ar_skeleton_joint_get_anchor_from_joint_transform(source)));
        }
    }

    void ReleaseTrackingObjects()
    {
        if (gSession != nullptr)
        {
            ar_session_stop(gSession);
            gSession = nullptr;
        }
        if (gWorldProvider != nullptr)
            gWorldProvider = nullptr;
        if (gHandProvider != nullptr)
            gHandProvider = nullptr;
        if (gDeviceAnchor != nullptr)
            gDeviceAnchor = nullptr;
        if (gLeftHandAnchor != nullptr)
            gLeftHandAnchor = nullptr;
        if (gRightHandAnchor != nullptr)
            gRightHandAnchor = nullptr;
    }
}

bool MGV_Input_Start(void)
{
    os_unfair_lock_lock(&gLock);
    if (gSession != nullptr)
    {
        os_unfair_lock_unlock(&gLock);
        return true;
    }

    gSession = ar_session_create();
    ar_world_tracking_configuration_t worldConfiguration = ar_world_tracking_configuration_create();
    gWorldProvider = ar_world_tracking_provider_create(worldConfiguration);
    gDeviceAnchor = ar_device_anchor_create();

    ar_data_providers_t providers = ar_data_providers_create();
    ar_data_providers_add_data_provider(providers, gWorldProvider);
    if (ar_hand_tracking_provider_is_supported())
    {
        ar_hand_tracking_configuration_t handConfiguration = ar_hand_tracking_configuration_create();
        gHandProvider = ar_hand_tracking_provider_create(handConfiguration);
        gLeftHandAnchor = ar_hand_anchor_create();
        gRightHandAnchor = ar_hand_anchor_create();
        ar_data_providers_add_data_provider(providers, gHandProvider);
    }

    const bool started = gSession != nullptr && gWorldProvider != nullptr;
    if (started)
        ar_session_run(gSession, providers);
    if (!started)
        ReleaseTrackingObjects();

    ResetTrackingState(gState);
    os_unfair_lock_unlock(&gLock);
    return started;
}

void MGV_Input_Stop(void)
{
    os_unfair_lock_lock(&gLock);
    ReleaseTrackingObjects();
    gState = {};
    ResetTrackingState(gState);
    os_unfair_lock_unlock(&gLock);
}

MGV_InputCapabilities MGV_Input_GetCapabilities(void)
{
    os_unfair_lock_lock(&gLock);
    MGV_InputCapabilities capabilities = {};
    capabilities.IsSupported = gSession != nullptr;
    capabilities.SupportsHeadTracking = gWorldProvider != nullptr;
    capabilities.SupportsHandTracking = gHandProvider != nullptr;
    capabilities.SupportsInteractions = true;
    capabilities.ViewCount = gState.ViewCount;
    capabilities.HandJointCount = MGV_HAND_JOINT_COUNT;
    capabilities.MaximumInteractionCount = MGV_MAX_INTERACTION_COUNT;
    os_unfair_lock_unlock(&gLock);
    return capabilities;
}

bool MGV_Input_UpdateTracking(uint64_t frameId, double timestamp, double predictedDisplayTime)
{
    os_unfair_lock_lock(&gLock);
    if (gSession == nullptr || gWorldProvider == nullptr || gDeviceAnchor == nullptr)
    {
        os_unfair_lock_unlock(&gLock);
        return false;
    }

    gState.FrameId = frameId;
    gState.Timestamp = timestamp;
    gState.PredictedDisplayTime = predictedDisplayTime;
    const ar_device_anchor_query_status_t headStatus =
        ar_world_tracking_provider_query_device_anchor_at_timestamp(
            gWorldProvider,
            predictedDisplayTime,
            gDeviceAnchor);
    if (headStatus == ar_device_anchor_query_status_success)
    {
        const simd_float4x4 originFromHead = ar_device_anchor_get_origin_from_anchor_transform(gDeviceAnchor);
        gState.HeadTrackingState = GetDeviceTrackingState(gDeviceAnchor);
        gState.OriginFromHead = CopyMatrix(originFromHead);
        gState.HeadView = CopyMatrix(simd_inverse(originFromHead));
    }
    else
    {
        gState.HeadTrackingState = MGV_TrackingState_Unavailable;
        gState.OriginFromHead = IdentityMatrix();
        gState.HeadView = IdentityMatrix();
    }

    if (gHandProvider != nullptr &&
        ar_hand_tracking_provider_query_anchors_at_timestamp(
            gHandProvider,
            predictedDisplayTime,
            gLeftHandAnchor,
            gRightHandAnchor) == ar_hand_anchor_query_status_success)
    {
        CopyHand(gState, 0, MGV_Handedness_Left, gLeftHandAnchor);
        CopyHand(gState, 1, MGV_Handedness_Right, gRightHandAnchor);
    }
    else
    {
        ResetHand(gState, 0, MGV_Handedness_Left);
        ResetHand(gState, 1, MGV_Handedness_Right);
    }

    const bool tracked = gState.HeadTrackingState != MGV_TrackingState_Unavailable;
    os_unfair_lock_unlock(&gLock);
    return tracked;
}

void MGV_Input_PublishViews(const MGV_ViewState* views, int32_t viewCount)
{
    os_unfair_lock_lock(&gLock);
    gState.ViewCount = views == nullptr
        ? 0
        : std::clamp(viewCount, int32_t(0), int32_t(MGV_MAX_VIEW_COUNT));
    for (int index = 0; index < gState.ViewCount; ++index)
        gState.Views[index] = views[index];
    for (int index = gState.ViewCount; index < MGV_MAX_VIEW_COUNT; ++index)
        gState.Views[index] = {};
    os_unfair_lock_unlock(&gLock);
}

void MGV_Input_PublishInteractions(const MGV_Interaction* interactions, int32_t interactionCount)
{
    os_unfair_lock_lock(&gLock);
    gState.InteractionCount = interactions == nullptr
        ? 0
        : std::clamp(
            interactionCount,
            int32_t(0),
            int32_t(MGV_MAX_INTERACTION_COUNT));
    for (int index = 0; index < gState.InteractionCount; ++index)
        gState.Interactions[index] = interactions[index];
    for (int index = gState.InteractionCount; index < MGV_MAX_INTERACTION_COUNT; ++index)
        gState.Interactions[index] = {};
    os_unfair_lock_unlock(&gLock);
}

bool MGV_Input_CopyState(MGV_InputState* state)
{
    if (state == nullptr)
        return false;

    os_unfair_lock_lock(&gLock);
    const bool available = gSession != nullptr && gState.FrameId != 0;
    if (available)
        std::memcpy(state, &gState, sizeof(*state));
    else
        std::memset(state, 0, sizeof(*state));
    os_unfair_lock_unlock(&gLock);
    return available;
}
