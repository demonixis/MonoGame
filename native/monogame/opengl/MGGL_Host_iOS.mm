// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#if defined(MG_IOS)

#include "MGGL_Host.h"
#include <QuartzCore/CAEAGLLayer.h>
#include <OpenGLES/EAGL.h>
#include <OpenGLES/ES3/gl.h>
#include <dlfcn.h>
#include <cstdio>

namespace
{
    struct IOSHost
    {
        __strong EAGLContext* context = nil;
        __weak CAEAGLLayer* layer = nil;
        GLuint framebuffer = 0;
        GLuint colorbuffer = 0;
    };

    IOSHost* State(MGGL_Host& host) { return static_cast<IOSHost*>(host.display); }

    bool CreateDrawable(IOSHost& state)
    {
        if (!state.layer || ![EAGLContext setCurrentContext:state.context])
            return false;
        glGenFramebuffers(1, &state.framebuffer);
        glBindFramebuffer(GL_FRAMEBUFFER, state.framebuffer);
        glGenRenderbuffers(1, &state.colorbuffer);
        glBindRenderbuffer(GL_RENDERBUFFER, state.colorbuffer);
        if (![state.context renderbufferStorage:GL_RENDERBUFFER fromDrawable:state.layer])
            return false;
        glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_RENDERBUFFER, state.colorbuffer);
        return glCheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE;
    }

    void DestroyDrawable(IOSHost& state)
    {
        if (![EAGLContext setCurrentContext:state.context])
            return;
        if (state.framebuffer) glDeleteFramebuffers(1, &state.framebuffer);
        if (state.colorbuffer) glDeleteRenderbuffers(1, &state.colorbuffer);
        state.framebuffer = 0;
        state.colorbuffer = 0;
    }
}

bool MGGL_Host_Create(MGGL_Host& host, MGG_PresentationSurface& presentation, int major, int minor)
{
    if (presentation.Kind != MGPresentationSurfaceKind::OpenGlesLayer || !presentation.Handle || major != 3 || minor != 0)
        return false;
    auto* state = new IOSHost();
    state->layer = (__bridge CAEAGLLayer*)presentation.Handle;
    state->layer.opaque = YES;
    state->layer.drawableProperties = @{
        kEAGLDrawablePropertyRetainedBacking: @NO,
        kEAGLDrawablePropertyColorFormat: kEAGLColorFormatRGBA8
    };
    state->context = [[EAGLContext alloc] initWithAPI:kEAGLRenderingAPIOpenGLES3];
    if (!state->context || !CreateDrawable(*state))
    {
        std::fprintf(stderr, "iOS Native OpenGL ES 3.0 context or drawable creation failed.\n");
        DestroyDrawable(*state);
        delete state;
        return false;
    }
    host.window = presentation.Handle;
    host.display = state;
    host.context = (__bridge void*)state->context;
    host.framebuffer = state->framebuffer;
    host.colorbuffer = state->colorbuffer;
    return true;
}

bool MGGL_Host_MakeCurrent(MGGL_Host& host)
{
    auto* state = State(host);
    return state && [EAGLContext setCurrentContext:state->context];
}

bool MGGL_Host_Resize(MGGL_Host& host, MGG_PresentationSurface& presentation, int, int)
{
    if (presentation.Kind != MGPresentationSurfaceKind::OpenGlesLayer || !presentation.Handle)
        return false;
    auto* state = State(host);
    if (!state)
        return false;
    DestroyDrawable(*state);
    state->layer = (__bridge CAEAGLLayer*)presentation.Handle;
    if (!CreateDrawable(*state))
        return false;
    host.window = presentation.Handle;
    host.framebuffer = state->framebuffer;
    host.colorbuffer = state->colorbuffer;
    return true;
}

bool MGGL_Host_Present(MGGL_Host& host, int)
{
    auto* state = State(host);
    if (!state || !MGGL_Host_MakeCurrent(host))
        return false;
    glBindRenderbuffer(GL_RENDERBUFFER, state->colorbuffer);
    return [state->context presentRenderbuffer:GL_RENDERBUFFER];
}

void MGGL_Host_Suspend(MGGL_Host& host)
{
    auto* state = State(host);
    if (state && [EAGLContext currentContext] == state->context)
        [EAGLContext setCurrentContext:nil];
}

void MGGL_Host_Destroy(MGGL_Host& host)
{
    auto* state = State(host);
    if (!state)
        return;
    DestroyDrawable(*state);
    if ([EAGLContext currentContext] == state->context)
        [EAGLContext setCurrentContext:nil];
    state->context = nil;
    delete state;
    host = {};
}

void* MGGL_Host_GetProcAddress(const char* name) { return dlsym(RTLD_DEFAULT, name); }

bool MGGL_Host_GetOpenXrGraphicsBinding(MGGL_Host&, MGG_OpenXrGraphicsBinding&) { return false; }

#endif
