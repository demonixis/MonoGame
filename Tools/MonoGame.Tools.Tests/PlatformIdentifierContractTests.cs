// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using MonoGame.Framework.Utilities;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline
{
    [TestFixture]
    public sealed class PlatformIdentifierContractTests
    {
        [Test]
        public void ManagedContentPlatformOrderMatchesThePublishedXnbContract()
        {
            var writerIdentifiers = (char[])typeof(ContentWriter).GetField(
                "TargetPlatformIdentifiers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var readerIdentifiers = (List<char>)typeof(ContentManager).GetField(
                "targetPlatformIdentifiers", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            const string expectedWriterIdentifiers = "wxiadXnrP5OSbVGsUAIKEuL";
            const string expectedReaderIdentifiers = "wxiadXnrP5OSbVGsUAIKEuLWMmpvgl";

            Assert.AreEqual(expectedWriterIdentifiers, new string(writerIdentifiers));
            Assert.AreEqual(expectedReaderIdentifiers, new string(readerIdentifiers.ToArray()));
            Assert.AreEqual(16, (int)TargetPlatform.Switch2);
            Assert.AreEqual(17, (int)TargetPlatform.MacOSMetal);
            Assert.AreEqual(18, (int)TargetPlatform.iOSMetal);
            Assert.AreEqual(19, (int)TargetPlatform.AndroidVK);
            Assert.AreEqual(20, (int)TargetPlatform.AndroidNativeGLES);
            Assert.AreEqual(21, (int)TargetPlatform.iOSNativeGLES);
            Assert.AreEqual(22, (int)TargetPlatform.DesktopNativeGL);
        }

        [Test]
        public void ManagedAndNativePlatformEnumsKeepTheSamePublishedValues()
        {
            Assert.AreEqual(13, (int)MonoGamePlatform.NintendoSwitch2);
            Assert.AreEqual(14, (int)MonoGamePlatform.MacOS);

            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null &&
                   !File.Exists(Path.Combine(directory.FullName, "native", "monogame", "include", "api_enums.h")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory, "Could not locate the repository root from the test output directory.");
            var header = File.ReadAllText(Path.Combine(
                directory.FullName,
                "native",
                "monogame",
                "include",
                "api_enums.h"));

            StringAssert.Contains("NintendoSwitch2 = 13", header);
            StringAssert.Contains("MacOS = 14", header);
        }
    }
}
