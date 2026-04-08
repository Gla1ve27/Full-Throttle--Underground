using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private const string UrpGlobalSettingsAssetPath = "Assets/Settings/ProjectURP/UniversalRenderPipelineGlobalSettings.asset";
        private const string HdrpGlobalSettingsAssetPath = "Assets/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset";
        private const string SsrtHdrpTypeName = "SSRT_HDRP";
        private const string SsrtHdrpAssemblyQualifiedHint = "SSRT_HDRP, Assembly-CSharp";

        [MenuItem("Underground/Project/Enable Post Processing", priority = 11)]
        public static void EnableSsrRendererFeaturesFromMenu()
        {
            EnsureProjectFolders();
            ConfigureRenderPipeline();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Rendering Ready", "Configured the active render pipeline volume profile, post-processing, and camera support.", "OK");
        }

        [MenuItem("Underground/Project/Configure SSRT3", priority = 13)]
        public static void ConfigureSsrt3FromMenu()
        {
            EnsureProjectFolders();
            ConfigureHdrpReflectionSupport();
            RegisterSsrtInHdrpGlobalSettings();
            VolumeProfile worldProfile = LoadOrCreateVolumeProfile(ProjectWorldVolumeProfilePath);
            VolumeProfile garageProfile = LoadOrCreateVolumeProfile(ProjectGarageVolumeProfilePath);

            ConfigureHdrpScreenSpaceReflections(worldProfile, false);
            ConfigureHdrpScreenSpaceReflections(garageProfile, true);
            ConfigureSsrt(garageProfile, true);
            ConfigureSsrt(worldProfile, false);
            EditorUtility.SetDirty(worldProfile);
            EditorUtility.SetDirty(garageProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SSRT3 Configured",
                "SSRT3 integration complete:\n\n" +
                "• SSRT_HDRP registered in HDRP Global Settings (After Opaque And Sky)\n" +
                "• HDRP SSR enabled on the project pipeline asset\n" +
                "• Reflection overrides added to WorldVolumeProfile and GarageVolumeProfile\n\n" +
                "Tip: Garage uses the sharper reflection preset; World uses the longer-range preset.",
                "OK");
        }

        private static void EnsureSsrRendererFeatures()
        {
            if (HasHdrpPackageInstalled())
            {
                ConfigureHdrpReflectionSupport();
                return;
            }

            ConfigureUrpCompatibilityMode();
        }

        private static void ConfigureDefaultVolumeProfile()
        {
            VolumeProfile worldProfile = LoadOrCreateVolumeProfile(ProjectWorldVolumeProfilePath);
            VolumeProfile garageProfile = LoadOrCreateVolumeProfile(ProjectGarageVolumeProfilePath);

            ConfigureWorldVolumeProfile(worldProfile);
            ConfigureGarageVolumeProfile(garageProfile);

            if (HasHdrpPackageInstalled())
            {
                ConfigureHdrpScreenSpaceReflections(worldProfile, false);
                ConfigureHdrpScreenSpaceReflections(garageProfile, true);
                RegisterSsrtInHdrpGlobalSettings();
                ConfigureSsrt(worldProfile, false);
                ConfigureSsrt(garageProfile, true);
            }

            EditorUtility.SetDirty(worldProfile);
            EditorUtility.SetDirty(garageProfile);
        }

        private static VolumeProfile LoadOrCreateVolumeProfile(string assetPath)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(assetPath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, assetPath);
            return profile;
        }

        private static void ConfigureWorldVolumeProfile(VolumeProfile profile)
        {
            DisableHdrpExposure(profile);
            ConfigureTonemapping(profile);
            DisableWorldVolumeLookOverrides(profile);
            ConfigureHdrpSky(profile, LoadDaySkyCubemap(), ResolveSkyExposure(LoadDaySkyboxMaterial(), 0.95f), 1f);
        }

        private static void ConfigureGarageVolumeProfile(VolumeProfile profile)
        {
            ConfigureBloom(profile, 2.2f, 0.008f, 0.18f);
            ConfigureColorAdjustments(profile, -0.45f, -5f, -4f);
            ConfigureHdrpExposure(profile, 7f, -2.4f);
            ConfigureTonemapping(profile);
            ConfigureVignette(profile, 0.025f, 0.18f);
            ConfigureChromaticAberration(profile, 0f);
            ConfigureMotionBlur(profile, 0f, 0.02f, 0.02f);
            ConfigureHdrpSky(profile, null, 0f, 0f);
        }

        private static void AttachGlobalVolume(Transform parent, string objectName, string profilePath)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath) ?? LoadOrCreateVolumeProfile(profilePath);
            Transform volumeTransform = parent.Find(objectName);
            GameObject volumeObject = volumeTransform != null ? volumeTransform.gameObject : new GameObject(objectName);
            volumeObject.transform.SetParent(parent, false);
            volumeObject.transform.localPosition = Vector3.zero;
            Volume volume = volumeObject.GetComponent<Volume>() ?? volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        private static void EnablePostProcessing(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.allowHDR = true;

            if (HasHdrpPackageInstalled())
            {
                Type hdCameraType = FindType("UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime");
                if (hdCameraType != null)
                {
                    Component hdCameraData = camera.GetComponent(hdCameraType);
                    if (hdCameraData == null)
                    {
                        camera.gameObject.AddComponent(hdCameraType);
                    }
                }

                return;
            }

            Type urpCameraType = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraType == null)
            {
                return;
            }

            Component additionalCameraData = camera.GetComponent(urpCameraType) ?? camera.gameObject.AddComponent(urpCameraType);
            SetPropertyIfPresent(additionalCameraData, "renderPostProcessing", true);
            SetPropertyIfPresent(additionalCameraData, "stopNaN", true);
            SetPropertyIfPresent(additionalCameraData, "volumeLayerMask", ~0);
            SetPropertyIfPresent(additionalCameraData, "volumeTrigger", camera.transform);

            Type antialiasingModeType = FindType("UnityEngine.Rendering.Universal.AntialiasingMode, Unity.RenderPipelines.Universal.Runtime");
            if (antialiasingModeType != null)
            {
                object fxaa = Enum.Parse(antialiasingModeType, "FastApproximateAntialiasing");
                SetPropertyIfPresent(additionalCameraData, "antialiasing", fxaa);
            }
        }

        private static void ConfigureUrpCompatibilityMode()
        {
            UnityEngine.Object globalSettings = AssetDatabase.LoadMainAssetAtPath(UrpGlobalSettingsAssetPath);
            if (globalSettings == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(globalSettings);
            SerializedProperty renderGraphProperty = serializedObject.FindProperty("m_EnableRenderGraph");
            if (renderGraphProperty != null)
            {
                renderGraphProperty.boolValue = false;
            }

            SerializedProperty referencesProperty = serializedObject.FindProperty("references.RefIds");
            if (referencesProperty != null)
            {
                for (int i = 0; i < referencesProperty.arraySize; i++)
                {
                    SerializedProperty referenceProperty = referencesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty typeProperty = referenceProperty.FindPropertyRelative("type.class");
                    if (typeProperty == null || typeProperty.stringValue != "RenderGraphSettings")
                    {
                        continue;
                    }

                    SerializedProperty dataProperty = referenceProperty.FindPropertyRelative("data");
                    SerializedProperty compatibilityProperty = dataProperty != null ? dataProperty.FindPropertyRelative("m_EnableRenderCompatibilityMode") : null;
                    if (compatibilityProperty != null)
                    {
                        compatibilityProperty.boolValue = true;
                    }
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(globalSettings);
        }

        private static void ConfigureHdrpReflectionSupport()
        {
            RenderPipelineAsset pipelineAsset = LoadHdrpRenderPipelineAsset();
            if (pipelineAsset == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(pipelineAsset);
            bool changed = false;

            changed |= SetSerializedBoolIfPresent(serializedObject, "m_RenderPipelineSettings.supportSSR", true);
            changed |= SetSerializedBoolIfPresent(serializedObject, "m_RenderPipelineSettings.supportSSRTransparent", true);
            changed |= SetSerializedBoolIfPresent(serializedObject, "m_ObsoleteFrameSettings.enableSSR", true);
            changed |= SetSerializedBoolIfPresent(serializedObject, "m_ObsoleteBakedOrCustomReflectionFrameSettings.enableSSR", true);
            changed |= SetSerializedBoolIfPresent(serializedObject, "m_ObsoleteRealtimeReflectionFrameSettings.enableSSR", true);

            if (!changed)
            {
                return;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipelineAsset);
        }

        private static bool SetSerializedBoolIfPresent(SerializedObject serializedObject, string propertyPath, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean || property.boolValue == value)
            {
                return false;
            }

            property.boolValue = value;
            return true;
        }

        private static void ConfigureBloom(VolumeProfile profile, float threshold, float intensity, float scatter)
        {
            VolumeComponent bloom = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Bloom, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Bloom, Unity.RenderPipelines.Universal.Runtime");
            if (bloom == null)
            {
                return;
            }

            SetComponentActive(bloom);
            SetVolumeParameter(bloom, "threshold", threshold);
            SetVolumeParameter(bloom, "intensity", intensity);
            SetVolumeParameter(bloom, "scatter", scatter);
        }

        private static void ConfigureColorAdjustments(VolumeProfile profile, float postExposure, float contrast, float saturation)
        {
            VolumeComponent colorAdjustments = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime",
                "UnityEngine.Rendering.HighDefinition.ColorAdjustments, Unity.RenderPipelines.HighDefinition.Runtime");
            if (colorAdjustments == null)
            {
                return;
            }

            SetComponentActive(colorAdjustments);
            SetVolumeParameter(colorAdjustments, "postExposure", postExposure);
            SetVolumeParameter(colorAdjustments, "contrast", contrast);
            SetVolumeParameter(colorAdjustments, "saturation", saturation);
        }

        private static void ConfigureHdrpExposure(VolumeProfile profile, float fixedExposure, float compensation = 0f)
        {
            VolumeComponent exposure = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Exposure, Unity.RenderPipelines.HighDefinition.Runtime");
            if (exposure == null)
            {
                return;
            }

            SetComponentActive(exposure);
            SetVolumeEnumParameter(exposure, "mode", "Fixed");
            SetVolumeParameter(exposure, "fixedExposure", fixedExposure);
            SetVolumeParameter(exposure, "compensation", compensation);
        }

        private static void DisableHdrpExposure(VolumeProfile profile)
        {
            DisableVolumeComponent(profile, "UnityEngine.Rendering.HighDefinition.Exposure, Unity.RenderPipelines.HighDefinition.Runtime");
        }

        private static void ConfigureHdrpSky(VolumeProfile profile, Cubemap cubemap, float exposure, float multiplier)
        {
            Type visualEnvironmentType = FindType("UnityEngine.Rendering.HighDefinition.VisualEnvironment, Unity.RenderPipelines.HighDefinition.Runtime");
            Type hdriSkyType = FindType("UnityEngine.Rendering.HighDefinition.HDRISky, Unity.RenderPipelines.HighDefinition.Runtime");
            if (visualEnvironmentType == null || hdriSkyType == null)
            {
                return;
            }

            VolumeComponent visualEnvironment = GetOrAddVolumeComponent(profile, visualEnvironmentType.AssemblyQualifiedName, visualEnvironmentType.FullName);
            if (visualEnvironment != null)
            {
                SetComponentActive(visualEnvironment);
                SetVolumeEnumParameter(visualEnvironment, "skyAmbientMode", "Dynamic");
                SetVolumeParameter(visualEnvironment, "cloudType", 0);
                SetVolumeEnumParameter(visualEnvironment, "fogType", "None");
                int skyTypeId = ResolveHdrpSkyTypeId(hdriSkyType);
                if (skyTypeId != 0)
                {
                    SetVolumeParameter(visualEnvironment, "skyType", skyTypeId);
                }
            }

            VolumeComponent hdriSky = GetOrAddVolumeComponent(profile, hdriSkyType.AssemblyQualifiedName, hdriSkyType.FullName);
            if (hdriSky == null)
            {
                return;
            }

            SetComponentActive(hdriSky);
            if (cubemap != null)
            {
                SetVolumeParameter(hdriSky, "hdriSky", cubemap);
                SetVolumeParameter(hdriSky, "rotation", 0f);
                SetVolumeParameter(hdriSky, "exposure", exposure);
                SetVolumeParameter(hdriSky, "multiplier", multiplier);
            }
            else
            {
                SetVolumeParameter(hdriSky, "exposure", -8f);
                SetVolumeParameter(hdriSky, "multiplier", 0f);
            }
        }

        private static void ConfigureHdrpScreenSpaceReflections(VolumeProfile profile, bool isGarageProfile)
        {
            VolumeComponent screenSpaceReflection = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.ScreenSpaceReflection, Unity.RenderPipelines.HighDefinition.Runtime");
            if (screenSpaceReflection == null)
            {
                return;
            }

            SetComponentActive(screenSpaceReflection);
            SetVolumeParameter(screenSpaceReflection, "enabled", true);
            SetVolumeParameter(screenSpaceReflection, "enabledTransparent", true);
            SetVolumeParameter(screenSpaceReflection, "reflectSky", true);
            SetVolumeParameter(screenSpaceReflection, "m_MinSmoothness", isGarageProfile ? 0.5f : 0.28f);
            SetVolumeParameter(screenSpaceReflection, "m_SmoothnessFadeStart", isGarageProfile ? 0.34f : 0.16f);
            SetVolumeParameter(screenSpaceReflection, "depthBufferThickness", isGarageProfile ? 0.02f : 0.02f);
            SetVolumeParameter(screenSpaceReflection, "screenFadeDistance", isGarageProfile ? 0.08f : 0.06f);
            SetVolumeParameter(screenSpaceReflection, "accumulationFactor", isGarageProfile ? 0.86f : 0.82f);
            SetVolumeParameter(screenSpaceReflection, "m_RayLength", isGarageProfile ? 36f : 96f);
            SetVolumeParameter(screenSpaceReflection, "m_ClampValue", isGarageProfile ? 8f : 14f);
            SetVolumeParameter(screenSpaceReflection, "m_Denoise", true);
            SetVolumeParameter(screenSpaceReflection, "m_DenoiserRadius", isGarageProfile ? 0.75f : 0.7f);
            SetVolumeParameter(screenSpaceReflection, "m_FullResolution", !isGarageProfile);
        }

        private static float ResolveSkyExposure(Material material, float fallbackExposure)
        {
            if (material == null)
            {
                return fallbackExposure;
            }

            return material.HasProperty("_Exposure") ? material.GetFloat("_Exposure") : fallbackExposure;
        }

        private static void DisableHdrpSkyOverride(VolumeProfile profile)
        {
            Type visualEnvironmentType = FindType("UnityEngine.Rendering.HighDefinition.VisualEnvironment, Unity.RenderPipelines.HighDefinition.Runtime");
            if (visualEnvironmentType != null)
            {
                VolumeComponent visualEnvironment = GetOrAddVolumeComponent(profile, visualEnvironmentType.AssemblyQualifiedName, visualEnvironmentType.FullName);
                if (visualEnvironment != null)
                {
                    SetComponentActive(visualEnvironment);
                    SetVolumeParameter(visualEnvironment, "cloudType", 0);
                    SetVolumeEnumParameter(visualEnvironment, "fogType", "None");
                    SetVolumeEnumParameter(visualEnvironment, "skyAmbientMode", "Dynamic");
                }
            }

            DisableVolumeComponent(profile, "UnityEngine.Rendering.HighDefinition.HDRISky, Unity.RenderPipelines.HighDefinition.Runtime");
            DisableVolumeComponent(profile, "UnityEngine.Rendering.HighDefinition.GradientSky, Unity.RenderPipelines.HighDefinition.Runtime");
            DisableVolumeComponent(profile, "UnityEngine.Rendering.HighDefinition.PhysicallyBasedSky, Unity.RenderPipelines.HighDefinition.Runtime");
        }

        private static void DisableWorldVolumeLookOverrides(VolumeProfile profile)
        {
            DisableVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Bloom, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Bloom, Unity.RenderPipelines.Universal.Runtime");
            DisableVolumeComponent(
                profile,
                "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime",
                "UnityEngine.Rendering.HighDefinition.ColorAdjustments, Unity.RenderPipelines.HighDefinition.Runtime");
            DisableVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Vignette, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime");
            DisableVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.ChromaticAberration, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.ChromaticAberration, Unity.RenderPipelines.Universal.Runtime");
            DisableVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.MotionBlur, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.MotionBlur, Unity.RenderPipelines.Universal.Runtime");
        }

        private static void DisableVolumeComponent(VolumeProfile profile, params string[] typeNames)
        {
            Type type = FindType(typeNames);
            if (type == null)
            {
                return;
            }

            VolumeComponent component = FindMatchingVolumeComponent(profile, type);
            if (component != null)
            {
                SetComponentActive(component, false);
            }
        }



        private static int ResolveHdrpSkyTypeId(Type skyType)
        {
            Type skySettingsType = FindType("UnityEngine.Rendering.HighDefinition.SkySettings, Unity.RenderPipelines.HighDefinition.Runtime");
            MethodInfo getUniqueIdMethod = skySettingsType?.GetMethod("GetUniqueID", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Type) }, null);
            if (getUniqueIdMethod == null)
            {
                return 0;
            }

            object uniqueId = getUniqueIdMethod.Invoke(null, new object[] { skyType });
            return uniqueId is int intId ? intId : 0;
        }

        private static void ConfigureTonemapping(VolumeProfile profile)
        {
            VolumeComponent tonemapping = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Tonemapping, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Tonemapping, Unity.RenderPipelines.Universal.Runtime");
            if (tonemapping == null)
            {
                return;
            }

            SetComponentActive(tonemapping);
            SetVolumeEnumParameter(tonemapping, "mode", "ACES");
            SetVolumeEnumParameter(tonemapping, "tonemappingMode", "ACES");
        }

        private static void DisableTonemapping(VolumeProfile profile)
        {
            DisableVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Tonemapping, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Tonemapping, Unity.RenderPipelines.Universal.Runtime");
        }

        private static void ConfigureVignette(VolumeProfile profile, float intensity, float smoothness)
        {
            VolumeComponent vignette = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Vignette, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.Vignette, Unity.RenderPipelines.Universal.Runtime");
            if (vignette == null)
            {
                return;
            }

            SetComponentActive(vignette);
            SetVolumeParameter(vignette, "intensity", intensity);
            SetVolumeParameter(vignette, "smoothness", smoothness);
        }

        private static void ConfigureChromaticAberration(VolumeProfile profile, float intensity)
        {
            VolumeComponent chromaticAberration = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.ChromaticAberration, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.ChromaticAberration, Unity.RenderPipelines.Universal.Runtime");
            if (chromaticAberration == null)
            {
                return;
            }

            SetComponentActive(chromaticAberration);
            SetVolumeParameter(chromaticAberration, "intensity", intensity);
        }

        private static void ConfigureMotionBlur(VolumeProfile profile, float intensity, float clamp, float maximumVelocity)
        {
            VolumeComponent motionBlur = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.MotionBlur, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.Universal.MotionBlur, Unity.RenderPipelines.Universal.Runtime");
            if (motionBlur == null)
            {
                return;
            }

            SetComponentActive(motionBlur);
            SetVolumeParameter(motionBlur, "intensity", intensity);
            SetVolumeParameter(motionBlur, "clamp", clamp);
            SetVolumeParameter(motionBlur, "maximumVelocity", maximumVelocity);
        }

        // ─── SSRT3 Integration ────────────────────────────────────────────

        /// <summary>
        /// Registers the SSRT_HDRP custom post-process type in the HDRP Global Settings
        /// under the "After Opaque And Sky" injection point. This is Step 1 of the
        /// SSRT3 integration plan.
        /// </summary>
        private static void RegisterSsrtInHdrpGlobalSettings()
        {
            // Attempt to find the SSRT_HDRP type at compile time.
            Type ssrtType = FindType(SsrtHdrpAssemblyQualifiedHint, SsrtHdrpTypeName);
            if (ssrtType == null)
            {
                Debug.LogWarning("[SSRT3] SSRT_HDRP type not found. Ensure the SSRT3 asset is imported under Assets/SSRT3.");
                return;
            }

            string ssrtFullTypeName = ssrtType.AssemblyQualifiedName;

            // Try the project-local global settings first, then the package default.
            UnityEngine.Object globalSettings = AssetDatabase.LoadMainAssetAtPath(HdrpGlobalSettingsAssetPath);
            if (globalSettings == null)
            {
                Debug.LogWarning($"[SSRT3] HDRP Global Settings asset not found at {HdrpGlobalSettingsAssetPath}.");
                return;
            }

            SerializedObject serializedObject = new SerializedObject(globalSettings);

            // The custom post-process orders are stored in a nested structure.
            // We need to find the "After Opaque And Sky" list which doesn't have a
            // direct top-level field but is inside the m_CustomPostProcessOrdersSettings
            // or the legacy flat lists. We search both the new nested structure (used in
            // HDRP 14+) and the legacy flat arrays.
            bool registered = false;

            // --- New structure (m_Settings / RefIds with CustomPostProcessOrdersSettings) ---
            registered = TryRegisterSsrtInRefIds(serializedObject, ssrtFullTypeName);

            // --- Legacy flat arrays (beforeTransparentCustomPostProcesses, etc.) ---
            if (!registered)
            {
                registered = TryRegisterSsrtInLegacyArrays(serializedObject, ssrtFullTypeName);
            }

            // --- Top-level m_CustomPostProcessOrdersSettings ---
            if (!registered)
            {
                registered = TryRegisterSsrtInTopLevelOrders(serializedObject, ssrtFullTypeName);
            }

            if (registered)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(globalSettings);
                Debug.Log("[SSRT3] Registered SSRT_HDRP in HDRP Global Settings → After Opaque And Sky.");
            }
            else
            {
                Debug.LogWarning("[SSRT3] Could not locate the custom post-process injection list in HDRP Global Settings. " +
                                 "Please add SSRT_HDRP manually via Edit > Project Settings > HDRP Global Settings.");
            }
        }

        private static bool TryRegisterSsrtInRefIds(SerializedObject serializedObject, string ssrtFullTypeName)
        {
            SerializedProperty refsProperty = serializedObject.FindProperty("references.RefIds");
            if (refsProperty == null || !refsProperty.isArray)
            {
                return false;
            }

            for (int i = 0; i < refsProperty.arraySize; i++)
            {
                SerializedProperty refElement = refsProperty.GetArrayElementAtIndex(i);
                SerializedProperty classProperty = refElement.FindPropertyRelative("type.class");
                if (classProperty == null || classProperty.stringValue != "CustomPostProcessOrdersSettings")
                {
                    continue;
                }

                // Found the CustomPostProcessOrdersSettings reference.
                // Look for "m_BeforeTransparentCustomPostProcesses" — that's injection point 0 (Before Transparent).
                // SSRT3 needs "After Opaque And Sky" which is NOT one of the standard 5 injection points
                // in the named fields. In HDRP, AfterOpaqueAndSky maps to the m_BeforeTransparentCustomPostProcesses list.
                // The SSRT_HDRP class declares: injectionPoint => CustomPostProcessInjectionPoint.AfterOpaqueAndSky
                // And HDRP 14+ maps this to m_BeforeTransparentCustomPostProcesses (InjectionPoint 0).
                SerializedProperty dataProperty = refElement.FindPropertyRelative("data");
                if (dataProperty == null)
                {
                    continue;
                }

                SerializedProperty beforeTransparent = dataProperty.FindPropertyRelative(
                    "m_BeforeTransparentCustomPostProcesses.m_CustomPostProcessTypesAsString");
                if (beforeTransparent != null && beforeTransparent.isArray)
                {
                    if (!SerializedArrayContains(beforeTransparent, ssrtFullTypeName) &&
                        !SerializedArrayContainsBySimpleName(beforeTransparent, SsrtHdrpTypeName))
                    {
                        beforeTransparent.InsertArrayElementAtIndex(beforeTransparent.arraySize);
                        beforeTransparent.GetArrayElementAtIndex(beforeTransparent.arraySize - 1).stringValue = ssrtFullTypeName;
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool TryRegisterSsrtInLegacyArrays(SerializedObject serializedObject, string ssrtFullTypeName)
        {
            SerializedProperty legacyArray = serializedObject.FindProperty("beforeTransparentCustomPostProcesses");
            if (legacyArray == null || !legacyArray.isArray)
            {
                return false;
            }

            if (!SerializedArrayContains(legacyArray, ssrtFullTypeName) &&
                !SerializedArrayContainsBySimpleName(legacyArray, SsrtHdrpTypeName))
            {
                legacyArray.InsertArrayElementAtIndex(legacyArray.arraySize);
                legacyArray.GetArrayElementAtIndex(legacyArray.arraySize - 1).stringValue = ssrtFullTypeName;
            }

            return true;
        }

        private static bool TryRegisterSsrtInTopLevelOrders(SerializedObject serializedObject, string ssrtFullTypeName)
        {
            SerializedProperty topLevel = serializedObject.FindProperty(
                "m_CustomPostProcessOrdersSettings.m_BeforeTransparentCustomPostProcesses.m_CustomPostProcessTypesAsString");
            if (topLevel == null || !topLevel.isArray)
            {
                return false;
            }

            if (!SerializedArrayContains(topLevel, ssrtFullTypeName) &&
                !SerializedArrayContainsBySimpleName(topLevel, SsrtHdrpTypeName))
            {
                topLevel.InsertArrayElementAtIndex(topLevel.arraySize);
                topLevel.GetArrayElementAtIndex(topLevel.arraySize - 1).stringValue = ssrtFullTypeName;
            }

            return true;
        }

        private static bool SerializedArrayContains(SerializedProperty array, string value)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).stringValue == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SerializedArrayContainsBySimpleName(SerializedProperty array, string simpleName)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                string entry = array.GetArrayElementAtIndex(i).stringValue;
                if (!string.IsNullOrEmpty(entry) && (entry == simpleName || entry.StartsWith(simpleName + ",")))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds the SSRT override to the volume profile with racing-optimized defaults.
        /// This is Step 2 of the SSRT3 integration plan.
        ///
        /// Recommended settings from the plan:
        ///   - Intensity (GIIntensity): 1.0
        ///   - Radius: 4.0 (covers car/road width)
        ///   - Rotation Count: 1 (gameplay) / 4 (garage/photo mode)
        ///   - Step Count: 12
        ///   - Thickness: 0.35 (prevents light leaking behind the car)
        /// </summary>
        private static void ConfigureSsrt(VolumeProfile profile, bool isGarageProfile)
        {
            // SSRT_HDRP is in the global namespace, compiled into Assembly-CSharp.
            VolumeComponent ssrt = GetOrAddVolumeComponent(profile, SsrtHdrpAssemblyQualifiedHint, SsrtHdrpTypeName);
            if (ssrt == null)
            {
                Debug.LogWarning("[SSRT3] Could not add SSRT_HDRP volume component. Ensure the SSRT3 asset is imported.");
                return;
            }

            SetComponentActive(ssrt);

            // Enable the effect
            SetVolumeParameter(ssrt, "enabled", true);

            // Sampling
            SetVolumeParameter(ssrt, "rotationCount", isGarageProfile ? 2 : 1);
            SetVolumeParameter(ssrt, "stepCount", isGarageProfile ? 8 : 10);
            SetVolumeParameter(ssrt, "radius", isGarageProfile ? 1.8f : 3.2f);
            SetVolumeParameter(ssrt, "expFactor", 1.0f);
            SetVolumeParameter(ssrt, "jitterSamples", true);
            SetVolumeParameter(ssrt, "mipOptimization", true);

            // GI — subtle for racing, not overblown
            SetVolumeParameter(ssrt, "GIIntensity", isGarageProfile ? 0.14f : 0.35f);
            SetVolumeParameter(ssrt, "multiBounceGI", 0.0f);
            SetVolumeParameter(ssrt, "normalApproximation", false);
            SetVolumeParameter(ssrt, "backfaceLighting", 0.0f);

            // Occlusion — the "grounded car" magic
            SetVolumeParameter(ssrt, "AOIntensity", isGarageProfile ? 0.2f : 0.45f);
            SetVolumeParameter(ssrt, "thickness", isGarageProfile ? 0.18f : 0.28f);

            // Filters
            SetVolumeParameter(ssrt, "temporalAccumulation", true);
            SetVolumeParameter(ssrt, "temporalResponse", isGarageProfile ? 0.22f : 0.3f);
            SetVolumeParameter(ssrt, "denoising", true);
            SetVolumeParameter(ssrt, "denoisingRadius", isGarageProfile ? 0.32f : 0.42f);

            Debug.Log($"[SSRT3] Configured SSRT override in {(isGarageProfile ? "GarageVolumeProfile" : "WorldVolumeProfile")}.");
        }

        private static void DisableSsrt(VolumeProfile profile)
        {
            DisableVolumeComponent(profile, SsrtHdrpAssemblyQualifiedHint, SsrtHdrpTypeName);
        }

        // ─── End SSRT3 Integration ────────────────────────────────────────

        private static VolumeComponent GetOrAddVolumeComponent(VolumeProfile profile, params string[] typeNames)
        {
            Type type = FindType(typeNames);
            if (type == null)
            {
                return null;
            }

            VolumeComponent existingComponent = FindMatchingVolumeComponent(profile, type);
            if (existingComponent != null)
            {
                return existingComponent;
            }

            MethodInfo addMethod = typeof(VolumeProfile).GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            if (addMethod == null)
            {
                return null;
            }

            try
            {
                return addMethod.Invoke(profile, new object[] { type, true }) as VolumeComponent;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            {
                existingComponent = FindMatchingVolumeComponent(profile, type);
                if (existingComponent != null)
                {
                    return existingComponent;
                }

                return null;
            }
        }

        private static VolumeComponent FindMatchingVolumeComponent(VolumeProfile profile, Type targetType)
        {
            for (int i = 0; i < profile.components.Count; i++)
            {
                VolumeComponent component = profile.components[i];
                if (component == null)
                {
                    continue;
                }

                Type componentType = component.GetType();
                if (componentType == targetType ||
                    targetType.IsAssignableFrom(componentType) ||
                    componentType.IsAssignableFrom(targetType) ||
                    string.Equals(componentType.FullName, targetType.FullName, StringComparison.Ordinal) ||
                    string.Equals(componentType.Name, targetType.Name, StringComparison.Ordinal))
                {
                    return component;
                }
            }

            return null;
        }

        private static void SetComponentActive(VolumeComponent component)
        {
            SetComponentActive(component, true);
        }

        private static void SetComponentActive(VolumeComponent component, bool isActive)
        {
            FieldInfo activeField = typeof(VolumeComponent).GetField("active", BindingFlags.Instance | BindingFlags.Public);
            if (activeField != null)
            {
                activeField.SetValue(component, isActive);
                return;
            }

            PropertyInfo activeProperty = typeof(VolumeComponent).GetProperty("active", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (activeProperty != null && activeProperty.CanWrite)
            {
                activeProperty.SetValue(component, isActive);
            }
        }

        private static void SetVolumeParameter(VolumeComponent component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            object parameter = field.GetValue(component);
            if (parameter == null)
            {
                return;
            }

            MethodInfo overrideMethod = FindOverrideMethod(parameter.GetType());
            if (overrideMethod == null)
            {
                return;
            }

            ParameterInfo[] parameters = overrideMethod.GetParameters();
            if (parameters.Length != 1)
            {
                return;
            }

            object convertedValue = ConvertValue(value, parameters[0].ParameterType);
            overrideMethod.Invoke(parameter, new[] { convertedValue });
        }

        private static void SetVolumeEnumParameter(VolumeComponent component, string fieldName, string enumName)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            object parameter = field.GetValue(component);
            if (parameter == null)
            {
                return;
            }

            MethodInfo overrideMethod = FindOverrideMethod(parameter.GetType());
            if (overrideMethod == null)
            {
                return;
            }

            ParameterInfo[] parameters = overrideMethod.GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum)
            {
                return;
            }

            try
            {
                object enumValue = Enum.Parse(parameters[0].ParameterType, enumName, true);
                overrideMethod.Invoke(parameter, new[] { enumValue });
            }
            catch (ArgumentException)
            {
                Debug.LogWarning($"[Rendering] Enum value '{enumName}' was not found on {parameters[0].ParameterType.FullName}.");
            }
        }

        private static MethodInfo FindOverrideMethod(Type parameterType)
        {
            MethodInfo[] methods = parameterType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == "Override")
                {
                    return methods[i];
                }
            }

            return null;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (targetType.IsEnum)
            {
                if (value is string stringValue)
                {
                    return Enum.Parse(targetType, stringValue);
                }

                return Enum.ToObject(targetType, value);
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(bool))
            {
                return Convert.ToBoolean(value);
            }

            if (targetType.FullName == "UnityEngine.LayerMask")
            {
                LayerMask layerMask = Convert.ToInt32(value);
                return layerMask;
            }

            return value;
        }

        private static void SetPropertyIfPresent(object target, string propertyName, object value)
        {
            if (target == null)
            {
                return;
            }

            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
            {
                return;
            }

            property.SetValue(target, ConvertValue(value, property.PropertyType));
        }
    }
}
