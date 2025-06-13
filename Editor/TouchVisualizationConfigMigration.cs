using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityVerseBridge.Core.Configuration;

namespace UnityVerseBridge.Core.Editor
{
    /// <summary>
    /// Migration utility to update TouchVisualizationConfig assets to use Canvas mode
    /// </summary>
    public static class TouchVisualizationConfigMigration
    {
        [MenuItem("UnityVerseBridge/Migrate Touch Configs to Canvas Mode")]
        public static void MigrateToCanvasMode()
        {
            // Find all TouchVisualizationConfig assets
            string[] guids = AssetDatabase.FindAssets("t:TouchVisualizationConfig");
            
            if (guids.Length == 0)
            {
                Debug.Log("[TouchVisualizationConfigMigration] No TouchVisualizationConfig assets found.");
                return;
            }
            
            int migratedCount = 0;
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TouchVisualizationConfig config = AssetDatabase.LoadAssetAtPath<TouchVisualizationConfig>(path);
                
                if (config != null)
                {
                    // Check if this is a Quest configuration (based on name or current settings)
                    bool isQuestConfig = path.ToLower().Contains("quest") || 
                                       (config.enableInHost && !config.enableInClient);
                    
                    if (isQuestConfig)
                    {
                        // Migrate to Canvas mode
                        config.mode = TouchVisualizationConfig.VisualizationMode.Canvas;
                        
                        // Set default Canvas settings if not already set
                        if (config.canvasRenderMode == 0) // Default enum value
                        {
                            config.canvasRenderMode = RenderMode.ScreenSpaceOverlay;
                        }
                        
                        if (config.canvasSortingOrder == 0)
                        {
                            config.canvasSortingOrder = 100;
                        }
                        
                        if (config.canvasIndicatorSize == 0)
                        {
                            config.canvasIndicatorSize = 50f;
                        }
                        
                        if (config.canvasIndicatorColor.a == 0) // Uninitialized color
                        {
                            config.canvasIndicatorColor = Color.red;
                        }
                        
                        // Default to showing both coordinates
                        config.canvasShowBothCoordinates = true;
                        
                        EditorUtility.SetDirty(config);
                        migratedCount++;
                        
                        Debug.Log($"[TouchVisualizationConfigMigration] Migrated {path} to Canvas mode");
                    }
                }
            }
            
            if (migratedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[TouchVisualizationConfigMigration] Successfully migrated {migratedCount} Quest configuration(s) to Canvas mode.");
            }
            else
            {
                Debug.Log("[TouchVisualizationConfigMigration] No Quest configurations found to migrate.");
            }
        }
        
        [MenuItem("UnityVerseBridge/Create Canvas Touch Config for Quest")]
        public static void CreateCanvasTouchConfig()
        {
            TouchVisualizationConfig config = ScriptableObject.CreateInstance<TouchVisualizationConfig>();
            
            // Set Canvas mode as default for Quest
            config.mode = TouchVisualizationConfig.VisualizationMode.Canvas;
            config.enableInHost = true;
            config.enableInClient = false;
            
            // Canvas settings
            config.canvasRenderMode = RenderMode.ScreenSpaceOverlay;
            config.canvasSortingOrder = 100;
            config.canvasIndicatorSize = 50f;
            config.canvasIndicatorColor = Color.red;
            config.canvasShowBothCoordinates = true;
            config.showCoordinates = true;
            config.coordinateTextColor = Color.white;
            
            // Performance settings
            config.maxTouchesDisplay = 10;
            config.touchFadeTime = 0f;
            
            // Create asset
            string path = "Assets/UnityVerseBridge/QuestCanvasTouchVisualizationConfig.asset";
            
            // Ensure directory exists
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            
            Debug.Log($"[TouchVisualizationConfigMigration] Created new Canvas touch visualization config at: {path}");
        }
    }
}