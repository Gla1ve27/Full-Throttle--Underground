using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UG2Audio.Import;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UG2Audio.Editor
{
    public sealed class UG2ExternalGinDecoder : IUG2AudioDecoder
    {
        private readonly string executablePath;
        private readonly string outputAssetFolder;
        private bool pathVisibilityLogged;

        public UG2ExternalGinDecoder(string executablePath, string outputAssetFolder)
        {
            this.executablePath = executablePath;
            this.outputAssetFolder = outputAssetFolder;
        }

        public bool CanDecode(UG2SourceAssetRecord source)
        {
            return source != null &&
                   string.Equals(source.extension, ".gin", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryDecode(UG2SourceAssetRecord source, out AudioClip clip, out string diagnostic)
        {
            clip = null;
            diagnostic = string.Empty;

            if (source == null)
            {
                diagnostic = "No source asset was provided.";
                return false;
            }

            if (!string.Equals(source.extension, ".gin", StringComparison.OrdinalIgnoreCase))
            {
                diagnostic = "Source asset is not a GIN stream: " + source.fileName + " (" + source.extension + ").";
                return false;
            }

            string decoder = ResolveExecutable();
            if (string.IsNullOrEmpty(decoder))
            {
                diagnostic = "No GIN decoder executable found. Install vgmstream-cli, add it to Unity's PATH, or set Tools/UG2 Audio/Decoder/Set vgmstream-cli Path.";
                Debug.LogWarning("[UG2ExternalGinDecoder] " + diagnostic);
                return false;
            }

            string outputFolder = EnsureAssetFolder(outputAssetFolder);
            string wavAssetPath = outputFolder + "/" + Path.GetFileNameWithoutExtension(source.fileName) + ".wav";
            string wavProjectPath = ToProjectFilePath(wavAssetPath);
            string outputDirectory = Path.GetDirectoryName(wavProjectPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            if (File.Exists(wavProjectPath))
                File.Delete(wavProjectPath);

            string workingDirectory = ResolveWorkingDirectory(source);
            string arguments = "-o \"" + wavProjectPath + "\" \"" + source.sourcePath + "\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = decoder,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Debug.Log("[UG2ExternalGinDecoder] Output WAV path before decode: " + wavProjectPath);
            Debug.Log("[UG2ExternalGinDecoder] Working directory: " + workingDirectory);
            Debug.Log("[UG2ExternalGinDecoder] Command line: " + FormatCommandLine(decoder, arguments));

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        diagnostic = "Failed to start decoder process.";
                        return false;
                    }

                    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    string stdout = stdoutTask.Result;
                    string stderr = stderrTask.Result;
                    bool wavExists = File.Exists(wavProjectPath);

                    Debug.Log("[UG2ExternalGinDecoder] Process exit code: " + process.ExitCode);
                    Debug.Log("[UG2ExternalGinDecoder] stdout:\n" + (string.IsNullOrEmpty(stdout) ? "<empty>" : stdout));
                    Debug.Log("[UG2ExternalGinDecoder] stderr:\n" + (string.IsNullOrEmpty(stderr) ? "<empty>" : stderr));
                    Debug.Log("[UG2ExternalGinDecoder] WAV exists after process exit: " + wavExists + " (" + wavProjectPath + ")");

                    if (process.ExitCode != 0 || !wavExists)
                    {
                        diagnostic = "Decoder failed with exit code " + process.ExitCode + ". Output WAV exists: " + wavExists + ". stdout: " + stdout + " stderr: " + stderr;
                        return false;
                    }
                }

                Debug.Log("[UG2ExternalGinDecoder] Unity-relative import path: " + wavAssetPath);
                AssetDatabase.ImportAsset(wavAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavAssetPath);
                Debug.Log("[UG2ExternalGinDecoder] AssetDatabase.LoadAssetAtPath<AudioClip> succeeded: " + (clip != null) + " (" + wavAssetPath + ")");
                diagnostic = clip == null ? "Decoded WAV was imported, but Unity did not load it as an AudioClip." : "Decoded " + source.fileName;
                return clip != null;
            }
            catch (Exception ex)
            {
                diagnostic = ex.Message;
                Debug.LogException(ex);
                return false;
            }
        }

        private string ResolveExecutable()
        {
            if (!pathVisibilityLogged)
            {
                UG2ExternalGinDecoderPreferences.LogPathVisibility();
                pathVisibilityLogged = true;
            }

            if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                return executablePath;

            string configuredPath = UG2ExternalGinDecoderPreferences.GetConfiguredExecutablePath();
            if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
                return configuredPath;

            if (!string.IsNullOrEmpty(configuredPath))
                Debug.LogWarning("[UG2ExternalGinDecoder] Configured vgmstream-cli path does not exist: " + configuredPath);

            string fromPath = FindOnPath("vgmstream-cli.exe");
            if (!string.IsNullOrEmpty(fromPath))
                return fromPath;

            return FindOnPath("vgmstream-cli");
        }

        private static string FindOnPath(string executableName)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string[] parts = path.Split(Path.PathSeparator);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim().Trim('"');
                if (string.IsNullOrEmpty(part))
                    continue;

                string candidate;
                try
                {
                    candidate = Path.Combine(part, executableName);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(candidate))
                    return candidate;
            }

            return string.Empty;
        }

        private static string ResolveWorkingDirectory(UG2SourceAssetRecord source)
        {
            if (source != null && !string.IsNullOrEmpty(source.sourcePath))
            {
                string sourceDirectory = Path.GetDirectoryName(source.sourcePath);
                if (!string.IsNullOrEmpty(sourceDirectory) && Directory.Exists(sourceDirectory))
                    return sourceDirectory;
            }

            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string FormatCommandLine(string fileName, string arguments)
        {
            return "\"" + fileName.Replace("\"", "\\\"") + "\" " + arguments;
        }

        private static string EnsureAssetFolder(string assetPath)
        {
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return assetPath;
        }

        private static string ToProjectFilePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relative);
        }
    }

    internal static class UG2ExternalGinDecoderPreferences
    {
        private const string DecoderPathEditorPrefsKey = "FullThrottle.UG2Audio.VgmstreamCliPath";

        public static string GetConfiguredExecutablePath()
        {
            return EditorPrefs.GetString(DecoderPathEditorPrefsKey, string.Empty);
        }

        public static void SetConfiguredExecutablePath(string path)
        {
            EditorPrefs.SetString(DecoderPathEditorPrefsKey, path ?? string.Empty);
        }

        [MenuItem("Tools/UG2 Audio/Decoder/Set vgmstream-cli Path")]
        private static void SetDecoderPathMenu()
        {
            string current = GetConfiguredExecutablePath();
            string startDirectory = !string.IsNullOrEmpty(current) && File.Exists(current)
                ? Path.GetDirectoryName(current)
                : string.Empty;
            string selected = EditorUtility.OpenFilePanel("Select vgmstream-cli executable", startDirectory, "exe");
            if (string.IsNullOrEmpty(selected))
                return;

            SetConfiguredExecutablePath(selected);
            Debug.Log("[UG2ExternalGinDecoder] Stored vgmstream-cli path in EditorPrefs: " + selected);
            LogPathVisibility();
        }

        [MenuItem("Tools/UG2 Audio/Decoder/Clear vgmstream-cli Path")]
        private static void ClearDecoderPathMenu()
        {
            SetConfiguredExecutablePath(string.Empty);
            Debug.Log("[UG2ExternalGinDecoder] Cleared configured vgmstream-cli path from EditorPrefs.");
            LogPathVisibility();
        }

        [MenuItem("Tools/UG2 Audio/Decoder/Log vgmstream-cli PATH Visibility")]
        public static void LogPathVisibility()
        {
            string configuredPath = GetConfiguredExecutablePath();
            string pathExecutable = FindOnPathForPreferences("vgmstream-cli.exe");
            if (string.IsNullOrEmpty(pathExecutable))
                pathExecutable = FindOnPathForPreferences("vgmstream-cli");

            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string configuredMessage = string.IsNullOrEmpty(configuredPath)
                ? "<empty>"
                : configuredPath + " exists=" + File.Exists(configuredPath);
            string pathMessage = string.IsNullOrEmpty(pathExecutable)
                ? "not found on Unity PATH"
                : pathExecutable;

            Debug.Log("[UG2ExternalGinDecoder] Configured vgmstream-cli path: " + configuredMessage);
            Debug.Log("[UG2ExternalGinDecoder] Unity PATH vgmstream-cli lookup: " + pathMessage);
            Debug.Log("[UG2ExternalGinDecoder] Unity PATH value: " + (string.IsNullOrEmpty(path) ? "<empty>" : path));
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Preferences/UG2 Audio", SettingsScope.User)
            {
                label = "UG2 Audio",
                keywords = new HashSet<string>(new[] { "UG2", "Audio", "GIN", "vgmstream" }),
                guiHandler = searchContext =>
                {
                    string current = GetConfiguredExecutablePath();
                    EditorGUILayout.LabelField("External GIN Decoder", EditorStyles.boldLabel);
                    string next = EditorGUILayout.TextField("vgmstream-cli Path", current);
                    if (!string.Equals(next, current, StringComparison.Ordinal))
                        SetConfiguredExecutablePath(next);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Browse"))
                        SetDecoderPathMenu();
                    if (GUILayout.Button("Clear"))
                        SetConfiguredExecutablePath(string.Empty);
                    if (GUILayout.Button("Log PATH Visibility"))
                        LogPathVisibility();
                    EditorGUILayout.EndHorizontal();

                    string configured = GetConfiguredExecutablePath();
                    bool configuredExists = !string.IsNullOrEmpty(configured) && File.Exists(configured);
                    EditorGUILayout.HelpBox(
                        "Configured path exists: " + configuredExists + "\n" +
                        "If this is false, imports will fall back to Unity's PATH lookup for vgmstream-cli.",
                        configuredExists ? MessageType.Info : MessageType.Warning);
                }
            };

            return provider;
        }

        private static string FindOnPathForPreferences(string executableName)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string[] parts = path.Split(Path.PathSeparator);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim().Trim('"');
                if (string.IsNullOrEmpty(part))
                    continue;

                string candidate;
                try
                {
                    candidate = Path.Combine(part, executableName);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(candidate))
                    return candidate;
            }

            return string.Empty;
        }
    }
}
