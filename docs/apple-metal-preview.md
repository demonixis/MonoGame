# Apple Metal backend preview

This document tracks the additive native Metal backend. It is intentionally
separate from the existing Vulkan, DirectX, and OpenGL backends.

## Scope

- macOS 15 or later through SDL2 and a native `CAMetalLayer` presenter.
- iOS and iPadOS 18 or later through UIKit and a native `CAMetalLayer`.
- macOS universal runtime (`arm64` and `x86_64`).
- iOS device `arm64` plus simulator `arm64` and `x86_64` in an XCFramework.
- A dedicated .NET SDK 10.0.300 lane under `scripts/apple-metal`.
- HLSL shader input compiled to DXIL and converted with Apple's Metal Shader
  Converter. Vulkan, MoltenVK, and SPIR-V are not part of this lane.

visionOS phase 1 is in progress. The repository now has a .NET 10 input-contract
solution, public head/hand/interaction APIs, an ARKit bridge that builds device
and simulator slices, and a typechecked SwiftUI fully immersive host. The
Compositor Services presenter, render loop, managed visionOS runtime/package,
shader payload, and simulator/device smoke tests are not implemented yet.
Workload set 10.0.300 currently exposes no visionOS workload, so the repository
does not invent a `net10.0-visionos` TFM. The detailed contract and commands are
in [`visionos-fully-immersive.md`](visionos-fully-immersive.md).

## Build

Install Xcode 26.3, .NET SDK 10.0.300 with workload set 10.0.300 for `macos`
and `ios`, Premake 5, and Apple's Metal Shader Converter. This keeps the Apple
lane on the exact .NET feature band selected for this fork. The Apple projects
target platform version 26.2, matching the 26.2.10233 packs carried by workload
set 10.0.300 and Xcode 26.3.

From `native/monogame`, generate and build only the Metal desktop runtime:

```sh
metal/build-sdl-macos.sh Release
premake5 --backend=metal gmake
make config=release
```

Build the iOS XCFramework from the repository root:

```sh
native/monogame/metal/build-ios-xcframework.sh Release
```

Run managed builds from `scripts/apple-metal` so its `global.json` pins SDK
10.0.300 and workload set 10.0.300. The helper scripts change to that
directory themselves, so they can also be invoked safely from the repository
root:

```sh
dotnet build ../../MonoGame.Framework/MonoGame.Framework.MacOS.Metal.csproj -c Release
dotnet build ../../MonoGame.Framework/MonoGame.Framework.iOS.Metal.csproj -c Release
dotnet build ../../Tools/MonoGame.Effect.Compiler/MonoGame.Effect.Compiler.csproj -c Release
./run-macos-smoke.sh Release osx-arm64
./run-ios-simulator-smoke.sh Release iossimulator-arm64
```

After installing Apple Metal Shader Converter, generate the six native stock
effect headers before producing a release runtime:

```sh
scripts/apple-metal/build-stock-effects.sh
```

The generated shader payload stores the converter reflection JSON next to the
entry point and metallib. The runtime parses `TopLevelArgumentBuffer` and
encodes reflected CBV, SRV, sampler, and descriptor-table entries at their
declared offsets. This metadata is part of the runtime binding contract and
must not be stripped from MGFX output.

`MacOSMetal` targets a macOS 15 metallib. `iOSMetal` emits an MTL3 universal
payload containing both an iOS 18 device metallib and an iOS 18 simulator
metallib; the native runtime selects the matching payload at load time. The
reflection JSON must be identical for both iOS variants, otherwise the content
build fails. This preserves one serialized iOS platform identifier without
attempting to load a macOS metallib on iOS.

`AppleMetalSmoke` opens an SDL2 Metal window and exits after three presented
frames. Before presentation it validates texture upload/readback, a depth-backed
MSAA render target and resolve, a converted `SpriteEffect` draw with texture and
sampler bindings, and RGBA backbuffer readback. This covers device creation, the
`CAMetalLayer` drawable path, native Metal vertex fetch, indexed draw arguments,
argument-buffer resources, synchronous resource reads, presentation, and clean
shutdown.
`AppleMetalPackageSmoke` is built only after packing and loads the adapter from
the produced macOS NuGet package, proving that an external consumer resolves
the bundled native runtime without a source-project or manual native reference.
`AppleMetalSmoke.iOS` reuses the same pixel-checked scenario in an iPhone
simulator. Its runner enables Metal API/GPU validation and requires the explicit
three-frame success marker because `simctl launch` does not propagate an
application crash as its own exit code.

## Current implementation

The native backend implements device and adapter discovery, swapchain and
drawable management, frame pacing, render passes, state objects, buffers,
textures, render targets, input layouts, queries, and draw entry points. The
managed targets, content-platform identifiers, package assets, and Apple CI
lane are also present. CPU-visible resources use shared storage on Apple GPUs
and managed storage plus explicit synchronization on Intel/AMD macOS devices;
the universal runtime is therefore not merely cross-compiled for x86_64.

Metallibs produced by Metal Shader Converter now use the official reserved
binding contract: descriptor heaps at buffer indices 0 and 1, the top-level
argument buffer at 2, draw parameters at 4 and 5, and Metal stage-in attributes
starting at 11. Reflection-driven direct resources and descriptor tables are
both supported. Small per-frame shared binding arenas keep indirect resource
data alive until submitted GPU work completes.

MSAA support and BC/S3TC compression are queried from the selected `MTLDevice`;
ETC2 and ASTC are exposed only for Apple GPU families. macOS display modes come
from CoreGraphics and iOS physical display dimensions come from the UIKit host,
keeping UIKit out of the shared Objective-C++ renderer.

The backend is now a functioning shader-rendering preview. The following gates
remain before treating the packages as production-qualified:

- Provision the separately downloaded official converter on the Apple CI
  runner. CI now calls `build-stock-effects.sh` before either native runtime is
  built and fails instead of publishing a package without the six effects.
- Run the full six-effect matrix and representative custom effects with Metal
  API validation. `SpriteEffect`, CBV/SRV/sampler binding, indexed rendering,
  MSAA resolve, and readback are covered by the automated macOS smoke.
- Run the shader smoke on a physical iOS arm64 device with a signed build.
- Complete lifecycle qualification: resize/fullscreen on macOS, and an iOS
  device run covering AOT/trimming, rotation, background/foreground, memory
  pressure, and drawable loss/recovery.

Apple's converter is a separately downloaded developer tool and is not
redistributed by this repository. A missing converter produces an actionable
effect-compiler error rather than falling back to Vulkan or SPIR-V.

## Required gates

- `dotnet build` succeeds for the macOS Metal and iOS Metal framework projects.
- The native macOS dylib contains both `arm64` and `x86_64` slices and has no
  Vulkan or MoltenVK dependency.
- The iOS XCFramework contains `ios-arm64` and
  `ios-arm64_x86_64-simulator` libraries.
- The six stock effects are generated before macOS/iOS native compilation; a
  release lane without `metal-shaderconverter` fails before packaging.
- The macOS smoke and iOS simulator smoke both pass with Metal API/GPU
  validation and the platform-specific metallib selected.
- Apple Metal content enum values and XNB identifiers stay append-only.
- The remaining stock-effect, lifecycle, and hardware gates above are complete
  before enabling Metal templates or making Metal the default on any Apple
  platform.
- visionOS remains excluded from packages and release jobs until the native
  presenter, official managed workload, shaders, lifecycle, and hardware gates
  in `visionos-fully-immersive.md` are complete.
