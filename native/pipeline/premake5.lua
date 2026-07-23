-- MonoGame - Copyright (C) MonoGame Foundation, Inc
-- This file is subject to the terms and conditions defined in
-- file 'LICENSE.txt', which is part of this source code package.
newoption {
    trigger = "arch",
    value = "ARCH",
    description = "Target architecture (x64 or arm64)",
    default = "x64",
    allowed = {
        { "x64", "64-bit x86" },
        { "arm64", "64-bit ARM" }
    }
}

function pipeline_native()
    if os.target() == "windows" then
        filter "platforms:x64"
        architecture "x86_64"
        filter "platforms:arm64"
        architecture "ARM64"
        filter {}
        platform_target_path = "../../Artifacts/native/mgpipeline/%{cfg.system}/%{cfg.platform}/%{cfg.buildcfg}"
    else
        local target_arch = _OPTIONS["arch"] or "x64"
        architecture(target_arch == "arm64" and "ARM64" or "x64")
        if os.target() == "macosx" then
            platform_target_path = "../../Artifacts/native/mgpipeline/%{cfg.system}/%{cfg.buildcfg}"
        else
            platform_target_path = "../../Artifacts/native/mgpipeline/%{cfg.system}/" .. target_arch .. "/%{cfg.buildcfg}"
        end
    end
    kind "SharedLib"
    language "C++"

    defines {"DLL_EXPORT", "STB_IMAGE_IMPLEMENTATION", "STB_IMAGE_WRITE_IMPLEMENTATION",
             "STB_IMAGE_RESIZE_IMPLEMENTATION"}

    filter "system:windows"
    defines {"STBI_WINDOWS_UTF8", "STBIW_WINDOWS_UTF8"}

    filter "system:linux"
    pic "On"

    filter {}
    targetdir(platform_target_path)
    targetname "mgpipeline"
    cppdialect "C++17"

    files {"include/**.h", "*.cpp"}
    removefiles {"mgfx-spvc.cpp"}
    includedirs {"include", "../monogame/include", "../../external/stb"}
end

workspace "pipeline"
configurations {"Debug", "Release"}
if os.target() == "windows" then
    platforms { "x64", "arm64" }
end

project "mgpipeline"
pipeline_native()
filter "configurations:Debug"
defines {"DEBUG"}
symbols "On"
filter "configurations:Release"
defines {"NDEBUG"}
optimize "On"

filter {"system:windows", "configurations:Release"}
buildoptions {"/MT"}

filter "system:macosx"
buildoptions {"-arch x86_64", "-arch arm64"}
linkoptions {"-arch x86_64", "-arch arm64"}
filter {}

project "mgfx-spvc"
kind "ConsoleApp"
language "C++"
cppdialect "C++17"
targetname "mgfx-spvc"
files {
    "mgfx-spvc.cpp",
    "external/spirv-cross/spirv_cross.cpp",
    "external/spirv-cross/spirv_cross_parsed_ir.cpp",
    "external/spirv-cross/spirv_parser.cpp",
    "external/spirv-cross/spirv_cfg.cpp",
    "external/spirv-cross/spirv_glsl.cpp"
}
includedirs {"external/spirv-cross/include", "external/spirv-cross"}

if os.target() == "windows" then
    filter "platforms:x64"
    architecture "x86_64"
    filter "platforms:arm64"
    architecture "ARM64"
    filter {}
    targetdir "../../Artifacts/native/mgfx-spvc/%{cfg.system}/%{cfg.platform}/%{cfg.buildcfg}"
else
    local target_arch = _OPTIONS["arch"] or "x64"
    architecture(target_arch == "arm64" and "ARM64" or "x64")
    if os.target() == "macosx" then
        targetdir "../../Artifacts/native/mgfx-spvc/%{cfg.system}/%{cfg.buildcfg}"
    else
        targetdir("../../Artifacts/native/mgfx-spvc/%{cfg.system}/" .. target_arch .. "/%{cfg.buildcfg}")
    end
end

filter "configurations:Debug"
defines {"DEBUG"}
symbols "On"
filter "configurations:Release"
defines {"NDEBUG"}
optimize "On"
filter "system:linux"
pic "On"
filter "system:macosx"
buildoptions {"-arch x86_64", "-arch arm64"}
linkoptions {"-arch x86_64", "-arch arm64"}
filter {}
