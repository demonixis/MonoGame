# Native Apple Metal backend

This document describes the native Metal target in this MonoGame fork,
including its architecture, dependencies, build process, usage, and the work
remaining before production qualification.

The detailed acceptance-status document remains
[`docs/apple-metal-preview.md`](docs/apple-metal-preview.md). If this README
diverges from that document, the status document and the current code take
precedence.

## Status and scope

The Metal backend is an additive preview target. It does not replace the
existing OpenGL or Vulkan backends and does not change their packages.

The fork also exposes an experimental, non-owning OpenXR interop surface used
by C3DE's optional XR module. On macOS Metal, an OpenXR runtime may require the
`MTLDevice` before graphics creation; the backend then creates and exposes its
command queue from that exact device, wraps runtime-owned color textures without
releasing them, and commits work without presenting a desktop drawable. This is
interop source/build scope only and does not qualify a macOS OpenXR runtime.

| Platform | Status | Host | Native artifact | Minimum OS |
| --- | --- | --- | --- | --- |
| Apple Silicon macOS | Functional preview | SDL2 + `CAMetalLayer` | Universal dylib | macOS 15 |
| Intel macOS | Functional preview | SDL2 + `CAMetalLayer` | Universal dylib | macOS 15 |
| iPhone/iPad arm64 | Preview; physical-device qualification incomplete | UIKit + `CAMetalLayer` | Static XCFramework library | iOS/iPadOS 18 |
| iOS simulator arm64/x64 | Functional preview | UIKit + `CAMetalLayer` | Static XCFramework library | iOS/iPadOS 18 |

The current public names are:

- macOS project: `MonoGame.Framework.MacOS.Metal.csproj`;
- iOS project: `MonoGame.Framework.iOS.Metal.csproj`;
- content platforms: `MacOSMetal` and `iOSMetal`;
- shader profiles: `MetalMacOS` and `MetaliOS`;
- native libraries: `mgruntime-metal`;
- packages: `MonoGame.Framework.MacOS.Metal` and
  `MonoGame.Framework.iOS.Metal`.

macOS and iOS remain separate application projects: one binary does not target
both platforms at the same time. A single solution can still share all game
logic through a common library and build both frontends from one solution or
CI command. Each frontend keeps its own TFM, RID, host, and native artifact.

## Non-negotiable rules

- Metal is used directly. This target does not depend on Vulkan, MoltenVK, or
  SPIR-V.
- macOS keeps SDL2 for windowing, input, and the platform loop.
- iOS keeps UIKit, input, audio, and its application lifecycle.
- The shared backend receives a typed presentation surface. No SDL, UIKit, or
  Metal type is exposed through the public XNA/MonoGame API.
- XNB and MGFX identifiers are append-only. They must never be renumbered or
  reused.
- `mgruntime-metal` remains separate from the Vulkan native library so the two
  backends cannot collide in an application or package.

## Architecture

The managed layer reuses the implementations under
`MonoGame.Framework/Platform/Native`. These classes call the backend-neutral C
ABI declared in `native/monogame/include/api_MGG.h`.

The native Metal layer is implemented in Objective-C++ in
`native/monogame/metal/MGG_Metal.mm`. It handles adapters, devices,
presentation surfaces, resources, state objects, shaders, draw calls,
synchronization, queries, readback, and deterministic destruction.

On macOS:

1. SDL2 creates the window with `SDL_WINDOW_METAL`.
2. SDL creates the Metal view and provides its `CAMetalLayer`.
3. The renderer configures the device, command queue, and drawables.
4. The framework retains SDL2 for input and window lifecycle management.

On iOS:

1. `iOSGameView` is a UIKit view backed by a `CAMetalLayer`.
2. UIKit passes the layer handle through the internal presentation surface.
3. The Objective-C++ renderer remains independent of the UIKit host.
4. The view updates the drawable size and resets the device when its size or
   orientation changes.

CPU-visible resources use shared storage on Apple GPUs. On Intel/AMD Macs, the
backend uses managed storage and performs the required explicit
synchronization.

`GraphicsDevice.SupportsCompletedGpuFrameTiming` reports whether the loaded
native ABI can publish completed presentation timings. When supported,
`TryDequeueCompletedGpuFrameTiming(out GpuFrameTiming)` is non-blocking. Metal
assigns one submission identifier per `Present` and publishes
`GPUStartTime`/`GPUEndTime` only from a successfully completed command-buffer
handler. CPU waits, display presentation latency, auxiliary readbacks and
`SubmitWithoutPresent` are outside the interval. The native queue holds 64
results; saturation drops the oldest result and increments the cumulative
`DroppedTimingCount`, which invalidates a performance qualification run.

## Shader pipeline

The only supported compilation path is:

```text
HLSL shader model 6.0 .fx file
  -> DXIL produced by DXC
  -> Apple Metal Shader Converter 4.0
  -> metallib / Metal IR + reflection JSON
  -> MGFX payload
  -> XNB produced by MGCB
```

Metal vertex and pixel shaders must use `vs_6_0` and `ps_6_0`, respectively.
Any other shader model produces an explicit compilation error.

The macOS `MTL2` payload contains one metallib targeting macOS 15. The
universal iOS `MTL3` payload contains two metallibs: one for devices and one
for simulators. The iOS build fails if both variants do not produce exactly
the same reflection layout.

Converter reflection is preserved in MGFX. The runtime uses it to encode CBVs,
SRVs, samplers, descriptor tables, and argument buffers at their official
offsets. This reflection must not be removed, and this pipeline must not be
replaced with a SPIR-V conversion path.

The six stock effects are:

- `AlphaTestEffect`;
- `BasicEffect`;
- `DualTextureEffect`;
- `EnvironmentMapEffect`;
- `SkinnedEffect`;
- `SpriteEffect`.

Their generated headers are written under `native/monogame/metal/macos` and
`native/monogame/metal/ios`. They are ignored build artifacts, not source files
to commit.

## Requirements

The Apple lane must be built on macOS with the following versions and tools:

- Xcode 26.6 with the Apple 26.5 SDKs;
- exact .NET SDK `10.0.302`;
- exact workload set `10.0.302`;
- .NET `macos` and `ios` workloads;
- [Apple Metal Shader Converter 4.0](https://developer.apple.com/metal/shader-converter/)
  available on `PATH`;
- Premake 5;
- CMake;
- Xcode command-line tools: `clang`, `make`, `lipo`, `libtool`, `xcodebuild`,
  `simctl`, `otool`, and `nm`.

Apple Metal Shader Converter is a separately downloaded Apple developer tool.
It is not redistributed in this repository or in MonoGame packages.

The dedicated SDK configuration is stored in
`scripts/apple-metal/global.json`. Do not add a root `global.json` solely for
this preview.

The managed projects use `TargetPlatformVersion` 26.5. The SDK, workload set,
Xcode version, and platform version are a validated set and must not be
upgraded independently.

Verify the environment from the dedicated lane:

```sh
cd scripts/apple-metal

dotnet --version
dotnet workload list
xcodebuild -version
premake5 --version
cmake --version
metal-shaderconverter --version
```

`dotnet --version` must print exactly `10.0.302`. Install the workloads using
the same feature band:

```sh
cd scripts/apple-metal
dotnet workload install macos ios --version 10.0.302
```

## Initialize the checkout

From the repository root:

```sh
git submodule update --init --recursive
```

The `external` directories contain pinned submodules or dependencies. The
build may create untracked directories inside them, such as
`native/monogame/external/faudio/build`. Such a directory makes the submodule
look dirty without changing its checked-out commit.

## Full build from a clean checkout

Unless a section states otherwise, run the following commands from the
repository root.

### 1. Build native dependencies

```sh
cd scripts/apple-metal
dotnet run --project ../../build/Build.csproj -- --target="Build Native Dependencies"
cd ../..
```

This step builds dependencies including SDL2 and FAudio. It does not introduce
Vulkan into the Metal artifact.

### 2. Generate the stock Metal effects

This step must run before compiling release native runtimes:

```sh
scripts/apple-metal/build-stock-effects.sh
```

The script compiles all six effects for macOS and iOS. It fails immediately if
`metal-shaderconverter` is not available on `PATH`.

### 3. Build the universal macOS runtime

```sh
cd native/monogame
metal/build-sdl-macos.sh Release
premake5 --backend=metal gmake
make config=release
cd ../..
```

Expected artifact:

```text
Artifacts/native/mgruntime/desktopmetal/macosx/Release/libmgruntime-metal.dylib
```

The dylib must contain both `arm64` and `x86_64` slices.

### 4. Build the iOS runtime

```sh
native/monogame/metal/build-ios-xcframework.sh Release
```

Expected artifact:

```text
Artifacts/native/mgruntime/iosmetal/Release/MGMetal.xcframework
```

It contains:

- `ios-arm64/libmgruntime-metal.a`;
- `ios-arm64_x86_64-simulator/libmgruntime-metal-simulator.a`.

### 5. Build the managed projects and effect compiler

Run these commands under `scripts/apple-metal` so the SDK and workload pins
are applied:

```sh
cd scripts/apple-metal

dotnet build ../../MonoGame.Framework/MonoGame.Framework.MacOS.Metal.csproj -c Release
dotnet build ../../MonoGame.Framework/MonoGame.Framework.iOS.Metal.csproj -c Release
dotnet build ../../Tools/MonoGame.Effect.Compiler/MonoGame.Effect.Compiler.csproj -c Release
```

## Incremental builds

After a managed-framework-only change:

```sh
cd scripts/apple-metal
dotnet build ../../MonoGame.Framework/MonoGame.Framework.MacOS.Metal.csproj -c Release
dotnet build ../../MonoGame.Framework/MonoGame.Framework.iOS.Metal.csproj -c Release
```

After changing the Metal renderer, rebuild the native runtimes. After changing
a stock effect or the Metal MGFX format, rerun `build-stock-effects.sh` and
then rebuild both native runtimes.

For a game whose `.csproj` is wired to the correct MGCB tasks, a normal
`dotnet build` compiles all `.fx` files declared in its `.mgcb` file. There is
no need to invoke the effect compiler manually or keep a precompiled `.mgfxo`
in source control.

## Declare Metal content with MGCB

A source application must use the MGCB version from this fork. An older
upstream `dotnet-mgcb` does not recognize `MacOSMetal` or `iOSMetal`.

The complete local MSBuild integration can be declared in the game project as
follows:

```xml
<PropertyGroup>
  <MonoGameRoot>../MonoGame</MonoGameRoot>
</PropertyGroup>

<Import Project="$(MonoGameRoot)/Tools/MonoGame.Content.Builder.Task/MonoGame.Content.Builder.Task.props" />

<PropertyGroup>
  <MonoGamePlatform>MacOSMetal</MonoGamePlatform>
  <EnableMGCBItems>false</EnableMGCBItems>
  <AutoRestoreMGCBTool>false</AutoRestoreMGCBTool>
  <_Command>&quot;$(MonoGameRoot)/Artifacts/MonoGame.Content.Builder/$(Configuration)/mgcb&quot;</_Command>
</PropertyGroup>

<ItemGroup>
  <MonoGameContentReference Include="Content/Content.mgcb">
    <ContentFolder>Content</ContentFolder>
  </MonoGameContentReference>
</ItemGroup>

<Target Name="BuildLocalMGCB" BeforeTargets="RunContentBuilder">
  <MSBuild Projects="$(MonoGameRoot)/Tools/MonoGame.Content.Builder/MonoGame.Content.Builder.csproj"
           Targets="Build"
           Properties="Configuration=$(Configuration)"
           RemoveProperties="RuntimeIdentifier;SelfContained;TargetFramework;TargetFrameworks" />
</Target>

<Import Project="$(MonoGameRoot)/Tools/MonoGame.Content.Builder.Task/MonoGame.Content.Builder.Task.targets" />
```

Adjust `MonoGameRoot` to the actual checkout location. Use `iOSMetal` for iOS.
The Metal framework package targets already set the correct
`MonoGamePlatform`, but the project must still consume a Content Builder
version that contains this fork's Metal support.

An effect in `Content.mgcb` uses the standard pipeline:

```text
#begin Effects/MyEffect.fx
/importer:EffectImporter
/processor:EffectProcessor
/processorParam:DebugMode=Auto
/build:Effects/MyEffect.fx
```

Minimal HLSL technique example:

```hlsl
technique Technique1
{
    pass Pass1
    {
        VertexShader = compile vs_6_0 VSMain();
        PixelShader = compile ps_6_0 PSMain();
    }
}
```

MGCB selects `MetalMacOS` for `MacOSMetal` and `MetaliOS` for `iOSMetal`. The
resulting XNB files are included as `BundleResource` items in macOS and iOS
application bundles.

## Use the target in an application

### From the source checkout

A macOS application references the managed project and, until it consumes the
package, the locally built native dylib:

```xml
<ItemGroup>
  <ProjectReference Include="../MonoGame/MonoGame.Framework/MonoGame.Framework.MacOS.Metal.csproj" />
  <NativeReference Include="../MonoGame/Artifacts/native/mgruntime/desktopmetal/macosx/Release/libmgruntime-metal.dylib">
    <Kind>Dynamic</Kind>
    <SmartLink>false</SmartLink>
  </NativeReference>
</ItemGroup>
```

The application project must target `net10.0-macos`, require macOS 15 or
later, and select either `osx-arm64` or `osx-x64` as its RID.

The `global.json` under `scripts/apple-metal` does not automatically apply to
an application located elsewhere. That application must also resolve the
exact 10.0.302 SDK through its environment or an equivalent local pin.

An iOS source application references
`MonoGame.Framework.iOS.Metal.csproj` and the XCFramework slice matching
`ios-arm64`, `iossimulator-arm64`, or `iossimulator-x64`. Exact
`NativeReference` examples are available in
`Tests/AppleMetalSmoke.iOS/AppleMetalSmoke.iOS.csproj`.

### From the preview packages

Locally built packages are written under:

```text
Artifacts/MonoGame.Framework/MacOS.Metal/Release
Artifacts/MonoGame.Framework/iOS.Metal/Release
```

Use them through a normal package reference:

```xml
<PackageReference Include="MonoGame.Framework.MacOS.Metal" Version="3.8.3.1" />
```

or:

```xml
<PackageReference Include="MonoGame.Framework.iOS.Metal" Version="3.8.3.1" />
```

The package version follows `MonoGame.props`. Do not permanently copy the
example version into a project that tracks a different revision of this fork.

Build the packages with:

```sh
cd scripts/apple-metal

dotnet pack ../../MonoGame.Framework/MonoGame.Framework.iOS.Metal.csproj -c Release --no-build
dotnet pack ../../MonoGame.Framework/MonoGame.Framework.MacOS.Metal.csproj -c Release
```

The macOS package contains the dylib under `runtimes/osx/native`. The iOS
package contains the XCFramework, and its MSBuild targets select the slice
matching the application RID.

## Validation

### Effect compiler and content-format tests

```sh
cd scripts/apple-metal

dotnet test ../../Tools/MonoGame.Tools.Tests/MonoGame.Tools.Tests.csproj \
  -c Release \
  --filter FullyQualifiedName~AppleMetalProfileTests
```

These tests protect the append-only values, XNB identifiers, Metal profiles,
and texture and audio platform support.

### macOS smoke test

```sh
scripts/apple-metal/run-macos-smoke.sh Release osx-arm64
scripts/apple-metal/run-macos-smoke.sh Release osx-x64
```

Running the `osx-x64` variant on Apple Silicon requires Rosetta 2.

The smoke test opens an SDL2 Metal window and presents three frames. It checks
texture upload and readback, an MSAA render target with depth and resolve, a
converted `SpriteEffect` draw, texture and sampler bindings, backbuffer
readback, presentation, and clean destruction.

For a manual session with Metal validation enabled:

```sh
MTL_DEBUG_LAYER=1 MTL_SHADER_VALIDATION=1 \
  scripts/apple-metal/run-macos-smoke.sh Release osx-arm64
```

### iOS simulator smoke test

```sh
scripts/apple-metal/run-ios-simulator-smoke.sh Release iossimulator-arm64
scripts/apple-metal/run-ios-simulator-smoke.sh Release iossimulator-x64
```

The runner selects an available iPhone simulator, enables Metal API/GPU
Validation, installs the application, and requires the explicit three-frame
success marker. A simulator UDID can be supplied as the third argument.

### Native artifact audit

```sh
dylib=Artifacts/native/mgruntime/desktopmetal/macosx/Release/libmgruntime-metal.dylib

lipo "$dylib" -verify_arch arm64 x86_64
! otool -L "$dylib" | grep -Eiq 'vulkan|moltenvk'
! nm -gU "$dylib" | grep -Eiq 'vulkan|moltenvk'
! strings "$dylib" | grep -Eiq 'libvulkan|moltenvk'

lipo Artifacts/native/mgruntime/iosmetal/Release/MGMetal.xcframework/ios-arm64/libmgruntime-metal.a \
  -verify_arch arm64
lipo Artifacts/native/mgruntime/iosmetal/Release/MGMetal.xcframework/ios-arm64_x86_64-simulator/libmgruntime-metal-simulator.a \
  -verify_arch arm64 x86_64
```

### macOS package smoke test

After `dotnet pack`:

```sh
cd scripts/apple-metal

dotnet build ../../Tests/AppleMetalPackageSmoke/AppleMetalPackageSmoke.csproj \
  -c Release \
  -r osx-arm64
```

This test proves that an external consumer resolves the native runtime from
the package without a source-project reference or manual `NativeReference`.

## Current limitations and production qualification

The backend renders shaders and passes its automated scenarios, but the
packages remain previews. Before declaring them production-ready, the project
must at least:

- provision Metal Shader Converter reproducibly on every Apple lane runner;
- validate all six stock effects and a representative set of custom effects
  under Metal API/GPU Validation;
- run the shader smoke test on a signed physical iPhone/iPad arm64 device;
- qualify macOS continuous-resize soak, fullscreen, multiple displays,
  sleep/wake, and temporary drawable loss;
- qualify iOS with AOT and trimming, rotations, background/foreground,
  memory pressure, recovery, and frame pacing;
- complete long-running tests, deferred-resource-destruction tests, and
  packaging audits;
- eliminate remaining Metal validation warnings before enabling public
  templates;
- measure performance and memory use on Apple GPUs and Intel/AMD Macs.

Metal must not become the default Apple target until these gates pass and the
migration is explicitly approved. Existing OpenGL and Vulkan targets remain
unchanged until then.

## Troubleshooting

### The 10.0.302 SDK is not found

Run repository commands from `scripts/apple-metal`, where the dedicated
`global.json` is resolved, and verify:

```sh
dotnet --list-sdks
dotnet --version
```

Roll-forward is disabled: `10.0.303` does not replace `10.0.302`.

### Metal Shader Converter is not found

```sh
command -v metal-shaderconverter
metal-shaderconverter --version
```

The executable must be visible to the process that runs `dotnet build` or
MGCB. There is no Vulkan/SPIR-V fallback.

### Stock effects are missing from the runtime

Rerun the following commands in this order:

```sh
scripts/apple-metal/build-stock-effects.sh

cd native/monogame
premake5 --backend=metal gmake
make config=release
```

Do not publish the runtime if any of the six generated headers is missing.

### The FAudio submodule appears modified

Check its commit first:

```sh
git submodule status -- native/monogame/external/faudio
git -C native/monogame/external/faudio status --short
```

A lone `?? build/` is a normal CMake artifact. It does not mean that the
submodule commit changed.

### An XNB asset is not found on macOS or iOS

Verify that the `.mgcb` is a `MonoGameContentReference`, that
`MonoGamePlatform` is `MacOSMetal` or `iOSMetal`, and that the XNB is present
under `Contents/Resources/Content` in the application bundle. Load the asset
with `Content.Load<T>("path/without-extension")` instead of manually reading a
`.mgfxo` from the build directory.

## Reference files

- `docs/apple-metal-preview.md`: current status and acceptance gates;
- `scripts/apple-metal/global.json`: SDK and workload-set pin;
- `scripts/apple-metal/build-stock-effects.sh`: stock-effect generation;
- `native/monogame/metal/MGG_Metal.mm`: shared native renderer;
- `native/monogame/metal/build-sdl-macos.sh`: universal macOS SDL2 build;
- `native/monogame/metal/build-ios-xcframework.sh`: iOS runtime build;
- `MonoGame.Framework/MonoGame.Framework.MacOS.Metal.csproj`: macOS target;
- `MonoGame.Framework/MonoGame.Framework.iOS.Metal.csproj`: iOS target;
- `Tools/MonoGame.Effect.Compiler/Effect/ShaderProfile.Metal.cs`: DXIL to
  Metal IR conversion;
- `Tests/AppleMetalSmoke`: macOS smoke test;
- `Tests/AppleMetalSmoke.iOS`: iOS simulator smoke test;
- `Tests/AppleMetalPackageSmoke`: macOS package-consumption smoke test.
