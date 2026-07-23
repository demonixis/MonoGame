// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

#include "spirv_glsl.hpp"

#include <cstdint>
#include <cctype>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace
{
    struct Options
    {
        std::string input;
        std::string output;
        std::string reflection;
        std::string stage;
        uint32_t version = 0;
        bool es = false;
    };

    std::string EscapeJson(const std::string& value)
    {
        std::ostringstream result;
        for (const unsigned char ch : value)
        {
            switch (ch)
            {
                case '"': result << "\\\""; break;
                case '\\': result << "\\\\"; break;
                case '\b': result << "\\b"; break;
                case '\f': result << "\\f"; break;
                case '\n': result << "\\n"; break;
                case '\r': result << "\\r"; break;
                case '\t': result << "\\t"; break;
                default:
                    if (ch < 0x20)
                    {
                        result << "\\u" << std::hex << std::setw(4) << std::setfill('0')
                               << static_cast<int>(ch) << std::dec;
                    }
                    else
                    {
                        result << static_cast<char>(ch);
                    }
                    break;
            }
        }
        return result.str();
    }

    Options ParseOptions(int argc, char** argv)
    {
        Options options;
        for (int index = 1; index < argc; ++index)
        {
            const std::string argument = argv[index];
            auto requireValue = [&]() -> std::string
            {
                if (++index >= argc)
                    throw std::runtime_error("Missing value after " + argument + ".");
                return argv[index];
            };

            if (argument == "--input")
                options.input = requireValue();
            else if (argument == "--output")
                options.output = requireValue();
            else if (argument == "--reflection")
                options.reflection = requireValue();
            else if (argument == "--version")
                options.version = static_cast<uint32_t>(std::stoul(requireValue()));
            else if (argument == "--stage")
                options.stage = requireValue();
            else if (argument == "--es")
                options.es = true;
            else
                throw std::runtime_error("Unknown argument: " + argument);
        }

        if (options.input.empty() || options.output.empty() || options.reflection.empty() || options.stage.empty())
            throw std::runtime_error("--input, --output, --reflection, and --stage are required.");
        if (options.stage != "vertex" && options.stage != "pixel")
            throw std::runtime_error("--stage must be vertex or pixel.");
        if (options.version != 300 && options.version != 320 && options.version != 410)
            throw std::runtime_error("--version must be 300, 320, or 410.");
        if (options.es != (options.version != 410))
            throw std::runtime_error("--es is required for versions 300/320 and forbidden for version 410.");

        return options;
    }

    std::vector<uint32_t> ReadSpirv(const std::string& path)
    {
        std::ifstream stream(path, std::ios::binary | std::ios::ate);
        if (!stream)
            throw std::runtime_error("Unable to open SPIR-V input: " + path);

        const std::streamsize size = stream.tellg();
        if (size <= 0 || (size % sizeof(uint32_t)) != 0)
            throw std::runtime_error("SPIR-V input is empty or not 32-bit aligned: " + path);

        stream.seekg(0, std::ios::beg);
        std::vector<uint32_t> words(static_cast<size_t>(size) / sizeof(uint32_t));
        if (!stream.read(reinterpret_cast<char*>(words.data()), size))
            throw std::runtime_error("Unable to read SPIR-V input: " + path);
        return words;
    }

    uint32_t GetBinding(spirv_cross::Compiler& compiler, spirv_cross::ID id)
    {
        if (!compiler.has_decoration(id, spv::DecorationBinding))
            return 0;
        return compiler.get_decoration(id, spv::DecorationBinding);
    }

    void FlattenDescriptorSets(spirv_cross::Compiler& compiler, const spirv_cross::ShaderResources& resources)
    {
        auto flatten = [&](const spirv_cross::SmallVector<spirv_cross::Resource>& list)
        {
            for (const auto& resource : list)
            {
                if (compiler.has_decoration(resource.id, spv::DecorationDescriptorSet))
                    compiler.set_decoration(resource.id, spv::DecorationDescriptorSet, 0);
            }
        };

        flatten(resources.uniform_buffers);
        flatten(resources.separate_images);
        flatten(resources.separate_samplers);
        flatten(resources.sampled_images);
    }

    std::string InterfaceName(spirv_cross::Compiler& compiler, const spirv_cross::Resource& resource)
    {
        std::string semantic;
        if (compiler.has_decoration(resource.id, spv::DecorationHlslSemanticGOOGLE))
            semantic = compiler.get_decoration_string(resource.id, spv::DecorationHlslSemanticGOOGLE);
        else
        {
            semantic = compiler.get_name(resource.id);
            const auto separator = semantic.find_last_of('.');
            if (separator != std::string::npos)
                semantic.erase(0, separator + 1);
        }

        for (char& character : semantic)
        {
            if (!std::isalnum(static_cast<unsigned char>(character)))
                character = '_';
        }
        return "mg_varying_" + semantic;
    }

    void NormalizeStageInterface(
        spirv_cross::Compiler& compiler,
        const spirv_cross::ShaderResources& resources,
        const std::string& stage)
    {
        // GLSL 4.10 and GLSL ES 3.00 link vertex outputs to fragment inputs by
        // name. DXC/SPIRV-Cross otherwise emits out_var_* and in_var_* names,
        // which only link reliably when explicit varying locations are honored
        // by a newer profile. Give both sides the same semantic-derived name.
        const auto& varyings = stage == "vertex" ? resources.stage_outputs : resources.stage_inputs;
        for (const auto& resource : varyings)
            compiler.set_name(resource.id, InterfaceName(compiler, resource));
    }

    void AddVertexPositionFixup(std::string& source)
    {
        const auto versionEnd = source.find('\n');
        const auto mainEnd = source.rfind("\n}");
        if (versionEnd == std::string::npos || mainEnd == std::string::npos || mainEnd <= versionEnd)
            throw std::runtime_error("Unable to inject the Native OpenGL vertex position fixup.");

        source.insert(versionEnd + 1, "\nuniform vec4 mg_posFixup;\n");
        const auto updatedMainEnd = source.rfind("\n}");
        source.insert(
            updatedMainEnd,
            "\n    gl_Position.y = gl_Position.y * mg_posFixup.y;"
            "\n    gl_Position.xy += mg_posFixup.zw * gl_Position.ww;");
    }
}

int main(int argc, char** argv)
{
    try
    {
        const Options options = ParseOptions(argc, argv);
        auto spirv = ReadSpirv(options.input);
        spirv_cross::CompilerGLSL compiler(spirv);

        auto resources = compiler.get_shader_resources();
        FlattenDescriptorSets(compiler, resources);
        NormalizeStageInterface(compiler, resources, options.stage);

        const std::string stagePrefix = options.stage == "vertex" ? "mg_vs_" : "mg_ps_";
        for (const auto& resource : resources.uniform_buffers)
        {
            const auto binding = std::to_string(GetBinding(compiler, resource.id));
            // OpenGL binds a uniform block by its block type name, not by the
            // instance name used to access its members in GLSL.  Keep both
            // names explicit and stage-qualified so vertex and pixel blocks
            // cannot merge during program linking.
            compiler.set_name(resource.base_type_id, stagePrefix + "block_" + binding);
            compiler.set_name(resource.id, stagePrefix + "ubo_" + binding);
        }
        for (const auto& resource : resources.separate_images)
            compiler.set_name(resource.id, stagePrefix + "texture_" + std::to_string(GetBinding(compiler, resource.id)));

        compiler.build_combined_image_samplers();
        for (const auto& sampler : compiler.get_combined_image_samplers())
        {
            const uint32_t imageBinding = GetBinding(compiler, sampler.image_id);
            const uint32_t samplerBinding = GetBinding(compiler, sampler.sampler_id);
            compiler.set_name(
                sampler.combined_id,
                stagePrefix + "texture_" + std::to_string(imageBinding) + "_sampler_" + std::to_string(samplerBinding));
            compiler.unset_decoration(sampler.combined_id, spv::DecorationBinding);
            compiler.unset_decoration(sampler.combined_id, spv::DecorationDescriptorSet);
        }

        spirv_cross::CompilerGLSL::Options glsl;
        glsl.version = options.version;
        glsl.es = options.es;
        glsl.vulkan_semantics = false;
        glsl.enable_420pack_extension = false;
        glsl.force_zero_initialized_variables = true;
        glsl.vertex.fixup_clipspace = options.stage == "vertex";
        compiler.set_common_options(glsl);

        std::string source = compiler.compile();
        if (options.stage == "vertex")
            AddVertexPositionFixup(source);
        if (!options.es)
        {
            const std::string versionDirective = "#version " + std::to_string(options.version);
            if (source.rfind(versionDirective, 0) == 0)
                source.replace(0, versionDirective.size(), versionDirective + " core");
        }
        resources = compiler.get_shader_resources();

        std::ofstream sourceStream(options.output, std::ios::binary);
        if (!sourceStream)
            throw std::runtime_error("Unable to open GLSL output: " + options.output);
        sourceStream.write(source.data(), static_cast<std::streamsize>(source.size()));

        std::ofstream reflection(options.reflection, std::ios::binary);
        if (!reflection)
            throw std::runtime_error("Unable to open reflection output: " + options.reflection);

        reflection << "{\n  \"schema\": 1,\n  \"uniformBuffers\": [";
        for (size_t index = 0; index < resources.uniform_buffers.size(); ++index)
        {
            const auto& resource = resources.uniform_buffers[index];
            if (index != 0)
                reflection << ',';
            reflection << "\n    {\"name\": \"" << EscapeJson(compiler.get_name(resource.base_type_id))
                       << "\", \"binding\": " << GetBinding(compiler, resource.id) << '}';
        }
        if (!resources.uniform_buffers.empty())
            reflection << '\n';

        reflection << "  ],\n  \"combinedSamplers\": [";
        const auto& combinedSamplers = compiler.get_combined_image_samplers();
        for (size_t index = 0; index < combinedSamplers.size(); ++index)
        {
            const auto& sampler = combinedSamplers[index];
            if (index != 0)
                reflection << ',';
            reflection << "\n    {\"name\": \"" << EscapeJson(compiler.get_name(sampler.combined_id))
                       << "\", \"textureBinding\": " << GetBinding(compiler, sampler.image_id)
                       << ", \"samplerBinding\": " << GetBinding(compiler, sampler.sampler_id) << '}';
        }
        if (!combinedSamplers.empty())
            reflection << '\n';

        reflection << "  ],\n  \"images\": [";
        size_t imageIndex = 0;
        for (const auto& resource : resources.separate_images)
        {
            bool isCombined = false;
            for (const auto& sampler : combinedSamplers)
            {
                if (sampler.image_id == resource.id)
                {
                    isCombined = true;
                    break;
                }
            }
            if (isCombined)
                continue;

            if (imageIndex++ != 0)
                reflection << ',';
            reflection << "\n    {\"name\": \"" << EscapeJson(compiler.get_name(resource.id))
                       << "\", \"textureBinding\": " << GetBinding(compiler, resource.id) << '}';
        }
        if (imageIndex != 0)
            reflection << '\n';

        reflection << "  ],\n  \"stageInputs\": [";
        for (size_t index = 0; index < resources.stage_inputs.size(); ++index)
        {
            const auto& resource = resources.stage_inputs[index];
            if (index != 0)
                reflection << ',';
            reflection << "\n    {\"name\": \"" << EscapeJson(compiler.get_name(resource.id))
                       << "\", \"location\": " << compiler.get_decoration(resource.id, spv::DecorationLocation);
            if (compiler.has_decoration(resource.id, spv::DecorationHlslSemanticGOOGLE))
            {
                reflection << ", \"semantic\": \""
                           << EscapeJson(compiler.get_decoration_string(resource.id, spv::DecorationHlslSemanticGOOGLE))
                           << '"';
            }
            reflection << '}';
        }
        if (!resources.stage_inputs.empty())
            reflection << '\n';
        reflection << "  ]\n}\n";

        return 0;
    }
    catch (const std::exception& exception)
    {
        std::cerr << "mgfx-spvc: " << exception.what() << '\n';
        return 1;
    }
}
