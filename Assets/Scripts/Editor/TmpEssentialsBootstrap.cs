using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Underground.EditorTools
{
    [InitializeOnLoad]
    public static class TmpEssentialsBootstrap
    {
        private const string TmpSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string SessionKey = "Underground.TmpEssentialsChecked";

        static TmpEssentialsBootstrap()
        {
            EditorApplication.delayCall += EnsureTmpEssentials;
        }

        [MenuItem("Underground/Project/Import/TMP Essential Resources")]
        private static void ImportTmpEssentialsManually()
        {
            EnsureTmpEssentials(true);
        }

        private static void EnsureTmpEssentials()
        {
            EnsureTmpEssentials(false);
        }

        private static void EnsureTmpEssentials(bool force)
        {
            if (!force && SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);

            string fullSettingsPath = Path.Combine(Application.dataPath, "TextMesh Pro/Resources/TMP Settings.asset");
            if (!force && File.Exists(fullSettingsPath))
            {
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
            string reason = force ? "by request" : $"because '{TmpSettingsAssetPath}' was missing";
            Debug.Log($"Imported TMP essential resources {reason}.");
        }
    }
}
