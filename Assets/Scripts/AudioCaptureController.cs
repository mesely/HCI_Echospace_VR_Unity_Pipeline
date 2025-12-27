using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BackendHttpClient))]
public class AudioCaptureController : MonoBehaviour
{
    [Header("Audio Capture")]
    public int sampleRate = 16000;
    public float chunkDurationSeconds = 0.5f;   // length of each chunk
    public int recordingLengthSeconds = 10;     // length of circular buffer clip

    private BackendHttpClient _backend;
    private AudioClip _micClip;
    private string _micDevice = null;           // null = default mic
    private int _chunkSamples;
    private int _seq = 0;

    private void Awake()
    {
        _backend = GetComponent<BackendHttpClient>();
        _chunkSamples = Mathf.RoundToInt(sampleRate * chunkDurationSeconds);
    }

    private void Start()
    {
        StartMicrophone();
        StartCoroutine(CaptureLoop());
    }

    private void StartMicrophone()
    {
        // Print all available devices
        Debug.Log("[AudioCaptureController] Available microphone devices:");
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[AudioCaptureController] ❌ No microphone devices found!");
            Debug.LogError("[AudioCaptureController] Make sure microphone permission is granted.");
            Debug.LogError("[AudioCaptureController] macOS: System Settings → Privacy & Security → Microphone");
            Debug.LogError("[AudioCaptureController] Android: Check manifest microphone permission.");
            return;
        }

        // List all devices
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            Debug.Log($"[AudioCaptureController]   [{i}] {Microphone.devices[i]}");
        }

        // Try to pick a good device:
        // Priority: external USB > Built-in Mic
        int deviceIndex = 0;
        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            string devName = Microphone.devices[i].ToLower();
            if (devName.Contains("usb") || devName.Contains("rode") || devName.Contains("external"))
            {
                deviceIndex = i;
                Debug.Log($"[AudioCaptureController] ✓ Selected external device: {Microphone.devices[i]}");
                break;
            }
        }

        _micDevice = Microphone.devices[deviceIndex];
        Debug.Log($"[AudioCaptureController] Using device: {_micDevice}");

        _micClip = Microphone.Start(_micDevice, true, recordingLengthSeconds, sampleRate);

        if (_micClip == null)
        {
            Debug.LogError("[AudioCaptureController] ❌ Failed to start microphone: " + _micDevice);
        }
        else
        {
            Debug.Log($"[✓ AudioCaptureController] Microphone started successfully.");
            Debug.Log($"[AudioCaptureController] Device: {_micDevice}, SampleRate: {sampleRate}Hz, Channels: {_micClip.channels}");
        }
    }

    private IEnumerator CaptureLoop()
    {
        // Small delay to let mic fill initial buffer
        yield return new WaitForSeconds(0.5f);

        while (true)
        {
            yield return new WaitForSeconds(chunkDurationSeconds);
            SendLatestChunk();
        }
    }

    private void SendLatestChunk()
    {
        if (_micClip == null)
            return;

        int micPos = Microphone.GetPosition(_micDevice);
        if (micPos <= 0)
            return;

        int totalSamples = _micClip.samples;
        int startSample = micPos - _chunkSamples;

        float[] chunkData = new float[_chunkSamples];

        if (startSample >= 0)
        {
            _micClip.GetData(chunkData, startSample);
        }
        else
        {
            int firstPartLength = _chunkSamples + startSample; // startSample negative
            int secondPartLength = -startSample;

            float[] firstPart = new float[firstPartLength];
            _micClip.GetData(firstPart, totalSamples - firstPartLength);

            float[] secondPart = new float[secondPartLength];
            _micClip.GetData(secondPart, 0);

            Array.Copy(firstPart, 0, chunkData, 0, firstPartLength);
            Array.Copy(secondPart, 0, chunkData, firstPartLength, secondPartLength);
        }

        // DEBUG: check energy
        float maxAbs = 0f;
        for (int i = 0; i < chunkData.Length; i++)
        {
            float v = Mathf.Abs(chunkData[i]);
            if (v > maxAbs) maxAbs = v;
        }
        Debug.Log($"[UnityAudio] chunk len={chunkData.Length}, maxAbs={maxAbs}");

        byte[] pcmBytes = FloatArrayToByteArray(chunkData);
        string pcmBase64 = Convert.ToBase64String(pcmBytes);

        double nowUnix = GetUnixTime();
        double startUnix = nowUnix - chunkDurationSeconds;

        var chunkReq = new AudioChunkRequest
        {
            session_id = _backend.sessionId,
            timestamp_unix = nowUnix,
            seq = _seq++,
            samplerate_hz = sampleRate,
            channels = 1,
            sample_format = "float32",
            frame_count = _chunkSamples,
            device_unix_time_start = startUnix,
            device_unix_time_end = nowUnix,
            pcm_base64 = pcmBase64
        };

        StartCoroutine(_backend.SendAudioChunk(chunkReq));
    }

    private static byte[] FloatArrayToByteArray(float[] data)
    {
        int len = data.Length;
        byte[] bytes = new byte[len * 4]; // 4 bytes per float32

        for (int i = 0; i < len; i++)
        {
            byte[] fBytes = BitConverter.GetBytes(data[i]);
            // Ensure little-endian order if needed; Unity/most platforms already use little-endian
            Array.Copy(fBytes, 0, bytes, i * 4, 4);
        }

        return bytes;
    }

    private double GetUnixTime()
    {
        return (DateTime.UtcNow -
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .TotalSeconds;
    }
}
