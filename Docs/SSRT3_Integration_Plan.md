# SSRT3 Integration Plan: Screen Space Ray Tracing for Full-Throttle

This document outlines how to use **SSRT3** (Screen Space Ray Tracing) in **Full-Throttle: Underground** to achieve premium, state-of-the-art visuals for a high-end street racing experience.

## 🏁 Overview
SSRT3 provides **Indirect Diffuse Illumination** (Global Illumination) and **Ground Truth Ambient Occlusion (GTAO)**. This asset is specifically tailored for HDRP and is much faster and cleaner than Unity's default SSGI.

---

## 💎 Visual Benefits for "Full-Throttle"

### 1. Grounded Vehicles (GTAO)
*   **Problem:** Standard AO often makes cars look like they are floating.
*   **SSRT Solution:** GTAO provides physically accurate contact shadows under the tires and chassis, making the cars feel weighted and integrated into the asphalt.

### 2. Dynamic Neon & Underglow (Emissive GI)
*   **Problem:** Neon underglow usually requires expensive area lights to illuminate the road.
*   **SSRT Solution:** Any emissive pixel (neon strips, brake lights) becomes a light source. The road will "catch" the glow from the car's underside in real-time.

### 3. Indirect Color Bleeding
*   **Effect:** A bright red car will cast a subtle red tint onto the pavement, enhancing the high-fidelity look.

---

## 🛠️ Implementation Steps

### Step 1: HDRP Global Settings
SSRT3 must be injected into the HDRP frame:
1.  Navigate to **Edit > Project Settings > HDRP Global Settings**.
2.  In **Custom Post Process Orders**, add `SSRT_HDRP` to the **After Opaque And Sky** injection point.

### Step 2: Volume Configuration
1.  Open **DefaultVolumeProfile.asset** (found in your root Assets).
2.  Add the **SSRT** override.
3.  **Recommended Settings:**
    *   **Intensity:** 1.0 (Adjust based on time of day).
    *   **Radius:** 3.0 - 5.0 (Covers the width of the car/road).
    *   **Rotation Count:** 1 (Gameplay) / 4 (Photo Mode/Garage).
    *   **Step Count:** 10 - 16.
    *   **Thickness:** 0.2 - 0.5 (Prevents light leaking behind the car).

### Step 3: Material Calibration
*   **Roads:** Ensure road materials have proper Normal maps and Smoothness to receive occlusion and light bounces.
*   **Neon:** Set your neon strips to **Emissive** with high intensity. SSRT3 will handle the rest.

---

## 🚀 Pro Tip: The Garage "Wow" Factor
In the Garage scene, you can crank the **SSRT** settings much higher (increased Rotation Count). Since the scene is less dynamic, the performance hit is negligible, but the increase in visual depth is massive.

## 📦 Architecture Guidelines
*   **Modular:** Keep SSRT configuration within Scene Volumes. Do not hardcode lighting dependencies into car prefabs.
*   **Scalable:** Use the **Global Settings** to enable/disable SSRT globally based on quality tiers (Low/Med/High).
