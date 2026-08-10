// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "api_MGG.h"
#include "mg_common.h"
#include "MGGL_Host.h"

#if !defined(MG_OPENGL)
#error MGG_OpenGL.cpp must only be compiled for the native OpenGL backend.
#endif

#define GL_GLEXT_PROTOTYPES 1
#if defined(MG_ANDROID)
#include <GLES3/gl3.h>
#include <GLES3/gl3ext.h>
#elif defined(MG_IOS)
#include <OpenGLES/ES3/gl.h>
#include <OpenGLES/ES3/glext.h>
#else
#include <SDL.h>
#if defined(__APPLE__)
#include <OpenGL/gl3.h>
#include <OpenGL/gl3ext.h>
#else
#include <SDL_opengl.h>
#include <SDL_opengl_glext.h>
#endif
#endif

#ifndef APIENTRY
#define APIENTRY GL_APIENTRY
#endif

#include <algorithm>
#include <array>
#include <cassert>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

#if defined(MG_ANDROID)
#define MGGL_BUILTIN_EFFECTS 1
#define MGGL_BUILTIN_SUFFIX _gles30_mgfxo
#include "android/AlphaTestEffect.gles30.mgfxo.h"
#include "android/BasicEffect.gles30.mgfxo.h"
#include "android/DualTextureEffect.gles30.mgfxo.h"
#include "android/EnvironmentMapEffect.gles30.mgfxo.h"
#include "android/SkinnedEffect.gles30.mgfxo.h"
#include "android/SpriteEffect.gles30.mgfxo.h"
#elif defined(MG_IOS)
#define MGGL_BUILTIN_EFFECTS 1
#define MGGL_BUILTIN_SUFFIX _gles30_mgfxo
#include "ios/AlphaTestEffect.gles30.mgfxo.h"
#include "ios/BasicEffect.gles30.mgfxo.h"
#include "ios/DualTextureEffect.gles30.mgfxo.h"
#include "ios/EnvironmentMapEffect.gles30.mgfxo.h"
#include "ios/SkinnedEffect.gles30.mgfxo.h"
#include "ios/SpriteEffect.gles30.mgfxo.h"
#else
#define MGGL_BUILTIN_EFFECTS 1
#define MGGL_BUILTIN_SUFFIX _gl41_mgfxo
#include "desktop/AlphaTestEffect.gl41.mgfxo.h"
#include "desktop/BasicEffect.gl41.mgfxo.h"
#include "desktop/DualTextureEffect.gl41.mgfxo.h"
#include "desktop/EnvironmentMapEffect.gl41.mgfxo.h"
#include "desktop/SkinnedEffect.gl41.mgfxo.h"
#include "desktop/SpriteEffect.gl41.mgfxo.h"
#endif

#ifndef GL_TEXTURE_MAX_ANISOTROPY_EXT
#define GL_TEXTURE_MAX_ANISOTROPY_EXT 0x84FE
#define GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT 0x84FF
#endif
#ifndef GL_COMPRESSED_RGBA_S3TC_DXT1_EXT
#define GL_COMPRESSED_RGBA_S3TC_DXT1_EXT 0x83F1
#define GL_COMPRESSED_RGBA_S3TC_DXT3_EXT 0x83F2
#define GL_COMPRESSED_RGBA_S3TC_DXT5_EXT 0x83F3
#endif
#ifndef GL_BGRA
#define GL_BGRA 0x80E1
#endif
#ifndef GL_UNSIGNED_SHORT_1_5_5_5_REV
#define GL_UNSIGNED_SHORT_1_5_5_5_REV 0x8366
#define GL_UNSIGNED_SHORT_4_4_4_4_REV 0x8365
#endif
#ifndef GL_UNSIGNED_INT_8_8_8_8_REV
#define GL_UNSIGNED_INT_8_8_8_8_REV 0x8367
#endif
#ifndef GL_CLAMP_TO_BORDER
#define GL_CLAMP_TO_BORDER 0x812D
#define GL_TEXTURE_BORDER_COLOR 0x1004
#endif
#ifndef GL_COMPRESSED_RGB_PVRTC_4BPPV1_IMG
#define GL_COMPRESSED_RGB_PVRTC_4BPPV1_IMG 0x8C00
#define GL_COMPRESSED_RGB_PVRTC_2BPPV1_IMG 0x8C01
#define GL_COMPRESSED_RGBA_PVRTC_4BPPV1_IMG 0x8C02
#define GL_COMPRESSED_RGBA_PVRTC_2BPPV1_IMG 0x8C03
#endif
#ifndef GL_ETC1_RGB8_OES
#define GL_ETC1_RGB8_OES 0x8D64
#endif
#ifndef GL_COMPRESSED_RGB8_ETC2
#define GL_COMPRESSED_RGB8_ETC2 0x9274
#define GL_COMPRESSED_SRGB8_ETC2 0x9275
#define GL_COMPRESSED_RGB8_PUNCHTHROUGH_ALPHA1_ETC2 0x9276
#define GL_COMPRESSED_SRGB8_PUNCHTHROUGH_ALPHA1_ETC2 0x9277
#define GL_COMPRESSED_RGBA8_ETC2_EAC 0x9278
#define GL_COMPRESSED_SRGB8_ALPHA8_ETC2_EAC 0x9279
#endif
#ifndef GL_COMPRESSED_RGBA_ASTC_4x4_KHR
#define GL_COMPRESSED_RGBA_ASTC_4x4_KHR 0x93B0
#define GL_COMPRESSED_RGBA_ASTC_5x5_KHR 0x93B2
#define GL_COMPRESSED_RGBA_ASTC_6x6_KHR 0x93B4
#define GL_COMPRESSED_RGBA_ASTC_8x8_KHR 0x93B7
#define GL_COMPRESSED_RGBA_ASTC_10x10_KHR 0x93BB
#define GL_COMPRESSED_RGBA_ASTC_12x12_KHR 0x93BD
#endif

namespace
{
#if defined(MG_ANDROID)
    constexpr mgint MGGLShaderProfile = 82;
    constexpr int MGGLRequiredMajor = 3;
    constexpr int MGGLRequiredMinor = 0;
#elif defined(MG_IOS)
    constexpr mgint MGGLShaderProfile = 82;
    constexpr int MGGLRequiredMajor = 3;
    constexpr int MGGLRequiredMinor = 0;
#else
    constexpr mgint MGGLShaderProfile = 84;
    constexpr int MGGLRequiredMajor = 4;
    constexpr int MGGLRequiredMinor = 1;
#endif
    constexpr uint32_t MGGLPayloadMagic = 0x314C474D; // MGL1
    constexpr uint32_t MGGLPayloadVersion = 1;
    constexpr int MGGLMaxTextureSlots = 16;
    constexpr int MGGLMaxVertexBuffers = 16;
    constexpr int MGGLMaxColorTargets = 4;

#define MGGL_FUNCTIONS(X) \
    X(const GLubyte*, GetString, (GLenum)) \
    X(const GLubyte*, GetStringi, (GLenum, GLuint)) \
    X(void, GetIntegerv, (GLenum, GLint*)) \
    X(void, GetFloatv, (GLenum, GLfloat*)) \
    X(void, GenVertexArrays, (GLsizei, GLuint*)) \
    X(void, DeleteVertexArrays, (GLsizei, const GLuint*)) \
    X(void, BindVertexArray, (GLuint)) \
    X(void, GenBuffers, (GLsizei, GLuint*)) \
    X(void, DeleteBuffers, (GLsizei, const GLuint*)) \
    X(void, BindBuffer, (GLenum, GLuint)) \
    X(void, BufferData, (GLenum, GLsizeiptr, const void*, GLenum)) \
    X(void, BufferSubData, (GLenum, GLintptr, GLsizeiptr, const void*)) \
    X(void, GetBufferSubData, (GLenum, GLintptr, GLsizeiptr, void*)) \
    X(void, BindBufferBase, (GLenum, GLuint, GLuint)) \
    X(void, GenTextures, (GLsizei, GLuint*)) \
    X(void, DeleteTextures, (GLsizei, const GLuint*)) \
    X(void, ActiveTexture, (GLenum)) \
    X(void, BindTexture, (GLenum, GLuint)) \
    X(void, TexParameteri, (GLenum, GLenum, GLint)) \
    X(void, TexImage2D, (GLenum, GLint, GLint, GLsizei, GLsizei, GLint, GLenum, GLenum, const void*)) \
    X(void, TexImage3D, (GLenum, GLint, GLint, GLsizei, GLsizei, GLsizei, GLint, GLenum, GLenum, const void*)) \
    X(void, TexSubImage2D, (GLenum, GLint, GLint, GLint, GLsizei, GLsizei, GLenum, GLenum, const void*)) \
    X(void, TexSubImage3D, (GLenum, GLint, GLint, GLint, GLint, GLsizei, GLsizei, GLsizei, GLenum, GLenum, const void*)) \
    X(void, CompressedTexSubImage2D, (GLenum, GLint, GLint, GLint, GLsizei, GLsizei, GLenum, GLsizei, const void*)) \
    X(void, CompressedTexSubImage3D, (GLenum, GLint, GLint, GLint, GLint, GLsizei, GLsizei, GLsizei, GLenum, GLsizei, const void*)) \
    X(void, CompressedTexImage2D, (GLenum, GLint, GLenum, GLsizei, GLsizei, GLint, GLsizei, const void*)) \
    X(void, CompressedTexImage3D, (GLenum, GLint, GLenum, GLsizei, GLsizei, GLsizei, GLint, GLsizei, const void*)) \
    X(void, GetTexImage, (GLenum, GLint, GLenum, GLenum, void*)) \
    X(void, GenerateMipmap, (GLenum)) \
    X(void, PixelStorei, (GLenum, GLint)) \
    X(void, GenSamplers, (GLsizei, GLuint*)) \
    X(void, DeleteSamplers, (GLsizei, const GLuint*)) \
    X(void, BindSampler, (GLuint, GLuint)) \
    X(void, SamplerParameteri, (GLuint, GLenum, GLint)) \
    X(void, SamplerParameterf, (GLuint, GLenum, GLfloat)) \
    X(void, SamplerParameterfv, (GLuint, GLenum, const GLfloat*)) \
    X(void, GenFramebuffers, (GLsizei, GLuint*)) \
    X(void, DeleteFramebuffers, (GLsizei, const GLuint*)) \
    X(void, BindFramebuffer, (GLenum, GLuint)) \
    X(void, FramebufferTexture2D, (GLenum, GLenum, GLenum, GLuint, GLint)) \
    X(void, FramebufferTextureLayer, (GLenum, GLenum, GLuint, GLint, GLint)) \
    X(void, FramebufferRenderbuffer, (GLenum, GLenum, GLenum, GLuint)) \
    X(void, DrawBuffers, (GLsizei, const GLenum*)) \
    X(void, InvalidateFramebuffer, (GLenum, GLsizei, const GLenum*)) \
    X(GLenum, CheckFramebufferStatus, (GLenum)) \
    X(void, BlitFramebuffer, (GLint, GLint, GLint, GLint, GLint, GLint, GLint, GLint, GLbitfield, GLenum)) \
    X(void, GenRenderbuffers, (GLsizei, GLuint*)) \
    X(void, DeleteRenderbuffers, (GLsizei, const GLuint*)) \
    X(void, BindRenderbuffer, (GLenum, GLuint)) \
    X(void, RenderbufferStorage, (GLenum, GLenum, GLsizei, GLsizei)) \
    X(void, RenderbufferStorageMultisample, (GLenum, GLsizei, GLenum, GLsizei, GLsizei)) \
    X(GLuint, CreateShader, (GLenum)) \
    X(void, ShaderSource, (GLuint, GLsizei, const GLchar* const*, const GLint*)) \
    X(void, CompileShader, (GLuint)) \
    X(void, GetShaderiv, (GLuint, GLenum, GLint*)) \
    X(void, GetShaderInfoLog, (GLuint, GLsizei, GLsizei*, GLchar*)) \
    X(void, DeleteShader, (GLuint)) \
    X(GLuint, CreateProgram, ()) \
    X(void, AttachShader, (GLuint, GLuint)) \
    X(void, LinkProgram, (GLuint)) \
    X(void, GetProgramiv, (GLuint, GLenum, GLint*)) \
    X(void, GetProgramInfoLog, (GLuint, GLsizei, GLsizei*, GLchar*)) \
    X(void, DeleteProgram, (GLuint)) \
    X(void, UseProgram, (GLuint)) \
    X(GLuint, GetUniformBlockIndex, (GLuint, const GLchar*)) \
    X(void, UniformBlockBinding, (GLuint, GLuint, GLuint)) \
    X(GLint, GetUniformLocation, (GLuint, const GLchar*)) \
    X(void, Uniform1i, (GLint, GLint)) \
    X(void, Uniform4f, (GLint, GLfloat, GLfloat, GLfloat, GLfloat)) \
    X(void, Enable, (GLenum)) \
    X(void, Disable, (GLenum)) \
    X(void, BlendColor, (GLfloat, GLfloat, GLfloat, GLfloat)) \
    X(void, BlendFuncSeparate, (GLenum, GLenum, GLenum, GLenum)) \
    X(void, BlendEquationSeparate, (GLenum, GLenum)) \
    X(void, ColorMask, (GLboolean, GLboolean, GLboolean, GLboolean)) \
    X(void, DepthMask, (GLboolean)) \
    X(void, DepthFunc, (GLenum)) \
    X(void, StencilMask, (GLuint)) \
    X(void, StencilFunc, (GLenum, GLint, GLuint)) \
    X(void, StencilOp, (GLenum, GLenum, GLenum)) \
    X(void, PolygonMode, (GLenum, GLenum)) \
    X(void, CullFace, (GLenum)) \
    X(void, FrontFace, (GLenum)) \
    X(void, Scissor, (GLint, GLint, GLsizei, GLsizei)) \
    X(void, Viewport, (GLint, GLint, GLsizei, GLsizei)) \
    X(void, DepthRangef, (GLfloat, GLfloat)) \
    X(void, ClearColor, (GLfloat, GLfloat, GLfloat, GLfloat)) \
    X(void, ClearDepthf, (GLfloat)) \
    X(void, ClearStencil, (GLint)) \
    X(void, Clear, (GLbitfield)) \
    X(void, ClearBufferfv, (GLenum, GLint, const GLfloat*)) \
    X(void, ClearBufferiv, (GLenum, GLint, const GLint*)) \
    X(void, ClearBufferfi, (GLenum, GLint, GLfloat, GLint)) \
    X(void, ReadPixels, (GLint, GLint, GLsizei, GLsizei, GLenum, GLenum, void*)) \
    X(void, EnableVertexAttribArray, (GLuint)) \
    X(void, DisableVertexAttribArray, (GLuint)) \
    X(void, VertexAttribPointer, (GLuint, GLint, GLenum, GLboolean, GLsizei, const void*)) \
    X(void, VertexAttribIPointer, (GLuint, GLint, GLenum, GLsizei, const void*)) \
    X(void, VertexAttribDivisor, (GLuint, GLuint)) \
    X(void, DrawArrays, (GLenum, GLint, GLsizei)) \
    X(void, DrawElements, (GLenum, GLsizei, GLenum, const void*)) \
    X(void, DrawElementsInstanced, (GLenum, GLsizei, GLenum, const void*, GLsizei)) \
    X(void, DrawElementsBaseVertex, (GLenum, GLsizei, GLenum, const void*, GLint)) \
    X(void, DrawElementsInstancedBaseVertex, (GLenum, GLsizei, GLenum, const void*, GLsizei, GLint)) \
    X(void, GenQueries, (GLsizei, GLuint*)) \
    X(void, DeleteQueries, (GLsizei, const GLuint*)) \
    X(void, BeginQuery, (GLenum, GLuint)) \
    X(void, EndQuery, (GLenum)) \
    X(void, GetQueryObjectuiv, (GLuint, GLenum, GLuint*)) \
    X(void, Finish, ())

    struct GLApi
    {
#define MGGL_MEMBER(result, name, args) result (APIENTRY* name) args = nullptr;
        MGGL_FUNCTIONS(MGGL_MEMBER)
#undef MGGL_MEMBER

        bool Load()
        {
#define MGGL_LOAD(result, name, args) \
            name = reinterpret_cast<decltype(name)>(MGGL_Host_GetProcAddress("gl" #name));
            MGGL_FUNCTIONS(MGGL_LOAD)
#undef MGGL_LOAD
            const bool common =
                GetString && GetStringi && GetIntegerv && GetFloatv &&
                GenVertexArrays && DeleteVertexArrays && BindVertexArray &&
                GenBuffers && DeleteBuffers && BindBuffer && BufferData && BufferSubData && BindBufferBase &&
                GenTextures && DeleteTextures && ActiveTexture && BindTexture && TexParameteri &&
                TexImage2D && TexImage3D && TexSubImage2D && TexSubImage3D &&
                CompressedTexImage2D && CompressedTexSubImage2D && GenerateMipmap && PixelStorei &&
                GenSamplers && DeleteSamplers && BindSampler && SamplerParameteri &&
                SamplerParameterf && SamplerParameterfv &&
                GenFramebuffers && DeleteFramebuffers && BindFramebuffer &&
                FramebufferTexture2D && FramebufferTextureLayer && FramebufferRenderbuffer &&
                DrawBuffers && CheckFramebufferStatus && BlitFramebuffer &&
                GenRenderbuffers && DeleteRenderbuffers && BindRenderbuffer &&
                RenderbufferStorage && RenderbufferStorageMultisample &&
                CreateShader && ShaderSource && CompileShader && GetShaderiv &&
                GetShaderInfoLog && DeleteShader && CreateProgram && AttachShader && LinkProgram &&
                GetProgramiv && GetProgramInfoLog && DeleteProgram && UseProgram &&
                GetUniformBlockIndex && UniformBlockBinding && GetUniformLocation && Uniform1i && Uniform4f &&
                Enable && Disable && BlendColor && BlendFuncSeparate && BlendEquationSeparate &&
                ColorMask && DepthMask && DepthFunc && StencilMask && StencilFunc && StencilOp &&
                CullFace && FrontFace && Scissor && Viewport && DepthRangef &&
                ClearColor && ClearDepthf && ClearStencil && Clear &&
                ClearBufferfv && ClearBufferiv && ClearBufferfi && ReadPixels &&
                EnableVertexAttribArray && DisableVertexAttribArray && VertexAttribPointer &&
                VertexAttribIPointer && VertexAttribDivisor && DrawArrays && DrawElements &&
                DrawElementsInstanced && GenQueries && DeleteQueries && BeginQuery && EndQuery &&
                GetQueryObjectuiv && Finish;
#if defined(MG_ANDROID) || defined(MG_IOS)
            return common;
#else
            return common && GetTexImage && PolygonMode && DrawElementsBaseVertex &&
                DrawElementsInstancedBaseVertex;
#endif
        }
    } gl;

    struct TextureFormat
    {
        GLenum internalFormat;
        GLenum format;
        GLenum type;
        bool compressed;
    };

    struct ShaderBinding
    {
        std::string name;
        int slot;
        int samplerSlot;
    };

    struct ProgramKey
    {
        const void* vertex;
        const void* pixel;
        bool operator==(const ProgramKey& other) const { return vertex == other.vertex && pixel == other.pixel; }
    };

    struct ProgramKeyHash
    {
        size_t operator()(const ProgramKey& key) const
        {
            return std::hash<const void*>()(key.vertex) ^ (std::hash<const void*>()(key.pixel) << 1);
        }
    };
}

struct MGG_GraphicsAdapter
{
    std::string name = "OpenGL 4.1 Core";
    std::vector<MGG_DisplayMode> modes;
};

struct MGG_GraphicsSystem
{
    MGG_GraphicsAdapter adapter;
};

struct MGG_Buffer
{
    GLuint handle = 0;
    GLenum target = 0;
    mgint size = 0;
    mgbool dynamic = false;
    std::vector<mgbyte> shadow;
};

struct MGG_Texture
{
    GLuint handle = 0;
    GLenum target = 0;
    TextureFormat glFormat{};
    MGTextureType type = MGTextureType::_2D;
    MGSurfaceFormat format = MGSurfaceFormat::Color;
    mgint width = 0;
    mgint height = 0;
    mgint depth = 0;
    mgint mipmaps = 1;
    mgint slices = 1;
    bool renderTarget = false;
    mgint samples = 1;
    GLuint msaaColor = 0;
    GLuint depthBuffer = 0;
    MGDepthFormat depthFormat = MGDepthFormat::None;
    bool independentDepthStencil = false;
    bool owned = true;
    std::vector<bool> compressedDefined;
};

struct MGG_Shader
{
    GLuint handle = 0;
    MGShaderStage stage = MGShaderStage::Vertex;
    std::vector<ShaderBinding> uniformBlocks;
    std::vector<ShaderBinding> samplers;
};

struct MGG_InputLayout
{
    std::vector<mgint> strides;
    std::vector<MGG_InputElement> elements;
};

struct MGG_BlendState { MGG_BlendState_Info info; };
struct MGG_DepthStencilState { MGG_DepthStencilState_Info info; };
struct MGG_RasterizerState { MGG_RasterizerState_Info info; };
struct MGG_SamplerState { GLuint handle = 0; };
struct MGG_OcclusionQuery { GLuint handle = 0; bool active = false; };

struct MGGLProgram
{
    GLuint handle = 0;
    GLint positionFixup = -1;
};

struct MGG_GraphicsDevice
{
    MGGL_Host host;
    bool suspended = true;
    mgint width = 0;
    mgint height = 0;
    GLuint vao = 0;
    GLuint framebuffer = 0;
    GLuint resolveReadFramebuffer = 0;
    GLuint resolveDrawFramebuffer = 0;
    GLuint backBufferDepth = 0;
    MGG_Texture* renderTargets[MGGLMaxColorTargets]{};
    mgint renderTargetSlices[MGGLMaxColorTargets]{};
    mgint renderTargetCount = 0;
    bool explicitRenderPass = false;
    MGG_Texture* explicitDepthStencilTarget = nullptr;
    MGRenderPassStoreAction explicitColorStoreActions[MGGLMaxColorTargets]{};
    MGRenderPassStoreAction explicitDepthStoreAction = MGRenderPassStoreAction::Store;
    MGRenderPassStoreAction explicitStencilStoreAction = MGRenderPassStoreAction::Store;
    MGG_Buffer* constantBuffers[2][MGGLMaxTextureSlots]{};
    MGG_Texture* textures[2][MGGLMaxTextureSlots]{};
    MGG_SamplerState* samplers[2][MGGLMaxTextureSlots]{};
    MGG_Buffer* vertexBuffers[MGGLMaxVertexBuffers]{};
    mgint vertexOffsets[MGGLMaxVertexBuffers]{};
    MGG_Buffer* indexBuffer = nullptr;
    GLenum indexType = GL_UNSIGNED_SHORT;
    MGG_Shader* shaders[2]{};
    MGG_InputLayout* inputLayout = nullptr;
    MGGLProgram* currentProgram = nullptr;
    std::unordered_map<ProgramKey, MGGLProgram, ProgramKeyHash> programs;
    MGTextureCompressionCapabilities textureCompression = MGTextureCompressionCapabilities::None;
    bool supportsAnisotropy = false;
    GLfloat maxAnisotropy = 1.0f;
    GLint apiMajor = MGGLRequiredMajor;
    GLint apiMinor = MGGLRequiredMinor;
    GLint maxDrawBuffers = MGGLMaxColorTargets;
    GLint maxColorAttachments = MGGLMaxColorTargets;
    GLint maxVertexAttributes = 16;
    MGCullMode currentCullMode = MGCullMode::None;
    GLboolean colorWriteMask[4]{GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE};
    GLboolean depthWriteMask = GL_TRUE;
    GLuint stencilWriteMask = 0xFFFFFFFFu;
    bool scissorTestEnabled = false;
};

namespace
{
    static bool HasExtension(const char* extension)
    {
        if (!extension)
            return false;
        if (gl.GetStringi)
        {
            GLint count = 0;
            gl.GetIntegerv(GL_NUM_EXTENSIONS, &count);
            for (GLint index = 0; index < count; ++index)
            {
                const auto* value = reinterpret_cast<const char*>(gl.GetStringi(GL_EXTENSIONS, static_cast<GLuint>(index)));
                if (value && std::strcmp(value, extension) == 0)
                    return true;
            }
            return false;
        }
        const auto* extensions = reinterpret_cast<const char*>(gl.GetString(GL_EXTENSIONS));
        if (!extensions)
            return false;
        const std::string list = std::string(" ") + extensions + " ";
        return list.find(std::string(" ") + extension + " ") != std::string::npos;
    }

    static MGTextureCompressionCapabilities AddCompression(
        MGTextureCompressionCapabilities current,
        MGTextureCompressionCapabilities value)
    {
        return static_cast<MGTextureCompressionCapabilities>(
            static_cast<mgint>(current) | static_cast<mgint>(value));
    }

    static GLenum ToPrimitive(MGPrimitiveType value)
    {
        switch (value)
        {
            case MGPrimitiveType::TriangleList: return GL_TRIANGLES;
            case MGPrimitiveType::TriangleStrip: return GL_TRIANGLE_STRIP;
            case MGPrimitiveType::LineList: return GL_LINES;
            case MGPrimitiveType::LineStrip: return GL_LINE_STRIP;
            case MGPrimitiveType::PointList: return GL_POINTS;
        }
        return GL_TRIANGLES;
    }

    static GLsizei PrimitiveVertexCount(MGPrimitiveType value, mgint primitives)
    {
        switch (value)
        {
            case MGPrimitiveType::TriangleList: return primitives * 3;
            case MGPrimitiveType::TriangleStrip: return primitives + 2;
            case MGPrimitiveType::LineList: return primitives * 2;
            case MGPrimitiveType::LineStrip: return primitives + 1;
            case MGPrimitiveType::PointList: return primitives;
        }
        return 0;
    }

    static GLenum ToCompare(MGCompareFunction value)
    {
        static const GLenum values[] = { GL_ALWAYS, GL_NEVER, GL_LESS, GL_LEQUAL, GL_EQUAL, GL_GEQUAL, GL_GREATER, GL_NOTEQUAL };
        return values[static_cast<int>(value)];
    }

    static GLenum ToBlend(MGBlend value)
    {
        static const GLenum values[] =
        {
            GL_ONE, GL_ZERO, GL_SRC_COLOR, GL_ONE_MINUS_SRC_COLOR, GL_SRC_ALPHA,
            GL_ONE_MINUS_SRC_ALPHA, GL_DST_COLOR, GL_ONE_MINUS_DST_COLOR, GL_DST_ALPHA,
            GL_ONE_MINUS_DST_ALPHA, GL_CONSTANT_COLOR, GL_ONE_MINUS_CONSTANT_COLOR, GL_SRC_ALPHA_SATURATE
        };
        return values[static_cast<int>(value)];
    }

    static GLenum ToBlendFunction(MGBlendFunction value)
    {
        static const GLenum values[] = { GL_FUNC_ADD, GL_FUNC_SUBTRACT, GL_FUNC_REVERSE_SUBTRACT, GL_MIN, GL_MAX };
        return values[static_cast<int>(value)];
    }

    static GLenum ToStencil(MGStencilOperation value)
    {
        static const GLenum values[] =
        {
            GL_KEEP, GL_ZERO, GL_REPLACE, GL_INCR_WRAP, GL_DECR_WRAP, GL_INCR, GL_DECR, GL_INVERT
        };
        return values[static_cast<int>(value)];
    }

    static GLenum ToAddress(MGTextureAddressMode value)
    {
        switch (value)
        {
            case MGTextureAddressMode::Wrap: return GL_REPEAT;
            case MGTextureAddressMode::Clamp: return GL_CLAMP_TO_EDGE;
            case MGTextureAddressMode::Mirror: return GL_MIRRORED_REPEAT;
            case MGTextureAddressMode::Border: return GL_CLAMP_TO_BORDER;
        }
        return GL_REPEAT;
    }

    static TextureFormat ToTextureFormat(MGSurfaceFormat format)
    {
        switch (format)
        {
            case MGSurfaceFormat::Color: return { GL_RGBA8, GL_RGBA, GL_UNSIGNED_BYTE, false };
            case MGSurfaceFormat::ColorSRgb: return { GL_SRGB8_ALPHA8, GL_RGBA, GL_UNSIGNED_BYTE, false };
            case MGSurfaceFormat::Bgr565: return { GL_RGB565, GL_RGB, GL_UNSIGNED_SHORT_5_6_5, false };
            case MGSurfaceFormat::Bgra5551: return { GL_RGB5_A1, GL_BGRA, GL_UNSIGNED_SHORT_1_5_5_5_REV, false };
            case MGSurfaceFormat::Bgra4444: return { GL_RGBA4, GL_BGRA, GL_UNSIGNED_SHORT_4_4_4_4_REV, false };
            case MGSurfaceFormat::NormalizedByte2: return { GL_RG8_SNORM, GL_RG, GL_BYTE, false };
            case MGSurfaceFormat::NormalizedByte4: return { GL_RGBA8_SNORM, GL_RGBA, GL_BYTE, false };
            case MGSurfaceFormat::Rgba1010102: return { GL_RGB10_A2, GL_RGBA, GL_UNSIGNED_INT_2_10_10_10_REV, false };
#if defined(MG_ANDROID) || defined(MG_IOS)
            case MGSurfaceFormat::Rg32:
            case MGSurfaceFormat::Rgba64: return { 0, 0, 0, false };
#else
            case MGSurfaceFormat::Rg32: return { GL_RG16, GL_RG, GL_UNSIGNED_SHORT, false };
            case MGSurfaceFormat::Rgba64: return { GL_RGBA16, GL_RGBA, GL_UNSIGNED_SHORT, false };
#endif
            case MGSurfaceFormat::Alpha8: return { GL_R8, GL_RED, GL_UNSIGNED_BYTE, false };
            case MGSurfaceFormat::Single: return { GL_R32F, GL_RED, GL_FLOAT, false };
            case MGSurfaceFormat::Vector2: return { GL_RG32F, GL_RG, GL_FLOAT, false };
            case MGSurfaceFormat::Vector4: return { GL_RGBA32F, GL_RGBA, GL_FLOAT, false };
            case MGSurfaceFormat::HalfSingle: return { GL_R16F, GL_RED, GL_HALF_FLOAT, false };
            case MGSurfaceFormat::HalfVector2: return { GL_RG16F, GL_RG, GL_HALF_FLOAT, false };
            case MGSurfaceFormat::HalfVector4:
            case MGSurfaceFormat::HdrBlendable: return { GL_RGBA16F, GL_RGBA, GL_HALF_FLOAT, false };
            case MGSurfaceFormat::Bgr32: return { GL_RGB8, GL_BGRA, GL_UNSIGNED_INT_8_8_8_8_REV, false };
            case MGSurfaceFormat::Bgra32: return { GL_RGBA8, GL_BGRA, GL_UNSIGNED_INT_8_8_8_8_REV, false };
            case MGSurfaceFormat::Bgr32SRgb: return { GL_SRGB8, GL_BGRA, GL_UNSIGNED_INT_8_8_8_8_REV, false };
            case MGSurfaceFormat::Bgra32SRgb: return { GL_SRGB8_ALPHA8, GL_BGRA, GL_UNSIGNED_INT_8_8_8_8_REV, false };
            case MGSurfaceFormat::Dxt1:
            case MGSurfaceFormat::Dxt1a: return { GL_COMPRESSED_RGBA_S3TC_DXT1_EXT, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Dxt3: return { GL_COMPRESSED_RGBA_S3TC_DXT3_EXT, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Dxt5: return { GL_COMPRESSED_RGBA_S3TC_DXT5_EXT, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Dxt1SRgb: return { 0x8C4D, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Dxt3SRgb: return { 0x8C4E, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Dxt5SRgb: return { 0x8C4F, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::RgbPvrtc2Bpp: return { GL_COMPRESSED_RGB_PVRTC_2BPPV1_IMG, GL_RGB, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::RgbPvrtc4Bpp: return { GL_COMPRESSED_RGB_PVRTC_4BPPV1_IMG, GL_RGB, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::RgbaPvrtc2Bpp: return { GL_COMPRESSED_RGBA_PVRTC_2BPPV1_IMG, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::RgbaPvrtc4Bpp: return { GL_COMPRESSED_RGBA_PVRTC_4BPPV1_IMG, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::RgbEtc1: return { GL_ETC1_RGB8_OES, GL_RGB, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Rgb8Etc2: return { GL_COMPRESSED_RGB8_ETC2, GL_RGB, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Srgb8Etc2: return { GL_COMPRESSED_SRGB8_ETC2, GL_RGB, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Rgb8A1Etc2: return { GL_COMPRESSED_RGB8_PUNCHTHROUGH_ALPHA1_ETC2, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Srgb8A1Etc2: return { GL_COMPRESSED_SRGB8_PUNCHTHROUGH_ALPHA1_ETC2, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Rgba8Etc2: return { GL_COMPRESSED_RGBA8_ETC2_EAC, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::SRgb8A8Etc2: return { GL_COMPRESSED_SRGB8_ALPHA8_ETC2_EAC, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Astc4X4Rgba: return { GL_COMPRESSED_RGBA_ASTC_4x4_KHR, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Astc5X5Rgba: return { GL_COMPRESSED_RGBA_ASTC_5x5_KHR, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Astc6X6Rgba: return { GL_COMPRESSED_RGBA_ASTC_6x6_KHR, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Astc8X8Rgba: return { GL_COMPRESSED_RGBA_ASTC_8x8_KHR, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Astc10X10Rgba: return { GL_COMPRESSED_RGBA_ASTC_10x10_KHR, GL_RGBA, GL_UNSIGNED_BYTE, true };
            case MGSurfaceFormat::Astc12X12Rgba: return { GL_COMPRESSED_RGBA_ASTC_12x12_KHR, GL_RGBA, GL_UNSIGNED_BYTE, true };
            default: return { 0, 0, 0, false };
        }
    }

    static GLenum ToTextureTarget(MGTextureType type)
    {
        switch (type)
        {
            case MGTextureType::_2D: return GL_TEXTURE_2D;
            case MGTextureType::_3D: return GL_TEXTURE_3D;
            case MGTextureType::Cube: return GL_TEXTURE_CUBE_MAP;
        }
        return GL_TEXTURE_2D;
    }

    static GLenum TextureSliceTarget(const MGG_Texture* texture, mgint slice)
    {
        return texture->type == MGTextureType::Cube ? GL_TEXTURE_CUBE_MAP_POSITIVE_X + slice : texture->target;
    }

    static GLenum ToDepthInternal(MGDepthFormat format)
    {
        switch (format)
        {
            case MGDepthFormat::Depth16: return GL_DEPTH_COMPONENT16;
            case MGDepthFormat::Depth24: return GL_DEPTH_COMPONENT24;
            case MGDepthFormat::Depth24Stencil8: return GL_DEPTH24_STENCIL8;
            case MGDepthFormat::Depth32Float: return GL_DEPTH_COMPONENT32F;
            default: return 0;
        }
    }

    static bool SupportsSampleableDepthFormat(MGDepthFormat format)
    {
        return format == MGDepthFormat::Depth16 ||
            format == MGDepthFormat::Depth24 ||
            format == MGDepthFormat::Depth24Stencil8 ||
            format == MGDepthFormat::Depth32Float;
    }

    static bool GetDepthTextureFormat(
        MGDepthFormat depthFormat,
        GLenum& format,
        GLenum& type)
    {
        format = GL_DEPTH_COMPONENT;
        type = GL_UNSIGNED_INT;
        switch (depthFormat)
        {
            case MGDepthFormat::Depth16:
                type = GL_UNSIGNED_SHORT;
                return true;
            case MGDepthFormat::Depth24:
                return true;
            case MGDepthFormat::Depth24Stencil8:
                format = GL_DEPTH_STENCIL;
                type = GL_UNSIGNED_INT_24_8;
                return true;
            case MGDepthFormat::Depth32Float:
                type = GL_FLOAT;
                return true;
            default:
                return false;
        }
    }

    static bool ValidRenderPassActions(
        MGRenderPassLoadAction loadAction,
        MGRenderPassStoreAction storeAction)
    {
        return loadAction >= MGRenderPassLoadAction::Load &&
            loadAction <= MGRenderPassLoadAction::DontCare &&
            storeAction >= MGRenderPassStoreAction::Store &&
            storeAction <= MGRenderPassStoreAction::DontCare;
    }

    struct Reader
    {
        const uint8_t* data;
        size_t size;
        size_t offset = 0;

        uint32_t U32()
        {
            if (offset + 4 > size)
                return 0;
            uint32_t value;
            std::memcpy(&value, data + offset, 4);
            offset += 4;
            return value;
        }

        std::string String()
        {
            const uint32_t length = U32();
            if (offset + length > size)
            {
                offset = size;
                return {};
            }
            std::string value(reinterpret_cast<const char*>(data + offset), length);
            offset += length;
            return value;
        }

        bool Valid() const { return offset <= size; }
    };

    static void PrintShaderError(GLuint shader)
    {
        GLint length = 0;
        gl.GetShaderiv(shader, GL_INFO_LOG_LENGTH, &length);
        std::vector<GLchar> log(std::max(1, length));
        gl.GetShaderInfoLog(shader, static_cast<GLsizei>(log.size()), nullptr, log.data());
        std::fprintf(stderr, "Native OpenGL shader compilation failed: %s\n", log.data());
    }

    static void PrintProgramError(GLuint program)
    {
        GLint length = 0;
        gl.GetProgramiv(program, GL_INFO_LOG_LENGTH, &length);
        std::vector<GLchar> log(std::max(1, length));
        gl.GetProgramInfoLog(program, static_cast<GLsizei>(log.size()), nullptr, log.data());
        std::fprintf(stderr, "Native OpenGL program link failed: %s\n", log.data());
    }

    static bool CheckVersion41()
    {
        GLint major = 0;
        GLint minor = 0;
        gl.GetIntegerv(GL_MAJOR_VERSION, &major);
        gl.GetIntegerv(GL_MINOR_VERSION, &minor);
        if (major > MGGLRequiredMajor || (major == MGGLRequiredMajor && minor >= MGGLRequiredMinor))
            return true;
        std::fprintf(stderr, "Native OpenGL requires API %d.%d; the active context reports %d.%d.\n", MGGLRequiredMajor, MGGLRequiredMinor, major, minor);
        return false;
    }

    static bool MakeCurrent(MGG_GraphicsDevice* device)
    {
        return device && MGGL_Host_MakeCurrent(device->host);
    }

    static bool ProbeSampleableDepthFormat(
        MGG_GraphicsDevice* device,
        MGDepthFormat depthFormat)
    {
        GLenum format = 0;
        GLenum type = 0;
        if (!MakeCurrent(device) ||
            !SupportsSampleableDepthFormat(depthFormat) ||
            !GetDepthTextureFormat(depthFormat, format, type))
            return false;

        GLint previousTexture = 0;
        GLint previousDrawFramebuffer = 0;
        GLint previousReadFramebuffer = 0;
        gl.GetIntegerv(GL_TEXTURE_BINDING_2D, &previousTexture);
        gl.GetIntegerv(GL_DRAW_FRAMEBUFFER_BINDING, &previousDrawFramebuffer);
        gl.GetIntegerv(GL_READ_FRAMEBUFFER_BINDING, &previousReadFramebuffer);

        GLuint texture = 0;
        GLuint framebuffer = 0;
        gl.GenTextures(1, &texture);
        gl.GenFramebuffers(1, &framebuffer);
        if (texture == 0 || framebuffer == 0)
        {
            if (framebuffer != 0)
                gl.DeleteFramebuffers(1, &framebuffer);
            if (texture != 0)
                gl.DeleteTextures(1, &texture);
            return false;
        }
        gl.BindTexture(GL_TEXTURE_2D, texture);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
        gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
        gl.TexImage2D(
            GL_TEXTURE_2D,
            0,
            ToDepthInternal(depthFormat),
            1,
            1,
            0,
            format,
            type,
            nullptr);

        gl.BindFramebuffer(GL_FRAMEBUFFER, framebuffer);
        const GLenum attachment = depthFormat == MGDepthFormat::Depth24Stencil8
            ? GL_DEPTH_STENCIL_ATTACHMENT
            : GL_DEPTH_ATTACHMENT;
        gl.FramebufferTexture2D(GL_FRAMEBUFFER, attachment, GL_TEXTURE_2D, texture, 0);
        const bool supported = gl.CheckFramebufferStatus(GL_FRAMEBUFFER) == GL_FRAMEBUFFER_COMPLETE;

        gl.BindFramebuffer(GL_DRAW_FRAMEBUFFER, static_cast<GLuint>(previousDrawFramebuffer));
        gl.BindFramebuffer(GL_READ_FRAMEBUFFER, static_cast<GLuint>(previousReadFramebuffer));
        gl.BindTexture(GL_TEXTURE_2D, static_cast<GLuint>(previousTexture));
        gl.DeleteFramebuffers(1, &framebuffer);
        gl.DeleteTextures(1, &texture);
        return supported;
    }

    static MGGLProgram* GetProgram(MGG_GraphicsDevice* device)
    {
        auto* vs = device->shaders[static_cast<int>(MGShaderStage::Vertex)];
        auto* ps = device->shaders[static_cast<int>(MGShaderStage::Pixel)];
        if (!vs || !ps)
            return nullptr;

        const ProgramKey key{vs, ps};
        auto found = device->programs.find(key);
        if (found != device->programs.end())
            return &found->second;

        MGGLProgram value;
        value.handle = gl.CreateProgram();
        gl.AttachShader(value.handle, vs->handle);
        gl.AttachShader(value.handle, ps->handle);
        gl.LinkProgram(value.handle);
        GLint linked = GL_FALSE;
        gl.GetProgramiv(value.handle, GL_LINK_STATUS, &linked);
        if (!linked)
        {
            PrintProgramError(value.handle);
            gl.DeleteProgram(value.handle);
            return nullptr;
        }

        const auto bindUniformBlocks = [&value](
            const std::vector<ShaderBinding>& bindings,
            const char* stage,
            int firstBindingPoint)
        {
            for (const auto& binding : bindings)
            {
                if (binding.slot < 0 || binding.slot >= MGGLMaxTextureSlots)
                {
                    std::fprintf(
                        stderr,
                        "Native OpenGL rejected %s uniform block '%s' with invalid slot %d.\n",
                        stage,
                        binding.name.c_str(),
                        binding.slot);
                    return false;
                }

                const GLuint index = gl.GetUniformBlockIndex(value.handle, binding.name.c_str());
                if (index == GL_INVALID_INDEX)
                {
                    std::fprintf(
                        stderr,
                        "Native OpenGL program is missing reflected %s uniform block '%s' (slot=%d). "
                        "Rebuild the effect with the matching Native OpenGL MGFX toolchain.\n",
                        stage,
                        binding.name.c_str(),
                        binding.slot);
                    return false;
                }
                gl.UniformBlockBinding(value.handle, index, firstBindingPoint + binding.slot);
            }
            return true;
        };

        if (!bindUniformBlocks(vs->uniformBlocks, "vertex", 0) ||
            !bindUniformBlocks(ps->uniformBlocks, "pixel", MGGLMaxTextureSlots))
        {
            gl.DeleteProgram(value.handle);
            return nullptr;
        }

        gl.UseProgram(value.handle);
        value.positionFixup = gl.GetUniformLocation(value.handle, "mg_posFixup");
        for (const auto* shader : {vs, ps})
        {
            const int stage = static_cast<int>(shader->stage);
            for (const auto& binding : shader->samplers)
            {
                const GLint location = gl.GetUniformLocation(value.handle, binding.name.c_str());
                if (location >= 0)
                    gl.Uniform1i(location, stage * MGGLMaxTextureSlots + binding.slot);
            }
        }

        auto inserted = device->programs.emplace(key, value);
        return &inserted.first->second;
    }

    static void BindResources(MGG_GraphicsDevice* device)
    {
        for (int stage = 0; stage < 2; ++stage)
        {
            for (int slot = 0; slot < MGGLMaxTextureSlots; ++slot)
            {
                auto* constantBuffer = device->constantBuffers[stage][slot];
                gl.BindBufferBase(
                    GL_UNIFORM_BUFFER,
                    stage * MGGLMaxTextureSlots + slot,
                    constantBuffer ? constantBuffer->handle : 0);

                const int unit = stage * MGGLMaxTextureSlots + slot;
                gl.ActiveTexture(GL_TEXTURE0 + unit);
                auto* texture = device->textures[stage][slot];
                if (texture)
                    gl.BindTexture(texture->target, texture->handle);
                else
                {
                    // Texture bindings are target-specific.  Leaving a null
                    // MonoGame slot untouched makes a later shader sample a
                    // resource left behind by an earlier pass.
                    gl.BindTexture(GL_TEXTURE_2D, 0);
                    gl.BindTexture(GL_TEXTURE_CUBE_MAP, 0);
                    gl.BindTexture(GL_TEXTURE_3D, 0);
                    gl.BindTexture(GL_TEXTURE_2D_ARRAY, 0);
                }
                gl.BindSampler(unit, 0);
            }

            auto* shader = device->shaders[stage];
            if (!shader)
                continue;
            for (const auto& binding : shader->samplers)
            {
                if (binding.slot < 0 || binding.slot >= MGGLMaxTextureSlots)
                    continue;
                auto* sampler = binding.samplerSlot >= 0 && binding.samplerSlot < MGGLMaxTextureSlots
                    ? device->samplers[stage][binding.samplerSlot]
                    : nullptr;
                gl.BindSampler(
                    stage * MGGLMaxTextureSlots + binding.slot,
                    sampler ? sampler->handle : 0);
            }
        }
    }

    struct VertexFormat
    {
        GLint size;
        GLenum type;
        GLboolean normalized;
        bool integer;
    };

    static VertexFormat ToVertexFormat(MGVertexElementFormat format)
    {
        switch (format)
        {
            case MGVertexElementFormat::Single: return {1, GL_FLOAT, GL_FALSE, false};
            case MGVertexElementFormat::Vector2: return {2, GL_FLOAT, GL_FALSE, false};
            case MGVertexElementFormat::Vector3: return {3, GL_FLOAT, GL_FALSE, false};
            case MGVertexElementFormat::Vector4: return {4, GL_FLOAT, GL_FALSE, false};
            case MGVertexElementFormat::Color: return {4, GL_UNSIGNED_BYTE, GL_TRUE, false};
            case MGVertexElementFormat::Byte4: return {4, GL_UNSIGNED_BYTE, GL_FALSE, true};
            case MGVertexElementFormat::Short2: return {2, GL_SHORT, GL_FALSE, true};
            case MGVertexElementFormat::Short4: return {4, GL_SHORT, GL_FALSE, true};
            case MGVertexElementFormat::NormalizedShort2: return {2, GL_SHORT, GL_TRUE, false};
            case MGVertexElementFormat::NormalizedShort4: return {4, GL_SHORT, GL_TRUE, false};
            case MGVertexElementFormat::HalfVector2: return {2, GL_HALF_FLOAT, GL_FALSE, false};
            case MGVertexElementFormat::HalfVector4: return {4, GL_HALF_FLOAT, GL_FALSE, false};
        }
        return {4, GL_FLOAT, GL_FALSE, false};
    }

    static bool PrepareDraw(MGG_GraphicsDevice* device)
    {
        if (!MakeCurrent(device) || !device->inputLayout)
            return false;
        auto* program = GetProgram(device);
        if (!program)
            return false;
        if (device->currentProgram != program)
        {
            gl.UseProgram(program->handle);
            device->currentProgram = program;
        }

        if (program->positionFixup >= 0)
            gl.Uniform4f(
                program->positionFixup,
                1.0f,
                device->renderTargetCount > 0 ? -1.0f : 1.0f,
                0.0f,
                0.0f);

        BindResources(device);
        gl.BindVertexArray(device->vao);
        for (GLuint location = 0; location < static_cast<GLuint>(device->maxVertexAttributes); ++location)
            gl.DisableVertexAttribArray(location);
        for (const auto& element : device->inputLayout->elements)
        {
            const int slot = static_cast<int>(element.VertexBufferSlot);
            auto* buffer = slot < MGGLMaxVertexBuffers ? device->vertexBuffers[slot] : nullptr;
            if (!buffer)
                continue;
            const auto format = ToVertexFormat(element.Format);
            gl.BindBuffer(GL_ARRAY_BUFFER, buffer->handle);
            const intptr_t offset = static_cast<intptr_t>(device->vertexOffsets[slot]) * device->inputLayout->strides[slot] + element.AlignedByteOffset;
            gl.EnableVertexAttribArray(element.ShaderLocation);
            if (format.integer)
                gl.VertexAttribIPointer(element.ShaderLocation, format.size, format.type, device->inputLayout->strides[slot], reinterpret_cast<const void*>(offset));
            else
                gl.VertexAttribPointer(element.ShaderLocation, format.size, format.type, format.normalized, device->inputLayout->strides[slot], reinterpret_cast<const void*>(offset));
            gl.VertexAttribDivisor(element.ShaderLocation, element.InstanceDataStepRate);
        }
        if (device->indexBuffer)
            gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, device->indexBuffer->handle);
        return true;
    }

    static void AttachTarget(GLenum attachment, MGG_Texture* texture, int slice, bool multisampled, GLenum framebufferTarget = GL_FRAMEBUFFER)
    {
        if (multisampled && texture->msaaColor)
        {
            gl.FramebufferRenderbuffer(framebufferTarget, attachment, GL_RENDERBUFFER, texture->msaaColor);
            return;
        }
        if (texture->type == MGTextureType::_3D || texture->target == GL_TEXTURE_2D_ARRAY)
            gl.FramebufferTextureLayer(framebufferTarget, attachment, texture->handle, 0, slice);
        else
            gl.FramebufferTexture2D(framebufferTarget, attachment, TextureSliceTarget(texture, slice), texture->handle, 0);
    }
}

void MGG_OpenXR_ConfigureVulkanBootstrap(void*, void*, void*, void*) {}
void MGG_OpenXR_ConfigureDirect3D12Adapter(mglong, mguint) {}
void MGG_OpenXR_ConfigureMetalDevice(void*) {}
void* MGG_OpenXR_GetVulkanGetInstanceProcAddr() { return nullptr; }

void MGG_OpenXR_GetGraphicsBinding(MGG_GraphicsDevice* device, MGG_OpenXrGraphicsBinding& binding)
{
    std::memset(&binding, 0, sizeof(binding));
    if (!device || !MGGL_Host_GetOpenXrGraphicsBinding(device->host, binding))
    {
        binding.Api = -1;
        std::fprintf(stderr, "Native OpenGL OpenXR is not supported on this platform.\n");
    }
}

MGG_Texture* MGG_OpenXR_WrapRenderTarget(MGG_GraphicsDevice* device, void* image, MGSurfaceFormat format, mgint width, mgint height, MGDepthFormat depthFormat)
{
#if defined(MG_ANDROID)
    if (!MakeCurrent(device) || !image || width <= 0 || height <= 0)
        return nullptr;
    const auto glFormat = ToTextureFormat(format);
    if (!glFormat.internalFormat || glFormat.compressed)
        return nullptr;
    auto* texture = new MGG_Texture();
    texture->handle = static_cast<GLuint>(reinterpret_cast<uintptr_t>(image));
    texture->target = GL_TEXTURE_2D;
    texture->glFormat = glFormat;
    texture->type = MGTextureType::_2D;
    texture->format = format;
    texture->width = width;
    texture->height = height;
    texture->depth = 1;
    texture->renderTarget = true;
    texture->depthFormat = depthFormat;
    texture->owned = false;
    if (depthFormat != MGDepthFormat::None)
    {
        gl.GenRenderbuffers(1, &texture->depthBuffer);
        gl.BindRenderbuffer(GL_RENDERBUFFER, texture->depthBuffer);
        gl.RenderbufferStorage(GL_RENDERBUFFER, ToDepthInternal(depthFormat), width, height);
    }
    return texture;
#else
    (void)device; (void)image; (void)format; (void)width; (void)height; (void)depthFormat;
    std::fprintf(stderr, "Native OpenGL OpenXR render-target wrapping is not supported on this platform.\n");
    return nullptr;
#endif
}

void MGG_OpenXR_PrepareForRuntimeRelease(MGG_GraphicsDevice* device, MGG_Texture*)
{
    if (MakeCurrent(device))
        gl.Finish();
}

void MGG_EffectResource_GetBytecode(const char* name, mgbyte*& bytecode, mgint& size)
{
    bytecode = nullptr;
    size = 0;
#if defined(MGGL_BUILTIN_EFFECTS)
#define MGGL_CONCAT_INNER(left, right) left##right
#define MGGL_CONCAT(left, right) MGGL_CONCAT_INNER(left, right)
#define MGGL_EFFECT(effect) \
    if (std::strcmp(name, #effect) == 0) \
    { \
        bytecode = const_cast<mgbyte*>(reinterpret_cast<const mgbyte*>(MGGL_CONCAT(effect, MGGL_BUILTIN_SUFFIX))); \
        size = static_cast<mgint>(sizeof(MGGL_CONCAT(effect, MGGL_BUILTIN_SUFFIX))); \
        return; \
    }
    MGGL_EFFECT(AlphaTestEffect)
    MGGL_EFFECT(BasicEffect)
    MGGL_EFFECT(DualTextureEffect)
    MGGL_EFFECT(EnvironmentMapEffect)
    MGGL_EFFECT(SkinnedEffect)
    MGGL_EFFECT(SpriteEffect)
#undef MGGL_EFFECT
#undef MGGL_CONCAT
#undef MGGL_CONCAT_INNER
#else
    (void)name;
#endif
}

MGG_GraphicsSystem* MGG_GraphicsSystem_Create()
{
    auto* system = new MGG_GraphicsSystem();
#if defined(MG_ANDROID) || defined(MG_IOS)
    system->adapter.modes.push_back({MGSurfaceFormat::Color, 1, 1});
#else
    const int displays = SDL_GetNumVideoDisplays();
    if (displays > 0)
    {
        SDL_DisplayMode current{};
        if (SDL_GetCurrentDisplayMode(0, &current) == 0)
            system->adapter.modes.push_back({MGSurfaceFormat::Color, current.w, current.h});
        const int modes = SDL_GetNumDisplayModes(0);
        for (int index = 0; index < modes; ++index)
        {
            SDL_DisplayMode mode{};
            if (SDL_GetDisplayMode(0, index, &mode) == 0)
                system->adapter.modes.push_back({MGSurfaceFormat::Color, mode.w, mode.h});
        }
    }
    if (system->adapter.modes.empty())
        system->adapter.modes.push_back({MGSurfaceFormat::Color, 800, 480});
#endif
    return system;
}

void MGG_GraphicsSystem_Destroy(MGG_GraphicsSystem* system) { delete system; }

MGG_GraphicsAdapter* MGG_GraphicsAdapter_Get(MGG_GraphicsSystem* system, mgint index)
{
    return system && index == 0 ? &system->adapter : nullptr;
}

void MGG_GraphicsAdapter_GetInfo(MGG_GraphicsAdapter* adapter, MGG_GraphicsAdaptor_Info& info)
{
    std::memset(&info, 0, sizeof(info));
    if (!adapter)
        return;
    info.DeviceName = const_cast<char*>(adapter->name.c_str());
    info.Description = const_cast<char*>(adapter->name.c_str());
    info.DisplayModes = adapter->modes.data();
    info.DisplayModeCount = static_cast<mgint>(adapter->modes.size());
    info.CurrentDisplayMode = adapter->modes.front();
}

static MGG_GraphicsDevice* MGGLCreateDevice(MGG_PresentationSurface* surface)
{
    if (!surface || !surface->Handle)
    {
        std::fprintf(stderr, "Native OpenGL requires a valid platform presentation surface.\n");
        return nullptr;
    }

    MGGL_Host host;
    if (!MGGL_Host_Create(host, *surface, MGGLRequiredMajor, MGGLRequiredMinor))
    {
        std::fprintf(stderr, "Unable to create the strict native OpenGL context.\n");
        return nullptr;
    }
    if (!gl.Load() || !CheckVersion41())
    {
        std::fprintf(stderr, "Unable to initialize the strict native OpenGL function table.\n");
        MGGL_Host_Destroy(host);
        return nullptr;
    }

    GLint apiMajor = 0;
    GLint apiMinor = 0;
    GLint maxDrawBuffers = 0;
    GLint maxColorAttachments = 0;
    GLint maxVertexAttributes = 0;
    gl.GetIntegerv(GL_MAJOR_VERSION, &apiMajor);
    gl.GetIntegerv(GL_MINOR_VERSION, &apiMinor);
    gl.GetIntegerv(GL_MAX_DRAW_BUFFERS, &maxDrawBuffers);
    gl.GetIntegerv(GL_MAX_COLOR_ATTACHMENTS, &maxColorAttachments);
    gl.GetIntegerv(GL_MAX_VERTEX_ATTRIBS, &maxVertexAttributes);
    if (maxDrawBuffers < MGGLMaxColorTargets || maxColorAttachments < MGGLMaxColorTargets)
    {
        std::fprintf(
            stderr,
            "Native OpenGL requires at least %d draw buffers and color attachments (reported %d/%d).\n",
            MGGLMaxColorTargets,
            maxDrawBuffers,
            maxColorAttachments);
        MGGL_Host_Destroy(host);
        return nullptr;
    }

    auto* device = new MGG_GraphicsDevice();
    device->host = host;
    device->suspended = false;
    device->apiMajor = apiMajor;
    device->apiMinor = apiMinor;
    device->maxDrawBuffers = maxDrawBuffers;
    device->maxColorAttachments = maxColorAttachments;
    device->maxVertexAttributes = maxVertexAttributes;
    gl.GenVertexArrays(1, &device->vao);
    gl.BindVertexArray(device->vao);
    gl.GenFramebuffers(1, &device->framebuffer);
    gl.GenFramebuffers(1, &device->resolveReadFramebuffer);
    gl.GenFramebuffers(1, &device->resolveDrawFramebuffer);

    if (HasExtension("GL_EXT_texture_filter_anisotropic"))
    {
        device->supportsAnisotropy = true;
        gl.GetFloatv(GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT, &device->maxAnisotropy);
        device->maxAnisotropy = std::max(device->maxAnisotropy, 1.0f);
    }
    if (HasExtension("GL_EXT_texture_compression_s3tc") ||
        HasExtension("GL_EXT_texture_compression_dxt1") ||
        HasExtension("GL_ANGLE_texture_compression_dxt5"))
        device->textureCompression = AddCompression(device->textureCompression, MGTextureCompressionCapabilities::S3tc);
#if defined(MG_ANDROID) || defined(MG_IOS)
    device->textureCompression = AddCompression(device->textureCompression, MGTextureCompressionCapabilities::Etc2);
#endif
    if (HasExtension("GL_KHR_texture_compression_astc_ldr") || HasExtension("GL_OES_texture_compression_astc"))
        device->textureCompression = AddCompression(device->textureCompression, MGTextureCompressionCapabilities::Astc);
    if (HasExtension("GL_IMG_texture_compression_pvrtc"))
        device->textureCompression = AddCompression(device->textureCompression, MGTextureCompressionCapabilities::Pvrtc);
    return device;
}

MGG_GraphicsDevice* MGG_GraphicsDevice_Create(MGG_GraphicsSystem*, MGG_GraphicsAdapter*)
{
    // Desktop creates the graphics device before the SDL window is passed to resize.
    // A temporary device object delays strict context creation until that first resize.
    return new MGG_GraphicsDevice();
}

MGG_GraphicsDevice* MGG_GraphicsDevice_CreateWithSurface(MGG_GraphicsSystem*, MGG_GraphicsAdapter*, MGG_PresentationSurface& surface)
{
    return MGGLCreateDevice(&surface);
}

void MGG_GraphicsDevice_Destroy(MGG_GraphicsDevice* device)
{
    if (!device)
        return;
    if (MakeCurrent(device))
    {
        for (auto& pair : device->programs)
            gl.DeleteProgram(pair.second.handle);
        if (device->framebuffer) gl.DeleteFramebuffers(1, &device->framebuffer);
        if (device->resolveReadFramebuffer) gl.DeleteFramebuffers(1, &device->resolveReadFramebuffer);
        if (device->resolveDrawFramebuffer) gl.DeleteFramebuffers(1, &device->resolveDrawFramebuffer);
        if (device->backBufferDepth) gl.DeleteRenderbuffers(1, &device->backBufferDepth);
        if (device->vao) gl.DeleteVertexArrays(1, &device->vao);
    }
    MGGL_Host_Destroy(device->host);
    delete device;
}

void MGG_GraphicsDevice_SuspendPresentation(MGG_GraphicsDevice* device)
{
    if (device)
    {
        MGGL_Host_Suspend(device->host);
        device->suspended = true;
    }
}

void MGG_GraphicsDevice_GetCaps(MGG_GraphicsDevice* device, MGG_GraphicsDevice_Caps& caps)
{
    std::memset(&caps, 0, sizeof(caps));
    caps.MaxTextureSlots = MGGLMaxTextureSlots;
    caps.MaxVertexTextureSlots = MGGLMaxTextureSlots;
    caps.MaxVertexBufferSlots = MGGLMaxVertexBuffers;
    caps.ShaderProfile = MGGLShaderProfile;
    caps.MaxMultiSampleCount = 1;
    caps.TextureCompression = MGTextureCompressionCapabilities::None;
    if (!MakeCurrent(device))
        return;

    GLint samples = 1;
    gl.GetIntegerv(GL_MAX_SAMPLES, &samples);
    caps.MaxMultiSampleCount = std::max(1, samples);
    caps.TextureCompression = device->textureCompression;
}

MGGraphicsDeviceStatus MGG_GraphicsDevice_GetCapsV2(
    MGG_GraphicsDevice* device,
    MGG_GraphicsDevice_CapsV2& caps,
    mguint capsSize)
{
    if (device == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;
    if (capsSize < sizeof(caps.StructSize) + sizeof(caps.AbiVersion))
        return MGGraphicsDeviceStatus::InsufficientSize;

    MGG_GraphicsDevice_Caps legacy{};
    MGG_GraphicsDevice_GetCaps(device, legacy);
    MGG_GraphicsDevice_CapsV2 value{};
    value.StructSize = sizeof(value);
    value.AbiVersion = 3;
    value.ApiMajor = device->apiMajor;
    value.ApiMinor = device->apiMinor;
    value.MaxTextureSlots = legacy.MaxTextureSlots;
    value.MaxVertexTextureSlots = legacy.MaxVertexTextureSlots;
    value.MaxVertexBufferSlots = legacy.MaxVertexBufferSlots;
    value.ShaderProfile = legacy.ShaderProfile;
    value.MaxMultiSampleCount = legacy.MaxMultiSampleCount;
    value.TextureCompression = legacy.TextureCompression;
    if (device->supportsAnisotropy)
        value.Features = MGNativeGraphicsFeatures::AnisotropicFiltering;
    if (ProbeSampleableDepthFormat(device, MGDepthFormat::Depth32Float) ||
        ProbeSampleableDepthFormat(device, MGDepthFormat::Depth24) ||
        ProbeSampleableDepthFormat(device, MGDepthFormat::Depth24Stencil8) ||
        ProbeSampleableDepthFormat(device, MGDepthFormat::Depth16))
    {
        value.Features = static_cast<MGNativeGraphicsFeatures>(
            static_cast<mguint>(value.Features) |
            static_cast<mguint>(MGNativeGraphicsFeatures::ExplicitRenderPass));
    }
    value.MaxAnisotropy = device->maxAnisotropy;
    value.MaxRenderTargets = std::min(
        MGGLMaxColorTargets,
        std::min(device->maxDrawBuffers, device->maxColorAttachments));
    value.MaxDrawBuffers = device->maxDrawBuffers;
    value.MaxColorAttachments = device->maxColorAttachments;
    std::memcpy(&caps, &value, std::min(capsSize, static_cast<mguint>(sizeof(value))));
    return MGGraphicsDeviceStatus::Success;
}

void MGG_GraphicsDevice_ResizeSwapchain(
    MGG_GraphicsDevice* device,
    MGG_PresentationSurface& surface,
    mgint width,
    mgint height,
    MGSurfaceFormat,
    MGDepthFormat depth,
    mgint,
    mgint syncInterval)
{
    if (!device)
        return;
    if (!surface.Handle)
    {
        device->suspended = true;
        return;
    }
    if (!device->host.context)
    {
        auto* initialized = MGGLCreateDevice(&surface);
        if (!initialized)
            return;
        *device = std::move(*initialized);
        initialized->host = {};
        delete initialized;
    }
    if (!MakeCurrent(device))
    {
        std::fprintf(stderr, "Native OpenGL context loss is fatal in v1.\n");
        device->suspended = true;
        return;
    }
    if (!MGGL_Host_Resize(device->host, surface, width, height))
    {
        std::fprintf(stderr, "Native OpenGL failed to recreate the presentation surface.\n");
        device->suspended = true;
        return;
    }
    device->width = width;
    device->height = height;
    device->suspended = false;
    gl.BindFramebuffer(GL_FRAMEBUFFER, device->host.framebuffer);
    if (device->host.framebuffer != 0)
    {
        if (device->backBufferDepth)
        {
            gl.DeleteRenderbuffers(1, &device->backBufferDepth);
            device->backBufferDepth = 0;
        }
        gl.FramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_RENDERBUFFER, 0);
        gl.FramebufferRenderbuffer(GL_FRAMEBUFFER, GL_STENCIL_ATTACHMENT, GL_RENDERBUFFER, 0);
        if (depth != MGDepthFormat::None)
        {
            gl.GenRenderbuffers(1, &device->backBufferDepth);
            gl.BindRenderbuffer(GL_RENDERBUFFER, device->backBufferDepth);
            gl.RenderbufferStorage(GL_RENDERBUFFER, ToDepthInternal(depth), width, height);
            const GLenum attachment = depth == MGDepthFormat::Depth24Stencil8 ? GL_DEPTH_STENCIL_ATTACHMENT : GL_DEPTH_ATTACHMENT;
            gl.FramebufferRenderbuffer(GL_FRAMEBUFFER, attachment, GL_RENDERBUFFER, device->backBufferDepth);
        }
        if (gl.CheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
            std::fprintf(stderr, "Native OpenGL presentation framebuffer is incomplete after resize.\n");
    }
    gl.Viewport(0, 0, width, height);
}

mgint MGG_GraphicsDevice_BeginFrame(MGG_GraphicsDevice* device)
{
    return MakeCurrent(device) && !device->suspended ? 0 : -1;
}

void MGG_GraphicsDevice_Present(MGG_GraphicsDevice* device, mgint, mgint syncInterval)
{
    if (!MakeCurrent(device) || device->suspended)
        return;
    MGGL_Host_Present(device->host, syncInterval);
}

void MGG_GraphicsDevice_SubmitWithoutPresent(MGG_GraphicsDevice* device)
{
    if (MakeCurrent(device))
        gl.Finish();
}

void MGG_GraphicsDevice_Clear(MGG_GraphicsDevice* device, MGClearOptions options, Vector4& color, mgfloat depth, mgint stencil)
{
    if (!MakeCurrent(device))
        return;
    GLbitfield mask = 0;
    const auto bits = static_cast<mgint>(options);
    if (bits & static_cast<mgint>(MGClearOptions::Target))
    {
        gl.ColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
        gl.ClearColor(color.X, color.Y, color.Z, color.W);
        mask |= GL_COLOR_BUFFER_BIT;
    }
    if (bits & static_cast<mgint>(MGClearOptions::DepthBuffer))
    {
        gl.DepthMask(GL_TRUE);
        gl.ClearDepthf(depth);
        mask |= GL_DEPTH_BUFFER_BIT;
    }
    if (bits & static_cast<mgint>(MGClearOptions::Stencil))
    {
        gl.StencilMask(0xFFFFFFFFu);
        gl.ClearStencil(stencil);
        mask |= GL_STENCIL_BUFFER_BIT;
    }

    // glClear obeys the scissor and write masks.  MonoGame Clear must cover
    // the complete target and must not mutate the render state tracked by the
    // managed GraphicsDevice.
    if (device->scissorTestEnabled)
        gl.Disable(GL_SCISSOR_TEST);
    gl.Clear(mask);
    if (bits & static_cast<mgint>(MGClearOptions::Target))
        gl.ColorMask(
            device->colorWriteMask[0],
            device->colorWriteMask[1],
            device->colorWriteMask[2],
            device->colorWriteMask[3]);
    if (bits & static_cast<mgint>(MGClearOptions::DepthBuffer))
        gl.DepthMask(device->depthWriteMask);
    if (bits & static_cast<mgint>(MGClearOptions::Stencil))
        gl.StencilMask(device->stencilWriteMask);
    if (device->scissorTestEnabled)
        gl.Enable(GL_SCISSOR_TEST);
}

void MGG_GraphicsDevice_SetBlendState(MGG_GraphicsDevice* device, MGG_BlendState* state, mgfloat r, mgfloat g, mgfloat b, mgfloat a)
{
    if (!MakeCurrent(device) || !state)
        return;
    const auto& info = state->info;
    const bool enabled = info.colorSourceBlend != MGBlend::One || info.colorDestBlend != MGBlend::Zero ||
        info.alphaSourceBlend != MGBlend::One || info.alphaDestBlend != MGBlend::Zero;
    if (enabled) gl.Enable(GL_BLEND); else gl.Disable(GL_BLEND);
    gl.BlendColor(r, g, b, a);
    gl.BlendFuncSeparate(ToBlend(info.colorSourceBlend), ToBlend(info.colorDestBlend), ToBlend(info.alphaSourceBlend), ToBlend(info.alphaDestBlend));
    gl.BlendEquationSeparate(ToBlendFunction(info.colorBlendFunc), ToBlendFunction(info.alphaBlendFunc));
    const auto channels = static_cast<mgint>(info.colorWriteChannels);
    device->colorWriteMask[0] = (channels & 1) != 0;
    device->colorWriteMask[1] = (channels & 2) != 0;
    device->colorWriteMask[2] = (channels & 4) != 0;
    device->colorWriteMask[3] = (channels & 8) != 0;
    gl.ColorMask(
        device->colorWriteMask[0],
        device->colorWriteMask[1],
        device->colorWriteMask[2],
        device->colorWriteMask[3]);
}

void MGG_GraphicsDevice_SetDepthStencilState(MGG_GraphicsDevice* device, MGG_DepthStencilState* state)
{
    if (!MakeCurrent(device) || !state)
        return;
    const auto& info = state->info;
    if (info.depthBufferEnable) gl.Enable(GL_DEPTH_TEST); else gl.Disable(GL_DEPTH_TEST);
    device->depthWriteMask = info.depthBufferWriteEnable ? GL_TRUE : GL_FALSE;
    gl.DepthMask(device->depthWriteMask);
    gl.DepthFunc(ToCompare(info.depthBufferFunction));
    if (info.stencilEnable) gl.Enable(GL_STENCIL_TEST); else gl.Disable(GL_STENCIL_TEST);
    device->stencilWriteMask = info.stencilWriteMask;
    gl.StencilMask(device->stencilWriteMask);
    gl.StencilFunc(ToCompare(info.stencilFunction), info.referenceStencil, info.stencilMask);
    gl.StencilOp(ToStencil(info.stencilFail), ToStencil(info.stencilDepthBufferFail), ToStencil(info.stencilPass));
}

static void MGGLApplyCullMode(MGG_GraphicsDevice* device, MGCullMode cullMode)
{
    device->currentCullMode = cullMode;
    if (cullMode == MGCullMode::None)
    {
        gl.Disable(GL_CULL_FACE);
        return;
    }

    gl.Enable(GL_CULL_FACE);
    gl.CullFace(GL_BACK);
    const bool offscreen = device->renderTargetCount > 0;
    if (cullMode == MGCullMode::CullClockwiseFace)
        gl.FrontFace(offscreen ? GL_CW : GL_CCW);
    else
        gl.FrontFace(offscreen ? GL_CCW : GL_CW);
}

void MGG_GraphicsDevice_SetRasterizerState(MGG_GraphicsDevice* device, MGG_RasterizerState* state)
{
    if (!MakeCurrent(device) || !state)
        return;
    const auto& info = state->info;
#if !defined(MG_ANDROID) && !defined(MG_IOS)
    gl.PolygonMode(GL_FRONT_AND_BACK, info.fillMode == MGFillMode::WireFrame ? GL_LINE : GL_FILL);
#else
    if (info.fillMode == MGFillMode::WireFrame)
        std::fprintf(stderr, "Wireframe rasterization is not available in OpenGL ES.\n");
#endif
    MGGLApplyCullMode(device, info.cullMode);
    device->scissorTestEnabled = info.scissorTestEnable;
    if (device->scissorTestEnabled) gl.Enable(GL_SCISSOR_TEST); else gl.Disable(GL_SCISSOR_TEST);
#if !defined(MG_ANDROID) && !defined(MG_IOS)
    if (info.multiSampleAntiAlias) gl.Enable(GL_MULTISAMPLE); else gl.Disable(GL_MULTISAMPLE);
#endif
    if (info.depthBias != 0 || info.slopeScaleDepthBias != 0)
    {
        gl.Enable(GL_POLYGON_OFFSET_FILL);
        // glPolygonOffset is part of OpenGL 1.1 and is intentionally not optional.
        ::glPolygonOffset(info.slopeScaleDepthBias, info.depthBias);
    }
    else
        gl.Disable(GL_POLYGON_OFFSET_FILL);
}

void MGG_GraphicsDevice_GetTitleSafeArea(mgint& x, mgint& y, mgint& width, mgint& height)
{
#if defined(MG_ANDROID) || defined(MG_IOS)
    x = y = 0;
    width = height = 1;
#else
    SDL_Rect bounds{};
    if (SDL_GetDisplayUsableBounds(0, &bounds) == 0)
    {
        x = bounds.x;
        y = bounds.y;
        width = bounds.w;
        height = bounds.h;
    }
    else
        x = y = width = height = 0;
#endif
}

void MGG_GraphicsDevice_SetViewport(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height, mgfloat minDepth, mgfloat maxDepth)
{
    if (!MakeCurrent(device))
        return;
    const int targetHeight = device->renderTargetCount > 0 && device->renderTargets[0] ? device->renderTargets[0]->height : device->height;
    gl.Viewport(x, targetHeight - y - height, width, height);
    gl.DepthRangef(minDepth, maxDepth);
}

void MGG_GraphicsDevice_SetScissorRectangle(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height)
{
    if (!MakeCurrent(device))
        return;
    const int targetHeight = device->renderTargetCount > 0 && device->renderTargets[0] ? device->renderTargets[0]->height : device->height;
    gl.Scissor(x, targetHeight - y - height, width, height);
}

static MGGraphicsDeviceStatus MGGLSetRenderTargets(
    MGG_GraphicsDevice* device,
    MGG_Texture** targets,
    mgint* slices,
    mgint count,
    MGG_Texture* independentDepthStencil = nullptr,
    bool explicitRenderPass = false)
{
    if (!MakeCurrent(device))
        return MGGraphicsDeviceStatus::DeviceUnavailable;
    if (count < 0 || count > MGGLMaxColorTargets)
        return MGGraphicsDeviceStatus::InvalidRenderTargetCount;
    if (count > 0 && targets == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;

    if (count > 0)
    {
        const MGG_Texture* first = targets[0];
        if (first == nullptr || !first->renderTarget)
            return MGGraphicsDeviceStatus::InvalidRenderTarget;
        for (mgint index = 0; index < count; ++index)
        {
            const MGG_Texture* target = targets[index];
            if (target == nullptr || !target->renderTarget)
                return MGGraphicsDeviceStatus::InvalidRenderTarget;
            if (!explicitRenderPass && count > 1 && target->format != MGSurfaceFormat::Color)
                return MGGraphicsDeviceStatus::RenderTargetFormatNotSupported;
            if (target->width != first->width || target->height != first->height)
                return MGGraphicsDeviceStatus::RenderTargetDimensionsMismatch;
            if ((explicitRenderPass || count > 1) && target->samples > 1)
                return MGGraphicsDeviceStatus::RenderTargetMultisamplingNotSupported;
            if (!explicitRenderPass && index > 0 && target->depthFormat != MGDepthFormat::None)
                return MGGraphicsDeviceStatus::RenderTargetDepthAttachmentNotSupported;
        }
        if (independentDepthStencil != nullptr)
        {
            if (!independentDepthStencil->independentDepthStencil)
                return MGGraphicsDeviceStatus::InvalidRenderTarget;
            if (independentDepthStencil->width != first->width ||
                independentDepthStencil->height != first->height)
                return MGGraphicsDeviceStatus::RenderTargetDimensionsMismatch;
            if (independentDepthStencil->samples > 1)
                return MGGraphicsDeviceStatus::RenderTargetMultisamplingNotSupported;
        }
    }

    if (device->explicitRenderPass && gl.InvalidateFramebuffer)
    {
        std::array<GLenum, MGGLMaxColorTargets + 2> discardAttachments{};
        GLsizei discardCount = 0;
        for (mgint index = 0; index < device->renderTargetCount; ++index)
        {
            if (device->explicitColorStoreActions[index] == MGRenderPassStoreAction::DontCare)
                discardAttachments[discardCount++] = GL_COLOR_ATTACHMENT0 + index;
        }
        if (device->explicitDepthStencilTarget != nullptr)
        {
            if (device->explicitDepthStoreAction == MGRenderPassStoreAction::DontCare)
                discardAttachments[discardCount++] = GL_DEPTH_ATTACHMENT;
            if (device->explicitDepthStencilTarget->depthFormat == MGDepthFormat::Depth24Stencil8 &&
                device->explicitStencilStoreAction == MGRenderPassStoreAction::DontCare)
                discardAttachments[discardCount++] = GL_STENCIL_ATTACHMENT;
        }
        if (discardCount > 0)
            gl.InvalidateFramebuffer(GL_FRAMEBUFFER, discardCount, discardAttachments.data());
    }

    std::array<MGG_Texture*, MGGLMaxColorTargets> incomingTargets{};
    std::array<mgint, MGGLMaxColorTargets> incomingSlices{};
    for (int index = 0; index < count; ++index)
    {
        incomingTargets[index] = targets ? targets[index] : nullptr;
        incomingSlices[index] = slices ? slices[index] : 0;
    }
    device->renderTargetCount = count;
    device->explicitRenderPass = explicitRenderPass;
    device->explicitDepthStencilTarget = independentDepthStencil;
    if (!explicitRenderPass)
    {
        std::fill(
            std::begin(device->explicitColorStoreActions),
            std::end(device->explicitColorStoreActions),
            MGRenderPassStoreAction::Store);
        device->explicitDepthStoreAction = MGRenderPassStoreAction::Store;
        device->explicitStencilStoreAction = MGRenderPassStoreAction::Store;
    }
    std::memset(device->renderTargets, 0, sizeof(device->renderTargets));
    if (count == 0)
    {
        gl.BindFramebuffer(GL_FRAMEBUFFER, device->host.framebuffer);
        MGGLApplyCullMode(device, device->currentCullMode);
        return MGGraphicsDeviceStatus::Success;
    }

    gl.BindFramebuffer(GL_FRAMEBUFFER, device->framebuffer);
    gl.FramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_RENDERBUFFER, 0);
    gl.FramebufferRenderbuffer(GL_FRAMEBUFFER, GL_STENCIL_ATTACHMENT, GL_RENDERBUFFER, 0);
    gl.FramebufferTexture2D(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_TEXTURE_2D, 0, 0);
    gl.FramebufferTexture2D(GL_FRAMEBUFFER, GL_STENCIL_ATTACHMENT, GL_TEXTURE_2D, 0, 0);
    std::array<GLenum, MGGLMaxColorTargets> drawBuffers{};
    for (int index = 0; index < MGGLMaxColorTargets; ++index)
    {
        device->renderTargets[index] = incomingTargets[index];
        device->renderTargetSlices[index] = incomingSlices[index];
        if (index < count && incomingTargets[index])
        {
            AttachTarget(GL_COLOR_ATTACHMENT0 + index, incomingTargets[index], incomingSlices[index], incomingTargets[index]->samples > 1);
            drawBuffers[index] = GL_COLOR_ATTACHMENT0 + index;
        }
        else
            gl.FramebufferTexture2D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0 + index, GL_TEXTURE_2D, 0, 0);
    }
    gl.DrawBuffers(count, drawBuffers.data());

    auto* first = incomingTargets[0];
    if (independentDepthStencil != nullptr)
    {
        const GLenum attachment = independentDepthStencil->depthFormat == MGDepthFormat::Depth24Stencil8
            ? GL_DEPTH_STENCIL_ATTACHMENT
            : GL_DEPTH_ATTACHMENT;
        gl.FramebufferTexture2D(
            GL_FRAMEBUFFER,
            attachment,
            GL_TEXTURE_2D,
            independentDepthStencil->handle,
            0);
    }
    else if (!explicitRenderPass && first && first->depthBuffer)
    {
        const GLenum attachment = first->depthFormat == MGDepthFormat::Depth24Stencil8 ? GL_DEPTH_STENCIL_ATTACHMENT : GL_DEPTH_ATTACHMENT;
        gl.FramebufferRenderbuffer(GL_FRAMEBUFFER, attachment, GL_RENDERBUFFER, first->depthBuffer);
    }
    const GLenum framebufferStatus = gl.CheckFramebufferStatus(GL_FRAMEBUFFER);
    if (framebufferStatus != GL_FRAMEBUFFER_COMPLETE)
    {
        std::fprintf(stderr, "Native OpenGL render-target framebuffer is incomplete (status=0x%04x).\n", framebufferStatus);
        return MGGraphicsDeviceStatus::FramebufferIncomplete;
    }
    MGGLApplyCullMode(device, device->currentCullMode);
    return MGGraphicsDeviceStatus::Success;
}

void MGG_GraphicsDevice_SetRenderTargets(MGG_GraphicsDevice* device, MGG_Texture** targets, mgint* slices, mgint count)
{
    (void)MGGLSetRenderTargets(device, targets, slices, count);
}

MGGraphicsDeviceStatus MGG_GraphicsDevice_SetRenderTargetsV2(
    MGG_GraphicsDevice* device,
    MGG_Texture** targets,
    mgint* slices,
    mgint count)
{
    return MGGLSetRenderTargets(device, targets, slices, count);
}

mgbyte MGG_GraphicsDevice_SupportsDepthStencilTargetFormatV3(
    MGG_GraphicsDevice* device,
    MGDepthFormat depthFormat)
{
    return ProbeSampleableDepthFormat(device, depthFormat) ? 1 : 0;
}

static void MGGLApplyExplicitRenderPassClears(
    MGG_GraphicsDevice* device,
    MGG_RenderPassColorAttachment* colorAttachments,
    mgint colorAttachmentCount,
    MGG_RenderPassDepthStencilAttachment* depthStencilAttachment)
{
    bool clearColor = false;
    for (mgint index = 0; index < colorAttachmentCount; ++index)
        clearColor |= colorAttachments[index].LoadAction == MGRenderPassLoadAction::Clear;
    const bool clearDepth = depthStencilAttachment != nullptr &&
        depthStencilAttachment->DepthLoadAction == MGRenderPassLoadAction::Clear;
    const bool clearStencil = depthStencilAttachment != nullptr &&
        static_cast<MGG_Texture*>(depthStencilAttachment->Target)->depthFormat == MGDepthFormat::Depth24Stencil8 &&
        depthStencilAttachment->StencilLoadAction == MGRenderPassLoadAction::Clear;
    if (!clearColor && !clearDepth && !clearStencil)
        return;

    if (device->scissorTestEnabled)
        gl.Disable(GL_SCISSOR_TEST);
    if (clearColor)
    {
        gl.ColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
        for (mgint index = 0; index < colorAttachmentCount; ++index)
        {
            if (colorAttachments[index].LoadAction == MGRenderPassLoadAction::Clear)
                gl.ClearBufferfv(GL_COLOR, index, &colorAttachments[index].ClearColor.X);
        }
        gl.ColorMask(
            device->colorWriteMask[0],
            device->colorWriteMask[1],
            device->colorWriteMask[2],
            device->colorWriteMask[3]);
    }
    if (clearDepth && clearStencil)
    {
        gl.DepthMask(GL_TRUE);
        gl.StencilMask(0xFFFFFFFFu);
        gl.ClearBufferfi(
            GL_DEPTH_STENCIL,
            0,
            depthStencilAttachment->ClearDepth,
            depthStencilAttachment->ClearStencil);
        gl.DepthMask(device->depthWriteMask);
        gl.StencilMask(device->stencilWriteMask);
    }
    else if (clearDepth)
    {
        gl.DepthMask(GL_TRUE);
        gl.ClearBufferfv(GL_DEPTH, 0, &depthStencilAttachment->ClearDepth);
        gl.DepthMask(device->depthWriteMask);
    }
    else if (clearStencil)
    {
        gl.StencilMask(0xFFFFFFFFu);
        gl.ClearBufferiv(GL_STENCIL, 0, &depthStencilAttachment->ClearStencil);
        gl.StencilMask(device->stencilWriteMask);
    }
    if (device->scissorTestEnabled)
        gl.Enable(GL_SCISSOR_TEST);
}

MGGraphicsDeviceStatus MGG_GraphicsDevice_SetRenderPassV3(
    MGG_GraphicsDevice* device,
    MGG_RenderPassColorAttachment* colorAttachments,
    mgint colorAttachmentCount,
    MGG_RenderPassDepthStencilAttachment* depthStencilAttachment)
{
    if (device == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;
    if (colorAttachmentCount <= 0 || colorAttachmentCount > MGGLMaxColorTargets)
        return MGGraphicsDeviceStatus::InvalidRenderTargetCount;
    if (colorAttachments == nullptr)
        return MGGraphicsDeviceStatus::InvalidArgument;

    std::array<MGG_Texture*, MGGLMaxColorTargets> targets{};
    std::array<mgint, MGGLMaxColorTargets> slices{};
    for (mgint index = 0; index < colorAttachmentCount; ++index)
    {
        const auto& attachment = colorAttachments[index];
        targets[index] = static_cast<MGG_Texture*>(attachment.Target);
        slices[index] = attachment.ArraySlice;
        if (targets[index] == nullptr || targets[index]->independentDepthStencil)
            return MGGraphicsDeviceStatus::InvalidRenderTarget;
        if (!ValidRenderPassActions(attachment.LoadAction, attachment.StoreAction))
            return MGGraphicsDeviceStatus::InvalidArgument;
    }

    MGG_Texture* depthTarget = nullptr;
    if (depthStencilAttachment != nullptr)
    {
        depthTarget = static_cast<MGG_Texture*>(depthStencilAttachment->Target);
        if (depthTarget == nullptr || !depthTarget->independentDepthStencil)
            return MGGraphicsDeviceStatus::InvalidRenderTarget;
        if (!ValidRenderPassActions(
                depthStencilAttachment->DepthLoadAction,
                depthStencilAttachment->DepthStoreAction) ||
            !ValidRenderPassActions(
                depthStencilAttachment->StencilLoadAction,
                depthStencilAttachment->StencilStoreAction))
            return MGGraphicsDeviceStatus::InvalidArgument;
    }

    const auto status = MGGLSetRenderTargets(
        device,
        targets.data(),
        slices.data(),
        colorAttachmentCount,
        depthTarget,
        true);
    if (status != MGGraphicsDeviceStatus::Success)
        return status;

    if (gl.InvalidateFramebuffer)
    {
        std::array<GLenum, MGGLMaxColorTargets + 2> discardAttachments{};
        GLsizei discardCount = 0;
        for (mgint index = 0; index < colorAttachmentCount; ++index)
        {
            if (colorAttachments[index].LoadAction == MGRenderPassLoadAction::DontCare)
                discardAttachments[discardCount++] = GL_COLOR_ATTACHMENT0 + index;
        }
        if (depthStencilAttachment != nullptr)
        {
            if (depthStencilAttachment->DepthLoadAction == MGRenderPassLoadAction::DontCare)
                discardAttachments[discardCount++] = GL_DEPTH_ATTACHMENT;
            if (depthTarget->depthFormat == MGDepthFormat::Depth24Stencil8 &&
                depthStencilAttachment->StencilLoadAction == MGRenderPassLoadAction::DontCare)
                discardAttachments[discardCount++] = GL_STENCIL_ATTACHMENT;
        }
        if (discardCount > 0)
            gl.InvalidateFramebuffer(GL_FRAMEBUFFER, discardCount, discardAttachments.data());
    }

    for (mgint index = 0; index < colorAttachmentCount; ++index)
        device->explicitColorStoreActions[index] = colorAttachments[index].StoreAction;
    for (mgint index = colorAttachmentCount; index < MGGLMaxColorTargets; ++index)
        device->explicitColorStoreActions[index] = MGRenderPassStoreAction::Store;
    device->explicitDepthStoreAction = depthStencilAttachment != nullptr
        ? depthStencilAttachment->DepthStoreAction
        : MGRenderPassStoreAction::Store;
    device->explicitStencilStoreAction = depthStencilAttachment != nullptr
        ? depthStencilAttachment->StencilStoreAction
        : MGRenderPassStoreAction::Store;

    MGGLApplyExplicitRenderPassClears(
        device,
        colorAttachments,
        colorAttachmentCount,
        depthStencilAttachment);
    return MGGraphicsDeviceStatus::Success;
}

void MGG_GraphicsDevice_SetConstantBuffer(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_Buffer* buffer)
{
    if (device && slot >= 0 && slot < MGGLMaxTextureSlots)
        device->constantBuffers[static_cast<int>(stage)][slot] = buffer;
}

void MGG_GraphicsDevice_SetTexture(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_Texture* texture)
{
    if (device && slot >= 0 && slot < MGGLMaxTextureSlots)
        device->textures[static_cast<int>(stage)][slot] = texture;
}

void MGG_GraphicsDevice_SetSamplerState(MGG_GraphicsDevice* device, MGShaderStage stage, mgint slot, MGG_SamplerState* state)
{
    if (device && slot >= 0 && slot < MGGLMaxTextureSlots)
        device->samplers[static_cast<int>(stage)][slot] = state;
}

void MGG_GraphicsDevice_SetIndexBuffer(MGG_GraphicsDevice* device, MGIndexElementSize size, MGG_Buffer* buffer)
{
    if (!device)
        return;
    device->indexBuffer = buffer;
    device->indexType = size == MGIndexElementSize::SixteenBits ? GL_UNSIGNED_SHORT : GL_UNSIGNED_INT;
}

void MGG_GraphicsDevice_SetVertexBuffer(MGG_GraphicsDevice* device, mgint slot, MGG_Buffer* buffer, mgint vertexOffset)
{
    if (device && slot >= 0 && slot < MGGLMaxVertexBuffers)
    {
        device->vertexBuffers[slot] = buffer;
        device->vertexOffsets[slot] = vertexOffset;
    }
}

void MGG_GraphicsDevice_SetShader(MGG_GraphicsDevice* device, MGShaderStage stage, MGG_Shader* shader)
{
    if (device)
    {
        device->shaders[static_cast<int>(stage)] = shader;
        device->currentProgram = nullptr;
    }
}

void MGG_GraphicsDevice_SetInputLayout(MGG_GraphicsDevice* device, MGG_InputLayout* layout)
{
    if (device)
        device->inputLayout = layout;
}

void MGG_GraphicsDevice_Draw(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint vertexStart, mgint vertexCount)
{
    if (PrepareDraw(device))
        gl.DrawArrays(ToPrimitive(primitiveType), vertexStart, vertexCount);
}

void MGG_GraphicsDevice_DrawIndexed(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint primitiveCount, mgint indexStart, mgint vertexStart)
{
    if (!PrepareDraw(device) || !device->indexBuffer)
        return;
    const intptr_t offset = static_cast<intptr_t>(indexStart) * (device->indexType == GL_UNSIGNED_SHORT ? 2 : 4);
    if (gl.DrawElementsBaseVertex)
        gl.DrawElementsBaseVertex(ToPrimitive(primitiveType), PrimitiveVertexCount(primitiveType, primitiveCount), device->indexType, reinterpret_cast<const void*>(offset), vertexStart);
    else if (vertexStart == 0)
        gl.DrawElements(ToPrimitive(primitiveType), PrimitiveVertexCount(primitiveType, primitiveCount), device->indexType, reinterpret_cast<const void*>(offset));
    else
        std::fprintf(stderr, "Native OpenGL ES 3.0 cannot draw indexed geometry with a non-zero base vertex.\n");
}

void MGG_GraphicsDevice_DrawIndexedInstanced(MGG_GraphicsDevice* device, MGPrimitiveType primitiveType, mgint primitiveCount, mgint indexStart, mgint vertexStart, mgint instanceCount)
{
    if (!PrepareDraw(device) || !device->indexBuffer)
        return;
    const intptr_t offset = static_cast<intptr_t>(indexStart) * (device->indexType == GL_UNSIGNED_SHORT ? 2 : 4);
    if (gl.DrawElementsInstancedBaseVertex)
        gl.DrawElementsInstancedBaseVertex(ToPrimitive(primitiveType), PrimitiveVertexCount(primitiveType, primitiveCount), device->indexType, reinterpret_cast<const void*>(offset), instanceCount, vertexStart);
    else if (vertexStart == 0)
        gl.DrawElementsInstanced(ToPrimitive(primitiveType), PrimitiveVertexCount(primitiveType, primitiveCount), device->indexType, reinterpret_cast<const void*>(offset), instanceCount);
    else
        std::fprintf(stderr, "Native OpenGL ES 3.0 cannot draw indexed instanced geometry with a non-zero base vertex.\n");
}

void MGG_GraphicsDevice_ResolveRenderTargets(MGG_GraphicsDevice* device)
{
    if (!MakeCurrent(device))
        return;
    for (int index = 0; index < device->renderTargetCount; ++index)
    {
        auto* texture = device->renderTargets[index];
        if (!texture || texture->samples <= 1 || !texture->msaaColor)
            continue;
        gl.BindFramebuffer(GL_READ_FRAMEBUFFER, device->resolveReadFramebuffer);
        gl.FramebufferRenderbuffer(GL_READ_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_RENDERBUFFER, texture->msaaColor);
        gl.BindFramebuffer(GL_DRAW_FRAMEBUFFER, device->resolveDrawFramebuffer);
        AttachTarget(GL_COLOR_ATTACHMENT0, texture, device->renderTargetSlices[index], false, GL_DRAW_FRAMEBUFFER);
        gl.BlitFramebuffer(0, 0, texture->width, texture->height, 0, 0, texture->width, texture->height, GL_COLOR_BUFFER_BIT, GL_NEAREST);
    }
    MGG_GraphicsDevice_SetRenderTargets(device, device->renderTargets, device->renderTargetSlices, device->renderTargetCount);
}

void MGG_GraphicsDevice_GetBackBufferData(MGG_GraphicsDevice* device, mgint x, mgint y, mgint width, mgint height, void* data, mgint count, mgint dataBytes)
{
    if (!MakeCurrent(device) || !data)
        return;
    gl.PixelStorei(GL_PACK_ALIGNMENT, 1);
    gl.ReadPixels(x, device->height - y - height, width, height, GL_RGBA, GL_UNSIGNED_BYTE, data);
    // OpenGL's origin is lower-left; flip the tightly packed result for MonoGame.
    const size_t row = width > 0 && height > 0
        ? (static_cast<size_t>(count) * static_cast<size_t>(dataBytes)) / static_cast<size_t>(height)
        : 0;
    std::vector<uint8_t> temporary(row);
    auto* bytes = static_cast<uint8_t*>(data);
    for (int top = 0, bottom = height - 1; top < bottom; ++top, --bottom)
    {
        std::memcpy(temporary.data(), bytes + top * row, row);
        std::memcpy(bytes + top * row, bytes + bottom * row, row);
        std::memcpy(bytes + bottom * row, temporary.data(), row);
    }
}

MGG_BlendState* MGG_BlendState_Create(MGG_GraphicsDevice*, MGG_BlendState_Info* info)
{
    if (!info) return nullptr;
    auto* state = new MGG_BlendState();
    state->info = *info;
    return state;
}

void MGG_BlendState_Destroy(MGG_GraphicsDevice*, MGG_BlendState* state) { delete state; }

MGG_DepthStencilState* MGG_DepthStencilState_Create(MGG_GraphicsDevice*, MGG_DepthStencilState_Info* info)
{
    if (!info) return nullptr;
    auto* state = new MGG_DepthStencilState();
    state->info = *info;
    return state;
}

void MGG_DepthStencilState_Destroy(MGG_GraphicsDevice*, MGG_DepthStencilState* state) { delete state; }

MGG_RasterizerState* MGG_RasterizerState_Create(MGG_GraphicsDevice*, MGG_RasterizerState_Info* info)
{
    if (!info) return nullptr;
    auto* state = new MGG_RasterizerState();
    state->info = *info;
    return state;
}

void MGG_RasterizerState_Destroy(MGG_GraphicsDevice*, MGG_RasterizerState* state) { delete state; }

MGG_SamplerState* MGG_SamplerState_Create(MGG_GraphicsDevice* device, MGG_SamplerState_Info* info)
{
    if (!MakeCurrent(device) || !info)
        return nullptr;
    auto* state = new MGG_SamplerState();
    gl.GenSamplers(1, &state->handle);
    gl.SamplerParameteri(state->handle, GL_TEXTURE_WRAP_S, ToAddress(info->AddressU));
    gl.SamplerParameteri(state->handle, GL_TEXTURE_WRAP_T, ToAddress(info->AddressV));
    gl.SamplerParameteri(state->handle, GL_TEXTURE_WRAP_R, ToAddress(info->AddressW));
    GLenum minFilter = GL_LINEAR_MIPMAP_LINEAR;
    GLenum magFilter = GL_LINEAR;
    switch (info->Filter)
    {
        case MGTextureFilter::Point: minFilter = GL_NEAREST_MIPMAP_NEAREST; magFilter = GL_NEAREST; break;
        case MGTextureFilter::LinearMipPoint: minFilter = GL_LINEAR_MIPMAP_NEAREST; break;
        case MGTextureFilter::PointMipLinear: minFilter = GL_NEAREST_MIPMAP_LINEAR; magFilter = GL_NEAREST; break;
        case MGTextureFilter::MinLinearMagPointMipLinear: magFilter = GL_NEAREST; break;
        case MGTextureFilter::MinLinearMagPointMipPoint: minFilter = GL_LINEAR_MIPMAP_NEAREST; magFilter = GL_NEAREST; break;
        case MGTextureFilter::MinPointMagLinearMipLinear: minFilter = GL_NEAREST_MIPMAP_LINEAR; break;
        case MGTextureFilter::MinPointMagLinearMipPoint: minFilter = GL_NEAREST_MIPMAP_NEAREST; break;
        default: break;
    }
    gl.SamplerParameteri(state->handle, GL_TEXTURE_MIN_FILTER, minFilter);
    gl.SamplerParameteri(state->handle, GL_TEXTURE_MAG_FILTER, magFilter);
#if !defined(MG_ANDROID) && !defined(MG_IOS)
    gl.SamplerParameterf(state->handle, GL_TEXTURE_LOD_BIAS, info->MipMapLevelOfDetailBias);
#endif
    if (info->FilterMode == MGTextureFilterMode::Comparison)
    {
        gl.SamplerParameteri(state->handle, GL_TEXTURE_COMPARE_MODE, GL_COMPARE_REF_TO_TEXTURE);
        gl.SamplerParameteri(state->handle, GL_TEXTURE_COMPARE_FUNC, ToCompare(info->ComparisonFunction));
    }
    if (info->Filter == MGTextureFilter::Anisotropic)
    {
        if (device->supportsAnisotropy)
            gl.SamplerParameterf(state->handle, GL_TEXTURE_MAX_ANISOTROPY_EXT,
                std::min(device->maxAnisotropy, static_cast<GLfloat>(std::max(1, info->MaximumAnisotropy))));
        else
            std::fprintf(stderr, "Native OpenGL anisotropic filtering was requested but is not supported by this driver.\n");
    }
    if (info->AddressU == MGTextureAddressMode::Border || info->AddressV == MGTextureAddressMode::Border || info->AddressW == MGTextureAddressMode::Border)
    {
        const GLfloat color[] =
        {
            ((info->BorderColor >> 0) & 0xFF) / 255.0f,
            ((info->BorderColor >> 8) & 0xFF) / 255.0f,
            ((info->BorderColor >> 16) & 0xFF) / 255.0f,
            ((info->BorderColor >> 24) & 0xFF) / 255.0f,
        };
        gl.SamplerParameterfv(state->handle, GL_TEXTURE_BORDER_COLOR, color);
    }
    return state;
}

void MGG_SamplerState_Destroy(MGG_GraphicsDevice* device, MGG_SamplerState* state)
{
    if (!state) return;
    if (device)
    {
        for (auto& stage : device->samplers)
        {
            for (auto& bound : stage)
            {
                if (bound == state)
                    bound = nullptr;
            }
        }
    }
    if (MakeCurrent(device) && state->handle) gl.DeleteSamplers(1, &state->handle);
    delete state;
}

MGG_Buffer* MGG_Buffer_Create(MGG_GraphicsDevice* device, MGBufferType type, mgbool dynamic, mgint sizeInBytes)
{
    if (!MakeCurrent(device) || sizeInBytes < 0)
        return nullptr;
    auto* buffer = new MGG_Buffer();
    buffer->target = type == MGBufferType::Index ? GL_ELEMENT_ARRAY_BUFFER : type == MGBufferType::Vertex ? GL_ARRAY_BUFFER : GL_UNIFORM_BUFFER;
    buffer->size = sizeInBytes;
    buffer->dynamic = dynamic;
    buffer->shadow.resize(sizeInBytes);
    gl.GenBuffers(1, &buffer->handle);
    gl.BindBuffer(buffer->target, buffer->handle);
    gl.BufferData(buffer->target, sizeInBytes, nullptr, dynamic ? GL_DYNAMIC_DRAW : GL_STATIC_DRAW);
    return buffer;
}

void MGG_Buffer_Destroy(MGG_GraphicsDevice* device, MGG_Buffer* buffer)
{
    if (!buffer) return;
    if (device)
    {
        for (auto& stage : device->constantBuffers)
        {
            for (auto& bound : stage)
            {
                if (bound == buffer)
                    bound = nullptr;
            }
        }
        for (auto& bound : device->vertexBuffers)
        {
            if (bound == buffer)
                bound = nullptr;
        }
        if (device->indexBuffer == buffer)
            device->indexBuffer = nullptr;
    }
    if (MakeCurrent(device) && buffer->handle) gl.DeleteBuffers(1, &buffer->handle);
    delete buffer;
}

void MGG_Buffer_SetData(MGG_GraphicsDevice* device, MGG_Buffer*& buffer, mgint offset, mgbyte* data, mgint elementCount, mgint vertexStride, mgint elementSizeInBytes, mgbool discard)
{
    if (!MakeCurrent(device) || !buffer || !data || offset < 0 || elementCount <= 0 || vertexStride <= 0 || elementSizeInBytes <= 0)
        return;
    const size_t span = static_cast<size_t>(elementCount - 1) * vertexStride + elementSizeInBytes;
    if (static_cast<size_t>(offset) + span > buffer->shadow.size())
        return;
    for (int index = 0; index < elementCount; ++index)
    {
        std::memcpy(
            buffer->shadow.data() + offset + static_cast<size_t>(index) * vertexStride,
            data + static_cast<size_t>(index) * elementSizeInBytes,
            elementSizeInBytes);
    }
    gl.BindBuffer(buffer->target, buffer->handle);
    if (discard)
        gl.BufferData(buffer->target, buffer->size, nullptr, buffer->dynamic ? GL_DYNAMIC_DRAW : GL_STATIC_DRAW);
    gl.BufferSubData(buffer->target, offset, span, buffer->shadow.data() + offset);
}

void MGG_Buffer_GetData(MGG_GraphicsDevice*, MGG_Buffer* buffer, mgint offset, mgbyte* data, mgint dataCount, mgint dataBytes, mgint dataStride)
{
    if (!buffer || !data || offset < 0 || dataStride <= 0)
        return;
    if (dataCount <= 0 || dataBytes <= 0)
        return;
    const size_t elementBytes = std::min(static_cast<size_t>(dataBytes), static_cast<size_t>(dataStride));
    for (int index = 0; index < dataCount; ++index)
    {
        const size_t source = static_cast<size_t>(offset) + static_cast<size_t>(index) * dataStride;
        if (source + elementBytes > buffer->shadow.size()) break;
        std::memcpy(data + index * elementBytes, buffer->shadow.data() + source, elementBytes);
    }
}

MGG_Texture* MGG_Texture_Create(MGG_GraphicsDevice* device, MGTextureType type, MGSurfaceFormat format, mgint width, mgint height, mgint depth, mgint mipmaps, mgint slices)
{
    if (!MakeCurrent(device) || width <= 0 || height <= 0 || depth <= 0)
        return nullptr;
    const auto glFormat = ToTextureFormat(format);
    if (!glFormat.internalFormat)
    {
        std::fprintf(stderr, "Native OpenGL does not support MonoGame surface format %d.\n", static_cast<int>(format));
        return nullptr;
    }
    auto* texture = new MGG_Texture();
    texture->type = type;
    texture->format = format;
    texture->glFormat = glFormat;
    texture->width = width;
    texture->height = height;
    texture->depth = depth;
    texture->mipmaps = std::max(1, mipmaps);
    texture->slices = std::max(1, slices);
    texture->target = type == MGTextureType::_2D && texture->slices > 1 ? GL_TEXTURE_2D_ARRAY : ToTextureTarget(type);
    if (glFormat.compressed && (type == MGTextureType::_3D || (type == MGTextureType::_2D && texture->slices > 1)))
    {
        std::fprintf(stderr, "Native OpenGL v1 does not support compressed 3D or array textures.\n");
        delete texture;
        return nullptr;
    }
    texture->compressedDefined.resize(static_cast<size_t>(texture->mipmaps) * (type == MGTextureType::Cube ? 6u : 1u));
    gl.GenTextures(1, &texture->handle);
    gl.BindTexture(texture->target, texture->handle);
    gl.TexParameteri(texture->target, GL_TEXTURE_MIN_FILTER, texture->mipmaps > 1 ? GL_LINEAR_MIPMAP_LINEAR : GL_LINEAR);
    gl.TexParameteri(texture->target, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
    gl.TexParameteri(texture->target, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    gl.TexParameteri(texture->target, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    gl.TexParameteri(texture->target, GL_TEXTURE_BASE_LEVEL, 0);
    gl.TexParameteri(texture->target, GL_TEXTURE_MAX_LEVEL, texture->mipmaps - 1);
    if (type == MGTextureType::_3D)
        gl.TexParameteri(texture->target, GL_TEXTURE_WRAP_R, GL_CLAMP_TO_EDGE);

    if (!glFormat.compressed)
    {
        for (int level = 0; level < texture->mipmaps; ++level)
        {
            const int levelWidth = std::max(1, width >> level);
            const int levelHeight = std::max(1, height >> level);
            const int levelDepth = std::max(1, depth >> level);
            if (type == MGTextureType::_3D || texture->target == GL_TEXTURE_2D_ARRAY)
                gl.TexImage3D(texture->target, level, glFormat.internalFormat, levelWidth, levelHeight,
                    type == MGTextureType::_3D ? levelDepth : texture->slices, 0, glFormat.format, glFormat.type, nullptr);
            else if (type == MGTextureType::Cube)
            {
                for (int face = 0; face < 6; ++face)
                    gl.TexImage2D(GL_TEXTURE_CUBE_MAP_POSITIVE_X + face, level, glFormat.internalFormat, levelWidth, levelHeight, 0, glFormat.format, glFormat.type, nullptr);
            }
            else
                gl.TexImage2D(texture->target, level, glFormat.internalFormat, levelWidth, levelHeight, 0, glFormat.format, glFormat.type, nullptr);
        }
    }
    return texture;
}

MGG_Texture* MGG_RenderTarget_Create(MGG_GraphicsDevice* device, MGTextureType type, MGSurfaceFormat format, mgint width, mgint height, mgint depth, mgint mipmaps, mgint slices, MGDepthFormat depthFormat, mgint multiSampleCount, MGRenderTargetUsage)
{
    auto* texture = MGG_Texture_Create(device, type, format, width, height, depth, mipmaps, slices);
    if (!texture)
        return nullptr;
    texture->renderTarget = true;
    texture->samples = std::max(1, multiSampleCount);
    texture->depthFormat = depthFormat;
    if (texture->samples > 1)
    {
        gl.GenRenderbuffers(1, &texture->msaaColor);
        gl.BindRenderbuffer(GL_RENDERBUFFER, texture->msaaColor);
        gl.RenderbufferStorageMultisample(GL_RENDERBUFFER, texture->samples, texture->glFormat.internalFormat, width, height);
    }
    if (depthFormat != MGDepthFormat::None)
    {
        gl.GenRenderbuffers(1, &texture->depthBuffer);
        gl.BindRenderbuffer(GL_RENDERBUFFER, texture->depthBuffer);
        if (texture->samples > 1)
            gl.RenderbufferStorageMultisample(GL_RENDERBUFFER, texture->samples, ToDepthInternal(depthFormat), width, height);
        else
            gl.RenderbufferStorage(GL_RENDERBUFFER, ToDepthInternal(depthFormat), width, height);
    }
    return texture;
}

MGG_Texture* MGG_DepthStencilTarget_Create(
    MGG_GraphicsDevice* device,
    mgint width,
    mgint height,
    MGDepthFormat depthFormat)
{
    if (width <= 0 || height <= 0 ||
        !ProbeSampleableDepthFormat(device, depthFormat))
        return nullptr;

    GLenum format = 0;
    GLenum type = 0;
    if (!GetDepthTextureFormat(depthFormat, format, type))
        return nullptr;

    auto* texture = new MGG_Texture();
    texture->target = GL_TEXTURE_2D;
    texture->type = MGTextureType::_2D;
    texture->format = MGSurfaceFormat::Single;
    texture->glFormat = { ToDepthInternal(depthFormat), format, type, false };
    texture->width = width;
    texture->height = height;
    texture->depth = 1;
    texture->mipmaps = 1;
    texture->slices = 1;
    texture->renderTarget = true;
    texture->samples = 1;
    texture->depthFormat = depthFormat;
    texture->independentDepthStencil = true;

    gl.GenTextures(1, &texture->handle);
    gl.BindTexture(GL_TEXTURE_2D, texture->handle);
    gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
    gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
    gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
    gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
    gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_BASE_LEVEL, 0);
    gl.TexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAX_LEVEL, 0);
    gl.TexImage2D(
        GL_TEXTURE_2D,
        0,
        texture->glFormat.internalFormat,
        width,
        height,
        0,
        texture->glFormat.format,
        texture->glFormat.type,
        nullptr);
    return texture;
}

void MGG_Texture_Destroy(MGG_GraphicsDevice* device, MGG_Texture* texture)
{
    if (!texture) return;
    if (device)
    {
        for (auto& stage : device->textures)
        {
            for (auto& bound : stage)
            {
                if (bound == texture)
                    bound = nullptr;
            }
        }
        for (mgint index = 0; index < device->renderTargetCount; ++index)
        {
            if (device->renderTargets[index] == texture)
                device->renderTargets[index] = nullptr;
        }
        if (device->explicitDepthStencilTarget == texture)
        {
            device->explicitDepthStencilTarget = nullptr;
            device->explicitRenderPass = false;
        }
    }
    if (MakeCurrent(device))
    {
        if (texture->depthBuffer) gl.DeleteRenderbuffers(1, &texture->depthBuffer);
        if (texture->msaaColor) gl.DeleteRenderbuffers(1, &texture->msaaColor);
        if (texture->owned && texture->handle) gl.DeleteTextures(1, &texture->handle);
    }
    delete texture;
}

void MGG_Texture_SetData(MGG_GraphicsDevice* device, MGG_Texture* texture, mgint level, mgint slice, mgint x, mgint y, mgint z, mgint width, mgint height, mgint depth, mgbyte* data, mgint dataBytes)
{
    if (!MakeCurrent(device) || !texture || !data)
        return;
    width = width > 0 ? width : std::max(1, texture->width >> level);
    height = height > 0 ? height : std::max(1, texture->height >> level);
    depth = depth > 0 ? depth : std::max(1, texture->depth >> level);
    gl.BindTexture(texture->target, texture->handle);
    gl.PixelStorei(GL_UNPACK_ALIGNMENT, 1);
    const GLenum target = TextureSliceTarget(texture, slice);
    if (texture->glFormat.compressed)
    {
        const size_t definedIndex = static_cast<size_t>(level) +
            static_cast<size_t>(texture->type == MGTextureType::Cube ? slice : 0) * texture->mipmaps;
        const bool wholeLevel = x == 0 && y == 0 &&
            width == std::max(1, texture->width >> level) &&
            height == std::max(1, texture->height >> level);
        if (wholeLevel && !texture->compressedDefined[definedIndex])
        {
            gl.CompressedTexImage2D(target, level, texture->glFormat.internalFormat, width, height, 0, dataBytes, data);
            texture->compressedDefined[definedIndex] = true;
        }
        else if (texture->compressedDefined[definedIndex])
        {
            gl.CompressedTexSubImage2D(target, level, x, y, width, height, texture->glFormat.internalFormat, dataBytes, data);
        }
        else
        {
            std::fprintf(stderr, "Native OpenGL rejected a partial upload to an undefined compressed mip level.\n");
        }
    }
    else if (texture->type == MGTextureType::_3D)
        gl.TexSubImage3D(target, level, x, y, z, width, height, depth, texture->glFormat.format, texture->glFormat.type, data);
    else if (texture->target == GL_TEXTURE_2D_ARRAY)
        gl.TexSubImage3D(target, level, x, y, slice, width, height, 1, texture->glFormat.format, texture->glFormat.type, data);
    else
        gl.TexSubImage2D(target, level, x, y, width, height, texture->glFormat.format, texture->glFormat.type, data);
    if (level == 0 && texture->mipmaps > 1)
        gl.GenerateMipmap(texture->target);
}

void MGG_Texture_GetData(MGG_GraphicsDevice* device, MGG_Texture* texture, mgint level, mgint slice, mgint x, mgint y, mgint z, mgint width, mgint height, mgint depth, mgbyte* data, mgint dataBytes)
{
    if (!MakeCurrent(device) || !texture || !data || texture->glFormat.compressed)
        return;
    width = width > 0 ? width : std::max(1, texture->width >> level);
    height = height > 0 ? height : std::max(1, texture->height >> level);
    depth = depth > 0 ? depth : std::max(1, texture->depth >> level);
    const bool whole = x == 0 && y == 0 && width == std::max(1, texture->width >> level) && height == std::max(1, texture->height >> level);
#if !defined(MG_ANDROID) && !defined(MG_IOS)
    if (gl.GetTexImage && whole && texture->target != GL_TEXTURE_2D_ARRAY &&
        (texture->type != MGTextureType::_3D || (z == 0 && depth == std::max(1, texture->depth >> level))))
    {
        gl.BindTexture(texture->target, texture->handle);
        gl.PixelStorei(GL_PACK_ALIGNMENT, 1);
        gl.GetTexImage(TextureSliceTarget(texture, slice), level, texture->glFormat.format, texture->glFormat.type, data);
        return;
    }
#endif
    gl.BindFramebuffer(GL_READ_FRAMEBUFFER, device->resolveReadFramebuffer);
    const int layers = texture->type == MGTextureType::_3D ? depth : 1;
    const size_t layerBytes = layers > 0 ? static_cast<size_t>(dataBytes) / layers : 0;
    for (int layer = 0; layer < layers; ++layer)
    {
        const int attachmentSlice = texture->type == MGTextureType::_3D ? z + layer : slice;
        AttachTarget(GL_COLOR_ATTACHMENT0, texture, attachmentSlice, false, GL_READ_FRAMEBUFFER);
        if (gl.CheckFramebufferStatus(GL_READ_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE)
        {
            std::fprintf(stderr, "Native OpenGL texture readback framebuffer is incomplete.\n");
            return;
        }
        gl.ReadPixels(x, y, width, height, texture->glFormat.format, texture->glFormat.type,
            data + static_cast<size_t>(layer) * layerBytes);
    }
}

MGG_InputLayout* MGG_InputLayout_Create(MGG_GraphicsDevice*, MGG_Shader*, mgint* strides, mgint streamCount, MGG_InputElement* elements, mgint elementCount)
{
    if (!strides || !elements || streamCount < 0 || elementCount < 0)
        return nullptr;
    auto* layout = new MGG_InputLayout();
    layout->strides.assign(strides, strides + streamCount);
    layout->elements.assign(elements, elements + elementCount);
    return layout;
}

void MGG_InputLayout_Destroy(MGG_GraphicsDevice* device, MGG_InputLayout* layout)
{
    if (device && device->inputLayout == layout)
        device->inputLayout = nullptr;
    delete layout;
}

MGG_Shader* MGG_Shader_Create(MGG_GraphicsDevice* device, MGShaderStage stage, mgbyte* bytecode, mgint sizeInBytes)
{
    if (!MakeCurrent(device) || !bytecode || sizeInBytes <= 0)
        return nullptr;
    Reader reader{bytecode, static_cast<size_t>(sizeInBytes)};
    const uint32_t magic = reader.U32();
    const uint32_t version = reader.U32();
    const uint32_t profile = reader.U32();
    const uint32_t payloadStage = reader.U32();
    const std::string source = reader.String();
    if (magic != MGGLPayloadMagic || version != MGGLPayloadVersion || profile != MGGLShaderProfile || payloadStage != static_cast<uint32_t>(stage))
    {
        std::fprintf(stderr, "Native OpenGL rejected incompatible shader payload (magic=%08x version=%u profile=%u stage=%u).\n", magic, version, profile, payloadStage);
        return nullptr;
    }

    auto* shader = new MGG_Shader();
    shader->stage = stage;
    const uint32_t uniformCount = reader.U32();
    for (uint32_t index = 0; index < uniformCount; ++index)
        shader->uniformBlocks.push_back({reader.String(), static_cast<int>(reader.U32()), -1});
    const uint32_t samplerCount = reader.U32();
    for (uint32_t index = 0; index < samplerCount; ++index)
    {
        const std::string name = reader.String();
        const int textureSlot = static_cast<int>(reader.U32());
        const int samplerSlot = static_cast<int>(reader.U32());
        shader->samplers.push_back({name, textureSlot, samplerSlot});
    }
    if (!reader.Valid())
    {
        delete shader;
        return nullptr;
    }

    shader->handle = gl.CreateShader(stage == MGShaderStage::Vertex ? GL_VERTEX_SHADER : GL_FRAGMENT_SHADER);
    const GLchar* sourcePointer = source.c_str();
    const GLint sourceLength = static_cast<GLint>(source.size());
    gl.ShaderSource(shader->handle, 1, &sourcePointer, &sourceLength);
    gl.CompileShader(shader->handle);
    GLint compiled = GL_FALSE;
    gl.GetShaderiv(shader->handle, GL_COMPILE_STATUS, &compiled);
    if (!compiled)
    {
        PrintShaderError(shader->handle);
        gl.DeleteShader(shader->handle);
        delete shader;
        return nullptr;
    }
    return shader;
}

void MGG_Shader_Destroy(MGG_GraphicsDevice* device, MGG_Shader* shader)
{
    if (!shader) return;
    if (MakeCurrent(device))
    {
        for (auto program = device->programs.begin(); program != device->programs.end();)
        {
            if (program->first.vertex == shader || program->first.pixel == shader)
            {
                if (device->currentProgram == &program->second)
                    device->currentProgram = nullptr;
                gl.DeleteProgram(program->second.handle);
                program = device->programs.erase(program);
            }
            else
                ++program;
        }
        for (auto& bound : device->shaders)
        {
            if (bound == shader)
                bound = nullptr;
        }
        if (shader->handle)
            gl.DeleteShader(shader->handle);
    }
    delete shader;
}

MGG_OcclusionQuery* MGG_OcclusionQuery_Create(MGG_GraphicsDevice* device)
{
    if (!MakeCurrent(device)) return nullptr;
    auto* query = new MGG_OcclusionQuery();
    gl.GenQueries(1, &query->handle);
    return query;
}

void MGG_OcclusionQuery_Destroy(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query)
{
    if (!query) return;
    if (MakeCurrent(device) && query->handle) gl.DeleteQueries(1, &query->handle);
    delete query;
}

void MGG_OcclusionQuery_Begin(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query)
{
    if (MakeCurrent(device) && query && !query->active)
    {
#if defined(MG_ANDROID) || defined(MG_IOS)
        gl.BeginQuery(GL_ANY_SAMPLES_PASSED, query->handle);
#else
        gl.BeginQuery(GL_SAMPLES_PASSED, query->handle);
#endif
        query->active = true;
    }
}

void MGG_OcclusionQuery_End(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query)
{
    if (MakeCurrent(device) && query && query->active)
    {
#if defined(MG_ANDROID) || defined(MG_IOS)
        gl.EndQuery(GL_ANY_SAMPLES_PASSED);
#else
        gl.EndQuery(GL_SAMPLES_PASSED);
#endif
        query->active = false;
    }
}

mgbyte MGG_OcclusionQuery_GetResult(MGG_GraphicsDevice* device, MGG_OcclusionQuery* query, mgint& pixelCount)
{
    pixelCount = 0;
    if (!MakeCurrent(device) || !query)
        return false;
    GLuint available = GL_FALSE;
    gl.GetQueryObjectuiv(query->handle, GL_QUERY_RESULT_AVAILABLE, &available);
    if (!available)
        return false;
    GLuint result = 0;
    gl.GetQueryObjectuiv(query->handle, GL_QUERY_RESULT, &result);
    pixelCount = static_cast<mgint>(std::min(result, static_cast<GLuint>(INT32_MAX)));
    return true;
}
