using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace UnityVerseBridge.Core.Editor
{
    public static class UnityVerseBridgeCreator
    {
        // Cleanup menu items
        [MenuItem("UnityVerseBridge/Cleanup/Remove All UnityVerseBridge Components", false, 100)]
        public static void CleanupAll()
        {
            if (EditorUtility.DisplayDialog("Remove UnityVerseBridge", 
                "This will remove all UnityVerseBridge prefabs, instances, and related assets.\n\nAre you sure?", 
                "Yes, Remove All", "Cancel"))
            {
                CleanupUnityVerseBridge();
            }
        }
        
        [MenuItem("UnityVerseBridge/Cleanup/Remove Quest Components Only", false, 101)]
        public static void CleanupQuestOnly()
        {
            if (EditorUtility.DisplayDialog("Remove Quest Components", 
                "This will remove Quest-specific UnityVerseBridge components and assets.\n\nAre you sure?", 
                "Yes, Remove Quest", "Cancel"))
            {
                CleanupUnityVerseBridge("Quest");
            }
        }
        
        [MenuItem("UnityVerseBridge/Cleanup/Remove Mobile Components Only", false, 102)]
        public static void CleanupMobileOnly()
        {
            if (EditorUtility.DisplayDialog("Remove Mobile Components", 
                "This will remove Mobile-specific UnityVerseBridge components and assets.\n\nAre you sure?", 
                "Yes, Remove Mobile", "Cancel"))
            {
                CleanupUnityVerseBridge("Mobile");
            }
        }
        
        [MenuItem("UnityVerseBridge/Cleanup/Remove Config Assets Only", false, 110)]
        public static void CleanupConfigsOnly()
        {
            if (EditorUtility.DisplayDialog("Remove Config Assets", 
                "This will remove all ConnectionConfig and WebRtcConfiguration assets.\n\nAre you sure?", 
                "Yes, Remove Configs", "Cancel"))
            {
                CleanupConfigAssets();
            }
        }
        
        [MenuItem("UnityVerseBridge/Cleanup/Remove Scene Instances Only", false, 111)]
        public static void CleanupSceneOnly()
        {
            if (EditorUtility.DisplayDialog("Remove Scene Instances", 
                "This will remove all UnityVerseBridge instances from the current scene.\n\nAre you sure?", 
                "Yes, Remove Instances", "Cancel"))
            {
                CleanupSceneInstances();
            }
        }
        // Add separator
        [MenuItem("UnityVerseBridge/—————————————", false, 50)]
        private static void Separator() {}
        
        // Quest and Mobile creation functionality is now centralized in UnityVerseBridgeMenu.cs
        // This avoids code duplication and provides better integration with Unity's GameObject menu

        // Cleanup methods
        private static void CleanupUnityVerseBridge(string filterType = null)
        {
            int removedCount = 0;
            
            // Remove from scene
            removedCount += CleanupSceneInstances(filterType);
            
            // Remove from assets
            removedCount += CleanupPrefabs(filterType);
            removedCount += CleanupConfigAssets(filterType);
            
            // Cleanup folders if empty
            CleanupEmptyFolders();
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[UnityVerseBridge] Cleanup complete. Removed {removedCount} items.");
        }
        
        private static int CleanupSceneInstances(string filterType = null)
        {
            int count = 0;
            
            // First, cleanup UnityVerseBridgeManager instances
            var managers = GameObject.FindObjectsByType<UnityVerseBridgeManager>(FindObjectsSortMode.None);
            
            foreach (var manager in managers)
            {
                if (filterType == null || manager.gameObject.name.Contains(filterType))
                {
                    // For Mobile setup, also check if there's a Canvas that was created with it
                    if (filterType == "Mobile" || manager.gameObject.name.Contains("Mobile"))
                    {
                        CleanupMobileCanvas(manager);
                    }
                    
                    GameObject.DestroyImmediate(manager.gameObject);
                    count++;
                }
            }
            
            // Also cleanup any orphaned UnityVerseBridge-related UI elements
            count += CleanupOrphanedUI();
            
            // Cleanup EventSystem if it's not being used by anything else
            CleanupOrphanedEventSystem();
            
            return count;
        }
        
        private static void CleanupMobileCanvas(UnityVerseBridgeManager manager)
        {
            // Try to find the associated video display
            var videoDisplayField = typeof(UnityVerseBridgeManager).GetField("videoDisplay", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (videoDisplayField != null)
            {
                var videoDisplay = videoDisplayField.GetValue(manager) as UnityEngine.UI.RawImage;
                if (videoDisplay != null)
                {
                    // Check if the Canvas parent has only UnityVerseBridge-related children
                    Canvas canvas = videoDisplay.GetComponentInParent<Canvas>();
                    if (canvas != null && ShouldDeleteCanvas(canvas))
                    {
                        GameObject.DestroyImmediate(canvas.gameObject);
                        Debug.Log("[UnityVerseBridge] Removed associated Canvas");
                    }
                }
            }
        }
        
        private static bool ShouldDeleteCanvas(Canvas canvas)
        {
            // Check if it's a UnityVerseBridge-created Canvas by name
            if (canvas.name == "UnityVerseBridge_Canvas")
                return true;
                
            // Also check the generic "Canvas" name with UnityVerse children
            if (canvas.name != "Canvas") 
                return false;
            
            // Check if all children are UnityVerseBridge-related
            Transform canvasTransform = canvas.transform;
            int childCount = canvasTransform.childCount;
            int unityVerseChildCount = 0;
            
            for (int i = 0; i < childCount; i++)
            {
                GameObject child = canvasTransform.GetChild(i).gameObject;
                if (child.name == "VideoDisplay" || 
                    child.name.Contains("UnityVerse") || 
                    child.name.Contains("ConnectionStatus") ||
                    child.name.Contains("LoadingPanel") ||
                    child.name.Contains("ErrorPanel") ||
                    child.name.Contains("MainUICanvas"))
                {
                    unityVerseChildCount++;
                }
            }
            
            // If all children are UnityVerseBridge-related, it's safe to delete
            return childCount > 0 && childCount == unityVerseChildCount;
        }
        
        private static int CleanupOrphanedUI()
        {
            int count = 0;
            
            // Find all Canvases and check for orphaned UnityVerseBridge UI
            var allCanvases = GameObject.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            
            foreach (var canvas in allCanvases)
            {
                if (ShouldDeleteCanvas(canvas))
                {
                    // Double-check that there's no UnityVerseBridgeManager using this Canvas
                    var managers = GameObject.FindObjectsByType<UnityVerseBridgeManager>(FindObjectsSortMode.None);
                    bool isInUse = false;
                    
                    foreach (var manager in managers)
                    {
                        var videoDisplayField = typeof(UnityVerseBridgeManager).GetField("videoDisplay", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (videoDisplayField != null)
                        {
                            var videoDisplay = videoDisplayField.GetValue(manager) as UnityEngine.UI.RawImage;
                            if (videoDisplay != null && videoDisplay.GetComponentInParent<Canvas>() == canvas)
                            {
                                isInUse = true;
                                break;
                            }
                        }
                    }
                    
                    if (!isInUse)
                    {
                        GameObject.DestroyImmediate(canvas.gameObject);
                        count++;
                        Debug.Log($"[UnityVerseBridge] Removed orphaned Canvas: {canvas.name}");
                    }
                }
            }
            
            return count;
        }
        
        private static void CleanupOrphanedEventSystem()
        {
            // Check if there are any UI elements left that need EventSystem
            var allCanvases = GameObject.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (allCanvases.Length == 0)
            {
                // No canvases left, check for EventSystem
                var eventSystem = GameObject.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                if (eventSystem != null && eventSystem.gameObject.name == "EventSystem")
                {
                    // Check if it has only standard components
                    var components = eventSystem.GetComponents<Component>();
                    bool hasOnlyStandardComponents = true;
                    
                    foreach (var component in components)
                    {
                        var typeName = component.GetType().Name;
                        if (typeName != "EventSystem" && 
                            typeName != "StandaloneInputModule" && 
                            typeName != "InputSystemUIInputModule" &&
                            typeName != "Transform" &&
                            typeName != "GameObject")
                        {
                            hasOnlyStandardComponents = false;
                            break;
                        }
                    }
                    
                    if (hasOnlyStandardComponents)
                    {
                        GameObject.DestroyImmediate(eventSystem.gameObject);
                        Debug.Log("[UnityVerseBridge] Removed orphaned EventSystem");
                    }
                }
            }
        }
        
        private static int CleanupPrefabs(string filterType = null)
        {
            int count = 0;
            string searchFilter = filterType != null ? 
                $"t:Prefab UnityVerseBridge_{filterType}" : "t:Prefab UnityVerseBridge";
            
            string[] guids = AssetDatabase.FindAssets(searchFilter);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.DeleteAsset(path))
                {
                    count++;
                }
            }
            
            return count;
        }
        
        private static int CleanupConfigAssets(string filterType = null)
        {
            int count = 0;
            
            // Clean UnityVerseConfig
            string[] configGuids = AssetDatabase.FindAssets("t:UnityVerseConfig");
            foreach (string guid in configGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (filterType == null || path.Contains(filterType))
                {
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        count++;
                    }
                }
            }
            
            // Clean legacy ConnectionConfig
            string[] legacyGuids = AssetDatabase.FindAssets("t:ConnectionConfig");
            foreach (string guid in legacyGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (filterType == null || path.Contains(filterType))
                {
                    if (AssetDatabase.DeleteAsset(path))
                    {
                        count++;
                    }
                }
            }
            
            return count;
        }
        
        private static void CleanupEmptyFolders()
        {
            string[] folders = { "Assets/UnityVerseBridge", "Assets/UnityVerse Bridge" };
            
            foreach (string folder in folders)
            {
                if (System.IO.Directory.Exists(folder))
                {
                    if (System.IO.Directory.GetFiles(folder).Length == 0 && 
                        System.IO.Directory.GetDirectories(folder).Length == 0)
                    {
                        AssetDatabase.DeleteAsset(folder);
                    }
                }
            }
        }
    }
}