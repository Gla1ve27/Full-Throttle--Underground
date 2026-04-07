using UnityEngine;

namespace Underground.Vehicle
{
    public class EngineModel : MonoBehaviour
    {
        [SerializeField] private AnimationCurve normalizedTorqueCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.25f, 0.65f),
            new Keyframe(0.5f, 1f),
            new Keyframe(0.8f, 0.85f),
            new Keyframe(1f, 0.6f));

        public float EvaluateNormalizedTorque(float normalizedRpm)
        {
            return normalizedTorqueCurve.Evaluate(Mathf.Clamp01(normalizedRpm));
        }
    }
}
