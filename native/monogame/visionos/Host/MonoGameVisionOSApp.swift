// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

import CompositorServices
import Metal
import Spatial
import SwiftUI

@main
struct MonoGameVisionOSApp: App {
    @State private var immersionStyle: ImmersionStyle = .full

    var body: some Scene {
        ImmersiveSpace {
            CompositorLayer(configuration: MonoGameLayerConfiguration()) { layerRenderer in
                layerRenderer.onSpatialEvent = publishSpatialEvents
                let handle = Unmanaged.passRetained(layerRenderer).toOpaque()
                MGV_Host_AttachLayerRenderer(handle)
            }
        }
        .immersionStyle(selection: $immersionStyle, in: .full)
        .upperLimbVisibility(.hidden)
    }
}

@MainActor
private struct MonoGameLayerConfiguration: CompositorLayerConfiguration {
    func makeConfiguration(
        capabilities: LayerRenderer.Capabilities,
        configuration: inout LayerRenderer.Configuration
    ) {
        // Keep system-selected formats until the MonoGame immersive presenter
        // negotiates its color, depth, foveation, and stereo-layout contract.
    }
}

@MainActor
private func publishSpatialEvents(_ events: SpatialEventCollection) {
    var nativeEvents: [MGV_Interaction] = []
    nativeEvents.reserveCapacity(min(events.count, Int(MGV_MAX_INTERACTION_COUNT)))

    for event in events.prefix(Int(MGV_MAX_INTERACTION_COUNT)) {
        var nativeEvent = MGV_Interaction()
        nativeEvent.Id = UInt64(bitPattern: Int64(event.id.hashValue))
        nativeEvent.Timestamp = event.timestamp
        nativeEvent.Kind = interactionKind(event.kind)
        nativeEvent.Phase = interactionPhase(event.phase)
        nativeEvent.Handedness = handedness(event.chirality)

        if let ray = event.selectionRay {
            nativeEvent.HasSelectionRay = true
            nativeEvent.SelectionRayOrigin = vector(
                x: ray.origin.x,
                y: ray.origin.y,
                z: ray.origin.z)
            nativeEvent.SelectionRayDirection = vector(
                x: ray.direction.x,
                y: ray.direction.y,
                z: ray.direction.z)
        }

        if let pose = event.inputDevicePose?.pose3D {
            nativeEvent.HasManipulatorPose = true
            nativeEvent.OriginFromManipulator = matrix(pose.matrix)
        }

        nativeEvents.append(nativeEvent)
    }

    nativeEvents.withUnsafeBufferPointer { buffer in
        MGV_Input_PublishInteractions(buffer.baseAddress, Int32(buffer.count))
    }
}

private func interactionKind(_ kind: SpatialEventCollection.Event.Kind) -> Int32 {
    switch kind {
    case .directPinch:
        return Int32(MGV_InteractionKind_DirectPinch.rawValue)
    case .indirectPinch:
        return Int32(MGV_InteractionKind_IndirectPinch.rawValue)
    default:
        return Int32(MGV_InteractionKind_Unknown.rawValue)
    }
}

private func interactionPhase(_ phase: SpatialEventCollection.Event.Phase) -> Int32 {
    switch phase {
    case .active:
        return Int32(MGV_InteractionPhase_Active.rawValue)
    case .ended:
        return Int32(MGV_InteractionPhase_Ended.rawValue)
    case .cancelled:
        return Int32(MGV_InteractionPhase_Cancelled.rawValue)
    @unknown default:
        return Int32(MGV_InteractionPhase_Cancelled.rawValue)
    }
}

private func handedness(_ chirality: Chirality?) -> Int32 {
    switch chirality {
    case .left:
        return Int32(MGV_Handedness_Left.rawValue)
    case .right:
        return Int32(MGV_Handedness_Right.rawValue)
    case nil:
        return Int32(MGV_Handedness_None.rawValue)
    }
}

private func vector(x: Double, y: Double, z: Double) -> MGV_Vector3 {
    return MGV_Vector3(X: Float(x), Y: Float(y), Z: Float(z))
}

private func matrix(_ source: simd_double4x4) -> MGV_Matrix {
    var destination = MGV_Matrix()
    destination.M11 = Float(source.columns.0.x)
    destination.M12 = Float(source.columns.0.y)
    destination.M13 = Float(source.columns.0.z)
    destination.M14 = Float(source.columns.0.w)
    destination.M21 = Float(source.columns.1.x)
    destination.M22 = Float(source.columns.1.y)
    destination.M23 = Float(source.columns.1.z)
    destination.M24 = Float(source.columns.1.w)
    destination.M31 = Float(source.columns.2.x)
    destination.M32 = Float(source.columns.2.y)
    destination.M33 = Float(source.columns.2.z)
    destination.M34 = Float(source.columns.2.w)
    destination.M41 = Float(source.columns.3.x)
    destination.M42 = Float(source.columns.3.y)
    destination.M43 = Float(source.columns.3.z)
    destination.M44 = Float(source.columns.3.w)
    return destination
}
