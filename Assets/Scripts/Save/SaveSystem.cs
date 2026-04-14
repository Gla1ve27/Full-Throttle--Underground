using System.IO;
using UnityEngine;
using Underground.Core.Architecture;

namespace Underground.Save
{
    public class SaveSystem : MonoBehaviour, ISaveService
    {
        private string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

        private void Awake()
        {
            ServiceLocator.Register<ISaveService>(this);
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ISaveService>(this);
        }

        public void Save(SaveGameData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }

        public SaveGameData Load()
        {
            Debug.Log($"[SaveSystem] Loading from: {SavePath}");
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[SaveSystem] Save file not found.");
                return null;
            }

            string json = File.ReadAllText(SavePath);
            Debug.Log($"[SaveSystem] JSON Content: {json}");
            return JsonUtility.FromJson<SaveGameData>(json);
        }

        public void DeleteSave()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
    }
}
