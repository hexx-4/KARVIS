using UnityEngine;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using TMPro;


public class Recorder : MonoBehaviour
{
    public TMP_Text promptText;
    public TMP_Text responseText;

    public SpeechToTextAgent sttAgent;
    public TTS ttsAgent;

    public GptHandler gpt;

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            promptText.text = "Mic device: " + Microphone.devices[0];
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
            responseText.text = " \" (listening) \" ";
        }

        // RT 버튼 Up
        if (OVRInput.GetUp(OVRInput.Button.SecondaryIndexTrigger))
        {
            sttAgent.StopNow();
            responseText.text = " \" (waiting) \" ";
        }
    }
    public void OnFinalResult(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length < 2) return; // 잡음 컷

        promptText.text = " 입력 : \" " + text + " \" ";
        gpt.SendPrompt(text);

    }
    public void OnGptResponseReceived(string response)
    {
        Debug.Log("[KARVIS][Recorder] GPT Response: " + response);
        responseText.text = " \" " + response + " \" ";
        ttsAgent.RunTTS(response);
    }

}

