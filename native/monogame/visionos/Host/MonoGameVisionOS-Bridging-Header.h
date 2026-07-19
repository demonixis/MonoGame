// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#pragma once

#include "../include/MGV_Input.h"

// Implemented by the immersive presenter. The pointer is a retained
// CompositorServices LayerRenderer Objective-C object.
MGV_EXPORT void MGV_Host_AttachLayerRenderer(void* layerRenderer);
