# 04 — Traffic, Police, and Race AI Mega
This module defines believable world population and competitive pressure.

## 1. Goal
Create three separate but interoperable AI layers:
- ambient traffic
- law enforcement / heat escalation
- racing opponents

Each must obey the world, not fake perfect pathing.

## 2. Traffic AI design
Traffic should:
- follow lanes
- vary speed
- react to obstacles
- despawn safely outside perception zones
- never pop directly in front of player

Traffic should not:
- race like opponents
- brake randomly for no reason
- clip through each other
- make highways unusably clogged

## 3. Traffic system architecture
```text
Scripts/AI/
  TrafficManager.cs
  TrafficVehicleAI.cs
  LaneGraph.cs
  SpawnVolume.cs
  DespawnVolume.cs
```

Simple manager example:
```csharp
using UnityEngine;
using System.Collections.Generic;

public class TrafficManager : MonoBehaviour
{
    public GameObject[] trafficPrefabs;
    public int desiredCount = 24;
    public float spawnRadius = 220f;
    public Transform player;
    private readonly List<GameObject> active = new();

    void Update()
    {
        while (active.Count < desiredCount)
            SpawnOne();
    }

    void SpawnOne()
    {
        if (!player || trafficPrefabs.Length == 0) return;

        Vector3 offset = Random.onUnitSphere;
        offset.y = 0f;
        Vector3 pos = player.position + offset.normalized * spawnRadius;

        GameObject prefab = trafficPrefabs[Random.Range(0, trafficPrefabs.Length)];
        GameObject go = Instantiate(prefab, pos, Quaternion.identity);
        active.Add(go);
    }
}
```

## 4. Police / heat system design
Heat should be systemic:
- reckless driving, collisions, and police sighting can add heat
- more heat means stronger units and more aggressive tactics
- police should use road network logic, not magical omniscience

Suggested heat tiers:
1. patrol attention
2. active pursuit
3. multiple units
4. aggressive containment + roadblocks
5. maximum pursuit pressure + heavy reinforcements

Example:
```csharp
using UnityEngine;

public class PoliceSystem : MonoBehaviour
{
    [Range(0,5)] public int heatLevel;
    public float decayTimer = 0f;
    public float decayDelay = 30f;

    public void AddHeat(int amount)
    {
        heatLevel = Mathf.Clamp(heatLevel + amount, 0, 5);
        decayTimer = 0f;
    }

    void Update()
    {
        if (heatLevel <= 0) return;

        decayTimer += Time.deltaTime;
        if (decayTimer > decayDelay)
        {
            heatLevel = Mathf.Max(0, heatLevel - 1);
            decayTimer = 0f;
        }
    }
}
```

## 5. Police tactics to implement progressively
Phase 1:
- chase and follow
- call for backup
- simple roadblock candidate selection

Phase 2:
- intercept vectors
- spike strips in high heat
- district-aware behavior

Phase 3:
- helicopter/support presentation if desired
- night-only escalation tuning
- cooldown/safehouse escape logic

## 6. Race AI design philosophy
Race AI must feel:
- competent
- imperfect
- pressure-capable
- readable

It must not feel:
- glued to spline
- impossible to overtake
- fully random
- cheating with instant grip

## 7. Race AI behavior ingredients
- target point lookahead
- speed planning
- overtake intent
- mistake probability
- recovery behavior
- aggression level
- defensive line selection
- awareness of traffic and player

## 8. Race AI controller example
```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RaceAIController : MonoBehaviour
{
    public Transform[] waypoints;
    public int currentIndex;
    public float engineForce = 6500f;
    public float steerTorque = 180f;
    public float lookAheadDistance = 12f;
    public float mistakeChance = 0.005f;
    public float aggression = 0.5f;

    private Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentIndex];
        Vector3 toTarget = target.position - transform.position;
        float dist = toTarget.magnitude;
        Vector3 local = transform.InverseTransformPoint(target.position);

        float steerInput = Mathf.Clamp(local.x / Mathf.Max(1f, lookAheadDistance), -1f, 1f);
        rb.AddForce(transform.forward * engineForce * Time.fixedDeltaTime, ForceMode.Force);
        rb.AddTorque(Vector3.up * steerInput * steerTorque * Time.fixedDeltaTime, ForceMode.Force);

        if (Random.value < mistakeChance)
            rb.AddTorque(Vector3.up * Random.Range(-120f, 120f) * Time.fixedDeltaTime, ForceMode.Impulse);

        if (dist < 8f)
            currentIndex = (currentIndex + 1) % waypoints.Length;
    }
}
```

## 9. Making AI feel intelligent
Add later:
- corner speed prediction
- dynamic lookahead based on speed
- opponent avoidance arcs
- route confidence score
- aggression when behind
- caution when damaged or unstable
- traffic awareness in mixed races

## 10. AI mistake design
Mistakes should include:
- braking a bit too late
- clipping curb slightly
- taking suboptimal line
- lifting throttle early under pressure
- oversteering when forced wide

Do not make mistakes:
- every 3 seconds
- in exactly the same way
- so strong the AI constantly crashes

## 11. Validation gates
Traffic:
- active around player without constant pop-in
- can move through network
- does not block every event route

Police:
- heat increases and decays properly
- pursuit pressure changes by heat level
- roadblocks only on valid wide roads

Race AI:
- can complete event route
- can be overtaken and can overtake
- visible mistakes occur but do not ruin entire race
