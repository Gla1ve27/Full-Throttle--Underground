using System;
using System.IO;
using FullThrottle.SacredCore.Runtime;
using UnityEngine;

namespace FullThrottle.SacredCore.Save
{
    /// <summary>
    /// Owns the single source of truth profile for the campaign.
    /// </summary>
    [DefaultExecutionOrder(-9900)]
    public sealed class FTSaveGateway : MonoBehaviour
    {
        [SerializeField] private string fileName = "fullthrottle_profile_v1.json";
        [SerializeField] private bool autoSaveOnApplicationQuit = true;

        public FTProfileData Profile { get; private set; } = new();
        public event Action<FTProfileData> ProfileLoaded;

        private string FullPath => Path.Combine(Application.persistentDataPath, fileName);

        public void LoadOrCreate()
        {
            if (File.Exists(FullPath))
            {
                try
                {
                    string json = File.ReadAllText(FullPath);
                    FTProfileEnvelope envelope = JsonUtility.FromJson<FTProfileEnvelope>(json);
                    Profile = envelope?.profile ?? new FTProfileData();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SacredCore] Save load failed, creating a fresh profile. {ex.Message}");
                    Profile = new FTProfileData();
                }
            }
            else
            {
                Profile = new FTProfileData();
                Save();
            }

            ProfileLoaded?.Invoke(Profile);
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FullPath) ?? Application.persistentDataPath);
            FTProfileEnvelope envelope = new() { profile = Profile };
            string json = JsonUtility.ToJson(envelope, true);
            File.WriteAllText(FullPath, json);
        }

        public void CreateFreshProfile(string starterCarId)
        {
            Profile = new FTProfileData();
            Profile.EnsureDefaults(starterCarId);
            Save();
            ProfileLoaded?.Invoke(Profile);
        }

        private void OnApplicationQuit()
        {
            if (autoSaveOnApplicationQuit)
            {
                Save();
            }
        }
    }
}
