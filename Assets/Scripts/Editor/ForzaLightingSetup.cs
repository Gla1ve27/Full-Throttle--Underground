using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// ForzaLightingSetup is an EDIT MODE ONLY tool.
// It bakes a lighting preset into the scene before you press Play.
// The DayNight Sun prefab owns all lighting at runtime and will overwrite
// any changes this tool makes during Play Mode every frame — so running it
// during Play Mode is both useless and causes InvalidOperationException errors.
public class ForzaLightingSetup : EditorWindow
{
    [MenuItem("Tools/Full-Throttle/Apply Forza Lighting (Day)")]
    public static void ApplyDayLighting()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[ForzaLightingSetup] This tool only works in Edit Mode. " +
                "Exit Play Mode first, then apply the preset. " +
                "The DayNight Sun prefab controls lighting at runtime.");
            return;
        }
        ApplyLighting(true);
    }

    [MenuItem("Tools/Full-Throttle/Apply Forza Lighting (Night)")]
    public static void ApplyNightLighting()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[ForzaLightingSetup] This tool only works in Edit Mode. " +
                "Exit Play Mode first, then apply the preset. " +
                "The DayNight Sun prefab controls lighting at runtime.");
            return;
        }
        ApplyLighting(false);
    }

    private static void ApplyLighting(bool isDay)
    {
        // 1. Find the Main Directional Light
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light dirLight = null;

        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                dirLight = light;
                break;
            }
        }

        if (dirLight != null)
        {
            Undo.RecordObject(dirLight.transform, "Change Light Angle");
            Undo.RecordObject(dirLight, "Change Light Properties");

            if (isDay)
            {
                dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Color dayColor;
                dirLight.color = ColorUtility.TryParseHtmlString("#FFF0E0", out dayColor) ? dayColor : Color.white;
                dirLight.enabled = true;
                dirLight.shadows = LightShadows.Soft;
            }
            else
            {
                dirLight.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
                Color nightColor;
                dirLight.color = ColorUtility.TryParseHtmlString("#3A5B8C", out nightColor) ? nightColor : Color.blue;
                dirLight.enabled = true;
                dirLight.shadows = LightShadows.Soft;
            }

            EditorUtility.SetDirty(dirLight);
            EditorUtility.SetDirty(dirLight.transform);
        }
        else
        {
            Debug.LogWarning("[ForzaLightingSetup] No Directional Light found in the scene to adjust!");
        }

        // 2. Adjust Environment Ambient
        if (isDay)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            Color skyDayColor;
            RenderSettings.ambientLight = ColorUtility.TryParseHtmlString("#8CA5C1", out skyDayColor) ? skyDayColor : Color.gray;
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            Color skyNightColor;
            RenderSettings.ambientLight = ColorUtility.TryParseHtmlString("#0A111F", out skyNightColor) ? skyNightColor : Color.black;
        }

        // Mark scene dirty so Unity saves all RenderSettings changes (Edit Mode only).
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"[ForzaLightingSetup] Applied {(isDay ? "Day" : "Night")} lighting preset to scene. " +
            "(Don't forget to check your active Volume Profile!)");
    }
}
