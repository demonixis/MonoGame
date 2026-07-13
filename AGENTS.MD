# MonoGame Repository Guide

This file is the operational guide for maintainers and automated coding agents
working in this checkout. Read it together with,
`CODESTYLE.md`, and `REQUIREMENTS.md` before changing the repository.

Upstream doesn't allow LLM-generated contributions BUT this fork is private and is open for LLM contributions.

## Project goals

- Preserve the public XNA-compatible API and its observable behavior unless a
  breaking change has been explicitly approved.
- Keep legacy and preview graphics backends isolated. A new backend must not
  silently change an existing platform package or content format.
- Prefer small, reviewable changes with tests over broad cleanup or mechanical
  rewrites.
- Treat package restore, application startup, device creation, rendering,
  suspend/resume, and clean shutdown as part of the supported contract.
- Maintain deterministic content builds and append-only identifiers in binary
  formats. Never reuse or renumber a published enum or XNB/MGFX platform ID.

## Repository map

- `MonoGame.Framework/`: shared framework sources and platform partials.
- `MonoGame.Framework.Content.Pipeline/`: importers, processors, XNB writers,
  and content build infrastructure.
- `Tools/MonoGame.Effect.Compiler/`: MGFX compiler and shader profiles.
- `Tools/MonoGame.Content.Builder*`: MGCB command-line and editor tooling.
- `native/monogame/`: native platform/runtime implementation and public C ABI.
- `src/NuGetPackages/`: package composition and native runtime assets.
- `Tests/`: framework, graphics, content, and interactive test projects.
- `build/`: Cake Frosting build orchestration and packaging tasks.
- `external/` and native `external/` directories: pinned submodules or vendored
  dependencies; do not edit them as ordinary source.
- `Artifacts/`: generated build output. It must not be treated as source.

## Source and compatibility rules

- Follow the existing style in the file first and `CODESTYLE.md` second. Use
  four spaces, Allman braces, and the repository copyright header in new source
  files.
- Avoid unrelated formatting, member reordering, public renaming, or nullable
  cleanup in feature patches.
- Keep platform behavior in partial files or backend-specific native folders.
  Shared code must not acquire platform framework dependencies.
- Add public API only when required. Document the compatibility effect and add
  API tests where the repository already has them.
- Append new serialized enum values and platform identifiers. Readers must keep
  accepting all existing values, and writers must emit a unique new value.
- Do not copy code from XNA binaries, decompilers, proprietary SDK samples, or
  sources with incompatible licenses.
- New third-party tools or binary artifacts require a recorded version,
  checksum, license review, and a reproducible acquisition path.

## Native graphics architecture

- Managed native graphics partials live under
  `MonoGame.Framework/Platform/Native`; the C ABI is declared in
  `native/monogame/include/api_MGG.h` and implemented by backend folders.
- Keep the MGG ABI backend-neutral. Never expose Objective-C, Metal, Vulkan,
  DirectX, or SDL types in public managed APIs.
- A backend must implement the complete MGG ownership contract: adapters,
  device and swapchain, resources, state objects, shaders, draw calls,
  synchronization, readback, queries, resize, and deterministic disposal.
- Native handles must have explicit ownership. GPU resources may only be freed
  after submitted work has completed. Device teardown must tolerate partially
  initialized resources and repeated disposal.
- Presentation surfaces are host-owned. The renderer receives a typed internal
  descriptor and must handle a temporarily unavailable drawable without
  blocking or crashing.
- Capabilities must be queried from the selected device. Do not advertise a
  format, MSAA level, resource limit, or feature because another backend has it.

## Apple Metal backend rules

- Metal is an additive preview backend for macOS and iOS. Existing OpenGL and
  Vulkan packages remain unchanged until a separately approved migration.
- Treat `docs/apple-metal-preview.md` as the living status and acceptance-gate
  document for this backend.
- Metal artifacts must not link or package Vulkan, MoltenVK, or SPIR-V tooling.
- macOS keeps SDL2 for windowing and input and uses an SDL Metal view backed by
  `CAMetalLayer`. iOS keeps UIKit, touch/controller input, audio/media, and its
  application lifecycle while replacing the OpenGL view with `CAMetalLayer`.
- The shared Objective-C++ renderer must depend on an internal Metal presenter,
  not directly on SDL or UIKit. This is the only visionOS preparation in the
  initial implementation: do not add a visionOS TFM, package, public enum,
  runtime host, or test until support is explicitly scheduled.
- The deployment floors are macOS 15 and iOS 18. macOS artifacts are universal
  `arm64+x64`; iOS artifacts include device arm64 and simulator arm64/x64.
- Shaders use HLSL -> DXIL -> Apple Metal Shader Converter -> Metal IR/metallib.
  The Metal pipeline must never route through SPIR-V. Converter reflection and
  the official argument-buffer runtime contract must be normalized into MGFX
  metadata during the content build; do not call shader rendering complete
  until that gate and the stock effects are validated.
- Metal packages use a distinct native library name (`mgruntime-metal`) so they
  cannot collide with the existing Vulkan runtime.

## Build and validation workflow

- Initialize submodules before the first build: `git submodule update --init
  --recursive`.
- Use `./build.sh` or `dotnet run --project build/Build.csproj --
  --target=Default` for the full supported build.
- Build the closest solution/project while iterating, then run the full relevant
  lane before handing work off.
- Run framework tests for the modified backend and
  `Tools/MonoGame.Tools.Tests` for content or shader changes.
- Use `git diff --check` before handoff. Never hide warnings introduced by the
  change and never rewrite golden files without inspecting the rendered result.
- Native Apple validation includes a real window/device smoke test, resize and
  suspend/resume behavior, validation-layer output, and an `otool`/`nm` audit
  proving that Metal packages contain no Vulkan or MoltenVK dependency.
- iOS validation includes simulator builds plus an arm64 device smoke run with
  AOT/trimming, orientation changes, background/foreground, and frame pacing.
- visionOS is intentionally not built or tested during the scaffold-only phase.

## .NET SDK policy

- The general repository CI remains on its established SDK until all existing
  mobile TFMs can migrate together.
- Apple Metal projects use a dedicated .NET SDK 10.0.300 lane with roll-forward
  disabled, workload set 10.0.300, and Xcode 26.3. Update the SDK, workload
  set, and Xcode pin together rather than independently.
- Do not add a root `global.json` merely for the Apple preview. Keep SDK pinning
  local to the dedicated build lane or helper entrypoint.

## Change hygiene

- Inspect `git status` before and after work. Existing changes belong to the
  user; preserve them and avoid overlapping edits where possible.
- Do not use destructive Git commands or modify submodule revisions unless the
  task explicitly requires it.
- Do not commit, push, open issues, or create pull requests unless the user asks
  for that exact action.
