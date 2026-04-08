using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;

namespace Underground.EditorTools
{
    [InitializeOnLoad]
    public static class UndergroundHdrpProjectConverter
    {
        private const string HdrpPackageId = "com.unity.render-pipelines.high-definition";
        private const string FcgHdrpPackagePath = "Assets/FCG/FCG-HDRP.unitypackage";
        private const string SimpleRetroHdrpPackagePath = "Assets/Polyeler/Simple Retro Car/HDRP_ExtractMe.unitypackage";
        private const string ManifestPath = "Packages/manifest.json";
        private const string EmbeddedUrpSsrPackagePath = "Packages/com.limworks.urpssrpackage";
        private const string ConversionQueuedKey = "Underground.HdrpConversionQueued";
        private const string ConversionCompletedKey = "Underground.HdrpConversionCompleted";
        private const string FcgImportKey = "Underground.HdrpImported.Fcg";
        private const string RetroImportKey = "Underground.HdrpImported.SimpleRetro";

        private static AddRequest addHdrpRequest;

        static UndergroundHdrpProjectConverter()
        {
            if (!EditorPrefs.GetBool(ConversionCompletedKey, false) || NeedsHdrpRepair())
            {
                SessionState.SetBool(ConversionQueuedKey, true);
                EditorApplication.delayCall += ProcessQueuedConversion;
            }
        }

        [MenuItem("Underground/Project/Convert Project To HDRP", priority = 9)]
        public static void ConvertProjectToHdrpNow()
        {
            SessionState.SetBool(ConversionQueuedKey, true);
            EditorPrefs.SetBool(ConversionCompletedKey, false);
            EditorApplication.delayCall += ProcessQueuedConversion;
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

            if (!HasHdrpPackageInstalled())
            {
                BeginHdrpPackageInstall();
                return;
            }

            ImportPackageOnce(FcgHdrpPackagePath, FcgImportKey);
            ImportPackageOnce(SimpleRetroHdrpPackagePath, RetroImportKey);
            PruneLegacyUrpPackages();
            UndergroundPrototypeBuilder.ApplyHdrpRenderPipeline(false);
            ArcadeCarsHdrpMaterialConverter.ConvertMaterials(false);
            UndergroundPrototypeBuilder.RebuildGeneratedScenePrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SessionState.SetBool(ConversionQueuedKey, false);
            EditorPrefs.SetBool(ConversionCompletedKey, true);
            Debug.Log("HDRP conversion finished. Reopen scenes so imported HDRP assets and rebuilt prefabs are used everywhere.");
        }

        private static bool NeedsHdrpRepair()
        {
            if (!HasHdrpPackageInstalled())
            {
                return true;
            }

            RenderPipelineAsset defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            return defaultPipeline == null || !defaultPipeline.GetType().FullName.Contains("HighDefinition");
        }

        private static void BeginHdrpPackageInstall()
        {
            if (addHdrpRequest != null)
            {
                return;
            }

            addHdrpRequest = Client.Add(HdrpPackageId);
            EditorApplication.update += WaitForHdrpPackageInstall;
            Debug.Log("Installing HDRP package for project conversion.");
        }

        private static void WaitForHdrpPackageInstall()
        {
            if (addHdrpRequest == null || !addHdrpRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= WaitForHdrpPackageInstall;

            if (addHdrpRequest.Status == StatusCode.Success)
            {
                Debug.Log("HDRP package installed.");
            }
            else if (addHdrpRequest.Status >= StatusCode.Failure)
            {
                Debug.LogError($"HDRP package install failed: {addHdrpRequest.Error?.message}");
            }

            addHdrpRequest = null;
            EditorApplication.delayCall += ProcessQueuedConversion;
        }

        private static void ImportPackageOnce(string packagePath, string editorPrefKey)
        {
            if (EditorPrefs.GetBool(editorPrefKey, false))
            {
                return;
            }

            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), packagePath);
            if (!File.Exists(absolutePath))
            {
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
            EditorPrefs.SetBool(editorPrefKey, true);
        }

        private static bool HasHdrpPackageInstalled()
        {
            return FindType(
                "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset, Unity.RenderPipelines.HighDefinition.Runtime",
                "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime") != null;
        }

        private static void PruneLegacyUrpPackages()
        {
            string manifestFullPath = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
            if (File.Exists(manifestFullPath))
            {
                string manifest = File.ReadAllText(manifestFullPath);
                string[] lines = manifest.Replace("\r\n", "\n").Split('\n');
                List<string> filteredLines = new List<string>(lines.Length);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("\"com.unity.render-pipelines.universal\""))
                    {
                        continue;
                    }

                    filteredLines.Add(lines[i]);
                }

                File.WriteAllText(manifestFullPath, string.Join(Environment.NewLine, filteredLines));
            }

            string embeddedPackageFullPath = Path.Combine(Directory.GetCurrentDirectory(), EmbeddedUrpSsrPackagePath);
            if (Directory.Exists(embeddedPackageFullPath))
            {
                FileUtil.DeleteFileOrDirectory(embeddedPackageFullPath);
                FileUtil.DeleteFileOrDirectory($"{embeddedPackageFullPath}.meta");
            }
        }

        private static Type FindType(params string[] typeNames)
        {
            for (int i = 0; i < typeNames.Length; i++)
            {
                Type type = Type.GetType(typeNames[i]);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
