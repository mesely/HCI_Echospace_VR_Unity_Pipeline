// Assets/StereoMicAndroidTest.cs
using UnityEngine;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

[RequireComponent(typeof(BackendHttpClient))]
public class StereoMicAndroidTest : MonoBehaviour
{
    [Header("Device Selection")]
    public string preferredDeviceKeyword = "Wireless"; // Rode Wireless vb.
    public bool preferConnectedDrivers = true;         // connected driver varsa onu seç
    public bool fallbackToFirstDevice = true;          // keyword bulamazsa 0. cihaz

    [Header("Recording")]
    public int sampleRate = 48000;
    public float probeIntervalSec = 0.25f;
    public int ringSeconds = 10;                       // ring buffer (sn)
    public uint windowFrames = 2048;                   // RMS için son N frame

    [Header("UI")]
    public bool showOnScreen = true;

    // FMOD
    private FMOD.System fmod;
    private Sound recSound;
    private int recDeviceId = -1;
    private bool recording = false;

    // Selected device info (cache)
    private string recDeviceName = "n/a";
    private int recDeviceChannels = -1;                // driver-reported
    private int recDeviceSysRate = -1;
    private SPEAKERMODE recDeviceMode = SPEAKERMODE.DEFAULT;
    private DRIVER_STATE recDeviceState = 0;

    // All detected devices (for UI display)
    private System.Collections.Generic.List<string> allDevicesList = new System.Collections.Generic.List<string>();

    // Actual capture format we create for the record sound
    private uint actualChannels = 1;
    private const uint BytesPerSample = 2;             // PCM16
    private uint FrameBytes => actualChannels * BytesPerSample;
    private uint ringBytes;

    // Probe results
    private double lastRmsL, lastRmsR;
    private string lastDominant = "n/a";
    private string status = "init";

    // Backend
    private BackendHttpClient _backend;
    private int _seq = 0;

    // --- Logging helpers: EVERY log has channel count + mic name ---
    private string LogPrefix => $"[FMOD][dev={recDeviceId} name='{recDeviceName}' ch={recDeviceChannels}] ";

    private void Log(string msg) => UnityEngine.Debug.Log(LogPrefix + msg);
    private void LogW(string msg) => UnityEngine.Debug.LogWarning(LogPrefix + msg);
    private void LogE(string msg) => UnityEngine.Debug.LogError(LogPrefix + msg);

    void Start()
    {
        Application.runInBackground = true;

        _backend = GetComponent<BackendHttpClient>();

#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            status = "Requesting MICROPHONE permission...";
            UnityEngine.Debug.Log(LogPrefix + status);
            Permission.RequestUserPermission(Permission.Microphone);
            return; // izin gelince Update içinde devam
        }
#endif
        TryStartProbe();
    }

    void Update()
    {
#if UNITY_ANDROID
        if (!recording &&
            Permission.HasUserAuthorizedPermission(Permission.Microphone) &&
            status.StartsWith("Requesting", StringComparison.OrdinalIgnoreCase))
        {
            TryStartProbe();
        }
#endif
    }

    void TryStartProbe()
    {
        status = "Starting FMOD probe...";
        UnityEngine.Debug.Log(LogPrefix + status);

        try
        {
            fmod = RuntimeManager.CoreSystem;
        }
        catch (Exception e)
        {
            status = "ERROR: RuntimeManager.CoreSystem failed";
            UnityEngine.Debug.LogError(LogPrefix + status + " :: " + e);
            return;
        }

        // Record driver bilgileri
        int numDrivers, numConnected;
        RESULT r = fmod.getRecordNumDrivers(out numDrivers, out numConnected);
        if (r != RESULT.OK)
        {
            status = "ERROR: getRecordNumDrivers failed: " + r;
            LogE(status);
            return;
        }

        Log($"Record devices: {numDrivers} (connected: {numConnected})");

        if (numDrivers <= 0)
        {
            status = "ERROR: No record drivers. (Android permission, audio route, or FMOD init timing)";
            LogE(status);
            return;
        }

        // Cihaz seçimi: keyword match, yoksa built-in mic, yoksa connected, yoksa 0
        int keywordMatch = -1;
        int builtInIndex = -1;
        int firstConnected = -1;
        allDevicesList.Clear();

        for (int i = 0; i < numDrivers; i++)
        {
            string name;
            System.Guid guidTmp;          // ✅ System.Guid (System.GUID değil)
            int sysRate;
            SPEAKERMODE spk;
            int ch;
            DRIVER_STATE state;

            r = fmod.getRecordDriverInfo(i, out name, 256, out guidTmp, out sysRate, out spk, out ch, out state);
            if (r != RESULT.OK)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + $"getRecordDriverInfo failed for {i}: {r}");
                continue;
            }

            bool isConnected = (state & DRIVER_STATE.CONNECTED) != 0;
            string connTag = isConnected ? "[CONNECTED]" : "[disconnected]";
            string devInfo = $"[{i}] {name} (ch={ch}, {sysRate}Hz) {connTag}";
            allDevicesList.Add(devInfo);

            // Her cihaz satırında channel sayısı görünsün
            UnityEngine.Debug.Log(LogPrefix + $"Device[{i}] '{name}' sysRate={sysRate} ch={ch} mode={spk} state={state} connected={isConnected}");

            if (firstConnected < 0 && isConnected) firstConnected = i;

            // Built-in mic preference: prefer MacBook/Built-in Microphone over Bluetooth (e.g., AirPods)
            if (builtInIndex < 0 && isConnected && !string.IsNullOrEmpty(name))
            {
                bool looksBuiltIn =
                    name.IndexOf("Microphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Built-in", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("MacBook", StringComparison.OrdinalIgnoreCase) >= 0;

                if (looksBuiltIn)
                {
                    builtInIndex = i;
                }
            }

            // Keyword kontrolü - boş string ise keyword'e bakma
            if (keywordMatch < 0 &&
                !string.IsNullOrEmpty(preferredDeviceKeyword) &&
                !string.IsNullOrEmpty(name) &&
                name.IndexOf(preferredDeviceKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                keywordMatch = i;
            }
        }

        // Seçim önceliği:
        // 1. Keyword match (eğer keyword boş değilse)
        // 2. Built-in mic (MacBook/Built-in) bağlıysa
        // 3. Connected cihaz (preferConnectedDrivers true ise)
        // 4. İlk cihaz (fallbackToFirstDevice true ise)
        if (keywordMatch >= 0)
        {
            recDeviceId = keywordMatch;
            Log($"Selected by keyword match: device {keywordMatch}");
        }
        else if (builtInIndex >= 0)
        {
            recDeviceId = builtInIndex;
            Log($"Selected built-in mic: device {builtInIndex}");
        }
        else if (preferConnectedDrivers && firstConnected >= 0)
        {
            recDeviceId = firstConnected;
            Log($"Selected by connected state: device {firstConnected}");
        }
        else if (fallbackToFirstDevice)
        {
            recDeviceId = 0;
            Log($"Selected by fallback: device 0");
        }

        if (recDeviceId < 0)
        {
            status = "ERROR: Device not found. Change preferredDeviceKeyword.";
            LogE(status);
            return;
        }

        // Seçilen cihazın adını + kanal sayısını kesin olarak al
        {
            System.Guid guidSel;          // ✅ System.Guid
            RESULT rr = fmod.getRecordDriverInfo(
                recDeviceId,
                out recDeviceName, 256,
                out guidSel,
                out recDeviceSysRate,
                out recDeviceMode,
                out recDeviceChannels,
                out recDeviceState
            );

            if (rr != RESULT.OK)
            {
                recDeviceName = "unknown";
                recDeviceChannels = -1;
                recDeviceSysRate = sampleRate;
                recDeviceMode = SPEAKERMODE.DEFAULT;
                recDeviceState = 0;
                LogW($"getRecordDriverInfo(selected) failed: {rr}");
            }
        }

        Log($"Selected device. sysRate={recDeviceSysRate}, mode={recDeviceMode}, state={recDeviceState}");

        // Stereo mu?
        string stereoTag = (recDeviceChannels == 2) ? "STEREO ✅" :
                           (recDeviceChannels == 1) ? "MONO ❌" :
                           $"UNKNOWN(ch={recDeviceChannels})";
        Log($"Driver reports: {stereoTag}");

        // Bizim kayıt sound'umuzun kanal sayısı:
        // - Driver 2 veriyorsa 2 yakala
        // - 1 veriyorsa 1 yakala
        // - saçma/unknown ise 1
        actualChannels = (uint)((recDeviceChannels == 2) ? 2 : 1);

        // Ring buffer size (bytes)
        ringBytes = (uint)(ringSeconds * sampleRate * actualChannels * BytesPerSample);
        Log($"Capture format: actualChannels={actualChannels}, frameBytes={FrameBytes}, ringBytes={ringBytes}");

        // Create a user sound for recording
        CREATESOUNDEXINFO ex = new CREATESOUNDEXINFO();
        ex.cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO));
        ex.numchannels = (int)actualChannels;
        ex.defaultfrequency = sampleRate;
        ex.format = SOUND_FORMAT.PCM16;
        ex.length = ringBytes;

        r = fmod.createSound((string)null, MODE.OPENUSER | MODE.LOOP_NORMAL, ref ex, out recSound);
        if (r != RESULT.OK)
        {
            status = "ERROR: createSound failed: " + r;
            LogE(status);
            return;
        }

        // Start recording
        r = fmod.recordStart(recDeviceId, recSound, true);
        if (r != RESULT.OK)
        {
            status = "ERROR: recordStart failed: " + r;
            LogE(status);
            SafeReleaseSound();
            return;
        }

        recording = true;
        status = "Recording started. Probing...";
        Log(status);

        CancelInvoke();
        InvokeRepeating(nameof(Probe), 0.5f, probeIntervalSec);
    }

    void OnDisable()
    {
        CancelInvoke();

        if (recording && recDeviceId >= 0)
        {
            fmod.recordStop(recDeviceId);
            recording = false;
            Log("Recording stopped.");
        }

        SafeReleaseSound();
    }

    void SafeReleaseSound()
    {
        try
        {
            if (recSound.hasHandle())
            {
                recSound.release();
                Log("Sound released.");
            }
        }
        catch { /* ignore */ }
    }

    void Probe()
    {
        if (!recording) return;

        bool isRec;
        RESULT r = fmod.isRecording(recDeviceId, out isRec);
        if (r != RESULT.OK || !isRec) return;

        uint recordPos;
        r = fmod.getRecordPosition(recDeviceId, out recordPos);
        if (r != RESULT.OK) return;

        uint windowBytes = windowFrames * FrameBytes;

        uint writeBytes = recordPos * FrameBytes;
        uint startBytes = (writeBytes + ringBytes - windowBytes) % ringBytes;

        IntPtr p1, p2;
        uint len1, len2;

        r = recSound.@lock(startBytes, windowBytes, out p1, out p2, out len1, out len2);
        if (r != RESULT.OK) return;

        double sumL = 0.0, sumR = 0.0;
        int frames = 0;

        // Collect mono-float samples for backend chunk
        System.Collections.Generic.List<float> monoSamples = new System.Collections.Generic.List<float>((int)windowFrames);

        ProcessBlock(p1, len1, actualChannels, ref sumL, ref sumR, ref frames, monoSamples);
        if (len2 > 0) ProcessBlock(p2, len2, actualChannels, ref sumL, ref sumR, ref frames, monoSamples);

        recSound.unlock(p1, p2, len1, len2);

        if (frames <= 0) return;

        double rmsL = Math.Sqrt(sumL / frames);
        double rmsR = (actualChannels >= 2) ? Math.Sqrt(sumR / frames) : 0.0;

        lastRmsL = rmsL;
        lastRmsR = rmsR;

        string dominant;
        if (actualChannels < 2)
        {
            dominant = "MONO input";
        }
        else
        {
            dominant = "similar";
            if (rmsL > rmsR * 1.25) dominant = "LEFT dominant";
            else if (rmsR > rmsL * 1.25) dominant = "RIGHT dominant";
        }

        lastDominant = dominant;

        // Her log satırında name + ch prefix var ✅
        Log($"RMS_L={rmsL:F6} RMS_R={rmsR:F6} => {dominant}");

        // Send latest captured window to backend as mono float32
        if (_backend != null)
        {
            float[] chunkData = monoSamples.ToArray();
            byte[] pcmBytes = FloatArrayToByteArray(chunkData);
            string pcmBase64 = Convert.ToBase64String(pcmBytes);

            double nowUnix = GetUnixTime();
            double durationSec = chunkData.Length / (double)sampleRate;
            double startUnix = nowUnix - durationSec;

            var chunkReq = new AudioChunkRequest
            {
                session_id = _backend.sessionId,
                timestamp_unix = nowUnix,
                seq = _seq++,
                samplerate_hz = sampleRate,
                channels = 1,
                sample_format = "float32",
                frame_count = chunkData.Length,
                device_unix_time_start = startUnix,
                device_unix_time_end = nowUnix,
                pcm_base64 = pcmBase64
            };

            StartCoroutine(_backend.SendAudioChunk(chunkReq));
        }
    }

    static void ProcessBlock(IntPtr ptr, uint byteLen, uint channels, ref double sumL, ref double sumR, ref int frames, System.Collections.Generic.List<float> monoOut)
    {
        if (ptr == IntPtr.Zero || byteLen < 2) return;

        int n = (int)byteLen;
        byte[] buf = new byte[n];
        Marshal.Copy(ptr, buf, 0, n);

        if (channels < 2)
        {
            // Mono PCM16: [M_lo, M_hi] repeating
            for (int i = 0; i + 1 < n; i += 2)
            {
                short M = (short)(buf[i] | (buf[i + 1] << 8));
                double mf = M / 32768.0;
                sumL += mf * mf;
                frames++;
                monoOut?.Add((float)mf);
            }
            return;
        }

        // Stereo PCM16 little-endian: [L_lo, L_hi, R_lo, R_hi] repeating
        for (int i = 0; i + 3 < n; i += 4)
        {
            short L = (short)(buf[i] | (buf[i + 1] << 8));
            short R = (short)(buf[i + 2] | (buf[i + 3] << 8));

            double lf = L / 32768.0;
            double rf = R / 32768.0;

            sumL += lf * lf;
            sumR += rf * rf;
            frames++;

             // Downmix to mono for backend
             float mono = (float)((lf + rf) * 0.5);
             monoOut?.Add(mono);
        }
    }

    private static byte[] FloatArrayToByteArray(float[] data)
    {
        int len = data.Length;
        byte[] bytes = new byte[len * 4];
        for (int i = 0; i < len; i++)
        {
            byte[] fBytes = BitConverter.GetBytes(data[i]);
            Array.Copy(fBytes, 0, bytes, i * 4, 4);
        }
        return bytes;
    }

    private double GetUnixTime()
    {
        return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    void OnGUI()
    {
        if (!showOnScreen) return;

        GUIStyle s = new GUIStyle(GUI.skin.label);
        s.fontSize = 22;

        GUIStyle titleStyle = new GUIStyle(s);
        titleStyle.fontSize = 26;
        titleStyle.fontStyle = FontStyle.Bold;

        GUIStyle deviceStyle = new GUIStyle(s);
        deviceStyle.fontSize = 18;

        int areaHeight = 300 + (allDevicesList.Count * 30);
        GUILayout.BeginArea(new Rect(20, 20, 1800, areaHeight));
        
        GUILayout.Label("FMOD Stereo Probe", titleStyle);
        GUILayout.Label(status, s);

#if UNITY_ANDROID
        GUILayout.Label("Android mic permission: " +
                        (Permission.HasUserAuthorizedPermission(Permission.Microphone) ? "GRANTED" : "NOT GRANTED"), s);
#endif

        GUILayout.Space(10);
        GUILayout.Label("═══ BULUNAN CİHAZLAR ═══", titleStyle);
        if (allDevicesList.Count > 0)
        {
            foreach (string dev in allDevicesList)
            {
                GUILayout.Label(dev, deviceStyle);
            }
        }
        else
        {
            GUILayout.Label("Henüz cihaz taranmadı.", deviceStyle);
        }

        GUILayout.Space(10);
        GUILayout.Label("═══ SEÇİLEN CİHAZ ═══", titleStyle);
        GUILayout.Label($"DeviceId={recDeviceId}  Name='{recDeviceName}'  DriverCh={recDeviceChannels}", s);
        GUILayout.Label($"SysRate={recDeviceSysRate}  Mode={recDeviceMode}  State={recDeviceState}", s);
        GUILayout.Label($"ActualCaptureCh={actualChannels}  WindowFrames={windowFrames}", s);

        if (recording)
        {
            GUILayout.Space(10);
            GUILayout.Label($"RMS_L={lastRmsL:F6}  RMS_R={lastRmsR:F6}  => {lastDominant}", s);
        }

        GUILayout.EndArea();
    }
}
