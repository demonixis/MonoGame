// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#if defined(MG_SDL2) && !defined(MG_ANDROID) && !defined(MG_IOS)

#include "MGGL_Host.h"
#include <SDL.h>
#include <cstdio>

bool MGGL_Host_Create(MGGL_Host& host, MGG_PresentationSurface& surface, int major, int minor)
{
    if (surface.Kind != MGPresentationSurfaceKind::SdlWindow || !surface.Handle)
        return false;
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, major);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, minor);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_CORE);
    SDL_GL_SetAttribute(SDL_GL_DOUBLEBUFFER, 1);
    SDL_GL_SetAttribute(SDL_GL_DEPTH_SIZE, 24);
    SDL_GL_SetAttribute(SDL_GL_STENCIL_SIZE, 8);
    auto* window = static_cast<SDL_Window*>(surface.Handle);
    auto context = SDL_GL_CreateContext(window);
    if (!context)
    {
        std::fprintf(stderr, "Unable to create OpenGL %d.%d Core: %s\n", major, minor, SDL_GetError());
        return false;
    }
    host.window = window;
    host.context = context;
    return MGGL_Host_MakeCurrent(host);
}

bool MGGL_Host_MakeCurrent(MGGL_Host& host)
{
    return host.window && host.context && SDL_GL_MakeCurrent(static_cast<SDL_Window*>(host.window), host.context) == 0;
}

bool MGGL_Host_Resize(MGGL_Host& host, MGG_PresentationSurface& surface, int, int)
{
    if (surface.Kind != MGPresentationSurfaceKind::SdlWindow || !surface.Handle)
        return false;
    host.window = surface.Handle;
    return MGGL_Host_MakeCurrent(host);
}

bool MGGL_Host_Present(MGGL_Host& host, int syncInterval)
{
    if (!MGGL_Host_MakeCurrent(host))
        return false;
    SDL_GL_SetSwapInterval(syncInterval > 0 ? 1 : 0);
    SDL_GL_SwapWindow(static_cast<SDL_Window*>(host.window));
    return true;
}

void MGGL_Host_Suspend(MGGL_Host&) {}

void MGGL_Host_Destroy(MGGL_Host& host)
{
    if (host.context)
        SDL_GL_DeleteContext(host.context);
    host = {};
}

void* MGGL_Host_GetProcAddress(const char* name) { return SDL_GL_GetProcAddress(name); }

bool MGGL_Host_GetOpenXrGraphicsBinding(MGGL_Host&, MGG_OpenXrGraphicsBinding&) { return false; }

#endif
