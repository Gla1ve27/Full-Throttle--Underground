using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace FullThrottle.Audio
{
    public class RuntimeRadioManager : MonoBehaviour
    {
        // Singleton so the radio persists across all scene loads.
        public static RuntimeRadioManager Instance { get; private set; }
        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;

        [Header("Radio Source")]
        [SerializeField] private bool useCustomAbsoluteFolder = false;
        [SerializeField] private string customAbsoluteFolder = "";
        [SerializeField] private string streamingAssetsSubfolder = "Radio/MainStation";
        [SerializeField] private bool includeSubfolders = false;
        [SerializeField] private bool autoPlayOnStart = true;
        [SerializeField] private bool autoNextTrack = true;
        [SerializeField] private bool shuffle = true;
        [SerializeField] private bool avoidImmediateShuffleRepeats = true;

        [Header("Popup")]
        [SerializeField] private RadioNowPlayingPopup nowPlayingPopup;
        [SerializeField] private bool showPopupOnFirstTrack = true;
        [SerializeField] private string radioStationText = "FULL THROTTLE FM";

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging = false;

        private readonly List<RuntimeRadioTrack> tracks = new List<RuntimeRadioTrack>();
        private System.Random rng;
        private int currentIndex = -1;
        private bool isLoadingTrack;
        private bool hasStartedPlayback;
        private bool suppressAutoAdvance;

        public IReadOnlyList<RuntimeRadioTrack> Tracks => tracks;
        public RuntimeRadioTrack CurrentTrack => currentIndex >= 0 && currentIndex < tracks.Count ? tracks[currentIndex] : null;
        public bool ShuffleEnabled => shuffle;

        private void Reset()
        {
            musicSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            // Enforce singleton: if another instance already exists, destroy this duplicate.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource == null)
                musicSource = GetComponent<AudioSource>();

            rng = new System.Random(Environment.TickCount);

            if (musicSource != null)
            {
                musicSource.playOnAwake = false;
                musicSource.loop = false;
                musicSource.volume = volume;
            }
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Re-discover the popup in the new scene if our reference was lost.
            if (nowPlayingPopup == null)
            {
                nowPlayingPopup = FindFirstObjectByType<RadioNowPlayingPopup>();
            }
        }

        private IEnumerator Start()
        {
            yield return ScanFolderAndBuildPlaylist();

            if (autoPlayOnStart && tracks.Count > 0)
                PlayNext(forceShowPopup: showPopupOnFirstTrack);
        }

        private void Update()
        {
            if (musicSource == null || isLoadingTrack)
                return;

            musicSource.volume = volume;

            if (musicSource.isPlaying)
            {
                hasStartedPlayback = true;
                return;
            }

            if (!hasStartedPlayback)
                return;

            if (suppressAutoAdvance)
                return;

            hasStartedPlayback = false;

            if (autoNextTrack && tracks.Count > 0)
                PlayNext(forceShowPopup: true);
        }

        [ContextMenu("Rebuild Playlist")]
        public void RebuildPlaylist()
        {
            StopAllCoroutines();
            StartCoroutine(RebuildRoutine());
        }

        public void PlayNext(bool forceShowPopup = true)
        {
            if (tracks.Count == 0 || musicSource == null || isLoadingTrack)
                return;

            suppressAutoAdvance = false;
            int nextIndex = GetNextTrackIndex();
            StartCoroutine(LoadAndPlayTrack(nextIndex, forceShowPopup));
        }

        public void PlayPrevious(bool forceShowPopup = true)
        {
            if (tracks.Count == 0 || musicSource == null || isLoadingTrack)
                return;

            suppressAutoAdvance = false;
            int previousIndex = currentIndex - 1;
            if (previousIndex < 0)
                previousIndex = tracks.Count - 1;

            StartCoroutine(LoadAndPlayTrack(previousIndex, forceShowPopup));
        }

        public void ToggleShuffle(bool enabled)
        {
            shuffle = enabled;
        }

        public void SetStationText(string newStationText)
        {
            radioStationText = newStationText;
        }

        public void StopRadio()
        {
            suppressAutoAdvance = true;
            hasStartedPlayback = false;

            if (musicSource != null)
                musicSource.Stop();
        }

        public void ResumeRadio()
        {
            suppressAutoAdvance = false;

            if (musicSource != null && musicSource.clip != null)
            {
                musicSource.Play();
                hasStartedPlayback = true;
            }
            else if (tracks.Count > 0)
            {
                PlayNext(forceShowPopup: true);
            }
        }

        private IEnumerator RebuildRoutine()
        {
            StopRadio();
            tracks.Clear();
            currentIndex = -1;
            yield return ScanFolderAndBuildPlaylist();

            if (autoPlayOnStart && tracks.Count > 0)
                PlayNext(forceShowPopup: showPopupOnFirstTrack);
        }

        private IEnumerator ScanFolderAndBuildPlaylist()
        {
            tracks.Clear();

            string rootFolder = GetMusicFolder();
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                Debug.LogWarning("RuntimeRadioManager: Music folder path is empty.");
                yield break;
            }

            if (!Directory.Exists(rootFolder))
            {
                Debug.LogWarning($"RuntimeRadioManager: Folder not found -> {rootFolder}");
                yield break;
            }

            SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files = Directory.GetFiles(rootFolder, "*.mp3", searchOption);

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                Mp3MetadataUtility.MetadataResult meta = Mp3MetadataUtility.ReadMetadata(file, fileName);

                RuntimeRadioTrack track = new RuntimeRadioTrack
                {
                    filePath = file,
                    fileName = fileName,
                    title = string.IsNullOrWhiteSpace(meta.title) ? fileName : meta.title,
                    artist = string.IsNullOrWhiteSpace(meta.artist) ? "Unknown Artist" : meta.artist,
                    album = meta.album ?? string.Empty,
                    albumArt = meta.albumArt,
                    clip = null
                };

                tracks.Add(track);

                if (verboseLogging)
                    Debug.Log($"RuntimeRadioManager: Added track '{track.DisplayTitle}' by '{track.DisplayArtist}'.");
            }

            if (tracks.Count == 0)
                Debug.LogWarning($"RuntimeRadioManager: No MP3 files found in {rootFolder}");
            else
                Debug.Log($"RuntimeRadioManager: Playlist ready. Found {tracks.Count} MP3 track(s).");

            yield return null;
        }

        private IEnumerator LoadAndPlayTrack(int index, bool showPopup)
        {
            if (index < 0 || index >= tracks.Count)
                yield break;

            isLoadingTrack = true;
            RuntimeRadioTrack track = tracks[index];

            if (track.clip == null)
                yield return LoadClip(track);

            if (track.clip == null)
            {
                Debug.LogWarning($"RuntimeRadioManager: Could not load clip for '{track.filePath}'. Skipping.");
                isLoadingTrack = false;
                PlayNext(forceShowPopup: false);
                yield break;
            }

            currentIndex = index;
            suppressAutoAdvance = false;

            musicSource.Stop();
            musicSource.clip = track.clip;
            musicSource.volume = volume;
            musicSource.Play();

            hasStartedPlayback = true;
            isLoadingTrack = false;

            if (showPopup && nowPlayingPopup != null)
                nowPlayingPopup.Show(track, radioStationText);

            if (verboseLogging)
                Debug.Log($"RuntimeRadioManager: Playing '{track.DisplayTitle}' by '{track.DisplayArtist}'.");
        }

        private IEnumerator LoadClip(RuntimeRadioTrack track)
        {
            string uri = new Uri(track.filePath).AbsoluteUri;

            // Use AudioType.UNKNOWN so FMOD auto-detects the codec.
            // AudioType.MPEG fails on many Unity/FMOD configurations.
            using (UnityWebRequest request = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
            {
                // Enable streaming so large files don't block memory.
                ((UnityEngine.Networking.DownloadHandlerAudioClip)request.downloadHandler).streamAudio = true;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"RuntimeRadioManager: Audio load failed for '{track.filePath}'. {request.error}");
                    yield break;
                }

                AudioClip loadedClip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
                if (loadedClip != null)
                {
                    loadedClip.name = track.DisplayTitle;
                    track.clip = loadedClip;
                }
                else
                {
                    Debug.LogWarning($"RuntimeRadioManager: Clip was null after download for '{track.filePath}'. File may be corrupted or unsupported.");
                }
            }
        }

        private int GetNextTrackIndex()
        {
            if (tracks.Count == 0)
                return -1;

            if (tracks.Count == 1)
                return 0;

            if (!shuffle)
            {
                int next = currentIndex + 1;
                if (next >= tracks.Count)
                    next = 0;

                return next;
            }

            int randomIndex = rng.Next(0, tracks.Count);

            if (avoidImmediateShuffleRepeats && tracks.Count > 1)
            {
                while (randomIndex == currentIndex)
                    randomIndex = rng.Next(0, tracks.Count);
            }

            return randomIndex;
        }

        private string GetMusicFolder()
        {
            if (useCustomAbsoluteFolder && !string.IsNullOrWhiteSpace(customAbsoluteFolder))
                return customAbsoluteFolder;

            return Path.Combine(Application.streamingAssetsPath, streamingAssetsSubfolder);
        }
    }
}
