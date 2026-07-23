# MonoGame - Copyright (C) MonoGame Foundation, Inc
# This file is subject to the terms and conditions defined in
# file 'LICENSE.txt', which is part of this source code package.

cmake_minimum_required(VERSION 3.22)
project(mgruntime_android_opengles LANGUAGES C CXX)

if(NOT ANDROID)
    message(FATAL_ERROR "The Android NDK toolchain is required.")
endif()
if(NOT ANDROID_ABI STREQUAL "arm64-v8a")
    message(FATAL_ERROR "Android Native GLES v1 only supports arm64-v8a.")
endif()
if(ANDROID_PLATFORM_LEVEL LESS 23)
    message(FATAL_ERROR "Android Native GLES requires API level 23 or newer.")
endif()

set(MONOGAME_NATIVE_ROOT "${CMAKE_CURRENT_LIST_DIR}/..")

add_library(mgruntime SHARED
    "${MONOGAME_NATIVE_ROOT}/opengl/MGG_OpenGL.cpp"
    "${MONOGAME_NATIVE_ROOT}/opengl/MGGL_Host_Android.cpp"
    "${MONOGAME_NATIVE_ROOT}/common/MGI.cpp"
    "${MONOGAME_NATIVE_ROOT}/common/mg_hash.cpp"
    "${MONOGAME_NATIVE_ROOT}/common/mg_library.cpp"
)

target_compile_features(mgruntime PRIVATE cxx_std_17)
target_compile_definitions(mgruntime PRIVATE DLL_EXPORT MG_ANDROID MG_OPENGL)
target_include_directories(mgruntime PRIVATE
    "${MONOGAME_NATIVE_ROOT}/include"
    "${MONOGAME_NATIVE_ROOT}/opengl"
    "${MONOGAME_NATIVE_ROOT}/../../external/stb"
)
target_compile_options(mgruntime PRIVATE -fvisibility=hidden -Wno-nullability-completeness)
target_link_libraries(mgruntime PRIVATE android EGL GLESv3 dl)

set_target_properties(mgruntime PROPERTIES
    OUTPUT_NAME mgruntime
    LIBRARY_OUTPUT_DIRECTORY "${MONOGAME_NATIVE_ROOT}/../../Artifacts/native/mgruntime/androidgles/android-arm64/${CMAKE_BUILD_TYPE}"
)
