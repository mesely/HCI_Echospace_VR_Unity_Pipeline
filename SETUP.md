# Pipeline Setup & Troubleshooting Guide

## Quick Start

### 1️⃣ Python Environment Setup

```bash
# Navigate to project
cd /Users/mesely/ses_yonu_test_2d

# Create venv (optional but recommended)
python3 -m venv venv
source venv/bin/activate

# Install dependencies
pip install numpy matplotlib sounddevice
pip install faster-whisper tensorflow tensorflow-hub  # Optional, for STT/YAMNet
```

### 2️⃣ Run the Simple Pipeline

This shows live audio SPL (dBFS) + frequency spectrum:

```bash
python3 SimplePipeline.py
```

✅ You should see:
- Real-time SPL plot (top) - shows sound intensity
- Frequency spectrum (bottom) - shows which frequencies are loud
- Microphone selection menu
- "🎤 Microphone active" message

### 3️⃣ Run Full Pipeline (with HTTP bridge for Unity)

```bash
python3 RealTimeSPLVisualizer.py
```

This starts an HTTP server on `0.0.0.0:8000` waiting for Unity audio.

---

## Unity Configuration

### Enable Insecure HTTP (needed for localhost development)

✅ Already done via `Assets/Editor/EnableInsecureHttpDev.cs`

This sets:
```csharp
PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
```

If you still see "Non-secure network connections disabled" error:
- Go to: Edit → Project Settings → Player → Other Settings
- Find: "Allow downloads over HTTP" 
- Set to: `Allow in Development Builds`

### Backend URL

In `BackendHttpClient.cs`, the URL is set to:
```csharp
public string baseUrl = "http://172.20.10.2:8000";
```

To test locally on your Mac, change it to:
```csharp
public string baseUrl = "http://localhost:8000";
```

Or use your Mac's IP:
```bash
# Find your Mac's IP
ifconfig | grep "inet " | grep -v 127.0.0.1
```

---

## Microphone Issues

### Issue: "No microphone devices found"

**macOS fix:**
```
System Settings → Privacy & Security → Microphone
  → Enable for: Terminal, Python, or your IDE
```

**Android fix:**
Add to `AndroidManifest.xml`:
```xml
<uses-permission android:name="android.permission.RECORD_AUDIO" />
```

In Unity's `StereoMicAndroidTest.cs`, code already handles this.

### Issue: Android device offline

If no Android device is connected:
1. Connect via USB
2. Run: `adb devices`
3. Authorize the connection on device

For **macOS-only development** (no Android):
- The code falls back to MacBook Pro's microphone ✅
- `AudioCaptureController.cs` lists all available mics
- Picks external USB mic first, then built-in mic

---

## File Structure

```
/Users/mesely/ses_yonu_test_2d/
├── .env                            # Environment config (API keys, hosts)
├── SimplePipeline.py               # ✅ Start here (live SPL + spectrum)
├── RealTimeSPLVisualizer.py        # Full pipeline with YAMNet/Whisper/LLM
├── pipeline_http_bridge.py         # HTTP server for Unity ↔ Python
├── audio_buffer.py                 # Ring buffer utility
├── load_env.py                     # .env file loader
│
├── Assets/
│   ├── Scripts/
│   │   ├── Networking/BackendHttpClient.cs       # Unity HTTP client
│   │   ├── AudioCaptureController.cs              # Mic capture (fixed ✅)
│   │   └── ...
│   ├── Editor/
│   │   └── EnableInsecureHttpDev.cs               # HTTP enable (fixed ✅)
│   └── ...
│
├── logs/                           # Output CSVs
│   ├── classification_log.csv
│   ├── transcription_log.csv
│   └── llm_events.csv
│
└── models/                         # YAMNet models
    ├── reduced_yamnet_savedmodel/
    └── reduced_labels.json
```

---

## Testing the Pipeline

### Test 1: Run SimplePipeline.py

Shows a live plot with your voice/sounds:

```bash
python3 SimplePipeline.py
# Speak or play music → see SPL spike + frequency peaks
```

### Test 2: Connect Unity

1. Run Python pipeline:
   ```bash
   python3 RealTimeSPLVisualizer.py
   ```

2. Run Unity scene with `StereoMicAndroidTest.cs` or `AudioCaptureController.cs`

3. Watch Unity send audio chunks to Python via HTTP

4. Python shows live plots updating with Unity audio

### Test 3: Check HTTP Bridge

```bash
# Send a test audio chunk from terminal
curl -X POST http://localhost:8000/audio_chunk \
  -H "Content-Type: application/json" \
  -d '{"session_id": "test", "seq": 1, "samplerate_hz": 48000, ...}'
```

---

## Logs & Output

**Python logs** (printed to console):
```
[HTTPBridge] Server started on 0.0.0.0:8000
[HTTPBridge] client_hello: session=default, device=MacBook-Pro
[AudioCaptureController] Microphone started successfully.
```

**Output CSV files** (in `logs/` folder):
- `classification_log.csv` → YAMNet predictions
- `classification_probs.csv` → Full label probabilities  
- `transcription_log.csv` → Speech-to-text
- `llm_events.csv` → LLM analysis

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "No microphone found" | Check macOS Privacy & Security permissions |
| "Non-secure connection" | Enable HTTP in PlayerSettings (already done) |
| "Port 8000 already in use" | Kill old process: `lsof -i :8000 \| kill -9 PID` |
| Python hangs | Python packages missing: `pip install sounddevice numpy` |
| No spectrum plot | Microphone not receiving audio - check device |
| Android not detected | Connect via USB, run `adb devices` |

---

## Next Steps

1. ✅ Run `SimplePipeline.py` → confirm audio works
2. ✅ Start Python HTTP server → confirm it listens
3. ✅ Run Unity scene → confirm it connects and sends audio
4. ✅ Watch live plots update in Python window
5. ✅ Check `logs/` folder for CSV recordings

---

**Questions?** Check the console output for `[HTTPBridge]`, `[AudioCaptureController]`, and `[Classifier]` log messages.
