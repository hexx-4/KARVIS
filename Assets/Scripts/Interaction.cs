using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Bridges Gpt tool-calls to predefined scene blocks.
/// - Only the whitelisted interactables are exposed to ChatGPT/LLM.
/// - Provides lightweight state summaries the model can consume.
/// - Applies actions from tool calls (e.g., recolor).
/// </summary>
public class Interaction : MonoBehaviour
{
    [Serializable]
    public class Interactable
    {
        public string name;
        public GameObject target;
    }

    [Header("References")]
    [Tooltip("The GPT chat client that raises block tool calls.")]
    public Gpt gpt;

    [Header("Interactables")]
    [Tooltip("Only these objects will be visible/controllable to the LLM tools.")]
    public List<Interactable> interactables = new List<Interactable>();

    [Header("Feedback")]
    [Tooltip("Optional event to surface tool results to UI/TTS/etc.")]
    public UnityEvent<string> OnToolActionFeedback;

    [Serializable]
    private class RecolorArgs
    {
        public string name;
        public string color;
    }

    [Serializable]
    private class StateArgs
    {
        public string name;
    }

    private void Awake()
    {
        if (gpt != null)
        {
            gpt.OnBlockToolCall.AddListener(HandleToolCall);
        }
        else
        {
            Debug.LogWarning("[Interaction] GPT reference not set; tool calls will be ignored.");
        }
    }

    private void OnDestroy()
    {
        if (gpt != null)
        {
            gpt.OnBlockToolCall.RemoveListener(HandleToolCall);
        }
    }

    private void HandleToolCall(Gpt.ToolCall call)
    {
        if (call == null || call.function == null)
        {
            return;
        }

        switch (call.function.name)
        {
            case "list_interactables":
                SendFeedback(BuildSceneSummary());
                break;

            case "get_interactable_state":
                HandleGetState(call.function.arguments);
                break;

            case "recolor_object":
                HandleRecolor(call.function.arguments);
                break;

            default:
                SendFeedback($"Unsupported tool '{call.function.name}'.");
                break;
        }
    }

    private void HandleGetState(string argsJson)
    {
        var args = JsonUtility.FromJson<StateArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name))
        {
            SendFeedback("get_interactable_state failed: missing name.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"get_interactable_state: '{args.name}' not found or not whitelisted.");
            return;
        }

        SendFeedback(BuildSingleSummary(interactable));
    }

    private void HandleRecolor(string argsJson)
    {
        var args = JsonUtility.FromJson<RecolorArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name) || string.IsNullOrWhiteSpace(args.color))
        {
            SendFeedback("recolor_object failed: missing name or color.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"recolor_object: '{args.name}' not found or not whitelisted.");
            return;
        }

        var renderers = interactable.target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            SendFeedback($"recolor_object: '{args.name}' has no renderer.");
            return;
        }

        if (!TryParseColor(args.color, out var parsed))
        {
            SendFeedback($"recolor_object: could not parse color '{args.color}'. Use names or hex (#RRGGBB).");
            return;
        }

        foreach (var r in renderers)
        {
            var mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i].color = parsed;
            }
        }

        SendFeedback($"recolor_object: '{args.name}' set to {ColorUtility.ToHtmlStringRGB(parsed)}.");
    }

    private Interactable FindInteractable(string name)
    {
        return interactables.Find(i => i != null && i.target != null && string.Equals(i.name, name, StringComparison.OrdinalIgnoreCase));
    }

    private string BuildSceneSummary()
    {
        var sb = new StringBuilder();
        sb.Append("{\"interactables\":[");

        bool first = true;
        foreach (var interactable in interactables)
        {
            if (interactable == null || interactable.target == null)
            {
                continue;
            }

            if (!first)
            {
                sb.Append(",");
            }

            sb.Append(BuildSingleSummary(interactable));
            first = false;
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string BuildSingleSummary(Interactable interactable)
    {
        var t = interactable.target.transform;
        var renderer = interactable.target.GetComponentInChildren<Renderer>();
        var colorHex = renderer != null ? ColorUtility.ToHtmlStringRGB(renderer.sharedMaterial.color) : "unknown";
        var rb = interactable.target.GetComponent<Rigidbody>();
        var velocity = rb != null ? rb.velocity : Vector3.zero;

        return $"{{\"name\":\"{interactable.name}\",\"position\":{{\"x\":{t.position.x:F2},\"y\":{t.position.y:F2},\"z\":{t.position.z:F2}}},\"velocity\":{{\"x\":{velocity.x:F2},\"y\":{velocity.y:F2},\"z\":{velocity.z:F2}}},\"color\":\"#{colorHex}\"}}";
    }

    private static bool TryParseColor(string input, out Color color)
    {
        // Accept hex (#RRGGBB / #RRGGBBAA) or named Unity colors.
        if (ColorUtility.TryParseHtmlString(input, out color))
        {
            return true;
        }

        switch (input.ToLowerInvariant())
        {
            case "red": color = Color.red; return true;
            case "green": color = Color.green; return true;
            case "blue": color = Color.blue; return true;
            case "yellow": color = Color.yellow; return true;
            case "white": color = Color.white; return true;
            case "black": color = Color.black; return true;
            case "gray":
            case "grey": color = Color.gray; return true;
            case "magenta": color = Color.magenta; return true;
            case "cyan": color = Color.cyan; return true;
            default: return false;
        }
    }

    private void SendFeedback(string text)
    {
        OnToolActionFeedback?.Invoke(text);
        Debug.Log($"[Interaction] {text}");
    }
}
