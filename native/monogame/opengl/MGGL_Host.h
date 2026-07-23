// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#pragma once

#include "api_enums.h"
#include "api_structs.h"

struct MGGL_Host
{
    void* window = nullptr;
    void* display = nullptr;
    void* surface = nullptr;
    void* context = nullptr;
    unsigned int framebuffer = 0;
    unsigned int colorbuffer = 0;
};

bool MGGL_Host_Create(MGGL_Host& host, MGG_PresentationSurface& surface, int major, int minor);
bool MGGL_Host_MakeCurrent(MGGL_Host& host);
bool MGGL_Host_Resize(MGGL_Host& host, MGG_PresentationSurface& surface, int width, int height);
bool MGGL_Host_Present(MGGL_Host& host, int syncInterval);
void MGGL_Host_Suspend(MGGL_Host& host);
void MGGL_Host_Destroy(MGGL_Host& host);
void* MGGL_Host_GetProcAddress(const char* name);
bool MGGL_Host_GetOpenXrGraphicsBinding(MGGL_Host& host, MGG_OpenXrGraphicsBinding& binding);
