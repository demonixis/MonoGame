# visionOS fully immersive phase

This document defines the additive visionOS phase for the native Metal fork.
The selected application mode is a fully immersive SwiftUI `ImmersiveSpace`
backed by Compositor Services. It does not add SDL2 to visionOS and does not
change the existing macOS, iOS, OpenGL, or Vulkan packages.

## Current status

| Area | Status |
| --- | --- |
| Public `VisionOSInput` API | Implemented and covered by managed contract tests |
| Head pose and predicted display time | Implemented in the ARKit bridge |
| Per-eye view/projection/viewport contract | Implemented; the future presenter must publish drawable views |
| Left/right 27-joint hand skeletons | Implemented in the ARKit bridge |
| Direct and indirect pinch interactions | Implemented in the SwiftUI spatial-event host |
| Native input XCFramework | Builds for xros arm64 and xrsimulator arm64/x64 |
| SwiftUI fully immersive host source | Typechecks against the xros SDK |
| Compositor Services Metal presenter/render loop | Not implemented |
| Managed visionOS TFM, runtime, and package | Blocked: .NET workload set 10.0.300 exposes no visionOS workload |
| visionOS Metal shader payload/content ID | Not defined; do not reuse macOS or iOS IDs |
| Linked simulator or device application | Not available; no visual or hardware qualification has occurred |

The checked-in Swift host is source for the eventual platform head, not a
claim that a runnable MonoGame application exists. Its retained
`LayerRenderer` is handed to the future `MGV_Host_AttachLayerRenderer`
presenter entry point, which is intentionally not implemented in this phase.

## Architecture

```text
SwiftUI ImmersiveSpace
  -> CompositorLayer / LayerRenderer
     -> future backend-neutral MGG immersive presenter
        -> shared native Metal renderer

ARKit world + hand providers ----+
SwiftUI spatial events ----------+-> atomic native MGV_InputState
Compositor drawable views -------+             |
                                                v
                                      VisionOSInput.TryGetState
```

Tracking is sampled for the compositor-predicted display time. Views and
interactions are published into the same native snapshot, and managed code
copies the complete snapshot under one lock into caller-owned spans. No array
or collection is allocated by `TryGetState`.

## Managed API

The platform-specific API lives under `Microsoft.Xna.Framework.Input`:

- `VisionOSInput.GetCapabilities()` reports the active host features.
- `VisionOSInput.TryGetState(...)` returns one coherent head, hand, view, and
  interaction snapshot.
- Each hand exposes exactly 27 append-only `VisionOSHandJointKind` values.
- `VisionOSInteraction` represents privacy-preserving direct or indirect pinch
  events supplied by the system. It does not expose raw eye-gaze data.
- `VisionOSViewState` contains each compositor view, projection, viewport,
  texture index, and texture-array slice needed by an immersive renderer.

Example game-side polling:

```csharp
Span<VisionOSHandJoint> left =
    stackalloc VisionOSHandJoint[VisionOSInput.HandJointCount];
Span<VisionOSHandJoint> right =
    stackalloc VisionOSHandJoint[VisionOSInput.HandJointCount];
Span<VisionOSInteraction> interactions =
    stackalloc VisionOSInteraction[VisionOSInput.MaximumInteractionCount];

if (VisionOSInput.TryGetState(left, right, interactions, out var state))
{
    Matrix headFromOrigin = state.HeadView;
    Matrix leftViewProjection = state.LeftView.ViewProjection;

    for (var index = 0; index < state.LeftHand.JointCount; ++index)
    {
        if (left[index].IsTracked)
        {
            Matrix originFromJoint = left[index].OriginFromJoint;
        }
    }
}
```

All poses use a right-handed session-origin space with positive X to the
right, positive Y up, and positive Z backward; translations are meters. Apple
column-vector matrices are transposed into MonoGame's row-vector `Matrix`
field convention by the native bridge. `ViewProjection` is consequently
`View * Projection`.

`TryGetState` returns `false` and clears all destinations on unsupported
platforms or before the native host has published its first frame. This keeps
shared game code portable without advertising unavailable capabilities.

## Build and validation

The managed solution is a .NET 10 contract lane, not a visionOS application
TFM. Run it from the pinned directory:

```sh
cd scripts/apple-metal
dotnet build ../../MonoGame.Framework.VisionOS.slnx -c Release
dotnet run --project ../../Tests/VisionOSInputContract/VisionOSInputContract.csproj -c Release
```

The expected managed marker is:

```text
VISIONOS_INPUT_CONTRACT pass
```

Build the native input bridge and typecheck the host from the repository root:

```sh
native/monogame/visionos/build-input-xcframework.sh Release
native/monogame/visionos/typecheck-host.sh
```

The expected native markers are:

```text
VISIONOS_INPUT_XCFRAMEWORK pass ...
VISIONOS_HOST_TYPECHECK pass
```

The XCFramework contains an xros arm64 static library and a universal
xrsimulator arm64/x64 static library. These build results validate source and
ABI shape only; they are not simulator presentation or Vision Pro evidence.

## Required next gates

1. Implement an internal Compositor Services presenter that consumes the
   `LayerRenderer`, negotiates layered or dedicated stereo textures, publishes
   drawable views, drives the Metal render loop, and handles unavailable
   drawables without blocking.
2. Add the managed visionOS project, RIDs, AOT/trimming, application lifecycle,
   and package only when an official .NET workload defines their contract.
3. Append a unique content platform ID and shader profile after Apple Metal
   Shader Converter has a validated visionOS device/simulator target. Never
   reuse `MacOSMetal` or `iOSMetal` payloads.
4. Link a minimal fully immersive application and validate launch, head pose,
   hand privacy authorization, direct/indirect pinch, view matrices, frame
   pacing, suspend/resume, clean shutdown, and temporary drawable loss.
5. Run simulator automation and a signed Apple Vision Pro smoke test with
   Metal API/GPU validation, then audit symbols and dependencies for the
   absence of Vulkan, MoltenVK, and SPIR-V components.

No visionOS package or preview designation is permitted before gates 1 through
3 are complete. No hardware-qualified claim is permitted before gate 5 passes.
