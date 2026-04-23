using System.Collections.Generic;
using FullThrottle.SacredCore.Runtime;
using FullThrottle.SacredCore.Save;
using FullThrottle.SacredCore.Story;
using FullThrottle.SacredCore.Vehicle;
using UnityEngine;

namespace FullThrottle.SacredCore.Race
{
    [DefaultExecutionOrder(-9560)]
    public sealed class FTRivalDirector : MonoBehaviour
    {
        [SerializeField] private List<FTRivalDefinition> rivals = new();

        private readonly Dictionary<string, FTRivalDefinition> map = new();
        private FTSaveGateway saveGateway;
        private FTCarRegistry carRegistry;

        public FTRivalDefinition ActiveRival { get; private set; }

        private void Awake()
        {
            FTServices.Register(this);
            saveGateway = FTServices.Get<FTSaveGateway>();
            carRegistry = FTServices.Get<FTCarRegistry>();
            Rebuild();
        }

        public void Rebuild()
        {
            map.Clear();
            for (int i = 0; i < rivals.Count; i++)
            {
                FTRivalDefinition rival = rivals[i];
                if (rival != null && !string.IsNullOrWhiteSpace(rival.rivalId))
                {
                    map[rival.rivalId] = rival;
                }
            }

            Debug.Log($"[SacredCore] Rival director indexed {map.Count} rivals.");
        }

        public bool TrySetActiveRival(string rivalId)
        {
            if (!map.TryGetValue(rivalId, out FTRivalDefinition rival))
            {
                Debug.LogWarning($"[SacredCore] Missing rival '{rivalId}'.");
                return false;
            }

            ActiveRival = rival;
            bool carValid = string.IsNullOrWhiteSpace(rival.signatureCarId) || carRegistry.TryGet(rival.signatureCarId, out _);
            Debug.Log($"[SacredCore] Active rival={rival.rivalId}, signatureCar={rival.signatureCarId}, carValid={carValid}.");
            return true;
        }

        public bool IsBeaten(string rivalId)
        {
            return saveGateway.Profile.beatenRivalIds.Contains(rivalId);
        }
    }
}
