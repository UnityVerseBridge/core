using UnityEngine;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Transform 관련 확장 메서드
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Transform의 전체 계층 경로를 반환
        /// </summary>
        public static string GetPath(this Transform transform)
        {
            if (transform == null) return "null";
            
            string path = transform.name;
            Transform parent = transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
    }
}