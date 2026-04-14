# 06 — Meta, Progression, UI, and Save Mega
This module covers everything that gives the driving loop long-term structure.

## 1. Goal
Create progression that rewards:
- skill
- style
- risk
- exploration

## 2. Meta systems
- garage
- vehicle selection
- event unlocks
- heat/risk/reward balancing
- player profile save
- HUD and menus

## 3. Progression model
Core currencies:
- cash/credits
- reputation/rep
- heat pressure as temporary risk variable

Suggested loop:
- enter free roam
- discover or generate events
- win races for cash/rep
- gain heat for illegal activity
- bank winnings by escaping and returning to safe location
- spend on tuning/cars/cosmetics

## 4. Save data structure
```csharp
using System.Collections.Generic;

[System.Serializable]
public class PlayerSaveData
{
    public string currentCarId;
    public int cash;
    public int rep;
    public int heat;
    public List<string> ownedCars = new();
    public List<string> unlockedEvents = new();
}
```

## 5. Save system example
```csharp
using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    public void SaveProfile(PlayerSaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.SetString("FT_Save", json);
        PlayerPrefs.Save();
    }

    public PlayerSaveData LoadProfile()
    {
        string json = PlayerPrefs.GetString("FT_Save", "");
        if (string.IsNullOrEmpty(json))
            return new PlayerSaveData();
        return JsonUtility.FromJson<PlayerSaveData>(json);
    }
}
```

## 6. Garage requirements
Garage should support:
- car browse/select
- basic stats view
- equipped car state
- upgrade hooks
- paint/cosmetic hooks later

## 7. HUD requirements
At minimum:
- speed
- mini objective/event prompt
- heat level
- event start/finish feedback
- checkpoint state
- police alert state

## 8. UI production rules
- readable at speed
- no oversized clutter
- critical warnings near eye-line
- style should support night racing identity

## 9. Validation gates
- save/load survives restart
- owned car and current car persist
- completed events can remain completed/unlocked
- HUD readable at 200 km/h equivalent gameplay
