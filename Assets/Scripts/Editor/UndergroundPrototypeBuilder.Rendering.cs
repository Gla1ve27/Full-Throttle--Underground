using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private const string UrpGlobalSettingsAssetPath = "Assets/Settings/ProjectURP/UniversalRenderPipelineGlobalSettings.asset";
        private const string HdrpGlobalSettingsAssetPath = "Assets/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset";


        [MenuItem("Underground/Project/Enable Post Processing", priority = 11)]
        public static void EnableSsrRendererFeaturesFromMenu()
        {
            EnsureProjectFolders();
            ConfigureRenderPipeline();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Rendering Ready", "Configured the active render pipeline volume profile, post-processing, and camera support.", "OK");
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

            if (worldProfile != null) ConfigureWorldVolumeProfile(worldProfile);
            if (garageProfile != null) ConfigureGarageVolumeProfile(garageProfile);

            if (HasHdrpPackageInstalled())
            {
                if (worldProfile != null) ConfigureHdrpScreenSpaceReflections(worldProfile, false);
                if (garageProfile != null) ConfigureHdrpScreenSpaceReflections(garageProfile, true);
            }

            if (worldProfile != null) EditorUtility.SetDirty(worldProfile);
            if (garageProfile != null) EditorUtility.SetDirty(garageProfile);
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
            // Pushed to the absolute flagship level for "Full-Throttle"
            ConfigureHdrpExposure(profile, 10.8f, -0.35f); // Natural automatic range
            
            ConfigureTonemapping(profile);
            ConfigureBloom(profile, 1.25f, 0.04f, 0.65f);
            ConfigureColorAdjustments(profile, 0.35f, 12f, 8f);
            
            // Professional Color Grading suite
            ConfigureSplitToning(profile);
            ConfigureShadowsMidtonesHighlights(profile);
            
            ConfigureHdrpScreenSpaceReflections(profile, false);
            ConfigureAmbientOcclusion(profile, 0.95f, 0.85f);
            ConfigureContactShadows(profile, 0.18f);
            ConfigureMicroShadows(profile); // Micro-detail for tires and vents
            
            ConfigureFog(profile, 0.04f);
            ConfigureFilmGrain(profile);
            ConfigureVignette(profile, 0.45f, 0.28f);
            ConfigureChromaticAberration(profile, 0.08f); // High-end lens distortion
            
            // Professional bokeh for background buildings
            ConfigureDepthOfField(profile);

            DisableWorldVolumeLookOverrides(profile);
            ConfigureHdrpSky(profile, LoadDaySkyCubemap(), ResolveSkyExposure(LoadDaySkyboxMaterial(), 0.95f), 1f);
        }

        private static void ConfigureGarageVolumeProfile(VolumeProfile profile)
        {
            // Pushed to a bright, glossy "Premium Showroom" look.
            ConfigureHdrpExposure(profile, 8.5f, 0.4f); 
            
            ConfigureTonemapping(profile);
            ConfigureBloom(profile, 1.8f, 0.05f, 0.4f);
            ConfigureColorAdjustments(profile, 0.05f, 12f, 8f); // Punchy contrast and saturation.
            
            ConfigureVignette(profile, 0.35f, 0.25f);
            ConfigureChromaticAberration(profile, 0.05f);
            
            // Critical for the "glossy" garage car feel.
            ConfigureHdrpScreenSpaceReflections(profile, true);
            
            // Ground the car in the garage.
            ConfigureAmbientOcclusion(profile, 0.85f, 0.5f);
            ConfigureContactShadows(profile);

            ConfigureHdrpSky(profile, null, -10f, 0f); // Pure dark background for focus.
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
            
            // Switching back to Automatic Histogram for peak dynamic range.
            SetVolumeEnumParameter(exposure, "mode", "Automatic");
            SetVolumeParameter(exposure, "meteringMode", 2); // MaskWeighted
            SetVolumeParameter(exposure, "limitMin", 8f);
            SetVolumeParameter(exposure, "limitMax", 14f);
            SetVolumeParameter(exposure, "compensation", compensation);
            SetVolumeParameter(exposure, "fixedExposure", fixedExposure);
        }

        private static void ConfigureContactShadows(VolumeProfile profile, float length = 0.15f)
        {
            VolumeComponent contactShadows = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.ContactShadows, Unity.RenderPipelines.HighDefinition.Runtime");
            if (contactShadows == null) return;

            SetComponentActive(contactShadows);
            SetVolumeParameter(contactShadows, "enable", true);
            SetVolumeParameter(contactShadows, "length", length); 
            SetVolumeParameter(contactShadows, "opacity", 1.0f);
        }

        private static void ConfigureDepthOfField(VolumeProfile profile)
        {
            VolumeComponent dof = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.DepthOfField, Unity.RenderPipelines.HighDefinition.Runtime");
            if (dof == null) return;

            SetComponentActive(dof);
            SetVolumeEnumParameter(dof, "focusMode", "UsePhysicalCamera");
            SetVolumeParameter(dof, "nearBlurStart", 1f);
            SetVolumeParameter(dof, "nearBlurEnd", 3f);
            SetVolumeParameter(dof, "farBlurStart", 250f);
            SetVolumeParameter(dof, "farBlurEnd", 800f);
            SetVolumeParameter(dof, "nearSampleCount", 5);
            SetVolumeParameter(dof, "farSampleCount", 7);
        }

        private static void ConfigureSplitToning(VolumeProfile profile)
        {
            VolumeComponent split = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.SplitToning, Unity.RenderPipelines.HighDefinition.Runtime");
            if (split == null) return;

            SetComponentActive(split);
            SetVolumeParameter(split, "shadows", new Color(0.15f, 0.18f, 0.25f)); // Cool darks
            SetVolumeParameter(split, "highlights", new Color(0.42f, 0.38f, 0.35f)); // Warm peaks
            SetVolumeParameter(split, "balance", 12f);
        }

        private static void ConfigureShadowsMidtonesHighlights(VolumeProfile profile)
        {
            VolumeComponent smh = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.ShadowsMidtonesHighlights, Unity.RenderPipelines.HighDefinition.Runtime");
            if (smh == null) return;

            SetComponentActive(smh);
            SetVolumeParameter(smh, "shadows", new Vector4(1f, 1f, 1f, -0.05f)); // Deepen blacks
            SetVolumeParameter(smh, "highlights", new Vector4(1f, 1f, 1f, 0.12f)); // Punch peaks
        }

        private static void ConfigureMicroShadows(VolumeProfile profile)
        {
            VolumeComponent ms = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.MicroShadows, Unity.RenderPipelines.HighDefinition.Runtime");
            if (ms == null) return;

            SetComponentActive(ms);
            SetVolumeParameter(ms, "opacity", 1.0f);
        }

        private static void ConfigureFilmGrain(VolumeProfile profile)
        {
            VolumeComponent grain = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.FilmGrain, Unity.RenderPipelines.HighDefinition.Runtime");
            if (grain == null) return;

            SetComponentActive(grain);
            SetVolumeEnumParameter(grain, "type", "Medium1");
            SetVolumeParameter(grain, "intensity", 0.12f);
            SetVolumeParameter(grain, "response", 0.8f);
        }

        private static void ConfigureFog(VolumeProfile profile, float density)
        {
            VolumeComponent fog = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.Fog, Unity.RenderPipelines.HighDefinition.Runtime");
            if (fog == null) return;

            SetComponentActive(fog);
            SetVolumeParameter(fog, "enabled", true);
            SetVolumeParameter(fog, "meanFreePath", 400f);
            SetVolumeParameter(fog, "albedo", new Color(0.81f, 0.88f, 1.0f));
        }

        private static void ConfigureAmbientOcclusion(VolumeProfile profile, float intensity, float radius)
        {
            // Note: In some HDRP versions, AO is part of 'AmbientOcclusion'.
            // In others, it uses 'ScreenSpaceAmbientOcclusion'.
            // Providing multiple fallbacks to resolve the "ExtensionOfNativeClass" error.
            VolumeComponent ao = GetOrAddVolumeComponent(
                profile,
                "UnityEngine.Rendering.HighDefinition.AmbientOcclusion, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.HighDefinition.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.HighDefinition.Runtime");
            
            if (ao == null) return;

            SetComponentActive(ao);
            SetVolumeParameter(ao, "intensity", intensity);
            SetVolumeParameter(ao, "radius", radius);
            
            // Try setting both possible parameter names to support multiple HDRP versions.
            SetVolumeParameter(ao, "directLightStrength", 0.5f);
            SetVolumeParameter(ao, "quality", 2); 
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

        private static VolumeComponent GetOrAddVolumeComponent(VolumeProfile profile, params string[] typeNames)
        {
            if (profile == null) return null;
            
            Type type = FindType(typeNames);
            if (type == null) return null;

            VolumeComponent existingComponent = FindMatchingVolumeComponent(profile, type);
            if (existingComponent != null) return existingComponent;

            // In some HDRP versions, profile.Add(type) fails with ExtensionOfNativeClass via reflection.
            try
            {
                MethodInfo addMethod = typeof(VolumeProfile).GetMethod("Add", new[] { typeof(System.Type), typeof(bool) });
                if (addMethod != null)
                {
                    object result = addMethod.Invoke(profile, new object[] { type, true });
                    if (result != null) return result as VolumeComponent;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Rendering] Detailed Add Failure for {type.Name}: {ex.InnerException?.Message ?? ex.Message}");
            }

            // Absolute final fallback to internal public API
            try
            {
                return profile.Add(type, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Rendering] Fallback Add Failure for {type.Name}: {ex.Message}");
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
