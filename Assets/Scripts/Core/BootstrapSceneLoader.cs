using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underground.Core
{
    public class BootstrapSceneLoader : MonoBehaviour
    {
        [SerializeField] private string firstSceneName = "MainMenu";

        private void Start()
        {
            if (!string.IsNullOrEmpty(firstSceneName))
            {
                SceneManager.LoadScene(firstSceneName);
            }
        }
    }
}
