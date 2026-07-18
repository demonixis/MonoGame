// MonoGame - Copyright (C) MonoGame Foundation, Inc
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace MonoGame.Tests.ContentPipeline
{
    [TestFixture]
    public class AndroidVulkanContentTargetsTests
    {
        [Test]
        public void AndroidVulkanUsesAndroidAssetClassificationAndPrefix()
        {
            var path = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "Assets",
                "MonoGame.Content.Builder.targets");
            var document = XDocument.Load(path);

            var prefix = document.Descendants()
                .Single(element =>
                    element.Name.LocalName == "PlatformResourcePrefix" &&
                    (string)element.Attribute("Condition") == "'$(MonoGamePlatform)' == 'AndroidVK'");
            Assert.AreEqual("$(MonoAndroidAssetsPrefix)", prefix.Value);

            var androidAsset = document.Descendants()
                .Single(element =>
                    element.Name.LocalName == "Output" &&
                    (string)element.Attribute("ItemName") == "AndroidAsset" &&
                    (string)element.Attribute("Condition") == "'$(MonoGamePlatform)' == 'AndroidVK'");
            Assert.NotNull(androidAsset);
        }
    }
}
