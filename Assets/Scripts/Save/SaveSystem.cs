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
            if (!File.Exists(SavePath))
            {
                return null;
            }

            string json = File.ReadAllText(SavePath);
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
