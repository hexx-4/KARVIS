using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

/// <summary>
/// Minimal OpenAI ChatCompletion client for Unity.
/// - Supports GPT-4o(-mini) text/vision endpoints.
/// - Accepts optional raw tools JSON so you can use function calling without adding a full JSON serializer.
/// - Keeps an in-memory history when <see cref="rememberHistory"/> is true.
///
/// This script intentionally avoids extra dependencies so it can be dropped into the current project
/// without additional packages. Tool handling is surfaced via events so you can wire your own
/// scene-logic callbacks.
/// </summary>
public class Gpt : MonoBehaviour
{
    [Header("OpenAI Settings")]
    [Tooltip("OpenAI API Key. Do NOT commit real keys; inject via inspector or a secure loader.")]
    public string apiKey;

    [Tooltip("ChatCompletions endpoint.")]
    public string apiUrl = "https://api.openai.com/v1/chat/completions";

    [Tooltip("Model name, e.g. gpt-4o-mini or gpt-4o.")]
    public string model = "gpt-4o-mini";

    [Header("Prompt & History")]
    [Tooltip("Optional system prompt injected at the start of each conversation.")]
    [TextArea]
    public string systemPrompt = "You are a helpful VR assistant.";

    [Tooltip("Keep previous turns in memory. If disabled, each call is stateless.")]
    public bool rememberHistory = true;

    [Header("Tools (Function Calling)")]
    [Tooltip("Raw JSON array for tools. This allows function calling without a custom JSON serializer.")]
    [TextArea(5, 12)]
    public string toolsJson;

    [Tooltip("If true, the request will include the tools JSON (when provided)." )]
    public bool includeTools = false;

    [Header("Networking")]
    [Tooltip("Request timeout in seconds.")]
    public int timeoutSeconds = 30;

    [System.Serializable]
    public class Message
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class ToolFunction
    {
        public string name;
        public string arguments;
    }

    [System.Serializable]
    public class ToolCall
    {
        public string id;
        public string type;
        public ToolFunction function;
    }

    [System.Serializable]
    public class Choice
    {
        public Message message;
        public ToolCall[] tool_calls;
    }

    [System.Serializable]
    public class ChatCompletionResponse
    {
        public Choice[] choices;
    }

    [Header("Events")]
    public UnityEvent<string> OnAssistantMessage;
    public UnityEvent<ToolCall[]> OnToolCalls;

    private readonly List<Message> _history = new List<Message>();
    private bool _isRequestRunning;

    /// <summary>
    /// Sends a user message to the model. If rememberHistory is true, conversation turns are preserved in memory.
    /// </summary>
    public void SendUserMessage(string text, string overrideToolsJson = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogError("[Gpt] API key is empty. Please set it in the inspector or inject securely at runtime.");
            return;
        }

        if (_isRequestRunning)
        {
            Debug.LogWarning("[Gpt] Request already in flight; skipping this call.");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[Gpt] Empty user message; ignoring.");
            return;
        }

        var messages = new List<Message>();

        if (rememberHistory)
        {
            if (_history.Count == 0 && !string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new Message { role = "system", content = systemPrompt });
            }

            messages.AddRange(_history);
        }
        else if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new Message { role = "system", content = systemPrompt });
        }

        messages.Add(new Message { role = "user", content = text });

        var tools = includeTools ? (!string.IsNullOrWhiteSpace(overrideToolsJson) ? overrideToolsJson : toolsJson) : null;
        StartCoroutine(SendRequest(messages, tools));
    }

    private IEnumerator SendRequest(List<Message> messages, string tools)
    {
        _isRequestRunning = true;

        var body = BuildRequestBody(messages, tools);
        var bodyRaw = Encoding.UTF8.GetBytes(body);

        using (var request = new UnityWebRequest(apiUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Gpt] Request failed: {request.error}\n{request.downloadHandler.text}");
                _isRequestRunning = false;
                yield break;
            }

            var json = request.downloadHandler.text;
            var response = JsonUtility.FromJson<ChatCompletionResponse>(json);

            if (response?.choices == null || response.choices.Length == 0)
            {
                Debug.LogWarning("[Gpt] Empty response or choices.");
                _isRequestRunning = false;
                yield break;
            }

            var choice = response.choices[0];
            var assistantMessage = choice.message;

            // History update
            if (rememberHistory)
            {
                _history.AddRange(new[]
                {
                    new Message { role = "user", content = messages[messages.Count - 1].content },
                    new Message { role = assistantMessage.role, content = assistantMessage.content }
                });
            }

            // Tool calls
            if (choice.tool_calls != null && choice.tool_calls.Length > 0)
            {
                OnToolCalls?.Invoke(choice.tool_calls);
            }

            if (!string.IsNullOrEmpty(assistantMessage.content))
            {
                OnAssistantMessage?.Invoke(assistantMessage.content);
            }
        }

        _isRequestRunning = false;
    }

    private string BuildRequestBody(List<Message> messages, string tools)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"model\":\"").Append(model).Append("\",");
        sb.Append("\"messages\":[");
        for (int i = 0; i < messages.Count; i++)
        {
            sb.Append(JsonUtility.ToJson(messages[i]));
            if (i < messages.Count - 1)
            {
                sb.Append(",");
            }
        }
        sb.Append("]");

        if (!string.IsNullOrWhiteSpace(tools))
        {
            sb.Append(",\"tools\":");
            sb.Append(tools);
            sb.Append(",\"tool_choice\":\"auto\"");
        }

        sb.Append("}");
        return sb.ToString();
    }
}
