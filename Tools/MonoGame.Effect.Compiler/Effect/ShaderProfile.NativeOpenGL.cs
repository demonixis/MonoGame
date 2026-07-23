// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Effect.Compiler.Effect.Spirv;
using MonoGame.Effect.TPGParser;
using MonoGame.Tool;

namespace MonoGame.Effect
{
    internal abstract class NativeOpenGLShaderProfile : ShaderProfile
    {
        internal const uint PayloadMagic = 0x314C474D; // MGL1
        internal const uint PayloadVersion = 1;
        private const int TextureSlotOffset = 32;
        private const int SamplerSlotOffset = 64;

        private readonly int _glslVersion;
        private readonly bool _isEs;

        protected NativeOpenGLShaderProfile(string name, byte formatId, int glslVersion, bool isEs)
            : base(name, formatId)
        {
            _glslVersion = glslVersion;
            _isEs = isEs;
        }

        internal override void AddMacros(Dictionary<string, string> macros)
        {
            macros.Add("SM6", "1");
            macros.Add("OPENGL", "1");
            macros.Add("NATIVE_OPENGL", "1");
            if (_isEs)
                macros.Add("GLES", "1");
            macros.Add(Name.ToUpperInvariant(), "1");
        }

        internal override void ValidateShaderModels(PassInfo pass)
        {
            if (!string.IsNullOrEmpty(pass.vsFunction) && pass.vsModel != "vs_6_0")
                throw new Exception($"Invalid {Name} vertex profile '{pass.vsModel}'! Requires vs_6_0.");
            if (!string.IsNullOrEmpty(pass.psFunction) && pass.psModel != "ps_6_0")
                throw new Exception($"Invalid {Name} pixel profile '{pass.psModel}'! Requires ps_6_0.");
        }

        internal override IEnumerable<string> GetBuildDependencies()
        {
            var tool = ResolveSpirvCrossTool();
            if (Path.IsPathRooted(tool) && File.Exists(tool))
                yield return tool;
        }

        internal override ShaderData CreateShader(
            ShaderResult shaderResult,
            string shaderFunction,
            string shaderProfile,
            bool isVertexShader,
            EffectObject effect,
            ref string errorsAndWarnings)
        {
            var outputPath = Path.GetDirectoryName(shaderResult.OutputFilePath);
            var sourceFileName = Path.GetFileNameWithoutExtension(shaderResult.FilePath) + "." + shaderFunction;
            var intermediateDirectory = outputPath;
            var hlslFile = Path.Combine(intermediateDirectory, sourceFileName + ".nativegl.hlsl");
            var spirvFile = Path.Combine(intermediateDirectory, sourceFileName + ".nativegl.spv");
            var dxcReflectionFile = Path.Combine(intermediateDirectory, sourceFileName + ".nativegl.dxc.txt");
            var glslFile = Path.Combine(intermediateDirectory, sourceFileName + ".nativegl.glsl");
            var glslReflectionFile = Path.Combine(intermediateDirectory, sourceFileName + ".nativegl.json");
            var cleanup = new[] { hlslFile, spirvFile, dxcReflectionFile, glslFile, glslReflectionFile };

            try
            {
                Directory.CreateDirectory(intermediateDirectory);
                var shaderContent = Regex.Replace(
                    shaderResult.FileContent,
                    @"(?<=\s+)" + Regex.Escape(shaderFunction) + @"(?=\s*[(])",
                    "main");
                File.WriteAllText(hlslFile, shaderContent);

                var toolArguments = BuildDxcArguments(
                    hlslFile,
                    spirvFile,
                    dxcReflectionFile,
                    isVertexShader,
                    shaderResult.Debug,
                    includeReflection: true);

                var result = Dxc.Run(toolArguments, out var stdout, out var stderr);
                errorsAndWarnings += stderr;
                if (result != 0)
                {
                    errorsAndWarnings += $"DXC returned error code '{result}' while building {Name}.\n{stdout}";
                    throw new ShaderCompilerException();
                }

                var reflectionInfo = SpirvReflectionInfo.Parse(File.ReadAllLines(dxcReflectionFile));

                toolArguments = BuildDxcArguments(
                    hlslFile,
                    spirvFile,
                    dxcReflectionFile,
                    isVertexShader,
                    shaderResult.Debug,
                    includeReflection: false);
                result = Dxc.Run(toolArguments, out stdout, out stderr);
                errorsAndWarnings += stderr;
                if (result != 0)
                {
                    errorsAndWarnings += $"DXC returned error code '{result}' while stripping {Name} reflection.\n{stdout}";
                    throw new ShaderCompilerException();
                }

                RunSpirvCross(spirvFile, glslFile, glslReflectionFile, isVertexShader, out stdout, out stderr);
                errorsAndWarnings += stderr;

                var bytecode = File.ReadAllBytes(spirvFile);
                foreach (var existing in effect.Shaders)
                {
                    if (bytecode.SequenceEqual(existing.Bytecode))
                        return existing;
                }

                var converterReflection = JsonSerializer.Deserialize<ConverterReflection>(
                    File.ReadAllText(glslReflectionFile),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (converterReflection == null || converterReflection.Schema != 1)
                    throw new ShaderCompilerException("mgfx-spvc returned an unsupported reflection schema.");

                var shaderData = new ShaderData(isVertexShader, effect.Shaders.Count, bytecode);
                var variables = reflectionInfo.Variables
                    .Where(variable => variable.DescriptorSet.HasValue && variable.BindingSlot.HasValue)
                    .ToArray();

                shaderData._cbuffers = BuildConstantBuffers(effect, variables, converterReflection, errorsAndWarnings);
                shaderData._samplers = BuildSamplers(shaderResult, variables, converterReflection, ref errorsAndWarnings);
                shaderData._attributes = BuildAttributes(reflectionInfo, isVertexShader, ref errorsAndWarnings);
                shaderData.ShaderCode = BuildPayload(
                    isVertexShader,
                    File.ReadAllText(glslFile),
                    converterReflection);

                effect.Shaders.Add(shaderData);
                return shaderData;
            }
            finally
            {
                if (Environment.GetEnvironmentVariable("MGFX_KEEP_INTERMEDIATES") != "1")
                {
                    foreach (var file in cleanup)
                        ExternalTool.DeleteFile(file);
                }
            }
        }

        private string BuildDxcArguments(
            string input,
            string output,
            string reflection,
            bool isVertexShader,
            bool debug,
            bool includeReflection)
        {
            var arguments = new StringBuilder();
            arguments.Append("-nologo -spirv -fvk-use-gl-layout ");
            if (includeReflection)
                arguments.Append("-fspv-reflect ");
            arguments.Append($"-fvk-t-shift {TextureSlotOffset} all ");
            arguments.Append($"-fvk-s-shift {SamplerSlotOffset} all ");
            if (!isVertexShader)
                arguments.Append("-auto-binding-space 1 ");
            arguments.Append($"-T {(isVertexShader ? "vs" : "ps")}_6_0 -E main ");
            if (isVertexShader)
                arguments.Append("-fvk-use-dx-position-w ");
            if (debug)
                arguments.Append("-Zi ");
            if (includeReflection)
                arguments.Append($"-Fc \"{reflection}\" ");
            arguments.Append($"-Fo \"{output}\" \"{input}\"");
            return arguments.ToString();
        }

        private void RunSpirvCross(
            string input,
            string output,
            string reflection,
            bool isVertexShader,
            out string stdout,
            out string stderr)
        {
            var tool = ResolveSpirvCrossTool();
            var arguments = $"--input \"{input}\" --output \"{output}\" --reflection \"{reflection}\" --version {_glslVersion} --stage {(isVertexShader ? "vertex" : "pixel")}";
            if (_isEs)
                arguments += " --es";

            var result = ExternalTool.Run(tool, arguments, out stdout, out stderr);
            if (result != 0)
                throw new ShaderCompilerException($"mgfx-spvc returned error code '{result}'.\n{stderr}\n{stdout}");
        }

        private static string ResolveSpirvCrossTool()
        {
            var configured = Environment.GetEnvironmentVariable("MGFX_SPVC_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var executable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "mgfx-spvc.exe" : "mgfx-spvc";
            var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            var system = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "macosx";
            var runtimeIdentifier = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? $"win-{architecture}"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? $"linux-{architecture}" : $"osx-{architecture}";

            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (var depth = 0; directory != null && depth < 8; ++depth, directory = directory.Parent)
            {
                var candidates = new[]
                {
                    Path.Combine(directory.FullName, "Artifacts", "native", "mgfx-spvc", system, "Release", executable),
                    Path.Combine(directory.FullName, "Artifacts", "native", "mgfx-spvc", system, architecture, "Release", executable),
                    Path.Combine(directory.FullName, "runtimes", runtimeIdentifier, "native", executable),
                    Path.Combine(directory.FullName, executable),
                };
                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        EnsureExecutable(candidate);
                        return candidate;
                    }
                }
            }

            return "mgfx-spvc";
        }

        private static void EnsureExecutable(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            try
            {
                var mode = File.GetUnixFileMode(path);
                mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                File.SetUnixFileMode(path, mode);
            }
            catch (PlatformNotSupportedException)
            {
            }
        }

        private static int[] BuildConstantBuffers(
            EffectObject effect,
            SpirvVariable[] variables,
            ConverterReflection reflection,
            string errorsAndWarnings)
        {
            var result = new List<int>();
            foreach (var block in reflection.UniformBuffers.OrderBy(buffer => buffer.Binding))
            {
                var variable = variables.FirstOrDefault(candidate =>
                    candidate.BindingSlot == block.Binding && candidate.Pointer?.PointerType?.Type == SpirvType.Struct);
                if (variable == null)
                    throw new ShaderCompilerException($"Unable to match GLSL uniform block '{block.Name}' at binding {block.Binding}.");

                var constantBuffer = ConstantBufferData.BuildFromSpirvStruct((SpirvTypeStruct)variable.Pointer.PointerType);
                if (constantBuffer.Size == 0)
                    continue;

                var match = effect.ConstantBuffers.FindIndex(existing => existing.SameAs(constantBuffer));
                if (match < 0)
                {
                    match = effect.ConstantBuffers.Count;
                    effect.ConstantBuffers.Add(constantBuffer);
                }
                result.Add(match);
            }
            return result.ToArray();
        }

        private static ShaderData.Sampler[] BuildSamplers(
            ShaderResult shaderResult,
            SpirvVariable[] variables,
            ConverterReflection reflection,
            ref string errorsAndWarnings)
        {
            var result = new List<ShaderData.Sampler>();
            var combinationsByTexture = reflection.CombinedSamplers
                .GroupBy(combination => combination.TextureBinding)
                .ToDictionary(group => group.Key, group => group.Count());

            foreach (var combination in reflection.CombinedSamplers)
            {
                var image = FindImage(variables, combination.TextureBinding);
                var samplerVariable = variables.FirstOrDefault(candidate =>
                    candidate.BindingSlot == combination.SamplerBinding &&
                    candidate.Pointer?.PointerType?.Type == SpirvType.Sampler);
                if (image == null || samplerVariable == null)
                    throw new ShaderCompilerException($"Unable to match combined sampler '{combination.Name}'.");

                var sampler = new ShaderData.Sampler
                {
                    textureSlot = combination.TextureBinding - TextureSlotOffset,
                    samplerSlot = combination.SamplerBinding - SamplerSlotOffset,
                    samplerName = samplerVariable.Name,
                    parameterName = combinationsByTexture[combination.TextureBinding] == 1
                        ? image.Name
                        : $"{samplerVariable.Name}+{image.Name}",
                    type = ToSamplerType((SpirvTypeImage)image.Pointer.PointerType),
                };

                if (!shaderResult.ShaderInfo.SamplerStates.TryGetValue(samplerVariable.Name, out var samplerStateInfo))
                {
                    errorsAndWarnings += $"Could not find sampler state info for sampler '{samplerVariable.Name}'; using defaults.\n";
                    samplerStateInfo = new SamplerStateInfo();
                }
                sampler.state = samplerStateInfo.State;
                result.Add(sampler);
            }

            foreach (var reflectedImage in reflection.Images)
            {
                var image = FindImage(variables, reflectedImage.TextureBinding);
                if (image == null)
                    throw new ShaderCompilerException($"Unable to match standalone image '{reflectedImage.Name}'.");
                result.Add(new ShaderData.Sampler
                {
                    textureSlot = reflectedImage.TextureBinding - TextureSlotOffset,
                    samplerSlot = -1,
                    samplerName = string.Empty,
                    parameterName = image.Name,
                    type = ToSamplerType((SpirvTypeImage)image.Pointer.PointerType),
                    state = null,
                });
            }

            return result.ToArray();
        }

        private static SpirvVariable FindImage(SpirvVariable[] variables, int binding)
        {
            return variables.FirstOrDefault(candidate =>
                candidate.BindingSlot == binding && candidate.Pointer?.PointerType?.Type == SpirvType.Image);
        }

        private static MojoShader.MOJOSHADER_samplerType ToSamplerType(SpirvTypeImage image)
        {
            return image.Dimensionality switch
            {
                ImageDimensionality.OneD => MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_1D,
                ImageDimensionality.ThreeD => MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_VOLUME,
                ImageDimensionality.Cube => MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_CUBE,
                _ => MojoShader.MOJOSHADER_samplerType.MOJOSHADER_SAMPLER_2D,
            };
        }

        private static ShaderData.Attribute[] BuildAttributes(
            SpirvReflectionInfo reflectionInfo,
            bool isVertexShader,
            ref string errorsAndWarnings)
        {
            if (!isVertexShader)
                return Array.Empty<ShaderData.Attribute>();

            var attributes = new List<ShaderData.Attribute>();
            foreach (var input in reflectionInfo.Input.OrderBy(candidate => candidate.Location))
            {
                var semantic = input.HlslSemantic ?? input.Id.Replace("%in_var_", string.Empty);
                var match = Regex.Match(semantic, @"(\D+)(\d+)?");
                var semanticIndex = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
                var usage = ToVertexUsage(match.Groups[1].Value, ref errorsAndWarnings);

                uint locationCount = 1;
                if (input.Pointer?.PointerType is SpirvTypeArray array)
                {
                    locationCount = array.Length;
                    if (array.ElementType is SpirvTypeMatrix arrayMatrix)
                        locationCount *= arrayMatrix.Columns;
                }
                else if (input.Pointer?.PointerType is SpirvTypeMatrix matrix)
                {
                    locationCount = matrix.Columns;
                }

                for (var locationIndex = 0; locationIndex < locationCount; ++locationIndex)
                {
                    attributes.Add(new ShaderData.Attribute
                    {
                        usage = usage,
                        index = semanticIndex + locationIndex,
                        location = (int)input.Location + locationIndex,
                        name = string.Empty,
                    });
                }
            }
            return attributes.ToArray();
        }

        private static VertexElementUsage ToVertexUsage(string semantic, ref string errorsAndWarnings)
        {
            switch (semantic.ToUpperInvariant())
            {
                case "SV_POSITION":
                case "POSITION": return VertexElementUsage.Position;
                case "COLOR": return VertexElementUsage.Color;
                case "NORMAL": return VertexElementUsage.Normal;
                case "TANGENT": return VertexElementUsage.Tangent;
                case "BINORMAL": return VertexElementUsage.Binormal;
                case "BLENDINDICES": return VertexElementUsage.BlendIndices;
                case "BLENDWEIGHT": return VertexElementUsage.BlendWeight;
                case "DEPTH": return VertexElementUsage.Depth;
                case "FOG": return VertexElementUsage.Fog;
                case "POINTSIZE": return VertexElementUsage.PointSize;
                case "TESSELLATEFACTOR": return VertexElementUsage.TessellateFactor;
                case "TEXCOORD": return VertexElementUsage.TextureCoordinate;
                default:
                    errorsAndWarnings += $"Unknown vertex shader input semantic '{semantic}'; defaulting to TEXCOORD.\n";
                    return VertexElementUsage.TextureCoordinate;
            }
        }

        private byte[] BuildPayload(bool isVertexShader, string source, ConverterReflection reflection)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(PayloadMagic);
            writer.Write(PayloadVersion);
            writer.Write((uint)FormatId);
            writer.Write(isVertexShader ? 0u : 1u);
            WriteString(writer, source);

            var uniformBuffers = reflection.UniformBuffers.OrderBy(buffer => buffer.Binding).ToArray();
            writer.Write((uint)uniformBuffers.Length);
            for (var index = 0; index < uniformBuffers.Length; ++index)
            {
                WriteString(writer, uniformBuffers[index].Name);
                writer.Write((uint)index);
            }

            writer.Write((uint)(reflection.CombinedSamplers.Length + reflection.Images.Length));
            foreach (var sampler in reflection.CombinedSamplers)
            {
                WriteString(writer, sampler.Name);
                writer.Write(sampler.TextureBinding - TextureSlotOffset);
                writer.Write(sampler.SamplerBinding - SamplerSlotOffset);
            }
            foreach (var image in reflection.Images)
            {
                WriteString(writer, image.Name);
                writer.Write(image.TextureBinding - TextureSlotOffset);
                writer.Write(-1);
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }

        private sealed class ConverterReflection
        {
            public int Schema { get; set; }
            public UniformBufferReflection[] UniformBuffers { get; set; } = Array.Empty<UniformBufferReflection>();
            public CombinedSamplerReflection[] CombinedSamplers { get; set; } = Array.Empty<CombinedSamplerReflection>();
            public ImageReflection[] Images { get; set; } = Array.Empty<ImageReflection>();
        }

        private sealed class UniformBufferReflection
        {
            public string Name { get; set; } = string.Empty;
            public int Binding { get; set; }
        }

        private sealed class CombinedSamplerReflection
        {
            public string Name { get; set; } = string.Empty;
            public int TextureBinding { get; set; }
            public int SamplerBinding { get; set; }
        }

        private sealed class ImageReflection
        {
            public string Name { get; set; } = string.Empty;
            public int TextureBinding { get; set; }
        }
    }

    internal sealed class OpenGLES30ShaderProfile : NativeOpenGLShaderProfile
    {
        public OpenGLES30ShaderProfile() : base("OpenGLES_3_0", 82, 300, true) { }
    }

    internal sealed class OpenGL41ShaderProfile : NativeOpenGLShaderProfile
    {
        public OpenGL41ShaderProfile() : base("OpenGL_4_1", 84, 410, false) { }
    }
}
