using UnityEngine;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;

public class VoiceRecorder : MonoBehaviour
{
    public SpeechToTextAgent sttAgent;
    private string micDevice;

    void Start()
    {
        if (sttAgent == null) sttAgent = GetComponent<SpeechToTextAgent>();
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            Debug.Log("[KARVIS] Mic device: " + micDevice);
        }
        else
        {
            Debug.LogError("[KARVIS] No microphone device found!");
        }
    }
    void Update()
    {
        // RT 버튼 Down
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            sttAgent.StartListening();
            Debug.Log("[KARVIS] STT START");
        }

        // RT 버튼 Up
        if (OVRInput.GetUp(OVRInput.Button.SecondaryIndexTrigger))
        {
            sttAgent.StopNow();
            Debug.Log("[KARVIS] STT STOP");
        }
    }
}
