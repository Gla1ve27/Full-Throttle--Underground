using UnityEditor;
using UnityEngine;

public class FixEasyRoadsPinkMaterials
{
    [MenuItem("Tools/Fix EasyRoads Pink Materials (HDRP 17+)")]
    public static void FixMaterials()
    {
        string[] searchFolders = new string[] { "Assets/EasyRoads3D", "Assets/EasyRoads3D Assets", "Assets/EasyRoads3D scenes" };
        
        // Filter out folders that don't exist to prevent AssetDatabase errors
        System.Collections.Generic.List<string> validFolders = new System.Collections.Generic.List<string>();
        foreach (string folder in searchFolders) {
            if (AssetDatabase.IsValidFolder(folder)) validFolders.Add(folder);
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", validFolders.ToArray());
        Shader hdrpLit = Shader.Find("HDRP/Lit");

        if (hdrpLit == null)
        {
            Debug.LogError("HDRP/Lit shader not found!");
            return;
        }

        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (mat != null && mat.shader != null)
            {
                bool needsUpgrade = false;
                if (mat.shader.name == "Hidden/InternalErrorShader") needsUpgrade = true;
                else if (!mat.shader.name.Contains("Shader Graph") &&
                         !mat.shader.name.StartsWith("UI/"))
                {
                    needsUpgrade = true; // Force upgrade / repair on ALL materials, including already converted HDRP/Lit that are rendering white.
                }

                if (needsUpgrade)
                {
                    bool isTransparent = mat.shader.name.ToLower().Contains("water") || 
                                         mat.shader.name.ToLower().Contains("transparent") ||
                                         mat.name.ToLower().Contains("water");

                    // Try to preserve the main texture and color from the OLD shader before switching
                    Texture mainTex = null;
                    Color baseColor = Color.white;
                    float cutoff = -1f;
                    
                    if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                    else if (mat.HasProperty("_BaseColorMap")) mainTex = mat.GetTexture("_BaseColorMap");
                    else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

                    if (mat.HasProperty("_Color")) baseColor = mat.GetColor("_Color");
                    else if (mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_TintColor")) baseColor = mat.GetColor("_TintColor");

                    if (mat.HasProperty("_Cutoff")) cutoff = mat.GetFloat("_Cutoff");

                    // Now change to HDRP
                    mat.shader = hdrpLit;

                    // Re-apply to HDRP properties
                    if (mainTex != null)
                    {
                        mat.SetTexture("_BaseColorMap", mainTex);
                    }
                    mat.SetColor("_BaseColor", baseColor);

                    if (isTransparent)
                    {
                        mat.SetFloat("_SurfaceType", 1f); // Transparent
                        mat.SetFloat("_BlendMode", 0f); // Alpha
                        mat.renderQueue = 3000;
                        mat.SetOverrideTag("RenderType", "Transparent");
                    }
                    else if (cutoff >= 0f)
                    {
                        // Enable Alpha Clipping if it was an alpha cutout material
                        mat.SetFloat("_AlphaCutoffEnable", 1f);
                        mat.SetFloat("_AlphaCutoff", cutoff);
                    }

                    // Reset emission
                    mat.SetColor("_EmissiveColor", Color.black);

                    // Use Reflection to call HDRP's internal ResetMaterialKeywords
                    // This is CRITICAL. Without this, HDRP materials often fail to sample their _BaseColorMap and render solid white.
                    try 
                    {
                        System.Type hdEditorUtilsType = System.Type.GetType("UnityEditor.Rendering.HighDefinition.HDEditorUtils, Unity.RenderPipelines.HighDefinition.Editor");
                        if (hdEditorUtilsType != null)
                        {
                            System.Reflection.MethodInfo resetMethod = hdEditorUtilsType.GetMethod("ResetMaterialKeywords", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            if (resetMethod != null)
                            {
                                resetMethod.Invoke(null, new object[] { mat });
                            }
                        }
                    }
                    catch (System.Exception e) {
                        Debug.LogWarning("Could not automatically reset HDRP keywords: " + e.Message);
                    }

                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Fixed {count} EasyRoads materials by converting them to standard HDRP/Lit.");
    }
}
