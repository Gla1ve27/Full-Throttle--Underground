using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Underground.World;

namespace Underground.EditorTools
{
    [InitializeOnLoad]
    public static class ArcadeCarsHdrpMaterialConverter
    {
        private const string ConversionQueuedKey = "Underground.ArcadeCarsHdrpConversionQueued";
        private static readonly string[] MaterialFolders =
        {
            "Assets/Fantastic City Generator/Traffic System/Materials",
            "Assets/High Matters/Free American Sedans/Art/Materials",
            "Assets/Police Car & Helicopter/Models/Materials",
            "Assets/Polyeler/Simple Retro Car/Materials",
            "Assets/RealisticMobileCars - Pro3DModels/Main/Materials",
            "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Materials",
            "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Materials",
            "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Ground"
        };
        private static readonly string[] PrefabFolders =
        {
            "Assets/Fantastic City Generator/Traffic System",
            "Assets/High Matters/Free American Sedans/Prefabs",
            "Assets/Police Car & Helicopter/Prefabs",
            "Assets/Polyeler/Simple Retro Car/Prefabs",
            "Assets/Prefabs/Vehicles",
            "Assets/RealisticMobileCars - Pro3DModels/RMCar26/Prefabs",
            "Assets/Store InvoGames/Car Asset Pack for Arcade & Demolition Racing Games/Prefabs"
        };

        static ArcadeCarsHdrpMaterialConverter()
        {
            if (NeedsConversion())
            {
                SessionState.SetBool(ConversionQueuedKey, true);
                EditorApplication.delayCall += ProcessQueuedConversion;
            }
        }

        [MenuItem("Underground/Project/Convert Vehicle Materials To HDRP", priority = 12)]
        public static void ConvertMaterialsFromMenu()
        {
            ConvertMaterials(true);
        }

        public static void ConvertMaterials(bool showDialog)
        {
            if (!HasHdrpPackageInstalled())
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("HDRP Missing", "HDRP is not installed yet, so the vehicle materials cannot be converted.", "OK");
                }

                return;
            }

            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("HDRP Shader Missing", "HDRP/Lit could not be found. Let Unity finish importing HDRP, then run the conversion again.", "OK");
                }

                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", MaterialFolders);
            bool changedAny = false;
            int convertedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                if (ConvertMaterial(material, hdrpLit))
                {
                    EditorUtility.SetDirty(material);
                    changedAny = true;
                    convertedCount++;
                }
            }

            int updatedPrefabCount = ConvertVehiclePrefabs();
            changedAny |= updatedPrefabCount > 0;

            if (changedAny)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            SessionState.SetBool(ConversionQueuedKey, false);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Vehicle Reflections Updated",
                    changedAny
                        ? $"Converted {convertedCount} vehicle materials and updated {updatedPrefabCount} vehicle prefabs for HDRP reflections."
                        : "Vehicle materials and prefabs were already using HDRP-compatible reflection settings.",
                    "OK");
            }
        }

        private static void ProcessQueuedConversion()
        {
            if (!SessionState.GetBool(ConversionQueuedKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += ProcessQueuedConversion;
                return;
            }

            ConvertMaterials(false);
        }

        private static bool NeedsConversion()
        {
            if (!HasHdrpPackageInstalled())
            {
                return false;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", MaterialFolders);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                if (MaterialNeedsUpgrade(material))
                {
                    return true;
                }
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", PrefabFolders);
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (PrefabNeedsReflectionController(path))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ConvertMaterial(Material material, Shader hdrpLit)
        {
            MaterialSourceData source = ReadSourceData(material);
            bool alreadyHdrp = material.shader != null && string.Equals(material.shader.name, "HDRP/Lit", StringComparison.Ordinal);

            material.shader = hdrpLit;
            material.name = material.name;

            SetColorIfPresent(material, "_BaseColor", source.baseColor);
            SetColorIfPresent(material, "_Color", source.baseColor);

            SetTextureIfPresent(material, "_BaseColorMap", source.baseColorMap);
            SetTextureIfPresent(material, "_BaseMap", source.baseColorMap);
            SetTextureIfPresent(material, "_MainTex", source.baseColorMap);

            SetTextureIfPresent(material, "_NormalMap", source.normalMap);
            SetTextureIfPresent(material, "_BumpMap", source.normalMap);
            SetFloatIfPresent(material, "_NormalScale", source.normalScale);
            SetFloatIfPresent(material, "_BumpScale", source.normalScale);

            SetTextureIfPresent(material, "_MaskMap", source.maskMap);
            SetFloatIfPresent(material, "_Metallic", source.metallic);
            SetFloatIfPresent(material, "_Smoothness", source.smoothness);

            bool emissive = source.emissiveMap != null || source.emissiveColor.maxColorComponent > 0.001f || IsLightMaterial(material.name);
            if (emissive)
            {
                Color emissiveColor = source.emissiveColor.maxColorComponent > 0.001f ? source.emissiveColor : source.baseColor;
                SetTextureIfPresent(material, "_EmissiveColorMap", source.emissiveMap);
                SetTextureIfPresent(material, "_EmissionMap", source.emissiveMap);
                SetColorIfPresent(material, "_EmissiveColor", emissiveColor);
                SetColorIfPresent(material, "_EmissionColor", emissiveColor);
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            if (source.normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            ConfigureSurface(material, source.isTransparent || IsTransparentMaterial(material.name));
            ConfigureHdrpReflectionResponse(material, source.isTransparent || IsTransparentMaterial(material.name));
            ApplyVehicleFinish(material, source.isTransparent || IsTransparentMaterial(material.name));
            return !alreadyHdrp || source.hasSerializedData || MaterialNeedsUpgrade(material);
        }

        private static void ConfigureHdrpReflectionResponse(Material material, bool isTransparent)
        {
            if (material == null)
            {
                return;
            }

            bool paintLike = IsPaintLikeMaterial(material.name);
            bool bodyLike = IsBodyLikeMaterial(material.name);

            if (!isTransparent)
            {
                SetFloatIfPresent(material, "_ReceivesSSR", 1f);
                SetFloatIfPresent(material, "_EnvironmentReflections", 1f);
                SetFloatIfPresent(material, "_GlossyReflections", 1f);
                SetFloatIfPresent(material, "_SpecularHighlights", 1f);
            }

            SetFloatIfPresent(material, "_ReceivesSSRTransparent", isTransparent ? 1f : 0f);
            SetFloatIfPresent(material, "_EnableCoat", paintLike || bodyLike ? 1f : 0f);
            SetFloatIfPresent(material, "_CoatMask", paintLike ? 1f : bodyLike ? 0.35f : 0f);

            material.DisableKeyword("_DISABLE_SSR");
            material.DisableKeyword("_DISABLE_SSR_TRANSPARENT");
        }

        private static void ApplyVehicleFinish(Material material, bool isTransparent)
        {
            if (material == null || isTransparent)
            {
                return;
            }

            float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            float smoothness = material.HasProperty("_Smoothness")
                ? material.GetFloat("_Smoothness")
                : material.HasProperty("_Glossiness")
                    ? material.GetFloat("_Glossiness")
                    : 0.55f;

            if (IsPaintLikeMaterial(material.name))
            {
                metallic = Mathf.Clamp(metallic, 0f, 0.08f);
                smoothness = Mathf.Clamp(smoothness, 0.8f, 0.94f);
            }
            else if (IsBodyLikeMaterial(material.name))
            {
                metallic = Mathf.Clamp(metallic, 0f, 0.18f);
                smoothness = Mathf.Clamp(smoothness, 0.68f, 0.86f);
            }
            else if (LooksLikeVehicleMaterial(material.name))
            {
                smoothness = Mathf.Clamp(smoothness, 0.6f, 0.82f);
            }

            SetFloatIfPresent(material, "_Metallic", metallic);
            SetFloatIfPresent(material, "_Smoothness", smoothness);
            SetFloatIfPresent(material, "_Glossiness", smoothness);
        }

        private static bool IsPaintLikeMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("paint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("taxi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("police", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("atlas", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsBodyLikeMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car_color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car colour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("car color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("traffic-car", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool LooksLikeVehicleMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName) &&
                   (materialName.IndexOf("car", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("vehicle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("taxi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("police", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("traffic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("paint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("grill", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    materialName.IndexOf("carbon", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool MaterialNeedsUpgrade(Material material)
        {
            if (material == null)
            {
                return false;
            }

            if (material.shader == null || !string.Equals(material.shader.name, "HDRP/Lit", StringComparison.Ordinal))
            {
                return true;
            }

            bool isTransparent = IsTransparentMaterial(material.name);
            if (!isTransparent &&
                ((material.HasProperty("_ReceivesSSR") && material.GetFloat("_ReceivesSSR") < 0.5f) ||
                 (material.HasProperty("_EnvironmentReflections") && material.GetFloat("_EnvironmentReflections") < 0.5f) ||
                 (material.HasProperty("_GlossyReflections") && material.GetFloat("_GlossyReflections") < 0.5f)))
            {
                return true;
            }

            if (material.HasProperty("_ReceivesSSRTransparent") &&
                isTransparent &&
                material.GetFloat("_ReceivesSSRTransparent") < 0.5f)
            {
                return true;
            }

            if ((IsPaintLikeMaterial(material.name) || IsBodyLikeMaterial(material.name)) &&
                material.HasProperty("_EnableCoat") &&
                material.GetFloat("_EnableCoat") < 0.5f)
            {
                return true;
            }

            return false;
        }

        private static MaterialSourceData ReadSourceData(Material material)
        {
            MaterialSourceData data = new MaterialSourceData
            {
                baseColor = GetSerializedColor(material, Color.white, "_BaseColor", "_Color"),
                emissiveColor = GetSerializedColor(material, Color.black, "_EmissiveColor", "_EmissionColor"),
                metallic = GetSerializedFloat(material, 0f, "_Metallic"),
                smoothness = GetSerializedFloat(material, 0.5f, "_Smoothness", "_Glossiness"),
                normalScale = Mathf.Max(0.0001f, GetSerializedFloat(material, 1f, "_NormalScale", "_BumpScale")),
                baseColorMap = GetSerializedTexture(material, "_BaseColorMap", "_BaseMap", "_MainTex"),
                normalMap = GetSerializedTexture(material, "_NormalMap", "_BumpMap"),
                emissiveMap = GetSerializedTexture(material, "_EmissiveColorMap", "_EmissionMap"),
                maskMap = GetSerializedTexture(material, "_MaskMap", "_MetallicGlossMap"),
                hasSerializedData = true
            };

            float mode = GetSerializedFloat(material, 0f, "_Mode", "_Surface");
            data.isTransparent = mode >= 1.5f || material.renderQueue >= (int)RenderQueue.Transparent;
            return data;
        }

        private static Texture GetSerializedTexture(Material material, params string[] propertyNames)
        {
            SerializedObject serializedObject = new SerializedObject(material);
            SerializedProperty texEnvs = serializedObject.FindProperty("m_SavedProperties.m_TexEnvs");
            if (texEnvs == null)
            {
                return null;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                for (int j = 0; j < texEnvs.arraySize; j++)
                {
                    SerializedProperty element = texEnvs.GetArrayElementAtIndex(j);
                    SerializedProperty keyProperty = element.FindPropertyRelative("first");
                    if (keyProperty == null || !string.Equals(keyProperty.stringValue, propertyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SerializedProperty textureProperty = element.FindPropertyRelative("second.m_Texture");
                    return textureProperty != null ? textureProperty.objectReferenceValue as Texture : null;
                }
            }

            return null;
        }

        private static Color GetSerializedColor(Material material, Color fallback, params string[] propertyNames)
        {
            SerializedObject serializedObject = new SerializedObject(material);
            SerializedProperty colors = serializedObject.FindProperty("m_SavedProperties.m_Colors");
            if (colors == null)
            {
                return fallback;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                for (int j = 0; j < colors.arraySize; j++)
                {
                    SerializedProperty element = colors.GetArrayElementAtIndex(j);
                    SerializedProperty keyProperty = element.FindPropertyRelative("first");
                    if (keyProperty == null || !string.Equals(keyProperty.stringValue, propertyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SerializedProperty colorProperty = element.FindPropertyRelative("second");
                    return colorProperty != null ? colorProperty.colorValue : fallback;
                }
            }

            return fallback;
        }

        private static float GetSerializedFloat(Material material, float fallback, params string[] propertyNames)
        {
            SerializedObject serializedObject = new SerializedObject(material);
            SerializedProperty floats = serializedObject.FindProperty("m_SavedProperties.m_Floats");
            if (floats == null)
            {
                return fallback;
            }

            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                for (int j = 0; j < floats.arraySize; j++)
                {
                    SerializedProperty element = floats.GetArrayElementAtIndex(j);
                    SerializedProperty keyProperty = element.FindPropertyRelative("first");
                    if (keyProperty == null || !string.Equals(keyProperty.stringValue, propertyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SerializedProperty valueProperty = element.FindPropertyRelative("second");
                    return valueProperty != null ? valueProperty.floatValue : fallback;
                }
            }

            return fallback;
        }

        private static void ConfigureSurface(Material material, bool transparent)
        {
            if (transparent)
            {
                SetFloatIfPresent(material, "_SurfaceType", 1f);
                SetFloatIfPresent(material, "_Surface", 1f);
                SetFloatIfPresent(material, "_BlendMode", 0f);
                SetFloatIfPresent(material, "_AlphaCutoffEnable", 0f);
                SetFloatIfPresent(material, "_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                return;
            }

            SetFloatIfPresent(material, "_SurfaceType", 0f);
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_BlendMode", 0f);
            SetFloatIfPresent(material, "_AlphaCutoffEnable", 0f);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        private static bool IsTransparentMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName)
                && materialName.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLightMaterial(string materialName)
        {
            return !string.IsNullOrEmpty(materialName)
                && materialName.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ConvertVehiclePrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", PrefabFolders);
            int updatedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!PrefabNeedsReflectionController(path))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null)
                {
                    continue;
                }

                try
                {
                    if (!VehicleReflectionRuntimeController.IsVehicleLikeObject(prefabRoot) ||
                        prefabRoot.GetComponent<VehicleReflectionRuntimeController>() != null)
                    {
                        continue;
                    }

                    prefabRoot.AddComponent<VehicleReflectionRuntimeController>();
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    updatedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            return updatedCount;
        }

        private static bool PrefabNeedsReflectionController(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer is ModelImporter)
            {
                return false;
            }

            string lowerPath = path.Replace('\\', '/');
            if (lowerPath.IndexOf("traffic_light", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("helicopter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("plane", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefabRoot != null &&
                   prefabRoot.GetComponent<VehicleReflectionRuntimeController>() == null &&
                   VehicleReflectionRuntimeController.IsVehicleLikeObject(prefabRoot);
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (texture != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static bool HasHdrpPackageInstalled()
        {
            Type hdAssetType = Type.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime");
            return hdAssetType != null;
        }

        private struct MaterialSourceData
        {
            public bool hasSerializedData;
            public bool isTransparent;
            public Color baseColor;
            public Color emissiveColor;
            public float metallic;
            public float smoothness;
            public float normalScale;
            public Texture baseColorMap;
            public Texture normalMap;
            public Texture emissiveMap;
            public Texture maskMap;
        }
    }
}
