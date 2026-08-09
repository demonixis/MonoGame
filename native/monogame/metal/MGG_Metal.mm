// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "api_MGG.h"
#include "../common/MG_FrameTiming.h"

#include <Metal/Metal.h>
#include <QuartzCore/CAMetalLayer.h>
#include <TargetConditionals.h>
#if TARGET_OS_OSX
#include <CoreGraphics/CoreGraphics.h>
#endif
#include <dispatch/dispatch.h>
#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

#if TARGET_OS_OSX
#if __has_include("macos/AlphaTestEffect.metal.mgfxo.h") && \
    __has_include("macos/BasicEffect.metal.mgfxo.h") && \
    __has_include("macos/DualTextureEffect.metal.mgfxo.h") && \
    __has_include("macos/EnvironmentMapEffect.metal.mgfxo.h") && \
    __has_include("macos/SkinnedEffect.metal.mgfxo.h") && \
    __has_include("macos/SpriteEffect.metal.mgfxo.h")
#define MG_METAL_BUILTIN_EFFECTS 1
#include "macos/AlphaTestEffect.metal.mgfxo.h"
#include "macos/BasicEffect.metal.mgfxo.h"
#include "macos/DualTextureEffect.metal.mgfxo.h"
#include "macos/EnvironmentMapEffect.metal.mgfxo.h"
#include "macos/SkinnedEffect.metal.mgfxo.h"
#include "macos/SpriteEffect.metal.mgfxo.h"
#include "mg_effect.h"
#endif
#else
#if __has_include("ios/AlphaTestEffect.metal.mgfxo.h") && \
    __has_include("ios/BasicEffect.metal.mgfxo.h") && \
    __has_include("ios/DualTextureEffect.metal.mgfxo.h") && \
    __has_include("ios/EnvironmentMapEffect.metal.mgfxo.h") && \
    __has_include("ios/SkinnedEffect.metal.mgfxo.h") && \
    __has_include("ios/SpriteEffect.metal.mgfxo.h")
#define MG_METAL_BUILTIN_EFFECTS 1
#include "ios/AlphaTestEffect.metal.mgfxo.h"
#include "ios/BasicEffect.metal.mgfxo.h"
#include "ios/DualTextureEffect.metal.mgfxo.h"
#include "ios/EnvironmentMapEffect.metal.mgfxo.h"
#include "ios/SkinnedEffect.metal.mgfxo.h"
#include "ios/SpriteEffect.metal.mgfxo.h"
#include "mg_effect.h"
#endif
#endif

#if defined(MG_SDL2)
#include <SDL.h>
#include <SDL_metal.h>
#endif

static constexpr NSUInteger MGMetalMaxColorTargets = 4;
static constexpr NSUInteger MGMetalMaxVertexBuffers = 16;
static constexpr NSUInteger MGMetalMaxVertexAttributes = 20;
static constexpr NSUInteger MGMetalIRStageInAttributeStartIndex = 11;
static constexpr NSUInteger MGMetalIRDescriptorHeapBindPoint = 0;
static constexpr NSUInteger MGMetalIRSamplerHeapBindPoint = 1;
static constexpr NSUInteger MGMetalIRArgumentBufferBindPoint = 2;
static constexpr NSUInteger MGMetalIRDrawArgumentsBindPoint = 4;
static constexpr NSUInteger MGMetalIRDrawInfoBindPoint = 5;
static constexpr NSUInteger MGMetalIRDescriptorCount = 16;
static constexpr NSUInteger MGMetalBindingArenaInitialSize = 256 * 1024;
static constexpr mgint MGMetalTextureCompressionS3tc = 1 << 0;
static constexpr mgint MGMetalTextureCompressionEtc2 = 1 << 1;
static constexpr mgint MGMetalTextureCompressionAstc = 1 << 2;

static __strong id<MTLDevice> g_openXrMetalDevice = nil;

void MGG_OpenXR_ConfigureVulkanBootstrap(void*, void*, void*, void*) {}
void MGG_OpenXR_ConfigureDirect3D12Adapter(mglong, mguint) {}

void MGG_OpenXR_ConfigureMetalDevice(void* device)
{
    g_openXrMetalDevice = (__bridge id<MTLDevice>)device;
}

void* MGG_OpenXR_GetVulkanGetInstanceProcAddr() { return nullptr; }

// This is the public ABI consumed by shaders emitted by Apple's Metal Shader
// Converter.  Keeping the small subset used by MonoGame local avoids a runtime
// dependency on the converter dylib, which remains a content-build tool only.
struct MGMetalIRDescriptorTableEntry
{
    uint64_t gpuAddress;
    uint64_t textureResourceId;
    uint64_t metadata;
};

struct MGMetalIRDrawArgument
{
    uint32_t vertexCountPerInstance;
    uint32_t instanceCount;
    uint32_t startVertexLocation;
    uint32_t startInstanceLocation;
};

struct MGMetalIRDrawIndexedArgument
{
    uint32_t indexCountPerInstance;
    uint32_t instanceCount;
    uint32_t startIndexLocation;
    int32_t baseVertexLocation;
    uint32_t startInstanceLocation;
};

union MGMetalIRDrawParams
{
    MGMetalIRDrawArgument draw;
    MGMetalIRDrawIndexedArgument drawIndexed;
};

struct MGMetalBindingArena
{
    __strong id<MTLBuffer> buffer = nil;
    NSUInteger offset = 0;
};

struct MGG_GraphicsAdapter
{
    __strong id<MTLDevice> device = nil;
    std::string name;
    MGG_DisplayMode current = { MGSurfaceFormat::Color, 0, 0 };
    std::vector<MGG_DisplayMode> modes;
};

struct MGG_GraphicsSystem
{
    std::vector<MGG_GraphicsAdapter*> adapters;
};

struct MGG_Buffer
{
    __strong id<MTLBuffer> buffer = nil;
    MGBufferType type = MGBufferType::Vertex;
    bool dynamic = false;
    int size = 0;
};

struct MGG_Texture
{
    __strong id<MTLTexture> texture = nil;
    __strong id<MTLTexture> multisampleTexture = nil;
    __strong id<MTLTexture> depthTexture = nil;
    MGTextureType type = MGTextureType::_2D;
    MGSurfaceFormat format = MGSurfaceFormat::Color;
    MGDepthFormat depthFormat = MGDepthFormat::None;
    int width = 0;
    int height = 0;
    int depth = 0;
    int mipmaps = 0;
    int slices = 0;
    int sampleCount = 1;
    bool independentDepthStencil = false;
};

struct MGG_Shader
{
    uint64_t pipelineResourceId = 0;
    MGShaderStage stage = MGShaderStage::Vertex;
    __strong id<MTLLibrary> library = nil;
    __strong id<MTLFunction> function = nil;
    std::string reflection;
    int maxSamplerSlot = -1;
    int maxTextureSlot = -1;
    int constantBufferOffsets[16] = {};
    int textureOffsets[16] = {};
    int samplerOffsets[16] = {};
    int rootConstantBufferOffset = -1;
    int resourceTableOffset = -1;
    int samplerTableOffset = -1;
    int topLevelArgumentBufferSize = 0;
};

struct MGG_InputLayout
{
    uint64_t pipelineResourceId = 0;
    __strong MTLVertexDescriptor* descriptor = nil;
    std::vector<uint32_t> strides;
};

struct MGG_BlendState
{
    uint64_t pipelineResourceId = 0;
    MGG_BlendState_Info targets[MGMetalMaxColorTargets] = {};
};

struct MGG_DepthStencilState
{
    __strong id<MTLDepthStencilState> state = nil;
    int referenceStencil = 0;
};

struct MGG_RasterizerState
{
    MGG_RasterizerState_Info info = {};
};

struct MGG_SamplerState
{
    __strong id<MTLSamplerState> state = nil;
};

struct MGG_OcclusionQuery
{
    __strong id<MTLBuffer> visibility = nil;
    __strong id<MTLCommandBuffer> commandBuffer = nil;
};

struct MGMetalVertexBinding
{
    MGG_Buffer* buffer = nullptr;
    int offset = 0;
};

struct MGMetalPipelineKey
{
    uint64_t vertexShaderId = 0;
    uint64_t pixelShaderId = 0;
    uint64_t inputLayoutId = 0;
    uint64_t blendStateId = 0;
    uint64_t colorFormats[MGMetalMaxColorTargets] = {};
    uint64_t depthFormat = 0;
    uint32_t sampleCount = 1;
    uint32_t colorCount = 0;

    bool operator==(const MGMetalPipelineKey& other) const
    {
        return vertexShaderId == other.vertexShaderId &&
            pixelShaderId == other.pixelShaderId &&
            inputLayoutId == other.inputLayoutId &&
            blendStateId == other.blendStateId &&
            depthFormat == other.depthFormat &&
            sampleCount == other.sampleCount &&
            colorCount == other.colorCount &&
            memcmp(colorFormats, other.colorFormats, sizeof(colorFormats)) == 0;
    }
};

struct MGMetalPipelineKeyHash
{
    size_t operator()(const MGMetalPipelineKey& key) const
    {
        size_t hash = 1469598103934665603ull;
        const uint64_t values[] = {
            key.vertexShaderId,
            key.pixelShaderId,
            key.inputLayoutId,
            key.blendStateId,
            key.depthFormat,
            key.sampleCount,
            key.colorCount,
            key.colorFormats[0],
            key.colorFormats[1],
            key.colorFormats[2],
            key.colorFormats[3]
        };
        for (uint64_t value : values)
        {
            hash ^= (size_t)value;
            hash *= 1099511628211ull;
        }
        return hash;
    }
};

struct MGMetalPipelineCacheEntry
{
    MGMetalPipelineKey key = {};
    __strong id<MTLRenderPipelineState> pipeline = nil;
};

struct MGG_GraphicsDevice
{
    __strong id<MTLDevice> device = nil;
    __strong id<MTLCommandQueue> queue = nil;
    __strong CAMetalLayer* layer = nil;
    __strong id<CAMetalDrawable> drawable = nil;
    __strong id<MTLCommandBuffer> commandBuffer = nil;
    __strong id<MTLRenderCommandEncoder> encoder = nil;
    __strong id<MTLTexture> backBufferMultisample = nil;
    __strong id<MTLTexture> backBufferDepth = nil;
    __strong id<MTLRenderPipelineState> pipeline = nil;
    __strong id<MTLCommandBuffer> lastSubmitted = nil;
    __strong MTLRenderPassDescriptor* renderPassDescriptor = nil;
    dispatch_semaphore_t inFlight = nullptr;
#if defined(MG_SDL2)
    SDL_MetalView metalView = nullptr;
#endif
    int width = 0;
    int height = 0;
    int sampleCount = 1;
    int frame = 0;
    int syncInterval = 1;
    MTLViewport viewport = { 0, 0, 1, 1, 0, 1 };
    MTLScissorRect scissor = { 0, 0, 1, 1 };
    MGG_Texture* renderTargets[MGMetalMaxColorTargets] = {};
    int renderTargetSlices[MGMetalMaxColorTargets] = {};
    int renderTargetCount = 0;
    bool explicitRenderPass = false;
    MGG_RenderPassColorAttachment explicitColorAttachments[MGMetalMaxColorTargets] = {};
    MGG_RenderPassDepthStencilAttachment explicitDepthStencilAttachment = {};
    bool hasExplicitDepthStencilAttachment = false;
    MGG_Shader* vertexShader = nullptr;
    MGG_Shader* pixelShader = nullptr;
    MGG_InputLayout* inputLayout = nullptr;
    MGG_BlendState* blendState = nullptr;
    MGG_DepthStencilState* depthStencilState = nullptr;
    MGG_RasterizerState* rasterizerState = nullptr;
    MGG_Buffer* indexBuffer = nullptr;
    MGIndexElementSize indexType = MGIndexElementSize::SixteenBits;
    MGMetalVertexBinding vertexBuffers[MGMetalMaxVertexBuffers] = {};
    MGG_Buffer* constantBuffers[2][16] = {};
    MGG_Texture* textures[2][16] = {};
    MGG_SamplerState* samplers[2][16] = {};
    MGMetalBindingArena bindingArenas[3];
    std::unordered_map<MGMetalPipelineKey, size_t, MGMetalPipelineKeyHash> pipelineLookup;
    std::unordered_map<MGMetalPipelineKey, uint64_t, MGMetalPipelineKeyHash> pipelineFailureFrame;
    std::vector<MGMetalPipelineCacheEntry> pipelineCache;
    uint64_t nextPipelineResourceId = 1;
    uint64_t frameSerial = 0;
    uint64_t presentationSerial = 0;
    uint64_t pipelineRequestCount = 0;
    uint64_t pipelineCompileCount = 0;
    uint64_t pipelineCacheHitCount = 0;
    uint64_t pipelineFailureCount = 0;
    bool pipelineTrace = false;
    bool pipelineDirty = true;
    MGG_CompletedGpuFrameTimingQueue completedGpuFrameTimings;
};

static MTLPixelFormat MGMetalSurfaceFormat(MGSurfaceFormat format)
{
    switch (format)
    {
        case MGSurfaceFormat::Color: return MTLPixelFormatRGBA8Unorm;
        case MGSurfaceFormat::ColorSRgb: return MTLPixelFormatRGBA8Unorm_sRGB;
        case MGSurfaceFormat::Bgr32:
        case MGSurfaceFormat::Bgra32: return MTLPixelFormatBGRA8Unorm;
        case MGSurfaceFormat::Bgr32SRgb:
        case MGSurfaceFormat::Bgra32SRgb: return MTLPixelFormatBGRA8Unorm_sRGB;
        case MGSurfaceFormat::Alpha8: return MTLPixelFormatA8Unorm;
        case MGSurfaceFormat::Single: return MTLPixelFormatR32Float;
        case MGSurfaceFormat::Vector2: return MTLPixelFormatRG32Float;
        case MGSurfaceFormat::Vector4: return MTLPixelFormatRGBA32Float;
        case MGSurfaceFormat::HalfSingle: return MTLPixelFormatR16Float;
        case MGSurfaceFormat::HalfVector2: return MTLPixelFormatRG16Float;
        case MGSurfaceFormat::HalfVector4:
        case MGSurfaceFormat::HdrBlendable: return MTLPixelFormatRGBA16Float;
        case MGSurfaceFormat::Bgr565: return MTLPixelFormatB5G6R5Unorm;
        case MGSurfaceFormat::Bgra5551: return MTLPixelFormatA1BGR5Unorm;
        case MGSurfaceFormat::Bgra4444: return MTLPixelFormatABGR4Unorm;
        case MGSurfaceFormat::NormalizedByte2: return MTLPixelFormatRG8Snorm;
        case MGSurfaceFormat::NormalizedByte4: return MTLPixelFormatRGBA8Snorm;
        case MGSurfaceFormat::Rgba1010102: return MTLPixelFormatRGB10A2Unorm;
        case MGSurfaceFormat::Rg32: return MTLPixelFormatRG16Unorm;
        case MGSurfaceFormat::Rgba64: return MTLPixelFormatRGBA16Unorm;
        case MGSurfaceFormat::Dxt1:
        case MGSurfaceFormat::Dxt1a: return MTLPixelFormatBC1_RGBA;
        case MGSurfaceFormat::Dxt1SRgb: return MTLPixelFormatBC1_RGBA_sRGB;
        case MGSurfaceFormat::Dxt3: return MTLPixelFormatBC2_RGBA;
        case MGSurfaceFormat::Dxt3SRgb: return MTLPixelFormatBC2_RGBA_sRGB;
        case MGSurfaceFormat::Dxt5: return MTLPixelFormatBC3_RGBA;
        case MGSurfaceFormat::Dxt5SRgb: return MTLPixelFormatBC3_RGBA_sRGB;
        case MGSurfaceFormat::RgbEtc1:
        case MGSurfaceFormat::Rgb8Etc2: return MTLPixelFormatETC2_RGB8;
        case MGSurfaceFormat::Srgb8Etc2: return MTLPixelFormatETC2_RGB8_sRGB;
        case MGSurfaceFormat::Rgb8A1Etc2: return MTLPixelFormatETC2_RGB8A1;
        case MGSurfaceFormat::Srgb8A1Etc2: return MTLPixelFormatETC2_RGB8A1_sRGB;
        case MGSurfaceFormat::Rgba8Etc2: return MTLPixelFormatEAC_RGBA8;
        case MGSurfaceFormat::SRgb8A8Etc2: return MTLPixelFormatEAC_RGBA8_sRGB;
        case MGSurfaceFormat::Astc4X4Rgba: return MTLPixelFormatASTC_4x4_LDR;
        case MGSurfaceFormat::Astc5X5Rgba: return MTLPixelFormatASTC_5x5_LDR;
        case MGSurfaceFormat::Astc6X6Rgba: return MTLPixelFormatASTC_6x6_LDR;
        case MGSurfaceFormat::Astc8X8Rgba: return MTLPixelFormatASTC_8x8_LDR;
        case MGSurfaceFormat::Astc10X10Rgba: return MTLPixelFormatASTC_10x10_LDR;
        case MGSurfaceFormat::Astc12X12Rgba: return MTLPixelFormatASTC_12x12_LDR;
        default: return MTLPixelFormatInvalid;
    }
}

static MTLPixelFormat MGMetalDepthFormat(MGDepthFormat format)
{
    switch (format)
    {
        case MGDepthFormat::Depth16: return MTLPixelFormatDepth16Unorm;
        case MGDepthFormat::Depth24: return MTLPixelFormatDepth32Float;
        case MGDepthFormat::Depth24Stencil8: return MTLPixelFormatDepth32Float_Stencil8;
        case MGDepthFormat::Depth32Float: return MTLPixelFormatDepth32Float;
        default: return MTLPixelFormatInvalid;
    }
}

static bool MGMetalSupportsSampleableDepthSemantic(MGDepthFormat format)
{
    // Metal has no exact 24-bit depth-only texture format. Keep the legacy
    // Depth24 promotion isolated from the explicit v3 contract. The
    // Depth24Stencil8 semantic retains stencil and may use higher depth
    // precision, matching Metal's only portable packed depth/stencil format.
    return format == MGDepthFormat::Depth16 ||
        format == MGDepthFormat::Depth24Stencil8 ||
        format == MGDepthFormat::Depth32Float;
}

static MTLLoadAction MGMetalLoadAction(MGRenderPassLoadAction action)
{
    switch (action)
    {
        case MGRenderPassLoadAction::Load: return MTLLoadActionLoad;
        case MGRenderPassLoadAction::Clear: return MTLLoadActionClear;
        default: return MTLLoadActionDontCare;
    }
}

static MTLStoreAction MGMetalStoreAction(MGRenderPassStoreAction action)
{
    return action == MGRenderPassStoreAction::Store
        ? MTLStoreActionStore
        : MTLStoreActionDontCare;
}

static MTLColorWriteMask MGMetalColorWriteMask(MGColorWriteChannels channels)
{
    const mgint bits = static_cast<mgint>(channels);
    MTLColorWriteMask mask = MTLColorWriteMaskNone;
    if ((bits & static_cast<mgint>(MGColorWriteChannels::Red)) != 0)
        mask |= MTLColorWriteMaskRed;
    if ((bits & static_cast<mgint>(MGColorWriteChannels::Green)) != 0)
        mask |= MTLColorWriteMaskGreen;
    if ((bits & static_cast<mgint>(MGColorWriteChannels::Blue)) != 0)
        mask |= MTLColorWriteMaskBlue;
    if ((bits & static_cast<mgint>(MGColorWriteChannels::Alpha)) != 0)
        mask |= MTLColorWriteMaskAlpha;
    return mask;
}

static MTLCompareFunction MGMetalCompare(MGCompareFunction function)
{
    switch (function)
    {
        case MGCompareFunction::Never: return MTLCompareFunctionNever;
        case MGCompareFunction::Less: return MTLCompareFunctionLess;
        case MGCompareFunction::LessEqual: return MTLCompareFunctionLessEqual;
        case MGCompareFunction::Equal: return MTLCompareFunctionEqual;
        case MGCompareFunction::GreaterEqual: return MTLCompareFunctionGreaterEqual;
        case MGCompareFunction::Greater: return MTLCompareFunctionGreater;
        case MGCompareFunction::NotEqual: return MTLCompareFunctionNotEqual;
        default: return MTLCompareFunctionAlways;
    }
}

static MTLStencilOperation MGMetalStencil(MGStencilOperation operation)
{
    switch (operation)
    {
        case MGStencilOperation::Zero: return MTLStencilOperationZero;
        case MGStencilOperation::Replace: return MTLStencilOperationReplace;
        case MGStencilOperation::Increment: return MTLStencilOperationIncrementWrap;
        case MGStencilOperation::Decrement: return MTLStencilOperationDecrementWrap;
        case MGStencilOperation::IncrementSaturation: return MTLStencilOperationIncrementClamp;
        case MGStencilOperation::DecrementSaturation: return MTLStencilOperationDecrementClamp;
        case MGStencilOperation::Invert: return MTLStencilOperationInvert;
        default: return MTLStencilOperationKeep;
    }
}

static MTLBlendFactor MGMetalBlend(MGBlend blend)
{
    switch (blend)
    {
        case MGBlend::Zero: return MTLBlendFactorZero;
        case MGBlend::SourceColor: return MTLBlendFactorSourceColor;
        case MGBlend::InverseSourceColor: return MTLBlendFactorOneMinusSourceColor;
        case MGBlend::SourceAlpha: return MTLBlendFactorSourceAlpha;
        case MGBlend::InverseSourceAlpha: return MTLBlendFactorOneMinusSourceAlpha;
        case MGBlend::DestinationColor: return MTLBlendFactorDestinationColor;
        case MGBlend::InverseDestinationColor: return MTLBlendFactorOneMinusDestinationColor;
        case MGBlend::DestinationAlpha: return MTLBlendFactorDestinationAlpha;
        case MGBlend::InverseDestinationAlpha: return MTLBlendFactorOneMinusDestinationAlpha;
        case MGBlend::BlendFactor: return MTLBlendFactorBlendColor;
        case MGBlend::InverseBlendFactor: return MTLBlendFactorOneMinusBlendColor;
        case MGBlend::SourceAlphaSaturation: return MTLBlendFactorSourceAlphaSaturated;
        default: return MTLBlendFactorOne;
    }
}

static MTLBlendOperation MGMetalBlendOperation(MGBlendFunction function)
{
    switch (function)
    {
        case MGBlendFunction::Subtract: return MTLBlendOperationSubtract;
        case MGBlendFunction::ReverseSubtract: return MTLBlendOperationReverseSubtract;
        case MGBlendFunction::Min: return MTLBlendOperationMin;
        case MGBlendFunction::Max: return MTLBlendOperationMax;
        default: return MTLBlendOperationAdd;
    }
}

static MTLVertexFormat MGMetalVertexFormat(MGVertexElementFormat format)
{
    switch (format)
    {
        case MGVertexElementFormat::Single: return MTLVertexFormatFloat;
        case MGVertexElementFormat::Vector2: return MTLVertexFormatFloat2;
        case MGVertexElementFormat::Vector3: return MTLVertexFormatFloat3;
        case MGVertexElementFormat::Vector4: return MTLVertexFormatFloat4;
        case MGVertexElementFormat::Color: return MTLVertexFormatUChar4Normalized;
        case MGVertexElementFormat::Byte4: return MTLVertexFormatUChar4;
        case MGVertexElementFormat::Short2: return MTLVertexFormatShort2;
        case MGVertexElementFormat::Short4: return MTLVertexFormatShort4;
        case MGVertexElementFormat::NormalizedShort2: return MTLVertexFormatShort2Normalized;
        case MGVertexElementFormat::NormalizedShort4: return MTLVertexFormatShort4Normalized;
        case MGVertexElementFormat::HalfVector2: return MTLVertexFormatHalf2;
        case MGVertexElementFormat::HalfVector4: return MTLVertexFormatHalf4;
        default: return MTLVertexFormatInvalid;
    }
}

static MTLPrimitiveType MGMetalPrimitive(MGPrimitiveType type)
{
    switch (type)
    {
        case MGPrimitiveType::TriangleStrip: return MTLPrimitiveTypeTriangleStrip;
        case MGPrimitiveType::LineList: return MTLPrimitiveTypeLine;
        case MGPrimitiveType::LineStrip: return MTLPrimitiveTypeLineStrip;
        case MGPrimitiveType::PointList: return MTLPrimitiveTypePoint;
        default: return MTLPrimitiveTypeTriangle;
    }
}

static int MGMetalPrimitiveElementCount(MGPrimitiveType type, int primitiveCount)
{
    switch (type)
    {
        case MGPrimitiveType::TriangleList: return primitiveCount * 3;
        case MGPrimitiveType::TriangleStrip: return primitiveCount + 2;
        case MGPrimitiveType::LineList: return primitiveCount * 2;
        case MGPrimitiveType::LineStrip: return primitiveCount + 1;
        default: return primitiveCount;
    }
}

static int MGMetalCompressedBlockHeight(MGSurfaceFormat format)
{
    switch (format)
    {
        case MGSurfaceFormat::Dxt1:
        case MGSurfaceFormat::Dxt1a:
        case MGSurfaceFormat::Dxt1SRgb:
        case MGSurfaceFormat::Dxt3:
        case MGSurfaceFormat::Dxt3SRgb:
        case MGSurfaceFormat::Dxt5:
        case MGSurfaceFormat::Dxt5SRgb:
        case MGSurfaceFormat::RgbEtc1:
        case MGSurfaceFormat::Rgb8Etc2:
        case MGSurfaceFormat::Srgb8Etc2:
        case MGSurfaceFormat::Rgb8A1Etc2:
        case MGSurfaceFormat::Srgb8A1Etc2:
        case MGSurfaceFormat::Rgba8Etc2:
        case MGSurfaceFormat::SRgb8A8Etc2:
        case MGSurfaceFormat::Astc4X4Rgba:
            return 4;
        case MGSurfaceFormat::Astc5X5Rgba:
            return 5;
        case MGSurfaceFormat::Astc6X6Rgba:
            return 6;
        case MGSurfaceFormat::Astc8X8Rgba:
            return 8;
        case MGSurfaceFormat::Astc10X10Rgba:
            return 10;
        case MGSurfaceFormat::Astc12X12Rgba:
            return 12;
        default:
            return 1;
    }
}

static NSUInteger MGMetalBytesPerRow(MGSurfaceFormat format, int height, int depth, int dataBytes)
{
    const int blockHeight = MGMetalCompressedBlockHeight(format);
    int rowCount = std::max((height + blockHeight - 1) / blockHeight, 1);
    const int bytesPerImage = dataBytes / std::max(depth, 1);
    return (NSUInteger)(bytesPerImage / rowCount);
}

static int MGMetalShaderStageIndex(MGShaderStage stage)
{
    if (stage == MGShaderStage::Vertex)
        return 0;
    if (stage == MGShaderStage::Pixel)
        return 1;
    return -1;
}

static void MGMetalEndEncoder(MGG_GraphicsDevice* device)
{
    if (device->encoder != nil)
    {
        [device->encoder endEncoding];
        device->encoder = nil;
    }
}

static int MGMetalSupportedSampleCount(id<MTLDevice> device, int requested)
{
    int sampleCount = std::max(requested, 1);
    while (sampleCount > 1 && ![device supportsTextureSampleCount:sampleCount])
        --sampleCount;
    return sampleCount;
}

static MTLStorageMode MGMetalCpuVisibleBufferStorageMode(id<MTLDevice> device)
{
#if TARGET_OS_OSX
    return device.hasUnifiedMemory ? MTLStorageModeShared : MTLStorageModeManaged;
#else
    (void)device;
    return MTLStorageModeShared;
#endif
}

static MTLStorageMode MGMetalCpuVisibleTextureStorageMode(id<MTLDevice> device)
{
#if TARGET_OS_OSX
    // Shared textures are unavailable on non-Apple GPU families, including
    // Intel and AMD devices. Managed textures keep the universal macOS build
    // functional on those systems.
    return [device supportsFamily:MTLGPUFamilyApple1] ? MTLStorageModeShared : MTLStorageModeManaged;
#else
    (void)device;
    return MTLStorageModeShared;
#endif
}

static MTLScissorRect MGMetalScissorRect(MGG_GraphicsDevice* device, bool enabled)
{
    NSUInteger width = (NSUInteger)std::max(device->width, 1);
    NSUInteger height = (NSUInteger)std::max(device->height, 1);
    if (device->renderTargetCount > 0 && device->renderTargets[0] != nullptr)
    {
        width = (NSUInteger)std::max(device->renderTargets[0]->width, 1);
        height = (NSUInteger)std::max(device->renderTargets[0]->height, 1);
    }

    if (!enabled)
        return { 0, 0, width, height };

    const NSUInteger x = std::min(device->scissor.x, width);
    const NSUInteger y = std::min(device->scissor.y, height);
    return {
        x,
        y,
        std::min(device->scissor.width, width - x),
        std::min(device->scissor.height, height - y)
    };
}

static void MGMetalReportCommandBufferError(id<MTLCommandBuffer> commandBuffer)
{
    if (commandBuffer.status == MTLCommandBufferStatusError)
    {
        fprintf(
            stderr,
            "Metal command buffer failed: %s\n",
            commandBuffer.error.localizedDescription.UTF8String);
    }
}

static id<MTLTexture> MGMetalCreateDepthTexture(
    id<MTLDevice> device,
    int width,
    int height,
    MGDepthFormat format,
    int sampleCount,
    int slices = 1,
    bool sampleable = false)
{
    MTLPixelFormat pixelFormat = MGMetalDepthFormat(format);
    if (pixelFormat == MTLPixelFormatInvalid)
        return nil;

    MTLTextureDescriptor* descriptor = [MTLTextureDescriptor new];
    if (sampleCount > 1)
        descriptor.textureType = slices > 1 ? MTLTextureType2DMultisampleArray : MTLTextureType2DMultisample;
    else
        descriptor.textureType = slices > 1 ? MTLTextureType2DArray : MTLTextureType2D;
    descriptor.pixelFormat = pixelFormat;
    descriptor.width = std::max(width, 1);
    descriptor.height = std::max(height, 1);
    descriptor.sampleCount = std::max(sampleCount, 1);
    descriptor.arrayLength = std::max(slices, 1);
    descriptor.storageMode = MTLStorageModePrivate;
    descriptor.usage = MTLTextureUsageRenderTarget |
        (sampleable ? MTLTextureUsageShaderRead : (MTLTextureUsage)0);
    return [device newTextureWithDescriptor:descriptor];
}

static bool MGMetalSupportsSampleableDepthFormat(
    id<MTLDevice> device,
    MGDepthFormat format)
{
    if (device == nil || !MGMetalSupportsSampleableDepthSemantic(format))
        return false;

    id<MTLTexture> probe = MGMetalCreateDepthTexture(
        device,
        1,
        1,
        format,
        1,
        1,
        true);
    return probe != nil;
}

static MTLRenderPassDescriptor* MGMetalCreateRenderPass(
    MGG_GraphicsDevice* device,
    MGClearOptions clearOptions,
    Vector4 clearColor,
    float clearDepth,
    int clearStencil)
{
    if (device->renderPassDescriptor == nil)
        device->renderPassDescriptor = [MTLRenderPassDescriptor new];
    MTLRenderPassDescriptor* pass = device->renderPassDescriptor;
    for (int index = 0; index < MGMetalMaxColorTargets; ++index)
    {
        MTLRenderPassColorAttachmentDescriptor* color = pass.colorAttachments[index];
        color.texture = nil;
        color.resolveTexture = nil;
        color.level = 0;
        color.slice = 0;
        color.depthPlane = 0;
        color.resolveLevel = 0;
        color.resolveSlice = 0;
        color.loadAction = MTLLoadActionDontCare;
        color.storeAction = MTLStoreActionDontCare;
    }
    pass.depthAttachment.texture = nil;
    pass.depthAttachment.level = 0;
    pass.depthAttachment.slice = 0;
    pass.depthAttachment.depthPlane = 0;
    pass.depthAttachment.loadAction = MTLLoadActionDontCare;
    pass.depthAttachment.storeAction = MTLStoreActionDontCare;
    pass.depthAttachment.clearDepth = 1.0;
    pass.stencilAttachment.texture = nil;
    pass.stencilAttachment.level = 0;
    pass.stencilAttachment.slice = 0;
    pass.stencilAttachment.depthPlane = 0;
    pass.stencilAttachment.loadAction = MTLLoadActionDontCare;
    pass.stencilAttachment.storeAction = MTLStoreActionDontCare;
    pass.stencilAttachment.clearStencil = 0;
    const bool clearTarget = (((int)clearOptions & (int)MGClearOptions::Target) != 0);

    if (device->renderTargetCount == 0)
    {
        if (device->drawable == nil)
            return nil;

        MTLRenderPassColorAttachmentDescriptor* color = pass.colorAttachments[0];
        if (device->sampleCount > 1)
        {
            color.texture = device->backBufferMultisample;
            color.resolveTexture = device->drawable.texture;
            color.storeAction = MTLStoreActionMultisampleResolve;
        }
        else
        {
            color.texture = device->drawable.texture;
            color.storeAction = MTLStoreActionStore;
        }
        color.loadAction = clearTarget ? MTLLoadActionClear : MTLLoadActionLoad;
        color.clearColor = MTLClearColorMake(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);

        if (device->backBufferDepth != nil)
        {
            pass.depthAttachment.texture = device->backBufferDepth;
            pass.depthAttachment.loadAction = (((int)clearOptions & (int)MGClearOptions::DepthBuffer) != 0) ? MTLLoadActionClear : MTLLoadActionLoad;
            pass.depthAttachment.storeAction = MTLStoreActionStore;
            pass.depthAttachment.clearDepth = clearDepth;
            if (device->backBufferDepth.pixelFormat == MTLPixelFormatDepth32Float_Stencil8)
            {
                pass.stencilAttachment.texture = device->backBufferDepth;
                pass.stencilAttachment.loadAction = (((int)clearOptions & (int)MGClearOptions::Stencil) != 0) ? MTLLoadActionClear : MTLLoadActionLoad;
                pass.stencilAttachment.storeAction = MTLStoreActionStore;
                pass.stencilAttachment.clearStencil = clearStencil;
            }
        }
    }
    else
    {
        for (int i = 0; i < device->renderTargetCount; ++i)
        {
            MGG_Texture* target = device->renderTargets[i];
            MTLRenderPassColorAttachmentDescriptor* color = pass.colorAttachments[i];
            color.level = 0;
            const NSUInteger slice = (NSUInteger)std::max(device->renderTargetSlices[i], 0);
            if (target->multisampleTexture != nil)
            {
                color.texture = target->multisampleTexture;
                color.slice = slice;
                color.resolveTexture = target->texture;
                color.resolveSlice = slice;
                color.storeAction = MTLStoreActionMultisampleResolve;
            }
            else
            {
                color.texture = target->texture;
                color.slice = slice;
                color.storeAction = device->explicitRenderPass
                    ? MGMetalStoreAction(device->explicitColorAttachments[i].StoreAction)
                    : MTLStoreActionStore;
            }
            if (device->explicitRenderPass)
            {
                const auto& attachment = device->explicitColorAttachments[i];
                color.loadAction = MGMetalLoadAction(attachment.LoadAction);
                color.clearColor = MTLClearColorMake(
                    attachment.ClearColor.X,
                    attachment.ClearColor.Y,
                    attachment.ClearColor.Z,
                    attachment.ClearColor.W);
            }
            else
            {
                color.loadAction = clearTarget ? MTLLoadActionClear : MTLLoadActionLoad;
                color.clearColor = MTLClearColorMake(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
            }
        }

        MGG_Texture* depthTarget = device->hasExplicitDepthStencilAttachment
            ? static_cast<MGG_Texture*>(device->explicitDepthStencilAttachment.Target)
            : device->renderTargets[0];
        id<MTLTexture> depthTexture = device->hasExplicitDepthStencilAttachment
            ? depthTarget->texture
            : depthTarget->depthTexture;
        if (depthTexture != nil)
        {
            const NSUInteger depthSlice = device->hasExplicitDepthStencilAttachment
                ? 0
                : (NSUInteger)std::max(device->renderTargetSlices[0], 0);
            pass.depthAttachment.texture = depthTexture;
            pass.depthAttachment.slice = depthSlice;
            if (device->hasExplicitDepthStencilAttachment)
            {
                const auto& attachment = device->explicitDepthStencilAttachment;
                pass.depthAttachment.loadAction = MGMetalLoadAction(attachment.DepthLoadAction);
                pass.depthAttachment.storeAction = MGMetalStoreAction(attachment.DepthStoreAction);
                pass.depthAttachment.clearDepth = attachment.ClearDepth;
            }
            else
            {
                pass.depthAttachment.loadAction = (((int)clearOptions & (int)MGClearOptions::DepthBuffer) != 0) ? MTLLoadActionClear : MTLLoadActionLoad;
                pass.depthAttachment.storeAction = MTLStoreActionStore;
                pass.depthAttachment.clearDepth = clearDepth;
            }
            if (depthTexture.pixelFormat == MTLPixelFormatDepth32Float_Stencil8)
            {
                pass.stencilAttachment.texture = depthTexture;
                pass.stencilAttachment.slice = depthSlice;
                if (device->hasExplicitDepthStencilAttachment)
                {
                    const auto& attachment = device->explicitDepthStencilAttachment;
                    pass.stencilAttachment.loadAction = MGMetalLoadAction(attachment.StencilLoadAction);
                    pass.stencilAttachment.storeAction = MGMetalStoreAction(attachment.StencilStoreAction);
                    pass.stencilAttachment.clearStencil = attachment.ClearStencil;
                }
                else
                {
                    pass.stencilAttachment.loadAction = (((int)clearOptions & (int)MGClearOptions::Stencil) != 0) ? MTLLoadActionClear : MTLLoadActionLoad;
                    pass.stencilAttachment.storeAction = MTLStoreActionStore;
                    pass.stencilAttachment.clearStencil = clearStencil;
                }
            }
        }
    }

    return pass;
}

static bool MGMetalEnsureEncoder(MGG_GraphicsDevice* device)
{
    if (device->encoder != nil)
        return true;
    if (device->commandBuffer == nil)
        return false;

    Vector4 color = { 0, 0, 0, 0 };
    MTLRenderPassDescriptor* pass = MGMetalCreateRenderPass(device, (MGClearOptions)0, color, 1.0f, 0);
    if (pass == nil)
        return false;

    device->encoder = [device->commandBuffer renderCommandEncoderWithDescriptor:pass];
    [device->encoder setViewport:device->viewport];
    const bool scissorEnabled = device->rasterizerState != nullptr &&
        device->rasterizerState->info.scissorTestEnable;
    [device->encoder setScissorRect:MGMetalScissorRect(device, scissorEnabled)];
    return true;
}

static MTLPixelFormat MGMetalCurrentColorFormat(MGG_GraphicsDevice* device, int index)
{
    if (device->renderTargetCount == 0)
        return device->layer.pixelFormat;
    if (index >= device->renderTargetCount)
        return MTLPixelFormatInvalid;
    return device->renderTargets[index]->texture.pixelFormat;
}

static MTLPixelFormat MGMetalCurrentDepthFormat(MGG_GraphicsDevice* device)
{
    if (device->renderTargetCount == 0)
        return device->backBufferDepth == nil ? MTLPixelFormatInvalid : device->backBufferDepth.pixelFormat;
    if (device->hasExplicitDepthStencilAttachment)
        return static_cast<MGG_Texture*>(device->explicitDepthStencilAttachment.Target)->texture.pixelFormat;
    MGG_Texture* first = device->renderTargets[0];
    return first->depthTexture == nil ? MTLPixelFormatInvalid : first->depthTexture.pixelFormat;
}

static NSUInteger MGMetalAlignUp(NSUInteger value, NSUInteger alignment)
{
    return (value + alignment - 1) & ~(alignment - 1);
}

static id<MTLBuffer> MGMetalAllocateBindingBlock(
    MGG_GraphicsDevice* device,
    NSUInteger length,
    NSUInteger& offset)
{
    MGMetalBindingArena& arena = device->bindingArenas[device->frame];
    offset = MGMetalAlignUp(arena.offset, 256);
    if (arena.buffer == nil || offset + length > arena.buffer.length)
    {
        const NSUInteger previousLength = arena.buffer == nil ? 0 : arena.buffer.length;
        const NSUInteger newLength = std::max(
            MGMetalBindingArenaInitialSize,
            std::max(previousLength * 2, MGMetalAlignUp(length, 256)));
        arena.buffer = [device->device newBufferWithLength:newLength options:MTLResourceStorageModeShared];
        arena.offset = 0;
        offset = 0;
    }
    if (arena.buffer == nil)
        return nil;
    arena.offset = offset + MGMetalAlignUp(length, 256);
    return arena.buffer;
}

static MGMetalIRDescriptorTableEntry MGMetalTextureDescriptor(id<MTLTexture> texture)
{
    MGMetalIRDescriptorTableEntry entry = {};
    entry.textureResourceId = texture.gpuResourceID._impl;
    return entry;
}

static MGMetalIRDescriptorTableEntry MGMetalSamplerDescriptor(id<MTLSamplerState> sampler)
{
    MGMetalIRDescriptorTableEntry entry = {};
    entry.gpuAddress = sampler.gpuResourceID._impl;
    return entry;
}

static bool MGMetalBindShaderResources(
    MGG_GraphicsDevice* device,
    MGShaderStage stage,
    MGG_Shader* shader)
{
    if (shader == nullptr)
        return false;

    const NSUInteger topLevelSize = (NSUInteger)std::max(
        shader->topLevelArgumentBufferSize,
        (int)sizeof(uint64_t));
    const NSUInteger resourceTableOffset = MGMetalAlignUp(topLevelSize, 16);
    const NSUInteger resourceTableSize = sizeof(MGMetalIRDescriptorTableEntry) * MGMetalIRDescriptorCount;
    const NSUInteger samplerTableOffset = MGMetalAlignUp(resourceTableOffset + resourceTableSize, 16);
    const NSUInteger samplerTableSize = sizeof(MGMetalIRDescriptorTableEntry) * MGMetalIRDescriptorCount;
    const NSUInteger blockSize = samplerTableOffset + samplerTableSize;

    NSUInteger blockOffset = 0;
    id<MTLBuffer> bindings = MGMetalAllocateBindingBlock(device, blockSize, blockOffset);
    if (bindings == nil)
        return false;

    uint8_t* block = (uint8_t*)bindings.contents + blockOffset;
    memset(block, 0, blockSize);
    MGMetalIRDescriptorTableEntry* resources =
        (MGMetalIRDescriptorTableEntry*)(block + resourceTableOffset);
    MGMetalIRDescriptorTableEntry* samplers =
        (MGMetalIRDescriptorTableEntry*)(block + samplerTableOffset);

    const int stageIndex = MGMetalShaderStageIndex(stage);
    MGG_Buffer* rootConstant = device->constantBuffers[stageIndex][0];
    if (shader->rootConstantBufferOffset >= 0 && rootConstant != nullptr && rootConstant->buffer != nil)
        *(uint64_t*)(block + shader->rootConstantBufferOffset) = rootConstant->buffer.gpuAddress;
    if (shader->resourceTableOffset >= 0)
        *(uint64_t*)(block + shader->resourceTableOffset) =
            bindings.gpuAddress + blockOffset + resourceTableOffset;
    if (shader->samplerTableOffset >= 0)
        *(uint64_t*)(block + shader->samplerTableOffset) =
            bindings.gpuAddress + blockOffset + samplerTableOffset;

    const MTLRenderStages renderStage = stage == MGShaderStage::Vertex ?
        MTLRenderStageVertex : MTLRenderStageFragment;
    for (int slot = 0; slot < 16; ++slot)
    {
        MGG_Buffer* constant = device->constantBuffers[stageIndex][slot];
        if (constant == nullptr || constant->buffer == nil)
            continue;
        if (shader->constantBufferOffsets[slot] >= 0)
        {
            MGMetalIRDescriptorTableEntry descriptor = {};
            descriptor.gpuAddress = constant->buffer.gpuAddress;
            descriptor.metadata = constant->size;
            memcpy(block + shader->constantBufferOffsets[slot], &descriptor, sizeof(descriptor));
        }
        if (shader->constantBufferOffsets[slot] >= 0 ||
            (slot == 0 && shader->rootConstantBufferOffset >= 0))
            [device->encoder useResource:constant->buffer usage:MTLResourceUsageRead stages:renderStage];
    }

    const int maxTextureSlot = std::min(shader->maxTextureSlot, (int)MGMetalIRDescriptorCount - 1);
    for (int slot = 0; slot < (int)MGMetalIRDescriptorCount; ++slot)
    {
        if (slot > maxTextureSlot && shader->textureOffsets[slot] < 0)
            continue;
        MGG_Texture* texture = device->textures[stageIndex][slot];
        if (texture == nullptr || texture->texture == nil)
            continue;
        const MGMetalIRDescriptorTableEntry descriptor = MGMetalTextureDescriptor(texture->texture);
        resources[slot] = descriptor;
        if (shader->textureOffsets[slot] >= 0)
            memcpy(block + shader->textureOffsets[slot], &descriptor, sizeof(descriptor));
        [device->encoder useResource:texture->texture usage:MTLResourceUsageRead stages:renderStage];
    }

    const int maxSamplerSlot = std::min(shader->maxSamplerSlot, (int)MGMetalIRDescriptorCount - 1);
    for (int slot = 0; slot < (int)MGMetalIRDescriptorCount; ++slot)
    {
        if (slot > maxSamplerSlot && shader->samplerOffsets[slot] < 0)
            continue;
        MGG_SamplerState* sampler = device->samplers[stageIndex][slot];
        if (sampler != nullptr && sampler->state != nil)
        {
            const MGMetalIRDescriptorTableEntry descriptor = MGMetalSamplerDescriptor(sampler->state);
            samplers[slot] = descriptor;
            if (shader->samplerOffsets[slot] >= 0)
                memcpy(block + shader->samplerOffsets[slot], &descriptor, sizeof(descriptor));
        }
    }

    if (stage == MGShaderStage::Vertex)
    {
        [device->encoder setVertexBuffer:bindings
                                  offset:blockOffset + resourceTableOffset
                                 atIndex:MGMetalIRDescriptorHeapBindPoint];
        [device->encoder setVertexBuffer:bindings
                                  offset:blockOffset + samplerTableOffset
                                 atIndex:MGMetalIRSamplerHeapBindPoint];
        [device->encoder setVertexBuffer:bindings offset:blockOffset atIndex:MGMetalIRArgumentBufferBindPoint];
    }
    else
    {
        [device->encoder setFragmentBuffer:bindings
                                    offset:blockOffset + resourceTableOffset
                                   atIndex:MGMetalIRDescriptorHeapBindPoint];
        [device->encoder setFragmentBuffer:bindings
                                    offset:blockOffset + samplerTableOffset
                                   atIndex:MGMetalIRSamplerHeapBindPoint];
        [device->encoder setFragmentBuffer:bindings offset:blockOffset atIndex:MGMetalIRArgumentBufferBindPoint];
    }
    return true;
}

static bool MGMetalUpdatePipeline(MGG_GraphicsDevice* device)
{
    if (!device->pipelineDirty && device->pipeline != nil)
        return true;
    if (device->vertexShader == nullptr || device->pixelShader == nullptr ||
        device->vertexShader->function == nil || device->pixelShader->function == nil)
        return false;

    MGMetalPipelineKey key = {};
    key.vertexShaderId = device->vertexShader->pipelineResourceId;
    key.pixelShaderId = device->pixelShader->pipelineResourceId;
    key.inputLayoutId = device->inputLayout == nullptr ? 0 : device->inputLayout->pipelineResourceId;
    key.blendStateId = device->blendState == nullptr ? 0 : device->blendState->pipelineResourceId;
    key.sampleCount = (uint32_t)(device->renderTargetCount == 0 ?
        device->sampleCount : device->renderTargets[0]->sampleCount);
    key.depthFormat = (uint64_t)MGMetalCurrentDepthFormat(device);
    key.colorCount = (uint32_t)(device->renderTargetCount == 0 ? 1 : device->renderTargetCount);
    for (uint32_t i = 0; i < key.colorCount; ++i)
        key.colorFormats[i] = (uint64_t)MGMetalCurrentColorFormat(device, (int)i);

    ++device->pipelineRequestCount;
    const auto cached = device->pipelineLookup.find(key);
    if (cached != device->pipelineLookup.end())
    {
        device->pipeline = device->pipelineCache[cached->second].pipeline;
        device->pipelineDirty = false;
        ++device->pipelineCacheHitCount;
        return true;
    }

    // A compiler-service interruption can otherwise make every draw using the
    // same variant retry synchronously and amplify one transient failure into a
    // frame-long compile storm.  Retry failed variants on the next frame.
    const auto failed = device->pipelineFailureFrame.find(key);
    if (failed != device->pipelineFailureFrame.end() && failed->second == device->frameSerial)
        return false;

    MTLRenderPipelineDescriptor* descriptor = [MTLRenderPipelineDescriptor new];
    descriptor.vertexFunction = device->vertexShader->function;
    descriptor.fragmentFunction = device->pixelShader->function;
    descriptor.vertexDescriptor = device->inputLayout == nullptr ? nil : device->inputLayout->descriptor;
    descriptor.rasterSampleCount = key.sampleCount;
    descriptor.depthAttachmentPixelFormat = (MTLPixelFormat)key.depthFormat;
    if (descriptor.depthAttachmentPixelFormat == MTLPixelFormatDepth32Float_Stencil8)
        descriptor.stencilAttachmentPixelFormat = descriptor.depthAttachmentPixelFormat;

    const int colorCount = (int)key.colorCount;
    for (int i = 0; i < colorCount; ++i)
    {
        MTLRenderPipelineColorAttachmentDescriptor* color = descriptor.colorAttachments[i];
        color.pixelFormat = (MTLPixelFormat)key.colorFormats[i];
        if (device->blendState == nullptr)
            continue;

        const MGG_BlendState_Info& info = device->blendState->targets[i];
        color.blendingEnabled = !(info.colorSourceBlend == MGBlend::One &&
                                  info.colorDestBlend == MGBlend::Zero &&
                                  info.alphaSourceBlend == MGBlend::One &&
                                  info.alphaDestBlend == MGBlend::Zero);
        color.sourceRGBBlendFactor = MGMetalBlend(info.colorSourceBlend);
        color.destinationRGBBlendFactor = MGMetalBlend(info.colorDestBlend);
        color.rgbBlendOperation = MGMetalBlendOperation(info.colorBlendFunc);
        color.sourceAlphaBlendFactor = MGMetalBlend(info.alphaSourceBlend);
        color.destinationAlphaBlendFactor = MGMetalBlend(info.alphaDestBlend);
        color.alphaBlendOperation = MGMetalBlendOperation(info.alphaBlendFunc);
        color.writeMask = MGMetalColorWriteMask(info.colorWriteChannels);
    }

    NSError* error = nil;
    ++device->pipelineCompileCount;
    device->pipeline = [device->device newRenderPipelineStateWithDescriptor:descriptor error:&error];
    if (device->pipeline == nil)
    {
        ++device->pipelineFailureCount;
        device->pipelineFailureFrame[key] = device->frameSerial;
        fprintf(
            stderr,
            "Metal pipeline creation failed: compile=%llu failures=%llu frame=%llu "
            "vs=%llu ps=%llu layout=%llu blend=%llu colors=%u samples=%u depth=%llu "
            "formats=[%llu,%llu,%llu,%llu]: %s\n",
            (unsigned long long)device->pipelineCompileCount,
            (unsigned long long)device->pipelineFailureCount,
            (unsigned long long)device->frameSerial,
            (unsigned long long)key.vertexShaderId,
            (unsigned long long)key.pixelShaderId,
            (unsigned long long)key.inputLayoutId,
            (unsigned long long)key.blendStateId,
            key.colorCount,
            key.sampleCount,
            (unsigned long long)key.depthFormat,
            (unsigned long long)key.colorFormats[0],
            (unsigned long long)key.colorFormats[1],
            (unsigned long long)key.colorFormats[2],
            (unsigned long long)key.colorFormats[3],
            error == nil ? "unknown error" : error.localizedDescription.UTF8String);
        return false;
    }

    const size_t cacheIndex = device->pipelineCache.size();
    MGMetalPipelineCacheEntry entry;
    entry.key = key;
    entry.pipeline = device->pipeline;
    device->pipelineCache.push_back(std::move(entry));
    device->pipelineLookup.emplace(key, cacheIndex);
    device->pipelineFailureFrame.erase(key);
    if (device->pipelineTrace)
    {
        fprintf(
            stderr,
            "Metal pipeline compiled: compile=%llu requests=%llu hits=%llu cache=%zu "
            "vs=%llu ps=%llu layout=%llu blend=%llu colors=%u samples=%u depth=%llu\n",
            (unsigned long long)device->pipelineCompileCount,
            (unsigned long long)device->pipelineRequestCount,
            (unsigned long long)device->pipelineCacheHitCount,
            device->pipelineCache.size(),
            (unsigned long long)key.vertexShaderId,
            (unsigned long long)key.pixelShaderId,
            (unsigned long long)key.inputLayoutId,
            (unsigned long long)key.blendStateId,
            key.colorCount,
            key.sampleCount,
            (unsigned long long)key.depthFormat);
    }
    device->pipelineDirty = false;
    return true;
}

static bool MGMetalPrepareDraw(MGG_GraphicsDevice* device)
{
    if (device == nullptr)
        return false;
    if (!MGMetalEnsureEncoder(device) || !MGMetalUpdatePipeline(device))
        return false;

    [device->encoder setRenderPipelineState:device->pipeline];
    if (device->depthStencilState != nullptr)
    {
        [device->encoder setDepthStencilState:device->depthStencilState->state];
        [device->encoder setStencilReferenceValue:device->depthStencilState->referenceStencil];
    }

    if (device->rasterizerState != nullptr)
    {
        [device->encoder setTriangleFillMode:device->rasterizerState->info.fillMode == MGFillMode::WireFrame ? MTLTriangleFillModeLines : MTLTriangleFillModeFill];
        MTLCullMode cull = MTLCullModeNone;
        if (device->rasterizerState->info.cullMode == MGCullMode::CullClockwiseFace)
            cull = MTLCullModeFront;
        else if (device->rasterizerState->info.cullMode == MGCullMode::CullCounterClockwiseFace)
            cull = MTLCullModeBack;
        [device->encoder setCullMode:cull];
        [device->encoder setFrontFacingWinding:MTLWindingClockwise];
        [device->encoder setDepthClipMode:device->rasterizerState->info.depthClipEnable ? MTLDepthClipModeClip : MTLDepthClipModeClamp];
        [device->encoder setDepthBias:device->rasterizerState->info.depthBias slopeScale:device->rasterizerState->info.slopeScaleDepthBias clamp:0.0f];
        [device->encoder setScissorRect:MGMetalScissorRect(device, device->rasterizerState->info.scissorTestEnable)];
    }

    for (NSUInteger i = 0; i < MGMetalMaxVertexBuffers; ++i)
    {
        MGMetalVertexBinding& binding = device->vertexBuffers[i];
        if (binding.buffer == nullptr || binding.buffer->buffer == nil ||
            device->inputLayout == nullptr || i >= device->inputLayout->strides.size())
            continue;
        const NSUInteger stride = device->inputLayout->strides[i];
        const NSUInteger byteOffset = (NSUInteger)binding.offset * stride;
        if (byteOffset > (NSUInteger)binding.buffer->size)
            continue;
        [device->encoder setVertexBuffer:binding.buffer->buffer
                                  offset:byteOffset
                                 atIndex:MGMetalIRStageInAttributeStartIndex + i];
    }

    return MGMetalBindShaderResources(device, MGShaderStage::Vertex, device->vertexShader) &&
        MGMetalBindShaderResources(device, MGShaderStage::Pixel, device->pixelShader);
}

#if !defined(MG_METAL_BUILTIN_EFFECTS)
void MGG_EffectResource_GetBytecode(const char* name, mgbyte*& bytecode, mgint& size)
{
    (void)name;
    bytecode = nullptr;
    size = 0;
}
#endif

MGG_GraphicsSystem* MGG_GraphicsSystem_Create()
{
    auto system = new MGG_GraphicsSystem();
    if (g_openXrMetalDevice != nil)
    {
        auto adapter = new MGG_GraphicsAdapter();
        adapter->device = g_openXrMetalDevice;
        adapter->name = g_openXrMetalDevice.name.UTF8String;
        adapter->current = { MGSurfaceFormat::Color, 1, 1 };
        adapter->modes.push_back(adapter->current);
        system->adapters.push_back(adapter);
        return system;
    }
#if TARGET_OS_OSX
    NSArray<id<MTLDevice>>* devices = MTLCopyAllDevices();
#else
    id<MTLDevice> defaultDevice = MTLCreateSystemDefaultDevice();
    NSArray<id<MTLDevice>>* devices = defaultDevice == nil ? @[] : @[defaultDevice];
#endif
    for (id<MTLDevice> device in devices)
    {
        auto adapter = new MGG_GraphicsAdapter();
        adapter->device = device;
        adapter->name = device.name.UTF8String;
#if TARGET_OS_OSX
        const CGDirectDisplayID display = CGMainDisplayID();
        CGDisplayModeRef currentMode = CGDisplayCopyDisplayMode(display);
        if (currentMode != nullptr)
        {
            adapter->current = {
                MGSurfaceFormat::Color,
                (mgint)CGDisplayModeGetPixelWidth(currentMode),
                (mgint)CGDisplayModeGetPixelHeight(currentMode)
            };
            CGDisplayModeRelease(currentMode);
            adapter->modes.push_back(adapter->current);
        }

        CFArrayRef displayModes = CGDisplayCopyAllDisplayModes(display, nullptr);
        if (displayModes != nullptr)
        {
            const CFIndex count = CFArrayGetCount(displayModes);
            for (CFIndex index = 0; index < count; ++index)
            {
                CGDisplayModeRef mode = (CGDisplayModeRef)CFArrayGetValueAtIndex(displayModes, index);
                MGG_DisplayMode candidate = {
                    MGSurfaceFormat::Color,
                    (mgint)CGDisplayModeGetPixelWidth(mode),
                    (mgint)CGDisplayModeGetPixelHeight(mode)
                };
                const bool duplicate = std::any_of(
                    adapter->modes.begin(),
                    adapter->modes.end(),
                    [&candidate](const MGG_DisplayMode& existing)
                    {
                        return existing.width == candidate.width && existing.height == candidate.height;
                    });
                if (!duplicate)
                    adapter->modes.push_back(candidate);
            }
            CFRelease(displayModes);
        }
#endif
        if (adapter->current.width <= 0 || adapter->current.height <= 0)
            adapter->current = { MGSurfaceFormat::Color, 1, 1 };
        if (adapter->modes.empty())
            adapter->modes.push_back(adapter->current);
        system->adapters.push_back(adapter);
    }
    return system;
}

void MGG_GraphicsSystem_Destroy(MGG_GraphicsSystem* system)
{
    if (system == nullptr)
        return;
    for (MGG_GraphicsAdapter* adapter : system->adapters)
        delete adapter;
    delete system;
}

MGG_GraphicsAdapter* MGG_GraphicsAdapter_Get(MGG_GraphicsSystem* system, mgint index)
{
    if (system == nullptr || index < 0 || index >= (mgint)system->adapters.size())
        return nullptr;
    return system->adapters[index];
}

void MGG_GraphicsAdapter_GetInfo(MGG_GraphicsAdapter* adapter, MGG_GraphicsAdaptor_Info& info)
{
    memset(&info, 0, sizeof(info));
    if (adapter == nullptr)
        return;
    info.DeviceName = (void*)adapter->name.c_str();
    info.Description = (void*)adapter->name.c_str();
    info.DisplayModes = adapter->modes.data();
    info.DisplayModeCount = (mgint)adapter->modes.size();
    info.CurrentDisplayMode = adapter->current;
}

MGG_GraphicsDevice* MGG_GraphicsDevice_Create(MGG_GraphicsSystem* system, MGG_GraphicsAdapter* adapter)
{
    (void)system;
    if (adapter == nullptr)
        return nullptr;
    auto device = new MGG_GraphicsDevice();
    device->device = adapter->device;
    device->queue = [device->device newCommandQueue];
    device->inFlight = dispatch_semaphore_create(3);
    const char* pipelineTrace = getenv("MONOGAME_METAL_PIPELINE_TRACE");
    device->pipelineTrace = pipelineTrace != nullptr && strcmp(pipelineTrace, "1") == 0;
    if (device->queue == nil)
    {
        delete device;
        return nullptr;
    }
    return device;
}

void MGG_GraphicsDevice_Destroy(MGG_GraphicsDevice* device)
{
    if (device == nullptr)
        return;
    MGMetalEndEncoder(device);
    if (device->commandBuffer != nil)
    {
        [device->commandBuffer commit];
        [device->commandBuffer waitUntilCompleted];
    }
    if (device->lastSubmitted != nil)
        [device->lastSubmitted waitUntilCompleted];
#if defined(MG_SDL2)
    if (device->metalView != nullptr)
        SDL_Metal_DestroyView(device->metalView);
#endif
    delete device;
}

void MGG_GraphicsDevice_GetCaps(MGG_GraphicsDevice* device, MGG_GraphicsDevice_Caps& caps)
{
    memset(&caps, 0, sizeof(caps));
    if (device == nullptr || device->device == nil)
        return;
    caps.MaxTextureSlots = 16;
    caps.MaxVertexTextureSlots = 16;
    caps.MaxVertexBufferSlots = (mgint)MGMetalMaxVertexBuffers;
    caps.ShaderProfile = 81;
    for (const mgint sampleCount : { 8, 4, 2, 1 })
    {
        if ([device->device supportsTextureSampleCount:(NSUInteger)sampleCount])
        {
            caps.MaxMultiSampleCount = sampleCount;
            break;
        }
    }
    mgint textureCompression = 0;
    if (device->device.supportsBCTextureCompression)
        textureCompression |= MGMetalTextureCompressionS3tc;
    if ([device->device supportsFamily:MTLGPUFamilyApple1])
        textureCompression |= MGMetalTextureCompressionEtc2 | MGMetalTextureCompressionAstc;
    caps.TextureCompression = static_cast<MGTextureCompressionCapabilities>(textureCompression);
}

MGGraphicsDeviceStatus MGG_GraphicsDevice_GetCapsV2(
    MGG_GraphicsDevice* device,
    MGG_GraphicsDevice_CapsV2& caps,
    mguint capsSize)
{
    if (device == nullptr || device->device == nil)
        return MGGraphicsDeviceStatus::InvalidArgument;
    if (capsSize < sizeof(caps.StructSize) + sizeof(caps.AbiVersion))
        return MGGraphicsDeviceStatus::InsufficientSize;

    MGG_GraphicsDevice_Caps legacy{};
    MGG_GraphicsDevice_GetCaps(device, legacy);
    MGG_GraphicsDevice_CapsV2 value{};
    value.StructSize = sizeof(value);
    value.AbiVersion = 3;
    value.ApiMajor = 3;
    value.ApiMinor = 0;
    value.MaxTextureSlots = legacy.MaxTextureSlots;
    value.MaxVertexTextureSlots = legacy.MaxVertexTextureSlots;
    value.MaxVertexBufferSlots = legacy.MaxVertexBufferSlots;
    value.ShaderProfile = legacy.ShaderProfile;
    value.MaxMultiSampleCount = legacy.MaxMultiSampleCount;
    value.TextureCompression = legacy.TextureCompression;
    value.Features = static_cast<MGNativeGraphicsFeatures>(
        static_cast<mguint>(MGNativeGraphicsFeatures::AnisotropicFiltering) |
        static_cast<mguint>(MGNativeGraphicsFeatures::CompletedGpuFrameTiming));
    if (MGMetalSupportsSampleableDepthFormat(device->device, MGDepthFormat::Depth32Float) ||
        MGMetalSupportsSampleableDepthFormat(device->device, MGDepthFormat::Depth24Stencil8) ||
        MGMetalSupportsSampleableDepthFormat(device->device, MGDepthFormat::Depth16))
    {
        value.Features = static_cast<MGNativeGraphicsFeatures>(
            static_cast<mguint>(value.Features) |
            static_cast<mguint>(MGNativeGraphicsFeatures::ExplicitRenderPass));
    }
    value.MaxAnisotropy = 16.0f;
    value.MaxRenderTargets = static_cast<mgint>(MGMetalMaxColorTargets);
    value.MaxDrawBuffers = static_cast<mgint>(MGMetalMaxColorTargets);
    value.MaxColorAttachments = static_cast<mgint>(MGMetalMaxColorTargets);
    std::memcpy(&caps, &value, std::min(capsSize, static_cast<mguint>(sizeof(value))));
    return MGGraphicsDeviceStatus::Success;
}

void MGG_GraphicsDevice_ResizeSwapchain(
    MGG_GraphicsDevice* device,
    MGG_PresentationSurface& surface,
    mgint width,
    mgint height,
    MGSurfaceFormat color,
    MGDepthFormat depth,
    mgint multiSampleCount,
    mgint syncInterval)
{
    if (device == nullptr || surface.Handle == nullptr)
        return;
    MGMetalEndEncoder(device);
    device->width = std::max(width, 1);
    device->height = std::max(height, 1);
    device->sampleCount = MGMetalSupportedSampleCount(device->device, multiSampleCount);
    device->syncInterval = syncInterval;

    if (surface.Kind == MGPresentationSurfaceKind::SdlWindow)
    {
#if defined(MG_SDL2)
        if (device->metalView == nullptr)
            device->metalView = SDL_Metal_CreateView((SDL_Window*)surface.Handle);
        device->layer = (__bridge CAMetalLayer*)SDL_Metal_GetLayer(device->metalView);
#endif
    }
    else if (surface.Kind == MGPresentationSurfaceKind::MetalLayer)
    {
        device->layer = (__bridge CAMetalLayer*)surface.Handle;
    }

    if (device->layer == nil)
        return;
    device->layer.device = device->device;
    device->layer.pixelFormat = color == MGSurfaceFormat::ColorSRgb ? MTLPixelFormatBGRA8Unorm_sRGB : MTLPixelFormatBGRA8Unorm;
    device->layer.framebufferOnly = NO;
    device->layer.drawableSize = CGSizeMake(device->width, device->height);
    device->layer.maximumDrawableCount = 3;
#if TARGET_OS_OSX
    device->layer.displaySyncEnabled = syncInterval > 0;
#endif

    if (device->sampleCount > 1)
    {
        MTLTextureDescriptor* descriptor = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:device->layer.pixelFormat width:device->width height:device->height mipmapped:NO];
        descriptor.textureType = MTLTextureType2DMultisample;
        descriptor.sampleCount = device->sampleCount;
        descriptor.storageMode = MTLStorageModePrivate;
        descriptor.usage = MTLTextureUsageRenderTarget;
        device->backBufferMultisample = [device->device newTextureWithDescriptor:descriptor];
    }
    else
    {
        device->backBufferMultisample = nil;
    }
    device->backBufferDepth = MGMetalCreateDepthTexture(device->device, device->width, device->height, depth, device->sampleCount);
    device->viewport = { 0, 0, (double)device->width, (double)device->height, 0, 1 };
    device->scissor = { 0, 0, (NSUInteger)device->width, (NSUInteger)device->height };
    device->pipelineDirty = true;
}

mgint MGG_GraphicsDevice_BeginFrame(MGG_GraphicsDevice* device)
{
    if (device == nullptr || device->layer == nil)
        return -1;
    dispatch_semaphore_wait(device->inFlight, DISPATCH_TIME_FOREVER);
    device->drawable = [device->layer nextDrawable];
    if (device->drawable == nil)
    {
        dispatch_semaphore_signal(device->inFlight);
        return -1;
    }
    device->commandBuffer = [device->queue commandBuffer];
    if (device->commandBuffer == nil)
    {
        dispatch_semaphore_signal(device->inFlight);
        device->drawable = nil;
        return -1;
    }
    device->frame = (device->frame + 1) % 3;
    ++device->frameSerial;
    device->bindingArenas[device->frame].offset = 0;
    return device->frame;
}

void MGG_GraphicsDevice_Clear(MGG_GraphicsDevice* device, MGClearOptions options, Vector4& color, mgfloat depth, mgint stencil)
{
    if (device == nullptr || device->commandBuffer == nil)
        return;
    MGMetalEndEncoder(device);
    MTLRenderPassDescriptor* pass = MGMetalCreateRenderPass(device, options, color, depth, stencil);
    if (pass == nil)
        return;
    device->encoder = [device->commandBuffer renderCommandEncoderWithDescriptor:pass];
    [device->encoder setViewport:device->viewport];
    const bool scissorEnabled = device->rasterizerState != nullptr &&
        device->rasterizerState->info.scissorTestEnable;
    [device->encoder setScissorRect:MGMetalScissorRect(device, scissorEnabled)];
}

void MGG_GraphicsDevice_Present(MGG_GraphicsDevice* device, mgint currentFrame, mgint syncInterval)
{
    (void)currentFrame;
    (void)syncInterval;
    if (device == nullptr || device->commandBuffer == nil)
        return;
    MGMetalEndEncoder(device);
    if (device->drawable != nil)
        [device->commandBuffer presentDrawable:device->drawable];
    dispatch_semaphore_t semaphore = device->inFlight;
    const uint64_t submissionId = ++device->presentationSerial;
    [device->commandBuffer addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
        MGMetalReportCommandBufferError(completedBuffer);
        if (completedBuffer.status == MTLCommandBufferStatusCompleted)
        {
            const CFTimeInterval start = completedBuffer.GPUStartTime;
            const CFTimeInterval end = completedBuffer.GPUEndTime;
            const double durationNanoseconds = (end - start) * 1'000'000'000.0;
            if (start > 0.0 && end >= start && std::isfinite(durationNanoseconds) &&
                durationNanoseconds > 0.0 && durationNanoseconds <= static_cast<double>(UINT64_MAX))
            {
                device->completedGpuFrameTimings.Push(
                    submissionId,
                    static_cast<uint64_t>(std::llround(durationNanoseconds)));
            }
        }
        dispatch_semaphore_signal(semaphore);
    }];
    [device->commandBuffer commit];
    device->lastSubmitted = device->commandBuffer;
    device->commandBuffer = nil;
    device->drawable = nil;
}

mgbyte MGG_GraphicsDevice_TryDequeueCompletedGpuFrameTiming(
    MGG_GraphicsDevice* device,
    MGG_GpuFrameTiming& timing)
{
    if (device == nullptr)
        return false;
    return device->completedGpuFrameTimings.TryPop(timing);
}

void MGG_GraphicsDevice_SubmitWithoutPresent(MGG_GraphicsDevice* device)
{
    if (device == nullptr || device->commandBuffer == nil)
        return;
    MGMetalEndEncoder(device);
    [device->commandBuffer addCompletedHandler:^(id<MTLCommandBuffer> completedBuffer) {
        MGMetalReportCommandBufferError(completedBuffer);
    }];
    [device->commandBuffer commit];
    device->lastSubmitted = device->commandBuffer;
    device->commandBuffer = [device->queue commandBuffer];
    device->pipelineDirty = true;
}

void MGG_OpenXR_GetGraphicsBinding(MGG_GraphicsDevice* device, MGG_OpenXrGraphicsBinding& binding)
{
    memset(&binding, 0, sizeof(binding));
    if (device == nullptr)
        return;
    binding.Api = 4;
    binding.Device = (__bridge void*)device->device;
    binding.Queue = (__bridge void*)device->queue;
}

MGG_Texture* MGG_OpenXR_WrapRenderTarget(MGG_GraphicsDevice* device, void* image, MGSurfaceFormat format, mgint width, mgint height, MGDepthFormat depthFormat)
{
    if (device == nullptr || image == nullptr || width <= 0 || height <= 0)
        return nullptr;
    auto texture = new MGG_Texture();
    texture->type = MGTextureType::_2D;
    texture->format = format;
    texture->width = width;
    texture->height = height;
    texture->depth = 1;
    texture->mipmaps = 1;
    texture->slices = 1;
    texture->sampleCount = 1;
    texture->depthFormat = depthFormat;
    texture->texture = (__bridge id<MTLTexture>)image;
    texture->depthTexture = MGMetalCreateDepthTexture(device->device, width, height, depthFormat, 1);
    return texture;
}

void MGG_OpenXR_PrepareForRuntimeRelease(MGG_GraphicsDevice* device, MGG_Texture* texture)
{
    (void)texture;
    if (device != nullptr)
        MGMetalEndEncoder(device);
}

void MGG_GraphicsDevice_SetBlendState(MGG_GraphicsDevice* device, MGG_BlendState* state, mgfloat factorR, mgfloat factorG, mgfloat factorB, mgfloat factorA)
{
    if (device == nullptr)
        return;
    if (device->blendState != state)
    {
        device->blendState = state;
        device->pipelineDirty = true;
    }
    if (MGMetalEnsureEncoder(device))
        [device->encoder setBlendColorRed:factorR green:factorG blue:factorB alpha:factorA];
}

void MGG_GraphicsDevice_SetDepthStencilState(MGG_GraphicsDevice* device, MGG_DepthStencilState* state)
{
    if (device == nullptr)
        return;
    device->depthStencilState = state;
    if (device->encoder != nil)
    {
        [device->encoder setDepthStencilState:state == nullptr ? nil : state->state];
        if (state != nullptr)
            [device->encoder setStencilReferenceValue:state->referenceStencil];
    }
}

void MGG_GraphicsDevice_SetRasterizerState(MGG_GraphicsDevice* device, MGG_RasterizerState* state)
{
    if (device == nullptr)
        return;
    device->rasterizerState = state;
    if (device->encoder != nil)
        [device->encoder setScissorRect:MGMetalScissorRect(device, state != nullptr && state->info.scissorTestEnable)];
}

void MGG_GraphicsDevice_GetTitleSafeArea(mgint& x, mgint& y, mgint& width, mgint& height)
{
    (void)x;
    (void)y;
    (void)width;
    (void)height;
}

void MGG_GraphicsDevice_SetViewport(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height, mgfloat minDepth, mgfloat maxDepth)
{
    if (device == nullptr || width < 0 || height < 0)
        return;
    device->viewport = { (double)x, (double)y, (double)width, (double)height, minDepth, maxDepth };
    if (device->encoder != nil)
        [device->encoder setViewport:device->viewport];
}

void MGG_GraphicsDevice_SetScissorRectangle(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height)
{
    if (device == nullptr)
        return;
    device->scissor = { (NSUInteger)std::max(x, 0), (NSUInteger)std::max(y, 0), (NSUInteger)std::max(width, 0), (NSUInteger)std::max(height, 0) };
    if (device->encoder != nil)
    {
        const bool scissorEnabled = device->rasterizerState != nullptr &&
            device->rasterizerState->info.scissorTestEnable;
        [device->encoder setScissorRect:MGMetalScissorRect(device, scissorEnabled)];
    }
}

void MGG_GraphicsDevice_SetRenderTargets(MGG_GraphicsDevice* device, MGG_Texture** targets, mgint* arraySlices, mgint count)
{
    if (device == nullptr)
        return;
    MGMetalEndEncoder(device);
    memset(device->renderTargets, 0, sizeof(device->renderTargets));
    memset(device->renderTargetSlices, 0, sizeof(device->renderTargetSlices));
    device->renderTargetCount = 0;
    device->explicitRenderPass = false;
    device->hasExplicitDepthStencilAttachment = false;
    memset(device->explicitColorAttachments, 0, sizeof(device->explicitColorAttachments));
    device->explicitDepthStencilAttachment = {};
    const int requestedCount = targets == nullptr ? 0 :
        std::min(std::max(count, 0), (mgint)MGMetalMaxColorTargets);
    for (int i = 0; i < requestedCount; ++i)
    {
        if (targets[i] == nullptr || targets[i]->texture == nil)
            break;
        device->renderTargets[i] = targets[i];
        device->renderTargetSlices[i] = arraySlices == nullptr ? 0 : arraySlices[i];
        ++device->renderTargetCount;
    }
    device->pipelineDirty = true;
}

MGGraphicsDeviceStatus MGG_GraphicsDevice_SetRenderTargetsV2(
    MGG_GraphicsDevice* device,
    MGG_Texture** targets,
    mgint* arraySlices,
    mgint count)
{
    if (device == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;
    if (count < 0 || count > static_cast<mgint>(MGMetalMaxColorTargets))
        return MGGraphicsDeviceStatus::InvalidRenderTargetCount;
    if (count > 0 && targets == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;
    for (mgint index = 0; index < count; ++index)
    {
        if (targets[index] == nullptr || targets[index]->texture == nil)
            return MGGraphicsDeviceStatus::InvalidRenderTarget;
    }
    MGG_GraphicsDevice_SetRenderTargets(device, targets, arraySlices, count);
    return MGGraphicsDeviceStatus::Success;
}

mgbyte MGG_GraphicsDevice_SupportsDepthStencilTargetFormatV3(
    MGG_GraphicsDevice* device,
    MGDepthFormat depthFormat)
{
    return device != nullptr && device->device != nil &&
        MGMetalSupportsSampleableDepthFormat(device->device, depthFormat)
        ? 1
        : 0;
}

MGGraphicsDeviceStatus MGG_GraphicsDevice_SetRenderPassV3(
    MGG_GraphicsDevice* device,
    MGG_RenderPassColorAttachment* colorAttachments,
    mgint colorAttachmentCount,
    MGG_RenderPassDepthStencilAttachment* depthStencilAttachment)
{
    if (device == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;
    if (colorAttachmentCount <= 0 || colorAttachmentCount > static_cast<mgint>(MGMetalMaxColorTargets))
        return MGGraphicsDeviceStatus::InvalidRenderTargetCount;
    if (colorAttachments == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;

    int width = 0;
    int height = 0;
    for (mgint index = 0; index < colorAttachmentCount; ++index)
    {
        const auto& attachment = colorAttachments[index];
        MGG_Texture* target = static_cast<MGG_Texture*>(attachment.Target);
        if (target == nullptr || target->texture == nil || target->independentDepthStencil)
            return MGGraphicsDeviceStatus::InvalidRenderTarget;
        if (target->sampleCount > 1)
            return MGGraphicsDeviceStatus::RenderTargetMultisamplingNotSupported;
        if (attachment.ArraySlice < 0 || attachment.ArraySlice >= std::max(target->slices, 1))
            return MGGraphicsDeviceStatus::InvalidRenderTarget;
        if (attachment.LoadAction < MGRenderPassLoadAction::Load ||
            attachment.LoadAction > MGRenderPassLoadAction::DontCare ||
            attachment.StoreAction < MGRenderPassStoreAction::Store ||
            attachment.StoreAction > MGRenderPassStoreAction::DontCare)
            return MGGraphicsDeviceStatus::InvalidArgument;
        if (index == 0)
        {
            width = target->width;
            height = target->height;
        }
        else if (target->width != width || target->height != height)
        {
            return MGGraphicsDeviceStatus::RenderTargetDimensionsMismatch;
        }
    }

    if (depthStencilAttachment != nullptr)
    {
        MGG_Texture* target = static_cast<MGG_Texture*>(depthStencilAttachment->Target);
        if (target == nullptr || target->texture == nil || !target->independentDepthStencil)
            return MGGraphicsDeviceStatus::InvalidRenderTarget;
        if (target->sampleCount > 1)
            return MGGraphicsDeviceStatus::RenderTargetMultisamplingNotSupported;
        if (target->width != width || target->height != height)
            return MGGraphicsDeviceStatus::RenderTargetDimensionsMismatch;
        if (depthStencilAttachment->DepthLoadAction < MGRenderPassLoadAction::Load ||
            depthStencilAttachment->DepthLoadAction > MGRenderPassLoadAction::DontCare ||
            depthStencilAttachment->StencilLoadAction < MGRenderPassLoadAction::Load ||
            depthStencilAttachment->StencilLoadAction > MGRenderPassLoadAction::DontCare ||
            depthStencilAttachment->DepthStoreAction < MGRenderPassStoreAction::Store ||
            depthStencilAttachment->DepthStoreAction > MGRenderPassStoreAction::DontCare ||
            depthStencilAttachment->StencilStoreAction < MGRenderPassStoreAction::Store ||
            depthStencilAttachment->StencilStoreAction > MGRenderPassStoreAction::DontCare)
            return MGGraphicsDeviceStatus::InvalidArgument;
    }

    MGMetalEndEncoder(device);
    memset(device->renderTargets, 0, sizeof(device->renderTargets));
    memset(device->renderTargetSlices, 0, sizeof(device->renderTargetSlices));
    memset(device->explicitColorAttachments, 0, sizeof(device->explicitColorAttachments));
    for (mgint index = 0; index < colorAttachmentCount; ++index)
    {
        device->renderTargets[index] = static_cast<MGG_Texture*>(colorAttachments[index].Target);
        device->renderTargetSlices[index] = colorAttachments[index].ArraySlice;
        device->explicitColorAttachments[index] = colorAttachments[index];
    }
    device->renderTargetCount = colorAttachmentCount;
    device->explicitRenderPass = true;
    device->hasExplicitDepthStencilAttachment = depthStencilAttachment != nullptr;
    device->explicitDepthStencilAttachment = depthStencilAttachment != nullptr
        ? *depthStencilAttachment
        : MGG_RenderPassDepthStencilAttachment{};
    device->pipelineDirty = true;
    return MGGraphicsDeviceStatus::Success;
}

void MGG_GraphicsDevice_SetConstantBuffer(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_Buffer* buffer)
{
    const int stageIndex = MGMetalShaderStageIndex(stage);
    if (device != nullptr && stageIndex >= 0 && slot >= 0 && slot < 16)
        device->constantBuffers[stageIndex][slot] = buffer;
}

void MGG_GraphicsDevice_SetTexture(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_Texture* texture)
{
    const int stageIndex = MGMetalShaderStageIndex(stage);
    if (device != nullptr && stageIndex >= 0 && slot >= 0 && slot < 16)
        device->textures[stageIndex][slot] = texture;
}

void MGG_GraphicsDevice_SetSamplerState(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_SamplerState* state)
{
    const int stageIndex = MGMetalShaderStageIndex(stage);
    if (device != nullptr && stageIndex >= 0 && slot >= 0 && slot < 16)
        device->samplers[stageIndex][slot] = state;
}

void MGG_GraphicsDevice_SetIndexBuffer(MGG_GraphicsDevice* device, MGIndexElementSize size, MGG_Buffer* buffer)
{
    if (device == nullptr)
        return;
    device->indexType = size;
    device->indexBuffer = buffer;
}

void MGG_GraphicsDevice_SetVertexBuffer(MGG_GraphicsDevice* device, mgint slot, MGG_Buffer* buffer, mgint vertexOffset)
{
    if (device == nullptr || slot < 0 || slot >= (mgint)MGMetalMaxVertexBuffers || vertexOffset < 0)
        return;
    device->vertexBuffers[slot] = { buffer, vertexOffset };
}

void MGG_GraphicsDevice_SetShader(MGG_GraphicsDevice* device, MGShaderStage stage, MGG_Shader* shader)
{
    if (device == nullptr || MGMetalShaderStageIndex(stage) < 0 ||
        (shader != nullptr && shader->stage != stage))
        return;
    if (stage == MGShaderStage::Vertex)
    {
        if (device->vertexShader == shader)
            return;
        device->vertexShader = shader;
    }
    else
    {
        if (device->pixelShader == shader)
            return;
        device->pixelShader = shader;
    }
    device->pipelineDirty = true;
}

void MGG_GraphicsDevice_SetInputLayout(MGG_GraphicsDevice* device, MGG_InputLayout* layout)
{
    if (device == nullptr)
        return;
    if (device->inputLayout == layout)
        return;
    device->inputLayout = layout;
    device->pipelineDirty = true;
}

static void MGMetalSetDrawArguments(
    MGG_GraphicsDevice* device,
    uint32_t vertexStart,
    uint32_t vertexCount,
    uint32_t instanceCount)
{
    MGMetalIRDrawParams parameters = {};
    parameters.draw = { vertexCount, instanceCount, vertexStart, 0 };
    const uint16_t indexType = 0;
    [device->encoder setVertexBytes:&parameters
                             length:sizeof(parameters)
                            atIndex:MGMetalIRDrawArgumentsBindPoint];
    [device->encoder setVertexBytes:&indexType
                             length:sizeof(indexType)
                            atIndex:MGMetalIRDrawInfoBindPoint];
}

static void MGMetalSetIndexedDrawArguments(
    MGG_GraphicsDevice* device,
    uint32_t indexStart,
    uint32_t indexCount,
    int32_t baseVertex,
    uint32_t instanceCount,
    MTLIndexType metalIndexType)
{
    MGMetalIRDrawParams parameters = {};
    parameters.drawIndexed = { indexCount, instanceCount, indexStart, baseVertex, 0 };
    const uint16_t indexType = (uint16_t)metalIndexType + 1;
    [device->encoder setVertexBytes:&parameters
                             length:sizeof(parameters)
                            atIndex:MGMetalIRDrawArgumentsBindPoint];
    [device->encoder setVertexBytes:&indexType
                             length:sizeof(indexType)
                            atIndex:MGMetalIRDrawInfoBindPoint];
}

void MGG_GraphicsDevice_Draw(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint vertexStart, mgint vertexCount)
{
    if (vertexCount <= 0 || !MGMetalPrepareDraw(device))
        return;
    MGMetalSetDrawArguments(device, vertexStart, vertexCount, 1);
    [device->encoder drawPrimitives:MGMetalPrimitive(primitiveType) vertexStart:vertexStart vertexCount:vertexCount];
}

void MGG_GraphicsDevice_DrawIndexed(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint primitiveCount, mgint indexStart, mgint vertexStart)
{
    if (primitiveCount <= 0 || device->indexBuffer == nullptr || !MGMetalPrepareDraw(device))
        return;
    const int count = MGMetalPrimitiveElementCount(primitiveType, primitiveCount);
    const MTLIndexType type = device->indexType == MGIndexElementSize::SixteenBits ? MTLIndexTypeUInt16 : MTLIndexTypeUInt32;
    const int elementSize = type == MTLIndexTypeUInt16 ? 2 : 4;
    MGMetalSetIndexedDrawArguments(device, indexStart, count, vertexStart, 1, type);
    [device->encoder drawIndexedPrimitives:MGMetalPrimitive(primitiveType) indexCount:count indexType:type indexBuffer:device->indexBuffer->buffer indexBufferOffset:indexStart * elementSize instanceCount:1 baseVertex:vertexStart baseInstance:0];
}

void MGG_GraphicsDevice_DrawIndexedInstanced(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint primitiveCount, mgint indexStart, mgint vertexStart, mgint instanceCount)
{
    if (primitiveCount <= 0 || instanceCount <= 0 || device->indexBuffer == nullptr || !MGMetalPrepareDraw(device))
        return;
    const int count = MGMetalPrimitiveElementCount(primitiveType, primitiveCount);
    const MTLIndexType type = device->indexType == MGIndexElementSize::SixteenBits ? MTLIndexTypeUInt16 : MTLIndexTypeUInt32;
    const int elementSize = type == MTLIndexTypeUInt16 ? 2 : 4;
    MGMetalSetIndexedDrawArguments(device, indexStart, count, vertexStart, instanceCount, type);
    [device->encoder drawIndexedPrimitives:MGMetalPrimitive(primitiveType) indexCount:count indexType:type indexBuffer:device->indexBuffer->buffer indexBufferOffset:indexStart * elementSize instanceCount:instanceCount baseVertex:vertexStart baseInstance:0];
}

void MGG_GraphicsDevice_ResolveRenderTargets(MGG_GraphicsDevice* device)
{
    if (device == nullptr || device->commandBuffer == nil)
        return;
    MGMetalEndEncoder(device);

    id<MTLBlitCommandEncoder> blit = nil;
    for (int i = 0; i < device->renderTargetCount; ++i)
    {
        MGG_Texture* target = device->renderTargets[i];
        if (target == nullptr || target->texture == nil || target->mipmaps <= 1)
            continue;
        if (blit == nil)
            blit = [device->commandBuffer blitCommandEncoder];
        [blit generateMipmapsForTexture:target->texture];
    }
    [blit endEncoding];
}

void MGG_GraphicsDevice_GetBackBufferData(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height, void* data, mgint count, mgint dataBytes)
{
    if (device == nullptr || device->drawable == nil || device->commandBuffer == nil ||
        data == nullptr || count <= 0 || dataBytes <= 0 || width <= 0 || height <= 0)
        return;

    MGMetalEndEncoder(device);
    const NSUInteger rowBytes = ((NSUInteger)count * dataBytes) / (NSUInteger)height;
    const NSUInteger alignedRowBytes = (rowBytes + 255) & ~((NSUInteger)255);
    const NSUInteger bufferLength = alignedRowBytes * (NSUInteger)height;
    id<MTLBuffer> staging = [device->device newBufferWithLength:bufferLength options:MGMetalCpuVisibleBufferStorageMode(device->device)];
    if (staging == nil)
        return;

    id<MTLBlitCommandEncoder> blit = [device->commandBuffer blitCommandEncoder];
    [blit copyFromTexture:device->drawable.texture
              sourceSlice:0
              sourceLevel:0
             sourceOrigin:MTLOriginMake(x, y, 0)
               sourceSize:MTLSizeMake(width, height, 1)
                 toBuffer:staging
        destinationOffset:0
   destinationBytesPerRow:alignedRowBytes
   destinationBytesPerImage:bufferLength];
#if TARGET_OS_OSX
    if (staging.storageMode == MTLStorageModeManaged)
        [blit synchronizeResource:staging];
#endif
    [blit endEncoding];
    [device->commandBuffer commit];
    [device->commandBuffer waitUntilCompleted];
    MGMetalReportCommandBufferError(device->commandBuffer);

    if (device->commandBuffer.status == MTLCommandBufferStatusCompleted)
    {
        const mgbyte* source = (const mgbyte*)staging.contents;
        mgbyte* destination = (mgbyte*)data;
        for (int row = 0; row < height; ++row)
        {
            memcpy(destination + row * rowBytes, source + row * alignedRowBytes, rowBytes);
            // CAMetalLayer drawables use BGRA while MonoGame's Color
            // backbuffer contract exposes RGBA byte ordering.
            mgbyte* destinationRow = destination + row * rowBytes;
            for (int column = 0; column < width && (NSUInteger)(column * 4 + 3) < rowBytes; ++column)
                std::swap(destinationRow[column * 4], destinationRow[column * 4 + 2]);
        }
    }

    // GetBackBufferData is synchronous. Continue the current frame on a fresh
    // command buffer so callers may draw or present after the readback.
    device->lastSubmitted = device->commandBuffer;
    device->commandBuffer = [device->queue commandBuffer];
    if (device->commandBuffer == nil)
    {
        dispatch_semaphore_signal(device->inFlight);
        device->drawable = nil;
    }
}

MGG_BlendState* MGG_BlendState_Create(MGG_GraphicsDevice* device, MGG_BlendState_Info* infos)
{
    if (device == nullptr || infos == nullptr)
        return nullptr;
    auto state = new MGG_BlendState();
    state->pipelineResourceId = device->nextPipelineResourceId++;
    memcpy(state->targets, infos, sizeof(state->targets));
    return state;
}

void MGG_BlendState_Destroy(MGG_GraphicsDevice* device, MGG_BlendState* state)
{
    (void)device;
    delete state;
}

MGG_DepthStencilState* MGG_DepthStencilState_Create(MGG_GraphicsDevice* device, MGG_DepthStencilState_Info* info)
{
    if (device == nullptr || info == nullptr)
        return nullptr;
    auto state = new MGG_DepthStencilState();
    MTLDepthStencilDescriptor* descriptor = [MTLDepthStencilDescriptor new];
    descriptor.depthCompareFunction = info->depthBufferEnable ? MGMetalCompare(info->depthBufferFunction) : MTLCompareFunctionAlways;
    descriptor.depthWriteEnabled = info->depthBufferEnable && info->depthBufferWriteEnable;
    if (info->stencilEnable)
    {
        MTLStencilDescriptor* stencil = [MTLStencilDescriptor new];
        stencil.stencilCompareFunction = MGMetalCompare(info->stencilFunction);
        stencil.stencilFailureOperation = MGMetalStencil(info->stencilFail);
        stencil.depthFailureOperation = MGMetalStencil(info->stencilDepthBufferFail);
        stencil.depthStencilPassOperation = MGMetalStencil(info->stencilPass);
        stencil.readMask = info->stencilMask;
        stencil.writeMask = info->stencilWriteMask;
        descriptor.frontFaceStencil = stencil;
        descriptor.backFaceStencil = stencil;
    }
    state->state = [device->device newDepthStencilStateWithDescriptor:descriptor];
    state->referenceStencil = info->referenceStencil;
    return state;
}

void MGG_DepthStencilState_Destroy(MGG_GraphicsDevice* device, MGG_DepthStencilState* state)
{
    (void)device;
    delete state;
}

MGG_RasterizerState* MGG_RasterizerState_Create(MGG_GraphicsDevice* device, MGG_RasterizerState_Info* info)
{
    (void)device;
    if (info == nullptr)
        return nullptr;
    auto state = new MGG_RasterizerState();
    state->info = *info;
    return state;
}

void MGG_RasterizerState_Destroy(MGG_GraphicsDevice* device, MGG_RasterizerState* state)
{
    (void)device;
    delete state;
}

static MTLSamplerAddressMode MGMetalAddress(MGTextureAddressMode mode)
{
    switch (mode)
    {
        case MGTextureAddressMode::Wrap: return MTLSamplerAddressModeRepeat;
        case MGTextureAddressMode::Mirror: return MTLSamplerAddressModeMirrorRepeat;
        case MGTextureAddressMode::Border: return MTLSamplerAddressModeClampToBorderColor;
        default: return MTLSamplerAddressModeClampToEdge;
    }
}

MGG_SamplerState* MGG_SamplerState_Create(MGG_GraphicsDevice* device, MGG_SamplerState_Info* info)
{
    if (device == nullptr || info == nullptr)
        return nullptr;
    auto state = new MGG_SamplerState();
    MTLSamplerDescriptor* descriptor = [MTLSamplerDescriptor new];
    descriptor.sAddressMode = MGMetalAddress(info->AddressU);
    descriptor.tAddressMode = MGMetalAddress(info->AddressV);
    descriptor.rAddressMode = MGMetalAddress(info->AddressW);
    const bool pointMin = info->Filter == MGTextureFilter::Point || info->Filter == MGTextureFilter::PointMipLinear || info->Filter == MGTextureFilter::MinPointMagLinearMipLinear || info->Filter == MGTextureFilter::MinPointMagLinearMipPoint;
    const bool pointMag = info->Filter == MGTextureFilter::Point || info->Filter == MGTextureFilter::LinearMipPoint || info->Filter == MGTextureFilter::MinLinearMagPointMipLinear || info->Filter == MGTextureFilter::MinLinearMagPointMipPoint;
    descriptor.minFilter = pointMin ? MTLSamplerMinMagFilterNearest : MTLSamplerMinMagFilterLinear;
    descriptor.magFilter = pointMag ? MTLSamplerMinMagFilterNearest : MTLSamplerMinMagFilterLinear;
    descriptor.mipFilter = info->Filter == MGTextureFilter::Point || info->Filter == MGTextureFilter::LinearMipPoint || info->Filter == MGTextureFilter::MinLinearMagPointMipPoint || info->Filter == MGTextureFilter::MinPointMagLinearMipPoint ? MTLSamplerMipFilterNearest : MTLSamplerMipFilterLinear;
    descriptor.maxAnisotropy = std::max(info->MaximumAnisotropy, 1);
    descriptor.lodMinClamp = info->MaxMipLevel;
    descriptor.lodAverage = NO;
    descriptor.supportArgumentBuffers = YES;
    if (info->BorderColor == 0xFF000000)
        descriptor.borderColor = MTLSamplerBorderColorOpaqueBlack;
    else if (info->BorderColor == 0xFFFFFFFF)
        descriptor.borderColor = MTLSamplerBorderColorOpaqueWhite;
    else
        descriptor.borderColor = MTLSamplerBorderColorTransparentBlack;
    if (info->FilterMode == MGTextureFilterMode::Comparison)
        descriptor.compareFunction = MGMetalCompare(info->ComparisonFunction);
    state->state = [device->device newSamplerStateWithDescriptor:descriptor];
    if (state->state == nil)
    {
        delete state;
        return nullptr;
    }
    return state;
}

void MGG_SamplerState_Destroy(MGG_GraphicsDevice* device, MGG_SamplerState* state)
{
    (void)device;
    delete state;
}

MGG_Buffer* MGG_Buffer_Create(MGG_GraphicsDevice* device, MGBufferType type, mgbool dynamic, mgint sizeInBytes)
{
    if (device == nullptr || sizeInBytes < 0)
        return nullptr;
    auto buffer = new MGG_Buffer();
    buffer->type = type;
    buffer->dynamic = dynamic;
    buffer->size = sizeInBytes;
    buffer->buffer = [device->device newBufferWithLength:std::max(sizeInBytes, 1) options:MGMetalCpuVisibleBufferStorageMode(device->device)];
    if (buffer->buffer == nil)
    {
        delete buffer;
        return nullptr;
    }
    return buffer;
}

void MGG_Buffer_Destroy(MGG_GraphicsDevice* device, MGG_Buffer* buffer)
{
    (void)device;
    delete buffer;
}

void MGG_Buffer_SetData(MGG_GraphicsDevice* device, MGG_Buffer*& buffer, mgint offset, mgbyte* data, mgint elementCount, mgint vertexStride, mgint elementSizeInBytes, mgbool discard)
{
    (void)vertexStride;
    if (buffer == nullptr || data == nullptr)
        return;
    const int byteCount = elementCount * elementSizeInBytes;
    if (discard && buffer->dynamic)
        buffer->buffer = [device->device newBufferWithLength:std::max(buffer->size, 1) options:MGMetalCpuVisibleBufferStorageMode(device->device)];
    if (offset < 0 || byteCount < 0 || offset + byteCount > buffer->size)
        return;
    memcpy((mgbyte*)buffer->buffer.contents + offset, data, byteCount);
#if TARGET_OS_OSX
    if (buffer->buffer.storageMode == MTLStorageModeManaged)
        [buffer->buffer didModifyRange:NSMakeRange(offset, byteCount)];
#endif
}

void MGG_Buffer_GetData(MGG_GraphicsDevice* device, MGG_Buffer* buffer, mgint offset, mgbyte* data, mgint dataCount, mgint dataBytes, mgint dataStride)
{
    (void)device;
    if (buffer == nullptr || data == nullptr || offset < 0 ||
        dataCount <= 0 || dataBytes <= 0 || dataStride <= 0)
        return;

    const size_t elementBytes = std::min(
        static_cast<size_t>(dataBytes),
        static_cast<size_t>(dataStride));
    const size_t sourceSpan =
        static_cast<size_t>(dataCount - 1) * static_cast<size_t>(dataStride) +
        elementBytes;
    if (static_cast<size_t>(offset) + sourceSpan > static_cast<size_t>(buffer->size))
        return;

    auto* source = static_cast<mgbyte*>(buffer->buffer.contents) + offset;
    if (dataStride == dataBytes)
        memcpy(data, source, static_cast<size_t>(dataCount) * elementBytes);
    else
    {
        for (int i = 0; i < dataCount; ++i)
        {
            memcpy(
                data + static_cast<size_t>(i) * static_cast<size_t>(dataBytes),
                source + static_cast<size_t>(i) * static_cast<size_t>(dataStride),
                elementBytes);
        }
    }
}

static MTLTextureDescriptor* MGMetalTextureDescriptor(id<MTLDevice> device, MGTextureType type, MTLPixelFormat format, int width, int height, int depth, int mipmaps, int slices)
{
    MTLTextureDescriptor* descriptor = [MTLTextureDescriptor new];
    descriptor.pixelFormat = format;
    descriptor.width = std::max(width, 1);
    descriptor.height = std::max(height, 1);
    descriptor.depth = std::max(depth, 1);
    descriptor.mipmapLevelCount = std::max(mipmaps, 1);
    if (type == MGTextureType::_3D)
    {
        descriptor.textureType = MTLTextureType3D;
        descriptor.arrayLength = 1;
    }
    else if (type == MGTextureType::Cube)
    {
        descriptor.textureType = slices > 6 ? MTLTextureTypeCubeArray : MTLTextureTypeCube;
        descriptor.arrayLength = std::max(slices / 6, 1);
    }
    else
    {
        descriptor.textureType = slices > 1 ? MTLTextureType2DArray : MTLTextureType2D;
        descriptor.arrayLength = std::max(slices, 1);
    }
    descriptor.storageMode = MGMetalCpuVisibleTextureStorageMode(device);
    descriptor.usage = MTLTextureUsageShaderRead;
    return descriptor;
}

MGG_Texture* MGG_Texture_Create(MGG_GraphicsDevice* device, MGTextureType type, MGSurfaceFormat format, mgint width, mgint height, mgint depth, mgint mipmaps, mgint slices)
{
    if (device == nullptr || width <= 0 || height <= 0 || depth <= 0 || mipmaps <= 0 || slices <= 0)
        return nullptr;
    const MTLPixelFormat pixelFormat = MGMetalSurfaceFormat(format);
    if (pixelFormat == MTLPixelFormatInvalid)
        return nullptr;
    auto texture = new MGG_Texture();
    texture->type = type;
    texture->format = format;
    texture->width = width;
    texture->height = height;
    texture->depth = depth;
    texture->mipmaps = mipmaps;
    texture->slices = slices;
    texture->texture = [device->device newTextureWithDescriptor:MGMetalTextureDescriptor(device->device, type, pixelFormat, width, height, depth, mipmaps, slices)];
    if (texture->texture == nil)
    {
        delete texture;
        return nullptr;
    }
    return texture;
}

MGG_Texture* MGG_RenderTarget_Create(MGG_GraphicsDevice* device, MGTextureType type, MGSurfaceFormat format, mgint width, mgint height, mgint depth, mgint mipmaps, mgint slices, MGDepthFormat depthFormat, mgint multiSampleCount, MGRenderTargetUsage usage)
{
    (void)usage;
    MGG_Texture* texture = MGG_Texture_Create(device, type, format, width, height, depth, mipmaps, slices);
    if (texture == nullptr)
        return nullptr;
    texture->sampleCount = MGMetalSupportedSampleCount(device->device, multiSampleCount);
    texture->depthFormat = depthFormat;
    MTLTextureDescriptor* renderTargetDescriptor = MGMetalTextureDescriptor(
        device->device,
        type,
        MGMetalSurfaceFormat(format),
        width,
        height,
        depth,
        mipmaps,
        slices);
    renderTargetDescriptor.usage = MTLTextureUsageRenderTarget | MTLTextureUsageShaderRead;
    texture->texture = [device->device newTextureWithDescriptor:renderTargetDescriptor];
    if (texture->texture == nil)
    {
        delete texture;
        return nullptr;
    }
    if (texture->sampleCount > 1)
    {
        MTLTextureDescriptor* descriptor = [MTLTextureDescriptor texture2DDescriptorWithPixelFormat:texture->texture.pixelFormat width:std::max(width, 1) height:std::max(height, 1) mipmapped:NO];
        if (slices > 1)
        {
            descriptor.textureType = MTLTextureType2DMultisampleArray;
            descriptor.arrayLength = slices;
        }
        else
        {
            descriptor.textureType = MTLTextureType2DMultisample;
        }
        descriptor.sampleCount = texture->sampleCount;
        descriptor.storageMode = MTLStorageModePrivate;
        descriptor.usage = MTLTextureUsageRenderTarget;
        texture->multisampleTexture = [device->device newTextureWithDescriptor:descriptor];
    }
    texture->depthTexture = MGMetalCreateDepthTexture(device->device, width, height, depthFormat, texture->sampleCount, slices);
    return texture;
}

MGG_Texture* MGG_DepthStencilTarget_Create(
    MGG_GraphicsDevice* device,
    mgint width,
    mgint height,
    MGDepthFormat depthFormat)
{
    if (device == nullptr || device->device == nil || width <= 0 || height <= 0 ||
        !MGMetalSupportsSampleableDepthSemantic(depthFormat))
        return nullptr;

    auto texture = new MGG_Texture();
    texture->type = MGTextureType::_2D;
    texture->depthFormat = depthFormat;
    texture->width = width;
    texture->height = height;
    texture->depth = 1;
    texture->mipmaps = 1;
    texture->slices = 1;
    texture->sampleCount = 1;
    texture->independentDepthStencil = true;
    texture->texture = MGMetalCreateDepthTexture(
        device->device,
        width,
        height,
        depthFormat,
        1,
        1,
        true);
    if (texture->texture == nil)
    {
        delete texture;
        return nullptr;
    }
    return texture;
}

void MGG_Texture_Destroy(MGG_GraphicsDevice* device, MGG_Texture* texture)
{
    (void)device;
    delete texture;
}

void MGG_Texture_SetData(MGG_GraphicsDevice* device, MGG_Texture* texture, mgint level, mgint slice, mgint x, mgint y, mgint z, mgint width, mgint height, mgint depth, mgbyte* data, mgint dataBytes)
{
    (void)device;
    if (texture == nullptr || texture->texture == nil || data == nullptr || dataBytes <= 0)
        return;
    width = width > 0 ? width : std::max(texture->width >> level, 1);
    height = height > 0 ? height : std::max(texture->height >> level, 1);
    depth = depth > 0 ? depth : std::max(texture->depth >> level, 1);
    const NSUInteger bytesPerImage = dataBytes / std::max(depth, 1);
    const NSUInteger bytesPerRow = MGMetalBytesPerRow(texture->format, height, depth, dataBytes);
    [texture->texture replaceRegion:MTLRegionMake3D(x, y, z, width, height, depth) mipmapLevel:level slice:std::max(slice, 0) withBytes:data bytesPerRow:bytesPerRow bytesPerImage:bytesPerImage];
}

void MGG_Texture_GetData(MGG_GraphicsDevice* device, MGG_Texture* texture, mgint level, mgint slice, mgint x, mgint y, mgint z, mgint width, mgint height, mgint depth, mgbyte* data, mgint dataBytes)
{
    if (device == nullptr || texture == nullptr || texture->texture == nil || data == nullptr || dataBytes <= 0)
        return;
    width = width > 0 ? width : std::max(texture->width >> level, 1);
    height = height > 0 ? height : std::max(texture->height >> level, 1);
    depth = depth > 0 ? depth : std::max(texture->depth >> level, 1);

    // Texture readback is a synchronous API. Finish pending render-target
    // resolves and mip generation before exposing shared texture memory.
    MGMetalEndEncoder(device);
    if (device->commandBuffer != nil)
    {
        id<MTLCommandBuffer> submitted = device->commandBuffer;
#if TARGET_OS_OSX
        if (texture->texture.storageMode == MTLStorageModeManaged)
        {
            id<MTLBlitCommandEncoder> synchronization = [submitted blitCommandEncoder];
            [synchronization synchronizeTexture:texture->texture slice:std::max(slice, 0) level:level];
            [synchronization endEncoding];
        }
#endif
        [submitted commit];
        [submitted waitUntilCompleted];
        MGMetalReportCommandBufferError(submitted);
        device->lastSubmitted = submitted;
        device->commandBuffer = [device->queue commandBuffer];
        if (submitted.status != MTLCommandBufferStatusCompleted || device->commandBuffer == nil)
            return;
    }

    const NSUInteger bytesPerImage = dataBytes / std::max(depth, 1);
    const NSUInteger bytesPerRow = MGMetalBytesPerRow(texture->format, height, depth, dataBytes);
    [texture->texture getBytes:data bytesPerRow:bytesPerRow bytesPerImage:bytesPerImage fromRegion:MTLRegionMake3D(x, y, z, width, height, depth) mipmapLevel:level slice:std::max(slice, 0)];
}

static bool MGMetalParseShaderReflection(MGG_Shader* shader)
{
    if (shader->reflection.empty())
        return false;

    NSData* data = [NSData dataWithBytes:shader->reflection.data() length:shader->reflection.size()];
    NSError* error = nil;
    id object = [NSJSONSerialization JSONObjectWithData:data options:0 error:&error];
    if (![object isKindOfClass:[NSDictionary class]])
        return false;

    NSDictionary* root = (NSDictionary*)object;
    NSArray* topLevel = root[@"TopLevelArgumentBuffer"];
    if (![topLevel isKindOfClass:[NSArray class]])
        return false;

    std::vector<int> tableBindingIndices;
    NSArray* usedResources = root[@"UsedResources"];
    if ([usedResources isKindOfClass:[NSArray class]])
    {
        for (id value in usedResources)
        {
            if (![value isKindOfClass:[NSDictionary class]])
                continue;
            NSDictionary* resource = (NSDictionary*)value;
            NSNumber* tableStartIndex = resource[@"tableStartIndex"];
            NSNumber* bindingIndex = resource[@"bindingIndex"];
            if ([tableStartIndex isKindOfClass:[NSNumber class]] &&
                [bindingIndex isKindOfClass:[NSNumber class]] &&
                tableStartIndex.unsignedIntValue != UINT32_MAX)
                tableBindingIndices.push_back(bindingIndex.intValue);
        }
        std::sort(tableBindingIndices.begin(), tableBindingIndices.end());
    }

    const int resourceBindingIndex = shader->stage == MGShaderStage::Vertex ? 2 : 3;
    const int samplerBindingIndex = shader->stage == MGShaderStage::Vertex ? 4 : 5;
    NSUInteger tableOrdinal = 0;
    for (id value in topLevel)
    {
        if (![value isKindOfClass:[NSDictionary class]])
            return false;
        NSDictionary* entry = (NSDictionary*)value;
        NSString* type = entry[@"Type"];
        NSNumber* offsetValue = entry[@"EltOffset"];
        NSNumber* sizeValue = entry[@"Size"];
        NSNumber* slotValue = entry[@"Slot"];
        if (![type isKindOfClass:[NSString class]] ||
            ![offsetValue isKindOfClass:[NSNumber class]] ||
            ![sizeValue isKindOfClass:[NSNumber class]] ||
            ![slotValue isKindOfClass:[NSNumber class]])
            return false;

        const int offset = offsetValue.intValue;
        const int size = sizeValue.intValue;
        const int slot = slotValue.intValue;
        if (offset < 0 || (size != (int)sizeof(uint64_t) &&
            size != (int)sizeof(MGMetalIRDescriptorTableEntry)) ||
            offset % (int)sizeof(uint64_t) != 0 || offset > 1024)
            return false;
        shader->topLevelArgumentBufferSize = std::max(
            shader->topLevelArgumentBufferSize,
            offset + size);

        if ([type isEqualToString:@"CBV"])
        {
            if (size == (int)sizeof(uint64_t))
                shader->rootConstantBufferOffset = offset;
            else if (slot >= 0 && slot < 16)
                shader->constantBufferOffsets[slot] = offset;
            else
                return false;
        }
        else if ([type isEqualToString:@"SRV"])
        {
            if (size != (int)sizeof(MGMetalIRDescriptorTableEntry) || slot < 0 || slot >= 16)
                return false;
            shader->textureOffsets[slot] = offset;
        }
        else if ([type isEqualToString:@"Sampler"])
        {
            if (size != (int)sizeof(MGMetalIRDescriptorTableEntry) || slot < 0 || slot >= 16)
                return false;
            shader->samplerOffsets[slot] = offset;
        }
        else if ([type isEqualToString:@"Table"])
        {
            if (size != (int)sizeof(uint64_t))
                return false;
            const int bindingIndex = tableOrdinal < tableBindingIndices.size() ?
                tableBindingIndices[tableOrdinal] : -1;
            if (bindingIndex == resourceBindingIndex ||
                (bindingIndex < 0 && shader->resourceTableOffset < 0))
                shader->resourceTableOffset = offset;
            else if (bindingIndex == samplerBindingIndex || bindingIndex < 0)
                shader->samplerTableOffset = offset;
            ++tableOrdinal;
        }
        else
        {
            return false;
        }
    }

    // Shaders without constants, textures, or samplers legitimately have an
    // empty top-level argument buffer.  The converter still emits a valid
    // reflection object for them and no resource offsets need to be encoded.
    return true;
}

MGG_InputLayout* MGG_InputLayout_Create(MGG_GraphicsDevice* device, MGG_Shader* vertexShader, mgint* strides, mgint streamCount, MGG_InputElement* elements, mgint elementCount)
{
    (void)vertexShader;
    if (device == nullptr || streamCount < 0 || streamCount > (mgint)MGMetalMaxVertexBuffers ||
        elementCount < 0 || elementCount > (mgint)MGMetalMaxVertexAttributes ||
        (streamCount > 0 && strides == nullptr) || (elementCount > 0 && elements == nullptr))
        return nullptr;

    for (int i = 0; i < streamCount; ++i)
    {
        if (strides[i] < 0)
            return nullptr;
    }
    for (int i = 0; i < elementCount; ++i)
    {
        if (elements[i].VertexBufferSlot >= (mguint)streamCount ||
            MGMetalVertexFormat(elements[i].Format) == MTLVertexFormatInvalid)
            return nullptr;
    }

    auto layout = new MGG_InputLayout();
    layout->pipelineResourceId = device->nextPipelineResourceId++;
    layout->descriptor = [MTLVertexDescriptor vertexDescriptor];
    layout->strides.resize(streamCount);
    for (int i = 0; i < streamCount; ++i)
    {
        const NSUInteger metalBufferIndex = MGMetalIRStageInAttributeStartIndex + i;
        layout->strides[i] = strides[i];
        layout->descriptor.layouts[metalBufferIndex].stride = strides[i];
        layout->descriptor.layouts[metalBufferIndex].stepFunction = MTLVertexStepFunctionPerVertex;
        layout->descriptor.layouts[metalBufferIndex].stepRate = 1;
    }
    for (int i = 0; i < elementCount; ++i)
    {
        const MGG_InputElement& element = elements[i];
        const NSUInteger attributeIndex = MGMetalIRStageInAttributeStartIndex + element.ShaderLocation;
        const NSUInteger metalBufferIndex = MGMetalIRStageInAttributeStartIndex + element.VertexBufferSlot;
        layout->descriptor.attributes[attributeIndex].format = MGMetalVertexFormat(element.Format);
        layout->descriptor.attributes[attributeIndex].offset = element.AlignedByteOffset;
        layout->descriptor.attributes[attributeIndex].bufferIndex = metalBufferIndex;
        if (element.InstanceDataStepRate > 0)
        {
            layout->descriptor.layouts[metalBufferIndex].stepFunction = MTLVertexStepFunctionPerInstance;
            layout->descriptor.layouts[metalBufferIndex].stepRate = element.InstanceDataStepRate;
        }
    }
    return layout;
}

void MGG_InputLayout_Destroy(MGG_GraphicsDevice* device, MGG_InputLayout* layout)
{
    (void)device;
    delete layout;
}

MGG_Shader* MGG_Shader_Create(MGG_GraphicsDevice* device, MGShaderStage stage, mgbyte* bytecode, mgint sizeInBytes)
{
    if (device == nullptr || MGMetalShaderStageIndex(stage) < 0 ||
        bytecode == nullptr || sizeInBytes <= 0)
        return nullptr;

    const uint32_t bindingLayoutMagic = 0x00B00B00;
    const uint32_t metalPayloadMagicV1 = 0x314C544D;
    const uint32_t metalPayloadMagicV2 = 0x324C544D;
    const uint32_t metalPayloadMagicV3 = 0x334C544D;
    const mgbyte* metalLibraryBytes = bytecode;
    int metalLibrarySize = sizeInBytes;
    NSString* entryPoint = @"main";
    int maxSamplerSlot = 15;
    int maxTextureSlot = 15;
    bool hasReflectionPayload = false;

    if (sizeInBytes >= 12 && *(const uint32_t*)bytecode == bindingLayoutMagic)
    {
        memcpy(&maxSamplerSlot, bytecode + 4, sizeof(maxSamplerSlot));
        memcpy(&maxTextureSlot, bytecode + 8, sizeof(maxTextureSlot));
        if (maxSamplerSlot < -1 || maxSamplerSlot >= (int)MGMetalIRDescriptorCount ||
            maxTextureSlot < -1 || maxTextureSlot >= (int)MGMetalIRDescriptorCount)
            return nullptr;
        metalLibraryBytes += 12;
        metalLibrarySize -= 12;
    }

    std::string reflection;
    if (metalLibrarySize >= 16 && *(const uint32_t*)metalLibraryBytes == metalPayloadMagicV3)
    {
        hasReflectionPayload = true;
        const uint32_t nameLength = *(const uint32_t*)(metalLibraryBytes + 4);
        const uint32_t reflectionLength = *(const uint32_t*)(metalLibraryBytes + 8);
        const uint32_t deviceLibraryLength = *(const uint32_t*)(metalLibraryBytes + 12);
        const uint64_t headerLength = 16;
        const uint64_t metadataLength = (uint64_t)nameLength + reflectionLength;
        if (metadataLength > (uint64_t)metalLibrarySize - headerLength)
            return nullptr;
        const uint64_t librariesLength = (uint64_t)metalLibrarySize - headerLength - metadataLength;
        if (deviceLibraryLength == 0 || deviceLibraryLength >= librariesLength)
            return nullptr;
        entryPoint = [[NSString alloc] initWithBytes:metalLibraryBytes + headerLength length:nameLength encoding:NSUTF8StringEncoding];
        reflection.assign(
            (const char*)metalLibraryBytes + headerLength + nameLength,
            reflectionLength);
        metalLibraryBytes += headerLength + metadataLength;
        metalLibrarySize = (int)librariesLength;
#if TARGET_OS_SIMULATOR
        metalLibraryBytes += deviceLibraryLength;
        metalLibrarySize -= (int)deviceLibraryLength;
#else
        metalLibrarySize = (int)deviceLibraryLength;
#endif
    }
    else if (metalLibrarySize >= 12 && *(const uint32_t*)metalLibraryBytes == metalPayloadMagicV2)
    {
        hasReflectionPayload = true;
        const uint32_t nameLength = *(const uint32_t*)(metalLibraryBytes + 4);
        const uint32_t reflectionLength = *(const uint32_t*)(metalLibraryBytes + 8);
        const uint64_t headerLength = 12;
        const uint64_t metadataLength = (uint64_t)nameLength + reflectionLength;
        if (metadataLength > (uint64_t)metalLibrarySize - headerLength)
            return nullptr;
        entryPoint = [[NSString alloc] initWithBytes:metalLibraryBytes + headerLength length:nameLength encoding:NSUTF8StringEncoding];
        reflection.assign(
            (const char*)metalLibraryBytes + headerLength + nameLength,
            reflectionLength);
        metalLibraryBytes += headerLength + metadataLength;
        metalLibrarySize -= (int)(headerLength + metadataLength);
    }
    else if (metalLibrarySize >= 8 && *(const uint32_t*)metalLibraryBytes == metalPayloadMagicV1)
    {
        const uint32_t nameLength = *(const uint32_t*)(metalLibraryBytes + 4);
        if (nameLength > (uint32_t)(metalLibrarySize - 8))
            return nullptr;
        entryPoint = [[NSString alloc] initWithBytes:metalLibraryBytes + 8 length:nameLength encoding:NSUTF8StringEncoding];
        metalLibraryBytes += 8 + nameLength;
        metalLibrarySize -= 8 + nameLength;
    }

    auto shader = new MGG_Shader();
    std::fill_n(shader->constantBufferOffsets, 16, -1);
    std::fill_n(shader->textureOffsets, 16, -1);
    std::fill_n(shader->samplerOffsets, 16, -1);
    shader->stage = stage;
    shader->reflection = std::move(reflection);
    shader->maxSamplerSlot = maxSamplerSlot;
    shader->maxTextureSlot = maxTextureSlot;
    if (hasReflectionPayload)
    {
        if (!MGMetalParseShaderReflection(shader))
        {
            fprintf(stderr, "Metal shader reflection JSON does not describe a valid argument-buffer layout.\n");
            delete shader;
            return nullptr;
        }
    }
    else
    {
        shader->rootConstantBufferOffset = 0;
        shader->resourceTableOffset = 8;
        shader->samplerTableOffset = 16;
        shader->topLevelArgumentBufferSize = 24;
    }
    // The managed bytecode buffer is only pinned for this call.  The default
    // dispatch-data destructor copies it, so Metal never retains that pointer.
    dispatch_data_t data = dispatch_data_create(metalLibraryBytes, metalLibrarySize, nullptr, DISPATCH_DATA_DESTRUCTOR_DEFAULT);
    NSError* error = nil;
    shader->library = [device->device newLibraryWithData:data error:&error];
    if (shader->library == nil)
    {
        fprintf(stderr, "Metal shader library load failed: %s\n", error.localizedDescription.UTF8String);
        delete shader;
        return nullptr;
    }
    shader->function = [shader->library newFunctionWithName:entryPoint];
    if (shader->function == nil)
    {
        fprintf(stderr, "Metal shader library does not contain entry point '%s'.\n", entryPoint.UTF8String);
        delete shader;
        return nullptr;
    }
    shader->pipelineResourceId = device->nextPipelineResourceId++;
    return shader;
}

void MGG_Shader_Destroy(MGG_GraphicsDevice* device, MGG_Shader* shader)
{
    (void)device;
    delete shader;
}

MGG_OcclusionQuery* MGG_OcclusionQuery_Create(MGG_GraphicsDevice* device)
{
    if (device == nullptr)
        return nullptr;
    auto query = new MGG_OcclusionQuery();
    query->visibility = [device->device newBufferWithLength:sizeof(uint64_t) options:MGMetalCpuVisibleBufferStorageMode(device->device)];
    if (query->visibility == nil)
    {
        delete query;
        return nullptr;
    }
    memset(query->visibility.contents, 0, sizeof(uint64_t));
#if TARGET_OS_OSX
    if (query->visibility.storageMode == MTLStorageModeManaged)
        [query->visibility didModifyRange:NSMakeRange(0, sizeof(uint64_t))];
#endif
    return query;
}

void MGG_OcclusionQuery_Destroy(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query)
{
    (void)device;
    delete query;
}

void MGG_OcclusionQuery_Begin(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query)
{
    if (query == nullptr || !MGMetalEnsureEncoder(device))
        return;
    memset(query->visibility.contents, 0, sizeof(uint64_t));
#if TARGET_OS_OSX
    if (query->visibility.storageMode == MTLStorageModeManaged)
        [query->visibility didModifyRange:NSMakeRange(0, sizeof(uint64_t))];
#endif
    query->commandBuffer = device->commandBuffer;
    [device->encoder setVisibilityResultMode:MTLVisibilityResultModeCounting offset:0];
}

void MGG_OcclusionQuery_End(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query)
{
    if (query == nullptr || device->encoder == nil)
        return;
    [device->encoder setVisibilityResultMode:MTLVisibilityResultModeDisabled offset:0];
}

mgbyte MGG_OcclusionQuery_GetResult(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query, mgint& pixelCount)
{
    if (device == nullptr || query == nullptr || query->commandBuffer == nil ||
        query->commandBuffer.status != MTLCommandBufferStatusCompleted)
        return false;
#if TARGET_OS_OSX
    if (query->visibility.storageMode == MTLStorageModeManaged)
    {
        id<MTLCommandBuffer> synchronization = [device->queue commandBuffer];
        id<MTLBlitCommandEncoder> blit = [synchronization blitCommandEncoder];
        [blit synchronizeResource:query->visibility];
        [blit endEncoding];
        [synchronization commit];
        [synchronization waitUntilCompleted];
        MGMetalReportCommandBufferError(synchronization);
        if (synchronization.status != MTLCommandBufferStatusCompleted)
            return false;
    }
#endif
    pixelCount = (mgint)std::min(*(uint64_t*)query->visibility.contents, (uint64_t)INT_MAX);
    return true;
}
