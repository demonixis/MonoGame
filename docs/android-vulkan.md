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
- .NET 10 SDK 10.0.300 with workload set 10.0.300 and the Android workload.

The package manifest declares API 29 and Vulkan 1.1 as required features, so
Android stores can filter incompatible devices. The first preview contains no
`x86`, `x86_64`, or `armeabi-v7a` runtime.

## Install and configure

Reference the Vulkan package instead of the GLES package:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-android</TargetFramework>
  <SupportedOSPlatformVersion>29</SupportedOSPlatformVersion>
  <RuntimeIdentifier>android-arm64</RuntimeIdentifier>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="MonoGame.Framework.Android.Vulkan" Version="..." />
</ItemGroup>
```

Do not reference `MonoGame.Framework.Android` in the same application. The
Vulkan package contributes exactly one `arm64-v8a/libmgruntime.so` through its
transitive build target. A direct `ProjectReference` to the framework project is
also supported: its Android library output carries the same single arm64 runtime.
A source-reference application must declare the same API 29/Vulkan 1.1 manifest
requirements itself; the package supplies those declarations through its
transitive manifest overlay.

Use `AndroidVK` as the MGCB platform:

```text
/platform:AndroidVK
```

`AndroidVK` emits MGFX 80 Vulkan shaders while retaining Android asset and
audio conventions. The MonoGame content MSBuild task treats `AndroidVK` as an
Android target, so built XNB files are emitted as `AndroidAsset` items rather
than desktop `Content` items. `Compressed` textures use the ETC family by default; an
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
dotnet build MonoGame.Framework/MonoGame.Framework.Android.Vulkan.csproj -c Release
dotnet pack MonoGame.Framework/MonoGame.Framework.Android.Vulkan.csproj -c Release
```

The native result is written to:

```text
Artifacts/native/mgruntime/androidvk/android-arm64/Release/libmgruntime.so
```

The managed project targets `net10.0-android`; its source-reference AAR contains
that native runtime under `jni/arm64-v8a`. `dotnet pack` additionally publishes
the runtime below `runtimes/android-arm64/native` for package consumers. Do not
add the same `.so` manually when consuming either form.

The `Build Android Vulkan` build task runs the Vulkan shader build, the NDK
runtime build, and framework packaging on a Linux x64 Android-workload lane.
The native target deliberately excludes SDL, GLES, FAudio, MoltenVK, and other
desktop dependencies.

## Build and deploy a consuming application

Build an APK for direct installation; use an AAB only for store/bundletool
packaging:

```sh
dotnet build path/to/Game.Android.Vulkan.csproj -c Release \
  -p:AndroidPackageFormats=apk -p:RuntimeIdentifier=android-arm64

adb devices
adb install -r path/to/application-Signed.apk
adb shell monkey -p your.application.id -c android.intent.category.LAUNCHER 1
```

An AAB cannot be passed directly to `adb install`. Before launch, audit the APK
for exactly one `lib/arm64-v8a/libmgruntime.so`, no other ABI, API 29 minimum,
and the required Vulkan 1.1 feature. Also verify that `AndroidVK` XNB outputs are
present below the application's Android assets; compiling content without
packaging it is not a usable player.

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
