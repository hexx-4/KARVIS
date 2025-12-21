using System.Collections.Generic;
using Meta.XR.ImmersiveDebugger;
using UnityEngine;
using Meta.XR.BuildingBlocks.AIBlocks;


[RequireComponent(typeof(LlmAgent))]
public sealed class GptHandler : MonoBehaviour
{
    [Header("Prompt Selection")]
    [Tooltip("Custom text to send as the prompt. If left empty, the default prompt from 'Selected Prompt' will be used.")]
    [SerializeField]
    private string userInput;

    [Tooltip("Choose a predefined default prompt if no custom user input is provided.")]
    [SerializeField]
    private DefaultPromptOption selectedPrompt;

    [Header("Image Source")]
    [Tooltip("Enable or disable sending an image along with the text prompt.")]
    public bool includeImage = true;

    [Tooltip("Select which image source to use when Include Image is enabled: Camera (fake image when in editor), Inspector Texture, or Image URL.")]
    [SerializeField]
    private PromptImageSource imageSource = PromptImageSource.Camera;

    [Tooltip("Image asset assigned in the Inspector. Only used when Image Source = InspectorTexture.")]
    [SerializeField]
    private Texture2D promptImage;

    [Tooltip("Direct URL to an image. Only used when Image Source = ImageUrl.")]
    [SerializeField]
    private string promptImageUrl;

    private LlmAgent _agent;

    private DefaultPromptOption _lastPrompt;

    private string _lastText;

    private void Awake()
    {
        _agent = GetComponent<LlmAgent>();
        _lastPrompt = selectedPrompt;
        _lastText = GetDefaultPromptText(_lastPrompt);
        if (string.IsNullOrWhiteSpace(userInput))
        {
            userInput = _lastText;
        }

        _agent.onPromptSent.AddListener(Logger);
        _agent.onResponseReceived.AddListener(Logger);
    }

    private void OnDestroy()
    {
        if (!(_agent == null))
        {
            _agent.onPromptSent.RemoveListener(Logger);
            _agent.onResponseReceived.RemoveListener(Logger);
        }
    }

    private void Update()
    {
        if (selectedPrompt != _lastPrompt)
        {
            if (string.IsNullOrWhiteSpace(userInput) || userInput == _lastText)
            {
                userInput = GetDefaultPromptText(selectedPrompt);
            }

            _lastText = GetDefaultPromptText(selectedPrompt);
            _lastPrompt = selectedPrompt;
        }
    }

    [DebugMember(DebugColor.Gray, Category = "LLM Agent")]
    public void SendPrompt(string customText = null)
    {
        string text = ((!string.IsNullOrWhiteSpace(customText)) ? customText : ((!string.IsNullOrWhiteSpace(userInput)) ? userInput : GetDefaultPromptText(selectedPrompt)));
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[LlmAgentHelper] No prompt to send.");
            return;
        }

        if (!includeImage)
        {
            _agent.SendTextOnlyAsync(text);
            return;
        }

        if (!_agent.ProviderSupportsVision)
        {
            Debug.LogWarning("[LlmAgentHelper] Current provider does not support image input. Sending text only.");
            _agent.SendTextOnlyAsync(text);
            return;
        }

        switch (imageSource)
        {
            case PromptImageSource.Camera:
                if (_agent.CanCapture)
                {
                    _agent.SendPromptWithPassthroughImageAsync(text);
                    break;
                }

                Debug.LogWarning("[LlmAgentHelper] Passthrough not available and no Editor fake camera. Sending text only.");
                _agent.SendTextOnlyAsync(text);
                break;
            case PromptImageSource.InspectorTexture:
                if ((bool)promptImage)
                {
                    _agent.SendPromptAsync(text, promptImage);
                    break;
                }

                Debug.LogWarning("[LlmAgentHelper] No Inspector texture assigned. Sending text only.");
                _agent.SendTextOnlyAsync(text);
                break;
            case PromptImageSource.ImageUrl:
                if (!string.IsNullOrWhiteSpace(promptImageUrl))
                {
                    ImageInput item = new ImageInput
                    {
                        url = promptImageUrl
                    };
                    _agent.SendPromptWithImagesAsync(text, new List<ImageInput> { item });
                }
                else
                {
                    Debug.LogWarning("[LlmAgentHelper] Empty image URL. Sending text only.");
                    _agent.SendTextOnlyAsync(text);
                }

                break;
            default:
                _agent.SendTextOnlyAsync(text);
                break;
        }
    }

    private static string GetDefaultPromptText(DefaultPromptOption o)
    {
        if (1 == 0)
        {
        }

        string result = o switch
        {
            DefaultPromptOption.DescribeImage => "What do you see on this image?",
            DefaultPromptOption.CapitalOfSwitzerland => "What is the capital of Switzerland?",
            DefaultPromptOption.Greeting => "Hi, how are you?",
            _ => string.Empty,
        };
        if (1 == 0)
        {
        }

        return result;
    }

    public static void Logger(string text)
    {
        Debug.Log(text);
    }
}