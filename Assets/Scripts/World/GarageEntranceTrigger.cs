using UnityEngine;
using UnityEngine.SceneManagement;

namespace Underground.World
{
    public class GarageEntranceTrigger : MonoBehaviour
    {
        [SerializeField] private string garageSceneName = "Garage";

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            SceneManager.LoadScene(garageSceneName);
        }
    }
}
