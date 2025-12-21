using System;
using System.IO;
using Unity.Collections;
using UnityEngine;
using Meta.XR;

# if UNITY_EDITOR
using UnityEngine.InputSystem;
# endif

public class RecorderLegacy : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("에디터에서 Space 키로 테스트할지 여부")]
    public bool useKeyboardInEditor = true;

    [Header("Audio Settings")]
    public int sampleRate = 16000;
    public int maxRecordSeconds = 30;

    [Header("Capture Settings")]
    [Tooltip("PassthroughCameraAccess 컴포넌트를 드래그 앤 드롭")]
    public PassthroughCameraAccess pca;
    [Tooltip("Application.persistentDataPath 하위 저장 폴더 이름")]
    public string saveFolderName = "KARVIS";
    private string lastWavPath;
    private string lastPngPath;

    private string micDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private float recordStartTime = 0f;

    private void Start()
    {
        // 마이크 디바이스 택
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            Debug.Log("[KARVIS][Recorder] Mic device: " + micDevice);
        }
        else
        {
            Debug.LogError("[KARVIS][Recorder] No microphone device found!");
        }

        // PCA 지원 여부 확인
        if (!PassthroughCameraAccess.IsSupported)
        {
            Debug.LogWarning("[KARVIS][Recorder] PassthroughCameraAccess not supported on this device/OS.");
        }

        if (pca == null)
        {
            Debug.LogWarning("[KARVIS][Recorder] PassthroughCameraAccess reference is null. Assign it in Inspector.");
        }
        else
        {
            pca.enabled = true;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (useKeyboardInEditor)
        {
            // 새로운 Input System 사용
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                    StartRecording();
                if (Keyboard.current.spaceKey.wasReleasedThisFrame)
                    StopRecordingAndCapture();
            }
            return;
        }
#endif

        // 실제 Quest: 오른손 인덱스 트리거 사용
        bool rtDown = OVRInput.GetDown(OVRInput.RawButton.RIndexTrigger);
        bool rtUp = OVRInput.GetUp(OVRInput.RawButton.RIndexTrigger);

        if (rtDown)
            StartRecording();

        if (rtUp)
            StopRecordingAndCapture();
    }

    // ==========================
    //   AUDIO RECORDING
    // ==========================

    private void StartRecording()
    {
        if (isRecording) return;

        if (string.IsNullOrEmpty(micDevice))
        {
            Debug.LogError("[KARVIS][Recorder] Cannot start recording: micDevice is null.");
            return;
        }

        Debug.Log("[KARVIS][Recorder] Start Recording...");
        isRecording = true;
        recordStartTime = Time.time;

        recordingClip = Microphone.Start(micDevice, false, maxRecordSeconds, sampleRate);
    }

    private void StopRecordingAndCapture()
    {
        if (!isRecording) return;

        Debug.Log("[KARVIS][Recorder] Stop Recording.");
        isRecording = false;

        if (recordingClip == null)
        {
            Debug.LogError("[KARVIS][Recorder] recordingClip is null.");
            return;
        }

        // 마이크 정지
        Microphone.End(micDevice);

        // 실제 녹음된 시간(초)
        float recordLength = Time.time - recordStartTime;
        int samplesRecorded = Mathf.FloorToInt(recordingClip.frequency * recordLength);

        if (samplesRecorded <= 0)
        {
            Debug.LogWarning("[KARVIS][Recorder] No audio recorded (length <= 0).");
        }
        else
        {
            // 샘플 데이터 잘라내기
            int channels = recordingClip.channels;
            float[] samples = new float[samplesRecorded * channels];
            recordingClip.GetData(samples, 0);

            AudioClip trimmed = AudioClip.Create(
                "TrimmedClip",
                samplesRecorded,
                channels,
                recordingClip.frequency,
                false
            );
            trimmed.SetData(samples, 0);

            SaveWav(trimmed);
        }

        // 패스스루 한 장 캡쳐
        CapturePassthroughFrame();
    }

    private void SaveWav(AudioClip clip)
    {
        string folderPath = Path.Combine(Application.persistentDataPath, saveFolderName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "voice_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav";
        string filePath = Path.Combine(folderPath, fileName);
        lastWavPath = filePath;

        byte[] wavData = WavUtility.FromAudioClip(clip);
        File.WriteAllBytes(filePath, wavData);

        Debug.Log("[KARVIS][Recorder] Saved WAV: " + filePath);
    }

    // ==========================
    //   PCA CAPTURE
    // ==========================

    private void CapturePassthroughFrame()
    {
        if (pca == null)
        {
            Debug.LogWarning("[KARVIS][Recorder] PCA is null. Skip passthrough capture.");
            return;
        }

        if (!PassthroughCameraAccess.IsSupported)
        {
            Debug.LogWarning("[KARVIS][Recorder] PCA not supported on this device/OS.");
            return;
        }

        if (!pca.IsPlaying)
        {
            Debug.LogWarning("[KARVIS][Recorder] PCA is not playing. Did you start it in the scene?");
            // pca.Start() 를 명시적으로 호출해야 하는 버전도 있음 (MRUK 문서 확인)
        }

        Vector2Int res = pca.CurrentResolution;
        if (res.x <= 0 || res.y <= 0)
        {
            Debug.LogWarning("[KARVIS][Recorder] PCA resolution invalid.");
            return;
        }

        NativeArray<Color32> colors = pca.GetColors();
        if (!colors.IsCreated || colors.Length == 0)
        {
            Debug.LogWarning("[KARVIS][Recorder] PCA GetColors returned empty array.");
            return;
        }

        Texture2D tex = new Texture2D(res.x, res.y, TextureFormat.RGBA32, false);
        tex.SetPixelData(colors, 0);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        string folderPath = Path.Combine(Application.persistentDataPath, saveFolderName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "pca_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        string filePath = Path.Combine(folderPath, fileName);
        lastPngPath = filePath;

        File.WriteAllBytes(filePath, png);
        Debug.Log("[KARVIS][Recorder] Saved PCA PNG: " + filePath);
    }
}


// ==========================
//   WAV UTILITY
// ==========================

public static class WavUtility
{
    const int HEADER_SIZE = 44;

    public static byte[] FromAudioClip(AudioClip clip)
    {
        using (var stream = new MemoryStream())
        {
            int sampleCount = clip.samples * clip.channels;
            int sampleRate = clip.frequency;

            // 헤더 공간 확보
            for (int i = 0; i < HEADER_SIZE; i++)
                stream.WriteByte(0);

            float[] samples = new float[sampleCount];
            clip.GetData(samples, 0);

            short[] intData = new short[sampleCount];
            byte[] bytesData = new byte[sampleCount * 2];

            const float rescaleFactor = 32767f;
            for (int i = 0; i < sampleCount; i++)
            {
                intData[i] = (short)(samples[i] * rescaleFactor);
                byte[] byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }

            stream.Write(bytesData, 0, bytesData.Length);

            // 헤더 작성
            stream.Seek(0, SeekOrigin.Begin);

            int fileSize = (int)stream.Length - 8;
            int byteRate = sampleRate * clip.channels * 2;

            // RIFF
            WriteString(stream, "RIFF");
            WriteInt(stream, fileSize);
            WriteString(stream, "WAVE");

            // fmt chunk
            WriteString(stream, "fmt ");
            WriteInt(stream, 16);
            WriteShort(stream, 1);
            WriteShort(stream, (short)clip.channels);
            WriteInt(stream, sampleRate);
            WriteInt(stream, byteRate);
            WriteShort(stream, (short)(clip.channels * 2));
            WriteShort(stream, 16);

            // data chunk
            WriteString(stream, "data");
            WriteInt(stream, sampleCount * 2);

            return stream.ToArray();
        }
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteInt(Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteShort(Stream stream, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
