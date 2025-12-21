using UnityEngine;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using TMPro;


public class Recorder : MonoBehaviour
{
    public string promptText;
    public string responseText;
    public TMP_Text resultText;
    public TMP_Text CamSyncText;
    public SpeechToTextAgent sttAgent;
    public TTS ttsAgent;

    public GptHandler gpt;
    public string role;
    public AudioSource on;
    public AudioSource off;
    public Transform[] balls;
    public Transform[] cubes;
    public Transform[] cylinders;

    void Start()
    {
        balls = GameObject.Find("Balls").GetComponentsInChildren<Transform>();
        cubes = GameObject.Find("Cubes").GetComponentsInChildren<Transform>();
        cylinders = GameObject.Find("Cylinders").GetComponentsInChildren<Transform>();
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
        if (OVRInput.GetDown(OVRInput.Button.One) && !ttsAgent.isResponding)
        {
            on.Play();
            sttAgent.StartListening();
            promptText = "Q. \" (listening) \" ";
            resultText.text = promptText + "\n" + responseText;
        }

        // RT 버튼 Up
        if (OVRInput.GetUp(OVRInput.Button.One) && !ttsAgent.isResponding)
        {
            off.Play();
            sttAgent.StopNow();
            responseText = "A. \" (waiting) \" ";
            resultText.text = promptText + "\n" + responseText;
        }

        if (OVRInput.Get(OVRInput.Button.Three))
        {   
            foreach (Transform t in balls)
            {
                t.transform.position = new Vector3(0, 1, 0);
            }
        }

        if (OVRInput.GetDown(OVRInput.Button.Four))
        {
            if (!gpt.includeImage)
            {
                gpt.includeImage = true;
                CamSyncText.text = "[Y] 카메라 동기화 ON";
            }
            else
            {
                gpt.includeImage = false;
                CamSyncText.text = "[Y] 카메라 동기화 OFF";
            }
        }
    }
    public void OnFinalResult(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length < 2) return; // 잡음 컷

        promptText = "Q. \" " + text + " \" ";
        resultText.text = promptText + "\n" + responseText;
        ttsAgent.isResponding = true;
        gpt.SendPrompt($"System: \"{role}\" / User: \"{text}\"");

    }
    public void OnGptResponseReceived(string response)
    {
        Debug.Log("[KARVIS][Recorder] GPT Response: " + response);
        responseText = "A. \" " + response + " \" ";
        resultText.text = promptText + "\n" + responseText;
        ttsAgent.RunTTS(response);
    }

}

