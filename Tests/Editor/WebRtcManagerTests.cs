using NUnit.Framework;
using UnityEngine;

namespace UnityVerseBridge.Core.Editor.Tests
{
    public class WebRtcManagerTests
    {
        [Test]
        public void WebRtcManager_CanBeCreated()
        {
            var gameObject = new GameObject("TestWebRtc");
            var manager = gameObject.AddComponent<WebRtcManager>();
            
            Assert.IsNotNull(manager);
            
            Object.DestroyImmediate(gameObject);
        }
    }
}
