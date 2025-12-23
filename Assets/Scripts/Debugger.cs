using UnityEngine;
using UnityEngine.Events;

public class Debugger : MonoBehaviour
{
    [Header("Reference")]
    public Gpt gpt;

    [Header("Options")]
    public bool logAssistantMessage = true;
    public bool logAllToolCalls = true;
    public bool logBlockToolCalls = true;

    private void Awake()
    {
        if (gpt == null)
        {
            Debug.LogWarning("[Debugger] Gpt reference not set.");
            return;
        }

        gpt.OnAssistantMessage.AddListener(OnAssistantMessage);
        gpt.OnToolCalls.AddListener(OnToolCalls);
        gpt.OnBlockToolCall.AddListener(OnBlockToolCall);
    }

    private void OnDestroy()
    {
        if (gpt == null) return;

        gpt.OnAssistantMessage.RemoveListener(OnAssistantMessage);
        gpt.OnToolCalls.RemoveListener(OnToolCalls);
        gpt.OnBlockToolCall.RemoveListener(OnBlockToolCall);
    }

    // 1️⃣ GPT 텍스트 응답
    private void OnAssistantMessage(string message)
    {
        if (!logAssistantMessage) return;

        Debug.Log(
            $"<color=#7FDBFF>[GPT]</color> {message}"
        );
    }

    // 2️⃣ 모든 ToolCall (필터 없음)
    private void OnToolCalls(Gpt.ToolCall[] calls)
    {
        if (!logAllToolCalls || calls == null) return;

        foreach (var call in calls)
        {
            if (call?.function == null) continue;

            Debug.Log(
                $"<color=#AAAAAA>[Tool]</color> {call.function.name}\n" +
                $"Args: {call.function.arguments}"
            );
        }
    }

    // 3️⃣ Block Tool (씬 제어용)
    private void OnBlockToolCall(Gpt.ToolCall call)
    {
        if (!logBlockToolCalls || call?.function == null) return;

        Debug.Log(
            $"<color=#FF851B>[BlockTool]</color> {call.function.name}\n" +
            $"Args: {call.function.arguments}"
        );
    }
}
