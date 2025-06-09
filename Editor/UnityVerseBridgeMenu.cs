using UnityEngine;
using UnityEditor;

namespace UnityVerseBridge.Core.Editor
{
    /// <summary>
    /// Unity 메뉴에 UnityVerseBridge 관련 유틸리티를 추가합니다.
    /// </summary>
    public static class UnityVerseBridgeMenu
    {
        [MenuItem("UnityVerseBridge/Open Settings")]
        public static void OpenSettings()
        {
            Debug.Log("UnityVerseBridge Settings - Coming Soon");
        }
        
        [MenuItem("UnityVerseBridge/Documentation")]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/your-repo/UnityVerseBridge");
        }
    }
}
