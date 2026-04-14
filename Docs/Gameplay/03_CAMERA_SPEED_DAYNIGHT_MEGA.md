# 03 — Camera, Speed Illusion, and Day/Night Mega
This module exists to solve the “why does my game still feel slow?” problem.

## 1. Goal
Make speed feel intense before the speedometer says insane numbers.

## 2. Camera design rules
- low-speed camera should feel stable and readable
- high-speed camera should widen FOV, pull back slightly, and increase environmental motion sensation
- drift camera should bias toward slide direction enough to communicate rotation, not enough to disorient
- camera must never jitter from raw rigidbody noise

## 3. Chase camera requirements
- smoothing between target and camera rig
- speed-based FOV
- speed-based distance
- optional landing compression
- minimal shake at cruise, stronger shake at high speed
- separate tuning for grounded vs airborne

Example controller:
```csharp
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public Rigidbody targetRb;

    public float baseDistance = 6.0f;
    public float maxDistance = 8.25f;
    public float baseHeight = 2.0f;
    public float baseFov = 65f;
    public float maxFov = 86f;
    public float moveSmooth = 6f;
    public float rotSmooth = 8f;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!targetRb && target) targetRb = target.GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (!target || !targetRb) return;

        float speed = targetRb.velocity.magnitude;
        float speed01 = Mathf.InverseLerp(0f, 70f, speed);

        float dist = Mathf.Lerp(baseDistance, maxDistance, speed01);
        float fov = Mathf.Lerp(baseFov, maxFov, speed01);

        Vector3 desiredPos = target.position - target.forward * dist + Vector3.up * baseHeight;
        transform.position = Vector3.Lerp(transform.position, desiredPos, Time.deltaTime * moveSmooth);

        Quaternion desiredRot = Quaternion.LookRotation((target.position + target.forward * 3f) - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, Time.deltaTime * rotSmooth);

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, Time.deltaTime * moveSmooth);
    }
}
```

## 4. Optional camera extras
- speed shake from filtered noise, not raw per-frame random
- near-miss impulse
- drift yaw bias
- boost burst punch
- collision camera damp

## 5. Speed illusion principles
Speed fantasy comes from contrast:
- near objects moving fast across the screen
- readable far horizon
- lane markers and poles passing rapidly
- camera widen and slight pullback
- rhythmic lighting and prop cadence

Do:
- place trees, lights, barriers, and signs close to fast roads
- keep skyline readable
- reduce clutter density in the far field
- maintain strong lane lines and edge rhythm

Do not:
- place giant walls right against every fast road
- overload roads with clutter that breaks route readability
- shrink the world scale so much the player feels toy-like

## 6. SpeedIllusionController
```csharp
using UnityEngine;

public class SpeedIllusionController : MonoBehaviour
{
    public Transform roadsidePropsRoot;
    public float nearDensityBoost = 1.2f;
    public float farScale = 0.85f;

    public void ApplyPropScaling()
    {
        foreach (Transform t in roadsidePropsRoot)
        {
            float d = Vector3.Distance(Vector3.zero, t.localPosition);
            float scale = Mathf.Lerp(1f, farScale, Mathf.InverseLerp(0f, 250f, d));
            t.localScale = Vector3.one * scale;
        }
    }
}
```

## 7. Day/night system goals
- Earth-like revolution impression
- sun and moon progression
- sky and ambient intensity shifts
- emissive materials more dramatic at night
- traffic and police rules can change with time
- race availability can change by time window

## 8. DayNightSystem example
```csharp
using UnityEngine;

public class DayNightSystem : MonoBehaviour
{
    public Light sun;
    public Light moon;
    public float fullDayLengthSeconds = 1800f;
    [Range(0f, 24f)] public float startHour = 18f;

    public float CurrentHour
    {
        get
        {
            float normalized = (Time.time / fullDayLengthSeconds + startHour / 24f) % 1f;
            return normalized * 24f;
        }
    }

    void Update()
    {
        float day01 = (Time.time / fullDayLengthSeconds + startHour / 24f) % 1f;
        float angle = day01 * 360f;

        if (sun) sun.transform.rotation = Quaternion.Euler(angle - 90f, 170f, 0f);
        if (moon) moon.transform.rotation = Quaternion.Euler(angle + 90f, 170f, 0f);
    }
}
```

## 9. Time-of-day gameplay hooks
At night:
- police aggression can rise
- illegal events can unlock
- neon and emissive signage scale up
- traffic can thin slightly on highways, but remain active in nightlife pockets

At day:
- visibility is high
- traffic can be denser in commercial/urban zones
- sanctioned events and time trials can be favored

## 10. HDRP setup notes
- enable bloom for chevrons and neon
- tune exposure curve so night is dark enough for emissives to pop
- use volumetric fog carefully; too much destroys corner readability
- keep motion blur moderate; too much smears route info
- reflections should help atmosphere, not whitewash every surface

## 11. Validation gates
- speed increase clearly changes FOV and camera distance
- no camera jitter while cornering at speed
- day-to-night transition is visible and smooth
- night makes red chevrons and signage pop, not blow out
- game feels faster after this module, even before handling tweaks
