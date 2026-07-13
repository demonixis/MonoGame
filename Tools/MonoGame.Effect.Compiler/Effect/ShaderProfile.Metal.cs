// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace MonoGame.Effect
{
    class MetalShaderProfile : DirectX12ShaderProfile
    {
        private const uint MetalPayloadMagic = 0x324C544D; // MTL2
        private const uint MetalUniversalPayloadMagic = 0x334C544D; // MTL3

        private readonly string[] _converterTargets;

        public MetalShaderProfile()
            : this("Metal", "--deployment-os=macOS --minimum-os-build-version=15.0.0")
        {
        }

        protected MetalShaderProfile(string name, params string[] converterTargets)
            : base(name, 81)
        {
            _converterTargets = converterTargets;
        }

        internal override void AddMacros(Dictionary<string, string> macros)
        {
            base.AddMacros(macros);
            macros.Add("METAL", "1");
        }

        internal override void ValidateShaderModels(PassInfo pass)
        {
            if (!string.IsNullOrEmpty(pass.vsFunction) && pass.vsModel != "vs_6_0")
                throw new Exception($"Invalid Metal vertex profile '{pass.vsModel}'! Requires vs_6_0.");

            if (!string.IsNullOrEmpty(pass.psFunction) && pass.psModel != "ps_6_0")
                throw new Exception($"Invalid Metal pixel profile '{pass.psModel}'! Requires ps_6_0.");
        }

        protected override byte[] TransformBytecode(
            byte[] dxil,
            string shaderFunction,
            bool isVertexShader,
            ref string errorsAndWarnings)
        {
            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "mgfx-metal-" + Guid.NewGuid().ToString("N"));
            var dxilPath = Path.Combine(temporaryDirectory, isVertexShader ? "shader.vs.dxil" : "shader.ps.dxil");

            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                File.WriteAllBytes(dxilPath, dxil);

                byte[] reflection = null;
                var metalLibraries = new List<byte[]>(_converterTargets.Length);
                for (var index = 0; index < _converterTargets.Length; ++index)
                {
                    var metalLibraryPath = Path.Combine(temporaryDirectory, $"shader.{index}.metallib");
                    var reflectionPath = Path.Combine(temporaryDirectory, $"shader.{index}.json");

                    string stdout;
                    string stderr;
                    var result = ExternalTool.Run(
                        "metal-shaderconverter",
                        $"\"{dxilPath}\" -o \"{metalLibraryPath}\" --output-reflection-file \"{reflectionPath}\" {_converterTargets[index]}",
                        out stdout,
                        out stderr,
                        workingDirectory: temporaryDirectory);

                    errorsAndWarnings += stdout;
                    errorsAndWarnings += stderr;
                    if (result != 0 || !File.Exists(metalLibraryPath) || !File.Exists(reflectionPath))
                    {
                        errorsAndWarnings +=
                            $"Metal Shader Converter failed for target '{_converterTargets[index]}'. " +
                            "Install Apple's metal-shaderconverter 4.0 and ensure it is available on PATH.\n";
                        throw new ShaderCompilerException();
                    }

                    var targetReflection = File.ReadAllBytes(reflectionPath);
                    if (reflection == null)
                        reflection = targetReflection;
                    else if (!reflection.AsSpan().SequenceEqual(targetReflection))
                    {
                        errorsAndWarnings +=
                            "Metal device and simulator reflection layouts differ; a universal iOS payload cannot be produced.\n";
                        throw new ShaderCompilerException();
                    }

                    metalLibraries.Add(File.ReadAllBytes(metalLibraryPath));
                }

                var entryPoint = Encoding.UTF8.GetBytes(shaderFunction);
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(metalLibraries.Count == 1 ? MetalPayloadMagic : MetalUniversalPayloadMagic);
                    writer.Write(entryPoint.Length);
                    writer.Write(reflection.Length);
                    if (metalLibraries.Count > 1)
                        writer.Write(metalLibraries[0].Length);
                    writer.Write(entryPoint);
                    writer.Write(reflection);
                    foreach (var metalLibrary in metalLibraries)
                        writer.Write(metalLibrary);
                    return stream.ToArray();
                }
            }
            catch (Exception ex) when (!(ex is ShaderCompilerException))
            {
                errorsAndWarnings += ex.Message + Environment.NewLine;
                errorsAndWarnings +=
                    "Metal effects require Apple's metal-shaderconverter 4.0 on macOS or Windows.\n";
                throw new ShaderCompilerException();
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temporaryDirectory))
                        Directory.Delete(temporaryDirectory, true);
                }
                catch
                {
                }
            }
        }
    }

    sealed class MetalMacOSShaderProfile : MetalShaderProfile
    {
        public MetalMacOSShaderProfile()
            : base("MetalMacOS", "--deployment-os=macOS --minimum-os-build-version=15.0.0")
        {
        }
    }

    sealed class MetaliOSShaderProfile : MetalShaderProfile
    {
        public MetaliOSShaderProfile()
            : base(
                "MetaliOS",
                "--deployment-os=iOS --minimum-os-build-version=18.0.0",
                "--deployment-os=iOSSimulator --minimum-os-build-version=18.0.0")
        {
        }
    }
}
