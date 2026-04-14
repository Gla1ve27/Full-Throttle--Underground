# 02 — Race Event Route System Mega
This document covers route extraction, event generation, checkpoints, chevrons, route boundaries, and event metadata.

## 1. Goal
Automatically generate fun event candidates from the world, then filter them so only raceable routes survive.

## 2. Event types for first production pass
- Sprint
- Circuit
- Drift
- Time Trial
- Speed Trap chain challenge
- Delivery / escape variant reuse of route graph

## 3. Route data model
```csharp
using System.Collections.Generic;
using UnityEngine;

public enum EventType
{
    Sprint,
    Circuit,
    Drift,
    TimeTrial,
    SpeedTrapChain
}

[System.Serializable]
public class RaceRouteData
{
    public string id;
    public EventType eventType;
    public List<Vector3> points = new();
    public float totalLength;
    public float avgCurvature;
    public float elevationVariance;
    public bool validForTraffic;
    public bool validForPolice;
}
```

## 4. Route extraction rules
- Sprint: long, readable, mostly forward-progress route
- Circuit: returns close to start, avoids repetitive overlap
- Drift: includes sweepers, transition corners, and recovery space
- Time Trial: route readability prioritized over combativeness

## 5. RaceEventGenerator design
Responsibilities:
- evaluate roads
- chain compatible road segments
- generate candidate routes
- score them
- discard weak routes
- emit event prefabs + metadata

Example:
```csharp
using System.Collections.Generic;
using UnityEngine;

public class RaceEventGenerator : MonoBehaviour
{
    public List<RoadData> roads = new();
    public List<RaceRouteData> generatedRoutes = new();

    public void GenerateAll()
    {
        foreach (var road in roads)
        {
            TryCreateSprint(road);
            TryCreateDrift(road);
        }

        TryCreateCircuits();
    }

    void TryCreateSprint(RoadData road)
    {
        if (road.length < 250f) return;
        if (road.role == RoadRole.Local) return;

        var route = new RaceRouteData
        {
            id = "SPR_" + road.id,
            eventType = EventType.Sprint,
            points = new List<Vector3>(road.points),
            totalLength = road.length,
            avgCurvature = road.averageCurvature,
            validForTraffic = true,
            validForPolice = true
        };
        generatedRoutes.Add(route);
    }

    void TryCreateDrift(RoadData road)
    {
        if (road.averageCurvature < 0.35f) return;
        var route = new RaceRouteData
        {
            id = "DRF_" + road.id,
            eventType = EventType.Drift,
            points = new List<Vector3>(road.points),
            totalLength = road.length,
            avgCurvature = road.averageCurvature,
            validForTraffic = false,
            validForPolice = false
        };
        generatedRoutes.Add(route);
    }

    void TryCreateCircuits()
    {
        // Build loops by connecting classified roads.
    }
}
```

## 6. Route scoring
Weighted score example:
- length: 25%
- readability: 20%
- corner quality: 20%
- district variety: 15%
- scenic value: 10%
- police suitability: 10%

Reject if:
- too many intersections
- dead-end risk
- route doubles back awkwardly
- route crosses too many clutter-heavy city knots

## 7. Checkpoint system
Use checkpoint prefabs with trigger colliders and route index.
```csharp
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public int routeIndex;
    public bool isFinish;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log($"Checkpoint {routeIndex} triggered");
    }
}
```

## 8. Chevron system
Goal:
- neon red outline
- dynamic pulse
- visible at night
- still readable in daytime
- supports left/right turn emphasis

Recommended visual setup:
- transparent inner area
- bright red emissive edge
- HDR emission active
- bloom enabled in HDRP
- optional world-space glow card behind arrow for distance readability

Controller example:
```csharp
using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ChevronController : MonoBehaviour
{
    public float pulseSpeed = 5f;
    public float minIntensity = 2f;
    public float maxIntensity = 8f;
    public Color emissiveColor = Color.red;
    private Renderer rend;
    private MaterialPropertyBlock mpb;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        float t = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_EmissiveColor", emissiveColor * intensity);
        mpb.SetColor("_EmissionColor", emissiveColor * intensity);
        rend.SetPropertyBlock(mpb);
    }
}
```

## 9. Chevron prefab recommendations
Prefab should contain:
- root transform
- visible mesh with emissive outline material
- optional decal/light card
- LOD group if many are spawned
- script that flips orientation based on turn direction

Suggested scriptable settings:
```csharp
using UnityEngine;

[CreateAssetMenu(menuName="FullThrottle/ChevronSettings")]
public class ChevronSettings : ScriptableObject
{
    public Color color = Color.red;
    public float minIntensity = 2f;
    public float maxIntensity = 8f;
    public float spacing = 8f;
}
```

## 10. Invisible boundary system
Goal:
- stop player from leaving play space
- avoid ugly hard stop feeling
- optionally warn player before full pushback

Layers:
1. soft boundary warning trigger
2. hard collision/pushback layer behind it

Example:
```csharp
using UnityEngine;

public class BoundaryVolume : MonoBehaviour
{
    public bool softWarning;
    public float pushForce = 30f;

    private void OnTriggerStay(Collider other)
    {
        if (softWarning && other.CompareTag("Player"))
            Debug.Log("Return to route");
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!softWarning && collision.rigidbody && collision.gameObject.CompareTag("Player"))
        {
            Vector3 dir = (collision.transform.position - transform.position).normalized;
            collision.rigidbody.AddForce(dir * pushForce, ForceMode.Acceleration);
        }
    }
}
```

## 11. Event prefab architecture
```text
Prefabs/Events/
  RaceEventRoot.prefab
  Checkpoint.prefab
  Chevron_Left.prefab
  Chevron_Right.prefab
  StartGate.prefab
  FinishGate.prefab
  BoundarySoft.prefab
  BoundaryHard.prefab
```

## 12. Event runtime flow
- player approaches event marker
- route preview generated
- event configuration loads
- checkpoint + chevron network spawns
- optional traffic/police rules adjust
- race starts
- cleanup happens at finish/cancel

## 13. Anti-failure rules
- never place chevrons on every tiny bend; only on meaningful direction changes
- never spawn event finish in cluttered intersection center
- never generate drift event in dense traffic district
- never place hard boundary where player hits it blind at full speed
- never generate sprint on roads with constant stop-start crossings

## 14. Validation gates
- at least 6 valid generated events across 3 district types
- chevrons readable in night and day
- event route traversal possible by both player and AI
- checkpoints correctly ordered
- no event route enters reserved/non-drivable space
