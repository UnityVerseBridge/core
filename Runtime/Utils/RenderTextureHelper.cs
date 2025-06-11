using UnityEngine;
using UnityEngine.Rendering;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Helper class for creating compatible RenderTextures across different platforms
    /// Ensures compatibility with various graphics APIs and rendering pipelines
    /// </summary>
    public static class RenderTextureHelper
    {
        /// <summary>
        /// Creates a RenderTexture optimized for streaming with platform-specific compatibility
        /// </summary>
        /// <param name="width">Width of the texture</param>
        /// <param name="height">Height of the texture</param>
        /// <param name="name">Name of the texture</param>
        /// <param name="depthBits">Depth buffer bits (24 for URP compatibility)</param>
        /// <returns>Created RenderTexture or null if creation fails</returns>
        public static RenderTexture CreateCompatibleRenderTexture(int width, int height, string name = "StreamTexture", int depthBits = 24)
        {
            // Determine supported format based on graphics API
            RenderTextureFormat format = DetermineOptimalFormat();
            
            // Create RenderTexture with URP-compatible settings
            var rt = new RenderTexture(width, height, depthBits, format)
            {
                name = name,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                enableRandomWrite = false,
                useDynamicScale = false,
                vrUsage = VRTextureUsage.None, // Not for VR rendering, for streaming
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            
            // Set depth format for URP compatibility
            if (depthBits > 0)
            {
                rt.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.D24_UNorm_S8_UInt;
            }
            
            // Create the texture immediately
            if (!rt.Create())
            {
                Debug.LogError($"[RenderTextureHelper] Failed to create RenderTexture");
                return null;
            }
            
            // Log color space information
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                Debug.Log($"[RenderTextureHelper] Using Linear color space, sRGB: {rt.sRGB}");
            }
            
            Debug.Log($"[RenderTextureHelper] Created RenderTexture: {name}, Size: {width}x{height}, Format: {format}, GraphicsAPI: {SystemInfo.graphicsDeviceType}");
            
            return rt;
        }
        
        /// <summary>
        /// Ensures an existing RenderTexture is compatible with current platform requirements
        /// </summary>
        /// <param name="existing">Existing RenderTexture to check</param>
        /// <param name="width">Required width</param>
        /// <param name="height">Required height</param>
        /// <param name="requireDepthBuffer">Whether depth buffer is required</param>
        /// <returns>Compatible RenderTexture (may be newly created)</returns>
        public static RenderTexture EnsureCompatibility(RenderTexture existing, int width, int height, bool requireDepthBuffer = true)
        {
            if (existing == null)
            {
                return CreateCompatibleRenderTexture(width, height);
            }
            
            // Check if recreation is needed
            bool needsRecreation = false;
            
            if (!existing.IsCreated())
            {
                Debug.Log($"[RenderTextureHelper] RenderTexture not created, creating...");
                needsRecreation = true;
            }
            else if (existing.width != width || existing.height != height)
            {
                Debug.Log($"[RenderTextureHelper] Size mismatch (current: {existing.width}x{existing.height}, required: {width}x{height}), recreating...");
                needsRecreation = true;
            }
            else if (requireDepthBuffer && existing.depth == 0)
            {
                Debug.Log($"[RenderTextureHelper] No depth buffer, recreating for URP compatibility...");
                needsRecreation = true;
            }
            else if (!IsFormatCompatible(existing.format))
            {
                Debug.Log($"[RenderTextureHelper] Format {existing.format} incompatible with current graphics API, recreating...");
                needsRecreation = true;
            }
            
            if (needsRecreation)
            {
                if (existing.IsCreated())
                {
                    existing.Release();
                }
                Object.Destroy(existing);
                return CreateCompatibleRenderTexture(width, height, existing.name);
            }
            
            return existing;
        }
        
        /// <summary>
        /// Determines the optimal RenderTexture format for the current platform
        /// </summary>
        private static RenderTextureFormat DetermineOptimalFormat()
        {
            // Default to BGRA32 for WebRTC compatibility
            RenderTextureFormat format = RenderTextureFormat.BGRA32;
            
            // Check Vulkan-specific requirements
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
            {
                if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA32))
                {
                    Debug.LogWarning($"[RenderTextureHelper] BGRA32 not supported on Vulkan, using ARGB32");
                    format = RenderTextureFormat.ARGB32;
                }
            }
            // Check Metal-specific requirements (iOS)
            else if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
            {
                // Metal generally supports BGRA32
                format = RenderTextureFormat.BGRA32;
            }
            // Check other graphics APIs
            else if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 ||
                     SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
            {
                // DirectX supports BGRA32
                format = RenderTextureFormat.BGRA32;
            }
            
            // Final verification
            if (!SystemInfo.SupportsRenderTextureFormat(format))
            {
                Debug.LogWarning($"[RenderTextureHelper] Format {format} not supported, falling back to Default");
                format = RenderTextureFormat.Default;
            }
            
            return format;
        }
        
        /// <summary>
        /// Checks if a RenderTexture format is compatible with the current graphics API
        /// </summary>
        private static bool IsFormatCompatible(RenderTextureFormat format)
        {
            // Special handling for Vulkan
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan && 
                format == RenderTextureFormat.BGRA32 && 
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA32))
            {
                return false;
            }
            
            return SystemInfo.SupportsRenderTextureFormat(format);
        }
        
        /// <summary>
        /// Creates a RenderTexture suitable for WebRTC video streaming
        /// </summary>
        public static RenderTexture CreateForWebRTCStreaming(int width, int height)
        {
            var rt = CreateCompatibleRenderTexture(width, height, "WebRTCStreamTexture", 24);
            
            if (rt != null)
            {
                // Additional WebRTC-specific settings could go here
                Debug.Log($"[RenderTextureHelper] Created WebRTC-optimized RenderTexture");
            }
            
            return rt;
        }
    }
}