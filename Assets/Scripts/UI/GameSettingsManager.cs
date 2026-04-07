using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Underground.UI
{
    public class GameSettingsManager : MonoBehaviour
    {
        private const string MasterVolumeKey = "UG_MasterVolume";
        private const string MusicVolumeKey = "UG_MusicVolume";
        private const string SfxVolumeKey = "UG_SfxVolume";
        private const string FullscreenKey = "UG_Fullscreen";
        private const string ResolutionIndexKey = "UG_ResolutionIndex";
        private const string VSyncKey = "UG_VSync";
        private const string QualityLevelKey = "UG_QualityLevel";
        private const string ShadowQualityKey = "UG_ShadowQuality";
        private const string TextureQualityKey = "UG_TextureQuality";
        private const string ShowHudKey = "UG_ShowHud";
        private const string CameraEffectsKey = "UG_CameraEffects";
        private const string CameraFieldOfViewKey = "UG_CameraFov";
        private const string InvertCameraYKey = "UG_InvertCameraY";
        private const string SteeringSensitivityKey = "UG_SteeringSensitivity";
        private const string PedalSensitivityKey = "UG_PedalSensitivity";
        private const string ReverseTapWindowKey = "UG_ReverseTapWindow";

        private static GameSettingsManager instance;

        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParameter = "MasterVolume";
        [SerializeField] private string musicVolumeParameter = "MusicVolume";
        [SerializeField] private string sfxVolumeParameter = "SFXVolume";

        [Header("UI")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        public event Action SettingsChanged;

        public float MasterVolume { get; private set; }
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public bool Fullscreen { get; private set; }
        public int ResolutionIndex { get; private set; }
        public bool VSyncEnabled { get; private set; }
        public int QualityLevel { get; private set; }
        public int ShadowQuality { get; private set; }
        public int TextureQuality { get; private set; }
        public bool ShowHud { get; private set; }
        public bool CameraEffectsEnabled { get; private set; }
        public float CameraFieldOfView { get; private set; }
        public bool InvertCameraY { get; private set; }
        public float SteeringSensitivity { get; private set; }
        public float PedalSensitivity { get; private set; }
        public float ReverseDoubleTapWindow { get; private set; }
        public Vector2 ReferenceResolution => referenceResolution;
        public static GameSettingsManager Instance => instance;

        private Resolution[] availableResolutions = Array.Empty<Resolution>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            CacheAvailableResolutions();
            LoadSettings();
            ApplyAllSettings(notifyListeners: false);
        }

        public IReadOnlyList<Resolution> GetAvailableResolutions()
        {
            if (availableResolutions == null || availableResolutions.Length == 0)
            {
                CacheAvailableResolutions();
            }

            return availableResolutions;
        }

        public string GetCurrentResolutionLabel()
        {
            if (availableResolutions == null || availableResolutions.Length == 0)
            {
                return $"{Screen.currentResolution.width}x{Screen.currentResolution.height}";
            }

            int clampedIndex = Mathf.Clamp(ResolutionIndex, 0, availableResolutions.Length - 1);
            Resolution resolution = availableResolutions[clampedIndex];
            return $"{resolution.width}x{resolution.height}";
        }

        public string GetQualityLabel()
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0)
            {
                return "Custom";
            }

            int clampedIndex = Mathf.Clamp(QualityLevel, 0, qualityNames.Length - 1);
            return qualityNames[clampedIndex];
        }

        public string GetShadowQualityLabel()
        {
            return ShadowQuality switch
            {
                0 => "Off",
                1 => "Low",
                _ => "High"
            };
        }

        public string GetTextureQualityLabel()
        {
            return TextureQuality switch
            {
                0 => "Low",
                1 => "Medium",
                _ => "High"
            };
        }

        public void RefreshAll()
        {
            ApplyAllSettings(notifyListeners: true);
        }

        public void CycleResolution(int direction)
        {
            if (availableResolutions == null || availableResolutions.Length == 0)
            {
                return;
            }

            int nextIndex = ResolutionIndex + Math.Sign(direction);
            if (nextIndex < 0)
            {
                nextIndex = availableResolutions.Length - 1;
            }
            else if (nextIndex >= availableResolutions.Length)
            {
                nextIndex = 0;
            }

            SetResolutionIndex(nextIndex);
        }

        public void SetResolutionIndex(int index)
        {
            if (availableResolutions == null || availableResolutions.Length == 0)
            {
                return;
            }

            ResolutionIndex = Mathf.Clamp(index, 0, availableResolutions.Length - 1);
            PlayerPrefs.SetInt(ResolutionIndexKey, ResolutionIndex);
            ApplyDisplaySettings();
            NotifySettingsChanged();
        }

        public void ToggleFullscreen()
        {
            SetFullscreen(!Fullscreen);
        }

        public void SetFullscreen(bool fullscreen)
        {
            Fullscreen = fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen ? 1 : 0);
            ApplyDisplaySettings();
            NotifySettingsChanged();
        }

        public void ToggleVSync()
        {
            SetVSync(!VSyncEnabled);
        }

        public void SetVSync(bool enabled)
        {
            VSyncEnabled = enabled;
            PlayerPrefs.SetInt(VSyncKey, VSyncEnabled ? 1 : 0);
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
            NotifySettingsChanged();
        }

        public void CycleQualityLevel(int direction)
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0)
            {
                return;
            }

            int nextIndex = QualityLevel + Math.Sign(direction);
            if (nextIndex < 0)
            {
                nextIndex = qualityNames.Length - 1;
            }
            else if (nextIndex >= qualityNames.Length)
            {
                nextIndex = 0;
            }

            SetQualityLevel(nextIndex);
        }

        public void SetQualityLevel(int level)
        {
            string[] qualityNames = QualitySettings.names;
            if (qualityNames == null || qualityNames.Length == 0)
            {
                return;
            }

            QualityLevel = Mathf.Clamp(level, 0, qualityNames.Length - 1);
            PlayerPrefs.SetInt(QualityLevelKey, QualityLevel);
            QualitySettings.SetQualityLevel(QualityLevel, true);
            ApplyShadowQuality();
            ApplyTextureQuality();
            NotifySettingsChanged();
        }

        public void CycleShadowQuality(int direction)
        {
            int nextValue = ShadowQuality + Math.Sign(direction);
            if (nextValue < 0)
            {
                nextValue = 2;
            }
            else if (nextValue > 2)
            {
                nextValue = 0;
            }

            SetShadowQuality(nextValue);
        }

        public void SetShadowQuality(int quality)
        {
            ShadowQuality = Mathf.Clamp(quality, 0, 2);
            PlayerPrefs.SetInt(ShadowQualityKey, ShadowQuality);
            ApplyShadowQuality();
            NotifySettingsChanged();
        }

        public void CycleTextureQuality(int direction)
        {
            int nextValue = TextureQuality + Math.Sign(direction);
            if (nextValue < 0)
            {
                nextValue = 2;
            }
            else if (nextValue > 2)
            {
                nextValue = 0;
            }

            SetTextureQuality(nextValue);
        }

        public void SetTextureQuality(int quality)
        {
            TextureQuality = Mathf.Clamp(quality, 0, 2);
            PlayerPrefs.SetInt(TextureQualityKey, TextureQuality);
            ApplyTextureQuality();
            NotifySettingsChanged();
        }

        public void ToggleHud()
        {
            SetShowHud(!ShowHud);
        }

        public void SetShowHud(bool showHud)
        {
            ShowHud = showHud;
            PlayerPrefs.SetInt(ShowHudKey, ShowHud ? 1 : 0);
            NotifySettingsChanged();
        }

        public void ToggleCameraEffects()
        {
            SetCameraEffectsEnabled(!CameraEffectsEnabled);
        }

        public void SetCameraEffectsEnabled(bool enabled)
        {
            CameraEffectsEnabled = enabled;
            PlayerPrefs.SetInt(CameraEffectsKey, CameraEffectsEnabled ? 1 : 0);
            NotifySettingsChanged();
        }

        public void ToggleInvertCameraY()
        {
            SetInvertCameraY(!InvertCameraY);
        }

        public void SetInvertCameraY(bool invert)
        {
            InvertCameraY = invert;
            PlayerPrefs.SetInt(InvertCameraYKey, InvertCameraY ? 1 : 0);
            NotifySettingsChanged();
        }

        public void SetCameraFieldOfView(float value)
        {
            CameraFieldOfView = Mathf.Clamp(value, 55f, 85f);
            PlayerPrefs.SetFloat(CameraFieldOfViewKey, CameraFieldOfView);
            NotifySettingsChanged();
        }

        public void SetSteeringSensitivity(float value)
        {
            SteeringSensitivity = Mathf.Clamp(value, 0.5f, 2f);
            PlayerPrefs.SetFloat(SteeringSensitivityKey, SteeringSensitivity);
            NotifySettingsChanged();
        }

        public void SetPedalSensitivity(float value)
        {
            PedalSensitivity = Mathf.Clamp(value, 0.5f, 2f);
            PlayerPrefs.SetFloat(PedalSensitivityKey, PedalSensitivity);
            NotifySettingsChanged();
        }

        public void SetReverseDoubleTapWindow(float value)
        {
            ReverseDoubleTapWindow = Mathf.Clamp(value, 0.1f, 0.6f);
            PlayerPrefs.SetFloat(ReverseTapWindowKey, ReverseDoubleTapWindow);
            NotifySettingsChanged();
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
            ApplyAudioSettings();
            NotifySettingsChanged();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            ApplyAudioSettings();
            NotifySettingsChanged();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            ApplyAudioSettings();
            NotifySettingsChanged();
        }

        public AudioMixerGroup GetMixerGroup(string name)
        {
            if (audioMixer == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            AudioMixerGroup[] groups = audioMixer.FindMatchingGroups(name);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }

        public void RouteAudioSource(AudioSource source, string preferredGroupName)
        {
            if (source == null)
            {
                return;
            }

            AudioMixerGroup group = GetMixerGroup(preferredGroupName);
            if (group != null)
            {
                source.outputAudioMixerGroup = group;
            }
        }

        private void LoadSettings()
        {
            MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.85f);
            SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 0.9f);
            Fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) != 0;
            VSyncEnabled = PlayerPrefs.GetInt(VSyncKey, 1) != 0;
            ShowHud = PlayerPrefs.GetInt(ShowHudKey, 1) != 0;
            CameraEffectsEnabled = PlayerPrefs.GetInt(CameraEffectsKey, 1) != 0;
            InvertCameraY = PlayerPrefs.GetInt(InvertCameraYKey, 0) != 0;
            CameraFieldOfView = PlayerPrefs.GetFloat(CameraFieldOfViewKey, 64f);
            SteeringSensitivity = PlayerPrefs.GetFloat(SteeringSensitivityKey, 1f);
            PedalSensitivity = PlayerPrefs.GetFloat(PedalSensitivityKey, 1f);
            ReverseDoubleTapWindow = PlayerPrefs.GetFloat(ReverseTapWindowKey, 0.3f);

            int qualityCount = Mathf.Max(1, QualitySettings.names.Length);
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(QualityLevelKey, Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, qualityCount - 1)), 0, qualityCount - 1);
            ShadowQuality = Mathf.Clamp(PlayerPrefs.GetInt(ShadowQualityKey, 2), 0, 2);
            TextureQuality = Mathf.Clamp(PlayerPrefs.GetInt(TextureQualityKey, 2), 0, 2);

            if (availableResolutions == null || availableResolutions.Length == 0)
            {
                ResolutionIndex = 0;
            }
            else
            {
                int defaultIndex = FindClosestResolutionIndex(Screen.currentResolution.width, Screen.currentResolution.height);
                ResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(ResolutionIndexKey, defaultIndex), 0, availableResolutions.Length - 1);
            }
        }

        private void ApplyAllSettings(bool notifyListeners)
        {
            ApplyDisplaySettings();
            QualitySettings.SetQualityLevel(QualityLevel, true);
            ApplyShadowQuality();
            ApplyTextureQuality();
            ApplyAudioSettings();

            if (notifyListeners)
            {
                NotifySettingsChanged();
            }
        }

        private void ApplyDisplaySettings()
        {
            if (availableResolutions == null || availableResolutions.Length == 0)
            {
                return;
            }

            Resolution resolution = availableResolutions[Mathf.Clamp(ResolutionIndex, 0, availableResolutions.Length - 1)];
            Screen.SetResolution(resolution.width, resolution.height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
        }

        private void ApplyShadowQuality()
        {
            switch (ShadowQuality)
            {
                case 0:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 0f;
                    QualitySettings.shadowCascades = 0;
                    break;
                case 1:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowDistance = 90f;
                    QualitySettings.shadowCascades = 2;
                    break;
                default:
                    QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                    QualitySettings.shadowDistance = 220f;
                    QualitySettings.shadowCascades = 4;
                    break;
            }
        }

        private void ApplyTextureQuality()
        {
            QualitySettings.globalTextureMipmapLimit = TextureQuality switch
            {
                0 => 2,
                1 => 1,
                _ => 0
            };
        }

        private void ApplyAudioSettings()
        {
            bool masterApplied = TrySetMixerVolume(masterVolumeParameter, MasterVolume);
            TrySetMixerVolume(musicVolumeParameter, MusicVolume);
            TrySetMixerVolume(sfxVolumeParameter, SfxVolume);

            if (!masterApplied)
            {
                AudioListener.volume = MasterVolume;
            }
        }

        private bool TrySetMixerVolume(string parameterName, float normalizedValue)
        {
            if (audioMixer == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            return audioMixer.SetFloat(parameterName, ToDecibels(normalizedValue));
        }

        private void NotifySettingsChanged()
        {
            PlayerPrefs.Save();
            SettingsChanged?.Invoke();
        }

        private void CacheAvailableResolutions()
        {
            Resolution[] source = Screen.resolutions;
            if (source == null || source.Length == 0)
            {
                availableResolutions = Array.Empty<Resolution>();
                return;
            }

            List<Resolution> uniqueResolutions = new List<Resolution>();
            for (int i = 0; i < source.Length; i++)
            {
                Resolution candidate = source[i];
                bool exists = false;
                for (int j = 0; j < uniqueResolutions.Count; j++)
                {
                    Resolution existing = uniqueResolutions[j];
                    if (existing.width == candidate.width && existing.height == candidate.height)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    uniqueResolutions.Add(candidate);
                }
            }

            uniqueResolutions.Sort((left, right) =>
            {
                int areaCompare = (left.width * left.height).CompareTo(right.width * right.height);
                return areaCompare != 0 ? areaCompare : left.width.CompareTo(right.width);
            });

            availableResolutions = uniqueResolutions.ToArray();
        }

        private int FindClosestResolutionIndex(int width, int height)
        {
            if (availableResolutions == null || availableResolutions.Length == 0)
            {
                return 0;
            }

            int bestIndex = 0;
            int bestScore = int.MaxValue;
            for (int i = 0; i < availableResolutions.Length; i++)
            {
                Resolution resolution = availableResolutions[i];
                int score = Mathf.Abs(resolution.width - width) + Mathf.Abs(resolution.height - height);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static float ToDecibels(float normalizedValue)
        {
            if (normalizedValue <= 0.0001f)
            {
                return -80f;
            }

            return Mathf.Clamp(20f * Mathf.Log10(normalizedValue), -80f, 0f);
        }
    }
}
