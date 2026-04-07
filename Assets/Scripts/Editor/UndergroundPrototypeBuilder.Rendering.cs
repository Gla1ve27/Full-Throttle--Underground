using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Underground.EditorTools
{
    public static partial class UndergroundPrototypeBuilder
    {
        private const string UrpGlobalSettingsAssetPath = "Assets/Settings/ProjectURP/UniversalRenderPipelineGlobalSettings.asset";

        [MenuItem("Underground/Project/Enable SSR Renderer Features", priority = 11)]
        public static void EnableSsrRendererFeaturesFromMenu()
        {
            EnsureProjectFolders();
            ConfigureRenderPipeline();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("SSR Ready", "Enabled the URP SSR renderer features, volume profile, and post-processing pipeline support.", "OK");
        }

        private static void EnsureSsrRendererFeatures()
        {
            ConfigureUrpCompatibilityMode();
        }

        private static void ConfigureDefaultVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectDefaultVolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProjectDefaultVolumeProfilePath);
            }

            Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.72f);

            ColorAdjustments colorAdjustments = GetOrAddVolumeComponent<ColorAdjustments>(profile);
            colorAdjustments.active = true;
            colorAdjustments.postExposure.Override(0.12f);
            colorAdjustments.contrast.Override(6f);
            colorAdjustments.saturation.Override(4f);

            Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.35f);

            ChromaticAberration chromaticAberration = GetOrAddVolumeComponent<ChromaticAberration>(profile);
            chromaticAberration.active = true;
            chromaticAberration.intensity.Override(0.015f);

            MotionBlur motionBlur = GetOrAddVolumeComponent<MotionBlur>(profile);
            motionBlur.active = true;
            motionBlur.intensity.Override(0f);
            motionBlur.clamp.Override(0.08f);

            EditorUtility.SetDirty(profile);
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
            }

            return component;
        }

        private static void AttachGlobalVolume(Transform parent, string objectName)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProjectDefaultVolumeProfilePath);
            if (profile == null)
            {
                return;
            }

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
            UniversalAdditionalCameraData additionalCameraData = camera.GetComponent<UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            additionalCameraData.renderPostProcessing = true;
            additionalCameraData.stopNaN = true;
            additionalCameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            additionalCameraData.volumeLayerMask = ~0;
            additionalCameraData.volumeTrigger = camera.transform;
        }

        private static void ConfigureUrpCompatibilityMode()
        {
            Object globalSettings = AssetDatabase.LoadMainAssetAtPath(UrpGlobalSettingsAssetPath);
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
    }
}
