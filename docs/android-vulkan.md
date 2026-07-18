# Android Native Vulkan backend preview

`MonoGame.Framework.Android.Vulkan` is an additive Android package that uses
MonoGame's Native Vulkan renderer. It does not replace
`MonoGame.Framework.Android`, select a backend automatically, or fall back to
GLES. The Android activity, touch, gamepad, audio, media, orientation, and
pause/resume host remain shared with the GLES package.

## Requirements

- Android 10 / API 29 or newer.
- An `arm64-v8a` device exposing Vulkan 1.1.
- `VK_KHR_swapchain`, a queue that supports both graphics and presentation,
  and scalar block layout support.
- .NET 8 with the Android workload for application builds.

The package manifest declares API 29 and Vulkan 1.1 as required features, so
Android stores can filter incompatible devices. The first preview contains no
`x86`, `x86_64`, or `armeabi-v7a` runtime.

## Install and configure

Reference the Vulkan package instead of the GLES package:

```xml
<PropertyGroup>
  <TargetFramework>net8.0-android</TargetFramework>
  <SupportedOSPlatformVersion>29</SupportedOSPlatformVersion>
  <RuntimeIdentifier>android-arm64</RuntimeIdentifier>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="MonoGame.Framework.Android.Vulkan" Version="..." />
</ItemGroup>
```

Do not reference `MonoGame.Framework.Android` in the same application. The
Vulkan package contributes exactly one `arm64-v8a/libmgruntime.so` through its
transitive build target.

Use `AndroidVK` as the MGCB platform:

```text
/platform:AndroidVK
```

`AndroidVK` emits MGFX 80 Vulkan shaders while retaining Android asset and
audio conventions. `Compressed` textures use the ETC family by default; an
opaque ETC1 payload is uploaded through the Vulkan ETC2 RGB format because the
bitstream is ETC2-compatible, while alpha textures use ETC2/EAC. ASTC is an
explicit opt-in:

```text
/processorParam:TextureFormat=AstcCompressed
```

DXT is never selected by default and is rejected for this content target.

## Build from source

Install Android NDK `28.2.13676358`. The build helper uses
`MONOGAME_ANDROID_NDK_ROOT`, or discovers that version below
`ANDROID_SDK_ROOT`/`ANDROID_HOME`:

```sh
bash native/monogame/android-vulkan/build-android.sh Release
dotnet pack MonoGame.Framework/MonoGame.Framework.Android.Vulkan.csproj -c Release
```

The native result is written to:

```text
Artifacts/native/mgruntime/androidvk/android-arm64/Release/libmgruntime.so
```

The `Build Android Vulkan` build task runs the Vulkan shader build, the NDK
runtime build, and framework packaging on a Linux x64 Android-workload lane.
The native target deliberately excludes SDL, GLES, FAudio, MoltenVK, and other
desktop dependencies.

## Runtime behavior and diagnostics

The game thread acquires an `ANativeWindow` from the Android `Surface`, creates
the Vulkan surface before selecting the logical device queue, and owns
presentation through `GraphicsDevice.Present()`. Rotation, surface recreation,
and backgrounding suspend presentation, wait for submitted work, destroy the
swapchain/surface, release the old `ANativeWindow`, and attach the replacement.
Frames remain safely idle while no surface is available.

Capture startup and lifecycle diagnostics with:

```sh
adb logcat -c
adb logcat MonoGameDebug:D AndroidGameView:D libc:I DEBUG:I '*:S'
```

Common startup failures identify the missing Vulkan 1.1 level, Android surface,
graphics/present queue, swapchain extension, or scalar block layout feature.
There is intentionally no GLES recovery path; a device that does not meet the
contract throws `NoSuitableGraphicsDeviceException`.

For Vulkan validation during local native development, use a debug runtime and
capture the validation-layer output in logcat. A release package must have no
validation errors and no retained `ANativeWindow` reference after repeated
surface destruction/recreation.

## Qualification status

The package is a preview. Compile, package, APK-consumption, ABI, ELF dependency,
content-platform, and desktop Vulkan regression gates are automated. Promotion
to qualified support additionally requires both physical-device lanes below:

- Android 10 / Vulkan 1.1 on an Adreno device.
- A recent arm64 Mali device.

Each lane must cover device/swapchain creation, triangle and `SpriteBatch`,
stock and `AndroidVK` MGCB effects, ETC2, render targets, depth/stencil, MSAA,
instancing, readback, FIFO VSync, rotation/resize/orientation lock,
background/foreground, repeated surface recreation, and clean shutdown with no
Vulkan validation error or `ANativeWindow` leak.
