using UnityEngine;
using UnityEngine.UI;

namespace UnityVerseBridge.Core.Configuration
{
    /// <summary>
    /// Touch visualization configuration for UnityVerseBridge
    /// </summary>
    [CreateAssetMenu(fileName = "TouchVisualizationConfig", menuName = "UnityVerseBridge/Touch Visualization Config")]
    public class TouchVisualizationConfig : ScriptableObject
    {
        [Header("Visualization Mode")]
        [Tooltip("How to visualize touch positions")]
        public VisualizationMode mode = VisualizationMode.Canvas;
        
        [Header("Enable Settings")]
        [Tooltip("Enable touch visualization in Host mode")]
        public bool enableInHost = true;
        
        [Tooltip("Enable touch visualization in Client mode")]
        public bool enableInClient = false;
        
        [Header("3D Cube Settings")]
        [Tooltip("Size of 3D touch cubes")]
        public float cubeSize = 0.2f;
        
        [Tooltip("Distance from camera for 3D cubes")]
        public float cubeDistance = 2f;
        
        [Tooltip("Color of 3D touch cubes")]
        public Color cubeColor = Color.red;
        
        [Tooltip("3D cube prefab (optional)")]
        public GameObject cubePrefab;
        
        [Header("Screen Plane Settings")]
        [Tooltip("Distance from camera for screen plane")]
        public float planeDistance = 0.5f;
        
        [Tooltip("Size of dots on screen plane")]
        public float dotSize = 0.05f;
        
        [Tooltip("Color of dots on screen plane")]
        public Color dotColor = Color.yellow;
        
        [Tooltip("Screen plane dot prefab (optional)")]
        public GameObject planeDotPrefab;
        
        [Header("Debug Display")]
        [Tooltip("Show coordinate text in 3D space")]
        public bool showCoordinates = true;
        
        [Tooltip("Font size for coordinate text")]
        public int coordinateFontSize = 50;
        
        [Tooltip("Color of coordinate text")]
        public Color coordinateTextColor = Color.white;
        
        [Header("Canvas Settings")]
        [Tooltip("Canvas render mode for touch visualization")]
        public RenderMode canvasRenderMode = RenderMode.ScreenSpaceOverlay;
        
        [Tooltip("Canvas sorting order")]
        public int canvasSortingOrder = 100;
        
        [Tooltip("Touch indicator size on canvas")]
        public float canvasIndicatorSize = 50f;
        
        [Tooltip("Color of canvas touch indicators")]
        public Color canvasIndicatorColor = Color.red;
        
        [Tooltip("Show both absolute and relative coordinates")]
        public bool canvasShowBothCoordinates = true;
        
        [Tooltip("Canvas indicator prefab (optional)")]
        public GameObject canvasIndicatorPrefab;
        
        [Header("Performance")]
        [Tooltip("Maximum number of simultaneous touches to display")]
        public int maxTouchesDisplay = 10;
        
        [Tooltip("Auto-hide touches after this duration (0 = never)")]
        public float touchFadeTime = 0f;
        
        public enum VisualizationMode
        {
            None,
            Cube3D,
            ScreenPlane,
            Both,
            Canvas
        }
        
        /// <summary>
        /// Check if visualization should be enabled for the given bridge mode
        /// </summary>
        public bool ShouldEnableForMode(UnityVerseBridgeManager.BridgeMode mode)
        {
            switch (mode)
            {
                case UnityVerseBridgeManager.BridgeMode.Host:
                    return enableInHost && this.mode != VisualizationMode.None;
                case UnityVerseBridgeManager.BridgeMode.Client:
                    return enableInClient && this.mode != VisualizationMode.None;
                default:
                    return false;
            }
        }
    }
}