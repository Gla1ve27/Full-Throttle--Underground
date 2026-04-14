# 05 — Vehicle, Handling, and Audio Mega
This module defines the core feel of driving and hearing the car.

## 1. Goal
Create an arcade-real hybrid:
- responsive enough to be fun immediately
- physical enough to feel grounded
- expressive in drift and grip states
- stable enough for AI and police use

## 2. Vehicle system components
```text
Scripts/Vehicle/
  VehicleController.cs
  VehicleInput.cs
  Drivetrain.cs
  SuspensionController.cs
  WheelVisualSync.cs
  DriftAssist.cs
  VehicleAudioController.cs
```

## 3. Handling principles
- immediate throttle response
- controlled rear slip under power
- strong but not snappy steering
- speed-sensitive steering reduction
- visible weight transfer
- recoverable slides

## 4. Simplified controller baseline
```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    public float engineForce = 9000f;
    public float brakeForce = 4000f;
    public float steerTorque = 220f;
    public float maxSpeed = 85f;
    public float steeringAtLowSpeed = 1.0f;
    public float steeringAtHighSpeed = 0.35f;

    private Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        float throttle = Input.GetAxis("Vertical");
        float steer = Input.GetAxis("Horizontal");

        float speed = rb.velocity.magnitude;
        float steerScale = Mathf.Lerp(steeringAtLowSpeed, steeringAtHighSpeed, Mathf.InverseLerp(0f, maxSpeed, speed));

        if (throttle > 0f && speed < maxSpeed)
            rb.AddForce(transform.forward * throttle * engineForce * Time.fixedDeltaTime, ForceMode.Force);

        if (throttle < 0f)
            rb.AddForce(-transform.forward * Mathf.Abs(throttle) * brakeForce * Time.fixedDeltaTime, ForceMode.Force);

        rb.AddTorque(Vector3.up * steer * steerTorque * steerScale * Time.fixedDeltaTime, ForceMode.Force);
    }
}
```

## 5. Drift assist design
Drift assist can:
- add mild yaw support when rear slip is detected
- reduce violent snapback
- preserve control for keyboard/controller users

It should not:
- drift the car automatically
- remove all need for throttle modulation
- make every corner a drift corner

## 6. Audio system goals
Engine audio must communicate:
- idle
- low rpm pull
- midrange load
- high rpm strain
- gear changes
- off-throttle decel

Use layered loops if possible:
- idle
- low
- mid
- high
Crossfade by rpm and load.

## 7. VehicleAudioController example
```csharp
using UnityEngine;

public class VehicleAudioController : MonoBehaviour
{
    public AudioSource idle;
    public AudioSource low;
    public AudioSource mid;
    public AudioSource high;
    public Rigidbody rb;

    public float maxAudibleSpeed = 85f;

    void Update()
    {
        if (!rb) return;

        float speed01 = Mathf.InverseLerp(0f, maxAudibleSpeed, rb.velocity.magnitude);

        idle.volume = 1f - speed01;
        low.volume = Mathf.Clamp01(1.2f - Mathf.Abs(speed01 - 0.25f) * 4f);
        mid.volume = Mathf.Clamp01(1.2f - Mathf.Abs(speed01 - 0.55f) * 4f);
        high.volume = Mathf.Clamp01(1.2f - Mathf.Abs(speed01 - 0.85f) * 4f);

        low.pitch = 0.85f + speed01 * 0.4f;
        mid.pitch = 0.9f + speed01 * 0.45f;
        high.pitch = 0.95f + speed01 * 0.55f;
    }
}
```

## 8. Handling tuning targets
Early target feel:
- 0–100 km/h equivalent should feel punchy
- car should start rotating under input without long dead zone
- at high speed the car should still turn, but not twitch
- braking should visibly settle the car
- handbrake can be added later for more arcade drift behavior

## 9. Validation gates
- car is fun in free roam before race mode exists
- car can take city corners and highway sweepers without chaos
- vehicle audio changes clearly with speed/load
- no unnatural hopping or spinning at modest impacts
