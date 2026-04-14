using UnityEngine;
using UnityEditor;

// One-shot menu action — no window required.
public static class DayNightCycleSetup
{
    [MenuItem("Underground/Setup/Auto-Setup Day Night Cycle")]
    public static void AutoSetup()
    {
        cyclemanager manager = Object.FindObjectOfType<cyclemanager>();
        if (manager == null)
        {
            GameObject managerObj = new GameObject("DayNight_TimeManager");
            manager = managerObj.AddComponent<cyclemanager>();
            Undo.RegisterCreatedObjectUndo(managerObj, "Create DayNight_TimeManager");
        }

        Undo.RecordObject(manager, "Auto-Setup Day Night Cycle");

        manager.sunIntensityMultiplier = new AnimationCurve(
            new Keyframe(0f,    0f),
            new Keyframe(0.24f, 0f),
            new Keyframe(0.26f, 0.5f),
            new Keyframe(0.5f,  1f),
            new Keyframe(0.74f, 0.5f),
            new Keyframe(0.76f, 0f),
            new Keyframe(1f,    0f)
        );

        manager.sunTemperatureCurve = new AnimationCurve(
            new Keyframe(0f,    0.2f),
            new Keyframe(0.25f, 0.2f),
            new Keyframe(0.3f,  0.5f),
            new Keyframe(0.5f,  0.65f),
            new Keyframe(0.7f,  0.5f),
            new Keyframe(0.75f, 0.2f),
            new Keyframe(1f,    0.2f)
        );

        manager.timeSpeed    = 0.5f;
        manager.sunIntensity = 100000f;

        EditorUtility.SetDirty(manager);
        Debug.Log("[DayNightCycle] Setup completed on: " + manager.gameObject.name);
    }
}