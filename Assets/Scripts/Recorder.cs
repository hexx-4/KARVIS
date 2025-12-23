using UnityEngine;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using TMPro;
using UnityEngine.InputSystem;


public class Recorder : MonoBehaviour
{
    public string promptText;
    public string responseText;
    public TMP_Text resultText;
    public SpeechToTextAgent sttAgent;
    public TTS ttsAgent;

    public Gpt gpt;
    public Interaction interaction;
    public AudioSource on;
    public AudioSource off;

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            Debug.Log("[KARVIS] Microphone device found: " + Microphone.devices[0]);
        }
        else
        {
            Debug.LogError("[KARVIS] No microphone device found!");
        }
    }

    void Update()
    {
        // RT 버튼 Down
        if ((OVRInput.GetDown(OVRInput.Button.One) && !ttsAgent.isResponding) || (Keyboard.current.rKey.wasPressedThisFrame && !ttsAgent.isResponding))
        {
            on.Play();  
            sttAgent.StartListening();
            promptText = "Q. \" (listening) \" ";
            resultText.text = promptText + "\n" + responseText;
        }

        // RT 버튼 Up
        if ((OVRInput.GetUp(OVRInput.Button.One) && !ttsAgent.isResponding) || (Keyboard.current.rKey.wasReleasedThisFrame && !ttsAgent.isResponding))
        {
            off.Play();
            sttAgent.StopNow();
            responseText = "A. \" (waiting) \" ";
            resultText.text = promptText + "\n" + responseText;
        }
    }
    public void OnFinalResult(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length < 2) return; // 잡음 컷

        promptText = "Q. \" " + text + " \" ";
        resultText.text = promptText + "\n" + responseText;
        ttsAgent.isResponding = true;
        gpt.SendUserMessage(text, interaction.Editor_ListInteractables_ForGPT());

    }
    public void OnGptResponseReceived(string response)
    {
        Debug.Log("[KARVIS][Recorder] GPT Response: " + response);
        responseText = "A. \" " + response + " \" ";
        resultText.text = promptText + "\n" + responseText;
        ttsAgent.RunTTS(response);
    }

}

