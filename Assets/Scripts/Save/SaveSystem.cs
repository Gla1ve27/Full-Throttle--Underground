using System.IO;
using UnityEngine;

namespace Underground.Save
{
    public class SaveSystem : MonoBehaviour
    {
        private string SavePath => Path.Combine(Application.persistentDataPath, "savegame.json");

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
