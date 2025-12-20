using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using OpenAI;

public class DictationResultHandler : MonoBehaviour
{   
    public TextMesh promptText;
    public TextMesh responseText;
    public SpeechToTextAgent sttAgent;
    public TTS tts;
    public GptHandler gptHandler;
    private string micDevice;

    void Start()
    {
        //GptHandler gptHandler = GetComponent<GptHandler>();
        if (sttAgent == null) sttAgent = GetComponent<SpeechToTextAgent>();
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            promptText.text = "Mic device: " + micDevice;
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

        // 👉 여기서 GPT 호출 (완료 후 TTS 실행)
        promptText.text = " 입력 : \" " + text + " \" ";
        StartCoroutine(SendGpt(text));
        
    }
    private void OnGptResponseReceived()
    {
        responseText.text = " \" " + gptHandler.lastResponse + " \" ";
        tts.RunTTS(gptHandler.lastResponse);
    }

    IEnumerator SendGpt(string input)
    {
        yield return gptHandler.SendReply(input, OnGptResponseReceived);
    }
}
