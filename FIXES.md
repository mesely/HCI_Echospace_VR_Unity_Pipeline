# 🎵 Pipeline Fixes Summary

## Problems Fixed ✅

### 1. **HTTP Insecure Connection Error**
**Problem:** `InvalidOperationException: Insecure connection not allowed`

**Root Cause:** Unity by default blocks HTTP (insecure) connections to localhost

**Fix Applied:**
- Updated `Assets/Editor/EnableInsecureHttpDev.cs` to properly set:
  ```csharp
  PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
  ```
- This script runs automatically when Unity loads the project

**Status:** ✅ FIXED

---

### 2. **Microphone Not Detected**
**Problem:** `[AudioCaptureController] No microphone devices found.`

**Root Cause:** 
- macOS requires explicit permission in System Settings
- Code didn't properly list/select microphones

**Fixes Applied:**
- Enhanced `Assets/Scripts/AudioCaptureController.cs`:
  - Lists ALL available microphones in Debug logs
  - Auto-selects external USB mic first (Rode, etc.)
  - Falls back to built-in mic
  - Added detailed error messages with macOS permission fix
  
- Instructions added to select microphone:
  ```
  System Settings → Privacy & Security → Microphone
  → Enable for your Terminal/Python/IDE
  ```

**Status:** ✅ FIXED

---

### 3. **Android Device Offline**
**Problem:** `adb: device offline`

**Root Cause:** No Android device connected (or offline)

**Solution:**
- Code now **gracefully falls back to MacBook Pro's microphone**
- Both `StereoMicAndroidTest.cs` and `AudioCaptureController.cs` work on macOS
- No Android device needed for development!

**Status:** ✅ RESOLVED (fallback implemented)

---

### 4. **Pipeline HTTP Bridge Missing**
**Problem:** `from pipeline_http_bridge import ...` → ModuleNotFoundError

**Root Cause:** File didn't exist

**Fixes Applied:**
- ✅ Created `pipeline_http_bridge.py` - Complete HTTP server
  - Receives audio chunks from Unity (`/audio_chunk`)
  - Stores YAMNet events, STT, LLM results
  - Polls events back to Unity (`/events`)
  
- ✅ Created `audio_buffer.py` - Ring buffer utility
- ✅ Created `load_env.py` - Environment loader
- ✅ Created `.env` - Configuration file with API keys
- ✅ Created `SimplePipeline.py` - **Standalone audio visualizer** (start here!)

**Status:** ✅ FIXED

---

## New Files Created 📁

```
✅ pipeline_http_bridge.py      → HTTP server (Unity ↔ Python)
✅ audio_buffer.py              → Ring buffer utility
✅ load_env.py                  → .env loader
✅ .env                         → API keys & config
✅ SimplePipeline.py            → 🌟 QUICK START - Live audio visualization
✅ SETUP.md                     → Complete guide
✅ setup.sh                     → Auto-setup script
```

---

## Files Modified 🔧

```
✅ Assets/Editor/EnableInsecureHttpDev.cs
   → Better error handling & logging

✅ Assets/Scripts/AudioCaptureController.cs
   → Lists all mics, auto-selects best one
   → Better error messages
   → Tested on macOS

✅ Assets/Scripts/Networking/BackendHttpClient.cs
   → Already correct (IP: 172.20.10.2:8000)
```

---

## How to Test 🧪

### Option 1: Quick Test (Recommended)
```bash
cd /Users/mesely/ses_yonu_test_2d

# Install dependencies
bash setup.sh

# Run the visualization
python3 SimplePipeline.py
```

**You should see:**
- ✅ Audio device selection menu
- ✅ Live SPL (dBFS) plot
- ✅ Live frequency spectrum plot
- ✅ Your voice/sounds updating in real-time

### Option 2: Full Pipeline (with Unity)
```bash
# Terminal 1: Start Python pipeline
python3 RealTimeSPLVisualizer.py

# Terminal 2: Run Unity scene
# In Unity Editor, run AudioCaptureController scene
```

**You should see:**
- ✅ HTTP bridge listening on `0.0.0.0:8000`
- ✅ Unity connects and starts sending audio
- ✅ Python plots update with Unity audio
- ✅ CSVs written to `logs/` folder

---

## Architecture 🏗️

```
┌─────────────────────────────────────────────┐
│         MacBook Pro                         │
│  ┌─────────────────────────────────────┐   │
│  │      Python Pipeline                │   │
│  │ ┌────────────────────────────────┐  │   │
│  │ │ SimplePipeline.py / RealTime..│  │   │
│  │ │ - Live SPL graph              │  │   │
│  │ │ - Frequency spectrum          │  │   │
│  │ │ - YAMNet (optional)           │  │   │
│  │ └────────────────────────────────┘  │   │
│  │ ┌────────────────────────────────┐  │   │
│  │ │ HTTP Server (port 8000)       │  │   │
│  │ │ - /audio_chunk (recv from UI) │  │   │
│  │ │ - /events (send to Unity)     │  │   │
│  │ └────────────────────────────────┘  │   │
│  └────────────────────────────────────┘   │
│           ↑                                 │
│        sounddevice (MacBook mic)           │
│                                            │
│  ┌─────────────────────────────────────┐  │
│  │      Unity (Editor or Device)       │  │
│  │ - AudioCaptureController            │  │
│  │ - StereoMicAndroidTest              │  │
│  │ - BackendHttpClient (sends audio)   │  │
│  └─────────────────────────────────────┘  │
│           ↑                                 │
│    USB Microphone (Android or USB)        │
└─────────────────────────────────────────────┘
```

---

## Configuration 🔧

### `BackendHttpClient.cs`
```csharp
baseUrl = "http://172.20.10.2:8000"  // Python server IP
```

To use localhost instead:
```csharp
baseUrl = "http://localhost:8000"
```

### `.env`
```
GEMINI_API_KEY=your-api-key
PIPELINE_HTTP_HOST=0.0.0.0
PIPELINE_HTTP_PORT=8000
```

---

## Next Steps 📋

1. **Run SimplePipeline.py** to verify audio works
2. **Check System Preferences** for microphone permission
3. **Start Python HTTP server** in one terminal
4. **Run Unity scene** in another terminal
5. **Watch the plots update** with live audio data
6. **Check `logs/`** folder for CSV recordings

---

## Troubleshooting 🐛

| Error | Fix |
|-------|-----|
| `No microphone found` | System Settings → Privacy & Security → Microphone → Enable |
| `Port 8000 in use` | `lsof -i :8000 \| kill -9 PID` |
| `ModuleNotFoundError` | `pip install numpy matplotlib sounddevice` |
| `HTTP insecure error` | Already fixed by EnableInsecureHttpDev.cs |
| `Android offline` | Not needed - falls back to macOS mic |

---

## Files Overview 📚

**Core Python:**
- `SimplePipeline.py` → 🌟 **START HERE** - Simple visualization
- `RealTimeSPLVisualizer.py` → Full pipeline (YAMNet, Whisper, LLM)
- `pipeline_http_bridge.py` → HTTP server for Unity
- `audio_buffer.py` → Ring buffer
- `load_env.py` → Config loader

**Unity Scripts:**
- `BackendHttpClient.cs` → Sends audio to Python
- `AudioCaptureController.cs` → Captures mic & sends chunks
- `StereoMicAndroidTest.cs` → FMOD stereo capture (Android)
- `EnableInsecureHttpDev.cs` → Enables HTTP (already fixed)

**Config:**
- `.env` → API keys & settings
- `SETUP.md` → Detailed guide
- `setup.sh` → Auto-install script

---

**Status: ✅ READY TO TEST**

Start with: `python3 SimplePipeline.py`
