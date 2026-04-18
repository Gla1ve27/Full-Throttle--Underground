# Full Throttle Minimap Starter Pack

This pack gives you a clean circular racing minimap for Unity 6.3 LTS + HDRP 17.

## Included scripts
- `MinimapCameraController.cs`
- `MinimapSystem.cs`
- `MinimapMarker.cs`
- `MinimapRouteOverlay.cs`

## What this setup does
- Orthographic top-down minimap camera
- Circular UI minimap using a RenderTexture
- Player arrow fixed in center
- Rotating map mode or north-up mode
- World markers for garage, home, race, gas station, etc.
- Optional route overlay using a LineRenderer

## 1. Create the RenderTexture
In the Project window:
- Right click -> Create -> Render Texture
- Name it `RT_Minimap`
- Start with `512 x 512`

## 2. Create the minimap camera
- Add a new Camera as a child of nothing or under a manager object
- Name it `MinimapCamera`
- Set Projection to `Orthographic`
- Set Target Texture to `RT_Minimap`
- Set Culling Mask to only minimap layers if possible
- Disable HDRP features you do not need on this camera
- Add `MinimapCameraController`
- Assign your player car transform as the target

Suggested values:
- Height = `80`
- Orthographic Size = `50`
- Rotate With Target = `true`

## 3. Build the UI hierarchy
Inside your main Canvas:

```text
MinimapRoot
 ├── CircleMask (Image + Mask)
 │    └── MapImage (RawImage using RT_Minimap)
 ├── IconContainer (RectTransform)
 │    ├── GarageIcon
 │    ├── HomeIcon
 │    └── RaceIcon
 ├── PlayerArrow (Image)
 └── FrameRing (Image)
```

### Important
- `CircleMask` should use a circular sprite and have the `Mask` component enabled
- `MapImage` is the RawImage showing `RT_Minimap`
- `IconContainer` should sit above `MapImage`
- `PlayerArrow` stays centered
- `FrameRing` is your decorative border UI

## 4. Add MinimapSystem
Put `MinimapSystem` on `MinimapRoot`.
Assign:
- `Map Viewport` = the RectTransform of `CircleMask`
- `Map Image` = the RawImage showing the render texture
- `Icon Container` = the icon parent RectTransform
- `Player Arrow` = the centered arrow RectTransform
- `Minimap Camera` = the camera with `MinimapCameraController`
- `Player Target` = your player car transform

Suggested values:
- Rotate Map With Player = `true`
- World Units Visible Radius = `70`
- Icon Edge Padding = `10`

## 5. Add markers
For each important world object:
- garage trigger
- house
- event start
- gas station

Do this:
1. Add `MinimapMarker` to that world object
2. Create a UI icon under `IconContainer`
3. Assign the icon's RectTransform and Image to the script
4. Assign the sprite and color you want

Recommended icon options:
- Clamp To Edge = true
- Rotate With World Object = false

## 6. Add the route overlay
- Create an empty object named `MinimapRoute`
- Add a `LineRenderer`
- Add `MinimapRouteOverlay`
- Put route waypoint transforms into the Route Points array
- Put the route object on a layer visible to the minimap camera

Suggested LineRenderer values:
- Alignment = View
- Width = `2` to `5` depending on scale
- Material = unlit transparent or a minimap-only material
- Use a bright route color like purple

## 7. Layers for a cleaner map
Best practice:
- `MinimapRoad`
- `MinimapPOI`
- `MinimapRoute`
- `MinimapPlayer`

If the full world looks messy in the minimap, create simplified minimap-only geometry later.

## 8. HDRP tips
For the minimap camera:
- turn off expensive effects if possible
- avoid volumetrics
- avoid extra shadows
- keep the culling mask small
- keep the render texture size reasonable

## 9. First target result
Your first good version should already have:
- circular minimap
- centered player arrow
- rotating map
- 3 to 5 icons
- one visible route line

After that you can polish the frame art and icon style.
