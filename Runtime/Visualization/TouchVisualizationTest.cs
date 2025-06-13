using UnityEngine;
using UnityVerseBridge.Core.Configuration;

namespace UnityVerseBridge.Core.Visualization
{
    /// <summary>
    /// Test script to verify TouchVisualizer setup
    /// </summary>
    public class TouchVisualizationTest : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("[TouchVisualizationTest] Checking for duplicate touch canvases...");
            
            // Find all canvases in scene
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            
            foreach (var canvas in allCanvases)
            {
                string canvasInfo = $"Canvas: {canvas.name} - RenderMode: {canvas.renderMode} - SortingOrder: {canvas.sortingOrder}";
                
                // Check for touch-related canvases
                if (canvas.name.ToLower().Contains("touch") || 
                    canvas.name.Contains("TouchVisualizationCanvas") ||
                    canvas.name.Contains("TouchCanvas") ||
                    canvas.name.Contains("TouchVisualizerCanvas") ||
                    canvas.name.Contains("Touch Display Canvas"))
                {
                    canvasInfo += " [TOUCH RELATED]";
                    
                    // Check if it's the correct one
                    if (canvas.name == "TouchVisualizationCanvas")
                    {
                        canvasInfo += " - CORRECT";
                    }
                    else
                    {
                        canvasInfo += " - SHOULD BE REMOVED/DISABLED";
                    }
                }
                
                Debug.Log(canvasInfo);
            }
            
            // Check for TouchVisualizer component
            TouchVisualizer visualizer = FindObjectOfType<TouchVisualizer>();
            if (visualizer != null)
            {
                Debug.Log($"[TouchVisualizationTest] TouchVisualizer component found on: {visualizer.gameObject.name}");
                
                // Check if canvas is properly managed
                Canvas touchCanvas = visualizer.GetComponentInChildren<Canvas>();
                if (touchCanvas != null)
                {
                    Debug.Log($"[TouchVisualizationTest] TouchVisualizer canvas active state: {touchCanvas.gameObject.activeSelf}");
                }
            }
            else
            {
                Debug.LogWarning("[TouchVisualizationTest] TouchVisualizer component not found!");
            }
            
            // Check for deprecated components
            var touchInputHandler = FindObjectOfType<TouchInputHandler>();
            if (touchInputHandler != null)
            {
                var handlerType = touchInputHandler.GetType();
                var showVisualizerField = handlerType.GetField("showTouchVisualizer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (showVisualizerField != null)
                {
                    bool showVisualizer = (bool)showVisualizerField.GetValue(touchInputHandler);
                    if (showVisualizer)
                    {
                        Debug.LogWarning("[TouchVisualizationTest] TouchInputHandler has showTouchVisualizer=true. This is deprecated!");
                    }
                }
            }
            
            Debug.Log("[TouchVisualizationTest] Check complete.");
        }
    }
}