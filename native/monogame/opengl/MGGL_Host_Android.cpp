// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#if defined(MG_ANDROID)

#include "MGGL_Host.h"
#include <EGL/egl.h>
#include <EGL/eglext.h>
#include <android/native_window.h>
#include <cstdio>

namespace
{
    struct AndroidHost
    {
        EGLDisplay display = EGL_NO_DISPLAY;
        EGLConfig config = nullptr;
        EGLSurface surface = EGL_NO_SURFACE;
        EGLContext context = EGL_NO_CONTEXT;
    };

    AndroidHost* State(MGGL_Host& host) { return static_cast<AndroidHost*>(host.display); }

    bool CreateSurface(AndroidHost& state, ANativeWindow* window)
    {
        state.surface = eglCreateWindowSurface(state.display, state.config, window, nullptr);
        return state.surface != EGL_NO_SURFACE;
    }
}

bool MGGL_Host_Create(MGGL_Host& host, MGG_PresentationSurface& presentation, int major, int minor)
{
    if (presentation.Kind != MGPresentationSurfaceKind::AndroidNativeWindow || !presentation.Handle || major != 3 || minor != 0)
        return false;
    auto* state = new AndroidHost();
    state->display = eglGetDisplay(EGL_DEFAULT_DISPLAY);
    if (state->display == EGL_NO_DISPLAY || !eglInitialize(state->display, nullptr, nullptr) || !eglBindAPI(EGL_OPENGL_ES_API))
        goto failure;

    {
        const EGLint attributes[] =
        {
            EGL_SURFACE_TYPE, EGL_WINDOW_BIT,
            EGL_RENDERABLE_TYPE, EGL_OPENGL_ES3_BIT_KHR,
            EGL_RED_SIZE, 8,
            EGL_GREEN_SIZE, 8,
            EGL_BLUE_SIZE, 8,
            EGL_ALPHA_SIZE, 8,
            EGL_DEPTH_SIZE, 24,
            EGL_STENCIL_SIZE, 8,
            EGL_NONE
        };
        EGLint count = 0;
        if (!eglChooseConfig(state->display, attributes, &state->config, 1, &count) || count != 1)
            goto failure;
    }

    {
        const EGLint contextAttributes[] =
        {
            EGL_CONTEXT_CLIENT_VERSION, major,
            EGL_NONE
        };
        state->context = eglCreateContext(state->display, state->config, EGL_NO_CONTEXT, contextAttributes);
        if (state->context == EGL_NO_CONTEXT)
            goto failure;
    }
    if (!CreateSurface(*state, static_cast<ANativeWindow*>(presentation.Handle)))
        goto failure;

    host.window = presentation.Handle;
    host.display = state;
    host.surface = reinterpret_cast<void*>(state->surface);
    host.context = reinterpret_cast<void*>(state->context);
    return MGGL_Host_MakeCurrent(host);

failure:
    std::fprintf(stderr, "Android Native GLES 3.0 EGL initialization failed (0x%x).\n", eglGetError());
    if (state->context != EGL_NO_CONTEXT) eglDestroyContext(state->display, state->context);
    if (state->display != EGL_NO_DISPLAY) eglTerminate(state->display);
    delete state;
    return false;
}

bool MGGL_Host_MakeCurrent(MGGL_Host& host)
{
    auto* state = State(host);
    return state && state->surface != EGL_NO_SURFACE &&
        eglMakeCurrent(state->display, state->surface, state->surface, state->context) == EGL_TRUE;
}

bool MGGL_Host_Resize(MGGL_Host& host, MGG_PresentationSurface& presentation, int, int)
{
    if (presentation.Kind != MGPresentationSurfaceKind::AndroidNativeWindow || !presentation.Handle)
        return false;
    auto* state = State(host);
    if (!state)
        return false;
    if (state->surface != EGL_NO_SURFACE)
    {
        eglMakeCurrent(state->display, EGL_NO_SURFACE, EGL_NO_SURFACE, state->context);
        eglDestroySurface(state->display, state->surface);
    }
    if (!CreateSurface(*state, static_cast<ANativeWindow*>(presentation.Handle)))
        return false;
    host.window = presentation.Handle;
    host.surface = reinterpret_cast<void*>(state->surface);
    return MGGL_Host_MakeCurrent(host);
}

bool MGGL_Host_Present(MGGL_Host& host, int syncInterval)
{
    auto* state = State(host);
    if (!state || !MGGL_Host_MakeCurrent(host))
        return false;
    eglSwapInterval(state->display, syncInterval > 0 ? 1 : 0);
    if (eglSwapBuffers(state->display, state->surface) == EGL_TRUE)
        return true;
    const EGLint error = eglGetError();
    if (error == EGL_CONTEXT_LOST)
        std::fprintf(stderr, "Android Native GLES context loss is fatal in v1.\n");
    return false;
}

void MGGL_Host_Suspend(MGGL_Host& host)
{
    auto* state = State(host);
    if (!state || state->surface == EGL_NO_SURFACE)
        return;
    eglMakeCurrent(state->display, EGL_NO_SURFACE, EGL_NO_SURFACE, state->context);
    eglDestroySurface(state->display, state->surface);
    state->surface = EGL_NO_SURFACE;
    host.surface = nullptr;
}

void MGGL_Host_Destroy(MGGL_Host& host)
{
    auto* state = State(host);
    if (!state)
        return;
    MGGL_Host_Suspend(host);
    if (state->context != EGL_NO_CONTEXT) eglDestroyContext(state->display, state->context);
    if (state->display != EGL_NO_DISPLAY) eglTerminate(state->display);
    delete state;
    host = {};
}

void* MGGL_Host_GetProcAddress(const char* name) { return reinterpret_cast<void*>(eglGetProcAddress(name)); }

bool MGGL_Host_GetOpenXrGraphicsBinding(MGGL_Host& host, MGG_OpenXrGraphicsBinding& binding)
{
    auto* state = State(host);
    if (!state || state->display == EGL_NO_DISPLAY || state->context == EGL_NO_CONTEXT || !state->config)
        return false;
    binding.Api = 1; // OpenXrGraphicsApi.OpenGL, including the Android GLES binding.
    binding.NativeWindow = host.window;
    binding.Display = reinterpret_cast<void*>(state->display);
    binding.Context = reinterpret_cast<void*>(state->context);
    binding.Configuration = reinterpret_cast<void*>(state->config);
    return true;
}

#endif
