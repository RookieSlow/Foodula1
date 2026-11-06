using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Foodula1.Tests.PlayMode
{
    /// <summary>
    /// Keeps a real runtime test visible in Test Runner's PlayMode tab.
    /// This is intentionally scene-independent so discovery can be verified
    /// before feature-specific PlayMode fixtures are added.
    /// </summary>
    public sealed class PlayModeAssemblySmokeTests
    {
        [UnityTest]
        public IEnumerator RuntimeTestAssemblyIsDiscoverable()
        {
            yield return null;
            Assert.IsTrue(Application.isPlaying, "The test must execute in a Unity PlayMode player context.");
        }
    }
}
