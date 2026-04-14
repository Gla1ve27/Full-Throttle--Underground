# Full Throttle Runtime Radio + Now Playing Popup System

This implementation gives Full Throttle a runtime MP3 radio system that reads real MP3 files instead of hard-coded song data.

It includes:

- runtime playlist scanning from a folder
- MP3 metadata reading
- optional album art extraction
- smooth slide-in popup
- fade animation
- auto-next track
- shuffle support
- optional radio station text
- no per-song hardcoding required

---

## 1. Why this system uses the raw MP3 files

Unity imported `AudioClip` assets do not give you a clean, reliable runtime metadata pipeline for title, artist, album, and cover art.

To read song metadata properly, use the original MP3 files from disk.

### Best setup
Put your songs in:

`Assets/StreamingAssets/Radio/MainStation/`

Example:

```text
Assets/
  StreamingAssets/
    Radio/
      MainStation/
        song01.mp3
        song02.mp3
        song03.mp3
```

The system scans that folder at runtime, reads the MP3 metadata, builds a playlist, then plays and shows the popup automatically.

---

## 2. Required dependencies

### Required
- Unity 6 or recent Unity version
- TextMeshPro
- Canvas UI
- AudioSource

### Strongly recommended for real metadata
- TagLib# / taglib-sharp DLL

This code is written so the project still compiles even if TagLib# is missing, but:

- with TagLib# present:
  - title, artist, album, and album art are read from the MP3
- without TagLib#:
  - the system falls back to filename as title
  - artist becomes `Unknown Artist`
  - no embedded album art is shown

---

## 3. Install TagLib#

Import a Unity-compatible `taglib-sharp` DLL into:

```text
Assets/Plugins/
```

You only need the DLL present in the project. The scripts use reflection, so they do not hard-reference the assembly at compile time.

---

## 4. Files included in this pack

### Scripts
- `RuntimeRadioTrack.cs`
- `Mp3MetadataUtility.cs`
- `RadioNowPlayingPopup.cs`
- `RuntimeRadioManager.cs`

I placed it inside Scripts/Radio/
---

## 5. Scene setup

### A. Create the audio object
Create an empty GameObject:

`RuntimeRadioManager`

Add:
- `AudioSource`
- `RuntimeRadioManager`

Recommended AudioSource settings:
- Play On Awake = Off
- Loop = Off
- Spatial Blend = 0
- Volume = 1

---

### B. Create the UI popup
Inside your Canvas create:

```text
Canvas
  NowPlayingPanel
    AlbumArt
    TitleText
    ArtistText
    StationText
```

### Recommended components

#### NowPlayingPanel
- `RectTransform`
- `Image`
- `CanvasGroup`
- `RadioNowPlayingPopup`

#### AlbumArt
- `Image`

#### TitleText
- `TextMeshProUGUI`

#### ArtistText
- `TextMeshProUGUI`

#### StationText
- `TextMeshProUGUI`

---

## 6. Recommended visual layout

### Panel
- anchor: top-left
- width: around 520 to 620
- height: around 110 to 140
- semi-transparent dark background
- slightly rounded sprite or styled panel
- subtle glow or shadow

### AlbumArt
- square image on the left
- around 72 to 96 px

### Text
- Title: bold, larger
- Artist: smaller, lighter
- StationText: optional, small uppercase label

### Suggested vibe
Use a sleek NFS/Forza-style look:
- dark translucent card
- bold title
- clean artist line
- station text like `FULL THROTTLE FM`

---

## 7. Hooking references in the inspector

### RuntimeRadioManager
Assign:
- `Music Source`
- `Now Playing Popup`
- `Streaming Assets Subfolder` = `Radio/MainStation`
- `Radio Station Text` = `FULL THROTTLE FM`

Optional:
- `Shuffle` = true
- `Auto Play On Start` = true
- `Auto Next Track` = true

### RadioNowPlayingPopup
Assign:
- `Panel`
- `CanvasGroup`
- `TitleText`
- `ArtistText`
- `StationText`
- `AlbumArtImage`
- optional fallback album art sprite

Recommended default animation values:
- Hidden Position = `(-700, -40)`
- Shown Position = `(24, -40)`
- Fade In Duration = `0.18`
- Slide In Duration = `0.32`
- Visible Duration = `4.25`
- Fade Out Duration = `0.20`
- Slide Out Duration = `0.28`

---

## 8. How it works

### RuntimeRadioManager
- scans a folder for MP3 files
- reads metadata from each MP3
- builds a runtime playlist
- loads and plays songs on demand
- auto-advances when a track ends
- supports shuffle
- tells the popup what to display

### Mp3MetadataUtility
- tries to locate TagLib# at runtime
- reads title, artist, album
- extracts embedded album art
- converts album art into a Unity Sprite

### RadioNowPlayingPopup
- updates text and art
- slides in
- fades in
- waits
- slides out
- fades out

---

## 9. Important workflow note

If your MP3 files are only imported into Unity as assets and not kept as raw files in `StreamingAssets`, you lose the clean metadata workflow.

For this system:
- keep the original MP3 files in `StreamingAssets`
- let the system load them from there

That is the correct approach for a runtime radio feature like this.

---

## 10. Optional folder alternatives

If you do not want to use `StreamingAssets`, the system supports a custom absolute folder path.

Enable:
- `Use Custom Absolute Folder`

Then set:
- `Custom Absolute Folder`

Example:
```text
D:/FullThrottle/Radio/MainStation
```

This is useful while developing or testing large music folders outside the Unity project.

---

## 11. Recommended test procedure

1. Add 3 to 5 MP3 files into `Assets/StreamingAssets/Radio/MainStation/`
2. Make sure the files have metadata in Windows properties or another tag editor
3. Import the scripts
4. Create the manager object
5. Create the popup UI
6. Assign references
7. Press Play
8. Confirm:
   - first track loads
   - popup appears
   - title and artist are correct
   - album art appears if embedded
   - next song auto-plays
   - shuffle works if enabled

---

## 12. Known limitations

### Desktop-focused by default
This version is ideal for Windows PC development, which matches Full Throttle well.

### TagLib# is needed for real metadata extraction
Without TagLib#:
- you still get playback
- popup still works
- metadata falls back to filename

### Popup styling is yours to skin
The provided code handles behavior and animation. You should style the UI to match Full Throttle’s visual identity.

---

## 13. Suggested Full Throttle polish after this

After the base system works, next upgrades can be:
- station logo icon
- crossfade between tracks
- separate stations with genre folders
- DJ/radio stinger intro
- pause popup when game is paused
- animated equalizer bars
- button prompt for next/previous station
- save last played station and track

---

## 14. Script summary

### `RuntimeRadioTrack.cs`
Stores per-track runtime data.

### `Mp3MetadataUtility.cs`
Reads metadata and album art from MP3 files.

### `RadioNowPlayingPopup.cs`
Handles slide/fade animation and text updates.

### `RuntimeRadioManager.cs`
Scans songs, loads clips, plays tracks, auto-advances, and supports shuffle.

---

## 15. Fastest working setup checklist

- Put MP3 files in `Assets/StreamingAssets/Radio/MainStation/`
- Import `taglib-sharp` DLL into `Assets/Plugins/`
- Add the 4 scripts
- Create `RuntimeRadioManager` object with `AudioSource`
- Create the popup UI
- Assign references
- Press Play

That is enough to get a fully runtime-driven radio popup working without hardcoding song names.

---

## 16. Recommended next step in your project

Once this works, wire it into your:
- garage scene
- free-roam world
- pause menu audio settings
- radio station switching input
- car HUD

That will make the soundtrack presentation feel much closer to the racing-game identity you want.
