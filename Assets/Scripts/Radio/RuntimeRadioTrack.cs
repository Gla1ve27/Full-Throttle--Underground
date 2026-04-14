using UnityEngine;

namespace FullThrottle.Audio
{
    [System.Serializable]
    public class RuntimeRadioTrack
    {
        public string filePath;
        public string fileName;
        public string title;
        public string artist;
        public string album;
        public Sprite albumArt;
        public AudioClip clip;

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(title))
                    return title;

                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;

                return "Unknown Title";
            }
        }

        public string DisplayArtist
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(artist))
                    return artist;

                return "Unknown Artist";
            }
        }
    }
}
