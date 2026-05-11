# StepsRecorder

A portable Windows application (no installer, no admin rights) that combines:
- **Steps Recorder** — captures mouse clicks, key presses, screenshots, and window context into a structured session report
- **Screen Recorder** — records the screen to MP4 via hardware-accelerated H.264 (NVIDIA/AMD/Intel) with automatic ffmpeg fallback

Both streams are synchronized by a shared session timestamp, so every step in the HTML report links directly to the corresponding moment in the video.

---

## Requirements

| Item | Version |
|------|---------|
| Windows | 10 (1803+) or 11 |
| .NET Runtime | Bundled (self-contained build) |
| GPU | Any DirectX 11 GPU (WGC requires it) |
| Disk | ~200 MB for app + ~50-200 MB per session |

---

## Quick Start

1. Download or build the `dist/` folder (see [Building](#building))
2. Place `ffmpeg.exe` next to `StepsRecorder.exe` (see [FFmpeg](#ffmpeg))
3. Run `StepsRecorder.exe` — a red tray icon appears
4. Press **F9** to start recording
5. Work normally — every click and key event is captured
6. Press **F9** again to stop — the output folder opens automatically

---

## Hotkeys

| Key | Action |
|-----|--------|
| **F9** | Start / Stop session (steps + video) |
| **F10** | Pause / Resume step capture only (video keeps recording) |

Hotkeys can be changed in `settings.json`.

---

## Output

Each session creates a timestamped subfolder inside `sessions/` (next to the EXE):

```
sessions/
  2024-01-15_143022/
    session.json       ← full JSON event log
    report.html        ← human-readable report with screenshots and video link
    report.md          ← Markdown version
    recording.mp4      ← screen recording
    screenshots/
      step_0001_001123ms.png
      step_0002_003456ms.png
      ...
```

The HTML report is **self-contained** (screenshots embedded as base64) and includes a video player that jumps to the exact timestamp when you click a step.

---

## Settings (`settings.json`)

Located next to the EXE. Created from `settings.template.json` on first run.

```json
{
  "OutputPath": "sessions",
  "Hotkeys": {
    "StartStop": "F9",
    "PauseSteps": "F10"
  },
  "Video": {
    "Width": 1920,
    "Height": 1080,
    "Fps": 30
  },
  "Screenshots": {
    "HighlightClicks": true,
    "HighlightRadius": 20
  },
  "Encoder": {
    "PreferHardware": true,
    "FfmpegPath": "ffmpeg.exe"
  }
}
```

| Setting | Description |
|---------|-------------|
| `OutputPath` | Where sessions are saved. Relative = next to EXE. |
| `Hotkeys.StartStop` | Global hotkey to start/stop recording |
| `Hotkeys.PauseSteps` | Global hotkey to pause step capture |
| `Video.Width/Height` | Recording resolution |
| `Video.Fps` | Recording frame rate (15–60) |
| `Screenshots.HighlightClicks` | Draw red circle at click position |
| `Screenshots.HighlightRadius` | Circle radius in pixels |
| `Encoder.PreferHardware` | Try GPU encoder first (`h264_nvenc`, `h264_amf`, `h264_qsv`) |
| `Encoder.FfmpegPath` | Path to ffmpeg.exe (relative or absolute) |

---

## FFmpeg

The app uses `ffmpeg.exe` for video encoding. Download the latest Windows build from:

**https://ffmpeg.org/download.html** → Windows → [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) → `ffmpeg-release-essentials.zip`

Extract `ffmpeg.exe` and place it:
- **Next to `StepsRecorder.exe`** in the `dist/` folder, **or**
- In `tools/ffmpeg.exe` in the repository root (the build script copies it automatically)

### Hardware Encoding

At startup the app detects available hardware encoders by running `ffmpeg -encoders`. Priority order:
1. `h264_nvenc` — NVIDIA GPU (GeForce GTX 1000+)
2. `h264_amf` — AMD GPU (Radeon RX 400+)
3. `h264_qsv` — Intel Quick Sync (6th gen Core+)
4. `libx264` — software fallback (always available)

The encoder used is recorded in `session.json` as `encoderUsed`.

---

## Building

### Prerequisites
- [.NET 9 SDK](https://dot.net)
- PowerShell 5.1+ (built into Windows)
- `ffmpeg.exe` in `tools\ffmpeg.exe`

### Steps

```powershell
git clone <this-repo>
cd Steps-recorder-
.\build\publish.ps1
```

Output lands in `dist\`. Copy the entire folder anywhere — it's fully portable.

### Run Tests

```powershell
dotnet test src\Tests\Tests.csproj
```

The `VideoSanityTests` test requires `ffmpeg.exe` on PATH or in `dist/`; it skips gracefully if not found.

---

## Troubleshooting

### Tray icon doesn't appear
- Ensure the EXE is not blocked by Windows SmartScreen (right-click → Properties → Unblock)
- Run from a local drive (not a network share — some WPF resources fail on UNC paths)

### Hotkeys don't work (F9/F10)
- Another app may have registered F9 or F10 globally (e.g. some games, OBS)
- Change hotkeys in `settings.json` and restart the app
- A balloon notification appears at startup if hotkey registration fails

### Video recording fails / no MP4 produced
1. Check that `ffmpeg.exe` is next to the EXE
2. Open Task Manager → check CPU/GPU usage during recording
3. Review ffmpeg output in the Windows Event Log or attach a debugger to see stderr
4. Try setting `"PreferHardware": false` in `settings.json` to force software encoding
5. Ensure the output path has write permissions

### WGC yellow border appears around the screen
- Windows 10/11 shows a system capture indicator (yellow border) whenever any app uses Windows.Graphics.Capture. This is a security feature and **cannot be disabled** by third-party apps.

### Screenshots are blank or very slow
- Screenshots use GDI+ (`Graphics.CopyFromScreen`). On some systems with exclusive-fullscreen games or DRM-protected content, screen capture is blocked by design.

### "Access denied" when starting
- Low-level input hooks (`WH_MOUSE_LL`) do **not** require admin rights
- If a UAC dialog appears, it is from another component. The app manifest explicitly requests `asInvoker` (no elevation)

---

## Architecture

```
src/
  App/           WPF tray app, hotkeys, status bar
  Core/
    Settings/    AppSettings, SettingsManager
    Steps/       InputHook (WH_MOUSE_LL/LL), WindowContext, ScreenshotService, StepsService
    ScreenCapture/ WgcScreenCapture (Windows.Graphics.Capture + D3D11)
    Encoding/    FfmpegVideoEncoder, EncoderDetector
    Session/     SessionController (coordinates everything), PathHelper
    Exporters/   JsonExporter, HtmlExporter, MarkdownExporter
  Tests/         xUnit tests (formatter + video sanity)
```

---

## License

MIT
