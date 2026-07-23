# Native OpenGL and OpenGL ES backend

This fork provides an additive native OpenGL backend implemented in C++ behind
the versioned `MGG_*` ABI. It does not replace `MonoGame.Framework.DesktopGL`,
the existing Android and iOS packages, `TargetPlatform.DesktopGL`, or the
historical `ShaderProfile.OpenGL`/MGFX 0 path.

## Platform contract

| Platform | Required API | Native host | Artifact |
| --- | --- | --- | --- |
| Android ARM64 | OpenGL ES 3.0 | EGL and `ANativeWindow` | `libmgruntime.so` |
| iOS/iPadOS | OpenGL ES 3.0 | `EAGLContext` and `CAEAGLLayer` | `MGOpenGL.xcframework` |
| macOS x64/ARM64 | OpenGL 4.1 Core | SDL | `libmgruntime.dylib` |
| Windows x64/ARM64 | OpenGL 4.1 Core | SDL | `mgruntime.dll` |
| Linux x64/ARM64 | OpenGL 4.1 Core | SDL | `libmgruntime.so` |

Context creation is strict. The backend reports an actionable error instead of
silently negotiating an older API. Android requires API 23 and packages only
`arm64-v8a`. Neither mobile Native OpenGL lane requests ES 2.0. The
desktop implementation uses the system OpenGL driver; ANGLE is not used.

Resize, orientation changes, suspension, surface destruction and surface
reattachment preserve managed resources while rebuilding presentation state.
A real context loss is fatal in this first version and is reported as such.

The size-aware `MGG_GraphicsDevice_GetCapsV2` structure has ABI version 2. It
reports the active API version, feature flags, texture-compression support,
detected maximum anisotropy, maximum render targets, draw buffers and color
attachments. A runtime that does not export V2 is queried through the legacy
capability entry point and receives conservative limits.

## Effect profiles

The Native OpenGL pipeline is separate from the DesktopGL/MojoShader path:

| Profile | MGFX format | GLSL output | XNB platform |
| --- | ---: | --- | --- |
| `OpenGLES_3_0` | 82 | `#version 300 es` | `AndroidNativeGLES` (`E`), `iOSNativeGLES` (`U`) |
| `OpenGL_4_1` | 84 | `#version 410 core` | `DesktopNativeGL` (`L`) |

MGFX compiles MonoGame vertex and pixel shaders with DXC to SPIR-V. The pinned
SPIRV-Cross revision is then built as `mgfx-spvc` and produces GLSL plus stable
reflection for uniform blocks, textures, samplers and vertex attributes. The
runtime rejects an effect payload whose profile does not match the active API.
MGCB records the resolved `mgfx-spvc` binary as an effect dependency, so changing
the converter invalidates incremental Native OpenGL XNBs instead of pairing stale
reflection with a newer runtime.
Geometry, tessellation and compute shaders are outside this first contract, and
the ES 3.0 profile rejects features that its target cannot represent.

Build the converter and regenerate all stock effects with:

```sh
cd native/pipeline
premake5 gmake2
make config=release mgfx-spvc
cd ../..
scripts/native-opengl/build-stock-effects.sh
```

On Windows, generate the pipeline with `premake5 vs2022` and build the Release
`mgfx-spvc` project before running the same script from Git Bash.

## Native builds

Desktop generation is intentionally restricted to this backend by
`--backend=opengl`:

```sh
cd native/monogame
premake5 --backend=opengl gmake2
make config=release desktopgl
```

Pass `--arch=arm64` on a native Linux ARM64 builder. On Windows, use
`premake5 --backend=opengl vs2022` and build `desktopgl` in Release for `x64`
and `ARM64`. The regular `Build Native` automation also builds and uploads the
OpenGL artifacts on its existing Windows, Linux and macOS runners.

Build the Android and Apple artifacts with:

```sh
native/monogame/android-opengles/build-android.sh Release
native/monogame/opengl/build-ios-xcframework.sh Release
```

The Android script requires NDK `28.2.13676358` and accepts
`MONOGAME_ANDROID_NDK_ROOT`. The iOS script creates an ARM64 device archive and
a universal ARM64/x64 simulator archive.

## Packages

Applications select this preview explicitly:

- `MonoGame.Framework.Android.OpenGLES`
- `MonoGame.Framework.iOS.OpenGLES`
- `MonoGame.Framework.Native.OpenGL`

The desktop meta-package depends on `MonoGame.Framework.Native` and on the
RID-specific `MonoGame.Runtime.Mac.OpenGL`,
`MonoGame.Runtime.Windows.OpenGL`, and `MonoGame.Runtime.Linux.OpenGL`
packages. It does not become a default backend. The iOS package links the
XCFramework statically and resolves the `MGG_*` entry points through
`__Internal`.

## Validation

Run the deterministic compiler contract tests:

```sh
dotnet test Tools/MonoGame.Tools.Tests/MonoGame.Tools.Tests.csproj \
  -c Release --filter NativeOpenGLProfileTests
```

On macOS, build the universal runtime and run the focused runtime smoke:

```sh
MONOGAME_OPENGL_CAPTURE_PATH=Artifacts/native-opengl-validation.png \
dotnet run --project Tests/MonoGame.Tests.DesktopNativeGL.csproj \
  -c Release -- --test=MonoGame.Tests.Graphics.NativeOpenGLValidationTest
```

Acceptance requires the complete output marker:

```text
MONOGAME_OPENGL_VALIDATION pass api=4.1 mrt=4 format=Color msaa=0
```

The smoke covers distinct four-target pixel readback, buffer round trips,
`Texture3D`, instanced indexed drawing, resize and presentation. Existing
single-target formats remain available. The accepted multi-target contract is
two through four `RenderTarget2D` instances using `Color` and identical
dimensions.
An existing single `RenderTargetCube` face binding remains supported for reflection
probes and sky captures; cube and 3D bindings are rejected only in MRT mode. Only
render target zero may own depth. A single multisampled render target is
retained, but remains unqualified; MRT with MSAA is rejected. Independent blend
state is not advertised, and base-vertex indexed drawing is not advertised on
the ES 3.0 profile. A build or package alone is not runtime or visual evidence.

Mobile qualification requires physical hardware and these exact markers:

```text
MONOGAME_GLES_VALIDATION pass api=3.0
```

The marker is required independently on Android ARM64 and iPhone/iPad. Simulator,
compile and package results must remain reported separately. Windows and Linux
generation, compilation and package audits run in CI, but their runtime and
visual qualification remain pending until the corresponding jobs emit
`MONOGAME_OPENGL_VALIDATION pass api=4.1` on real drivers.
