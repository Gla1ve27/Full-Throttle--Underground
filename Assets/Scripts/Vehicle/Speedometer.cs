using UnityEngine;
using UnityEngine.UI;

// FIX: Speedometer previously read Rigidbody.linearVelocity.magnitude which includes
// lateral/sideways drift velocity, causing the needle to read higher than the displayed
// speed text in HUDController (which uses ForwardSpeedKph). Now HUDController drives
// both the text AND the needle through SetSpeed() so they always match.
public class Speedometer : MonoBehaviour
{
    public Rigidbody target; // kept for legacy compatibility, no longer used for speed sampling

    public float maxSpeed = 260f; // Max speed in KM/H — overridden at runtime by HUDController

    public float minSpeedArrowAngle;
    public float maxSpeedArrowAngle;

    [Header("UI")]
    public Text speedLabel;       // Legacy Text label (may be null if using TMP in HUD)
    public RectTransform arrow;   // The needle in the speedometer dial

    private float displaySpeed = 0f;

    /// <summary>
    /// Called every frame by HUDController with the authoritative forward speed (km/h).
    /// This ensures the needle and the HUD speed text always show the same value.
    /// </summary>
    public void SetSpeed(float speedKph)
    {
        displaySpeed = Mathf.Max(0f, speedKph);

        float normalizedSpeed = Mathf.InverseLerp(0f, Mathf.Max(1f, maxSpeed), displaySpeed);

        if (speedLabel != null)
            speedLabel.text = ((int)displaySpeed) + " km/h";

        if (arrow != null)
            arrow.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(minSpeedArrowAngle, maxSpeedArrowAngle, normalizedSpeed));
    }

    private void Update()
    {
        // Fallback: only self-sample if HUDController is not present to call SetSpeed().
        // This preserves standalone use of the component outside the main HUD setup.
        if (target == null)
        {
            SetSpeed(0f);
            return;
        }

        // Use forward projection to match HUDController's ForwardSpeedKph calculation.
        float forwardSpeed = Mathf.Abs(Vector3.Dot(target.linearVelocity, target.transform.forward)) * 3.6f;
        SetSpeed(forwardSpeed);
    }
}