using UnityEditor;
using UnityEngine;
using System.IO;

namespace Underground.EditorTools
{
    [InitializeOnLoad]
    public class FixMissingPrefabsAutoRunner
    {
        static FixMissingPrefabsAutoRunner()
        {
            EditorApplication.delayCall += FixPrefabs;
        }

        private static void FixPrefabs()
        {
            if (SessionState.GetBool("Underground.FixedMissingSlimUIPrefabs", false))
            {
                return;
            }

            string[] paths = new string[]
            {
                "Assets/SlimUI/Modern Menu 1/Prefabs/Canvas Templates/Canvas_DefaultTemplate1.prefab",
                "Assets/SlimUI/Modern Menu 1/Prefabs/Canvas Templates/Canvas_DefaultMobileTemplate1.prefab"
            };

            bool anyFixed = false;
            foreach (string path in paths)
            {
                if (!File.Exists(path)) continue;
                
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    int totalCount = 0;
                    totalCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefab);
                    
                    foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        totalCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    }

                    if (totalCount > 0)
                    {
                        Debug.Log($"Removed {totalCount} missing scripts from {path}");
                        EditorUtility.SetDirty(prefab);
                        anyFixed = true;
                    }
                }
            }

            if (anyFixed)
            {
                AssetDatabase.SaveAssets();
            }
            
            SessionState.SetBool("Underground.FixedMissingSlimUIPrefabs", true);
        }
    }
}
