using Underground.Save;

namespace Underground.Core.Architecture
{
    public interface ISaveService
    {
        void Save(SaveGameData data);
        SaveGameData Load();
        void DeleteSave();
    }
}
