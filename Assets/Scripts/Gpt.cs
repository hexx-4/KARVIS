using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class Gpt : MonoBehaviour
{
    [Header("OpenAI")]
    private string apiKey = "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";
    public string apiUrl = "https://api.openai.com/v1/chat/completions";
    public string model = "gpt-4o-mini";

    [Header("Prompt")]
    [TextArea]
    public string systemPrompt;

    [Header("Scene Context")]
    public Interaction interaction;
    public bool injectSceneContext = true;

    [Header("Tools")]
    [TextArea(5, 12)]
    private string toolsJson = @"
[
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""recolor_object"",
            ""description"": ""Change the color of an interactable object."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": {
                        ""type"": ""string"",
                        ""description"": ""Name of the interactable object.""
                    },
                    ""color"": {
                        ""type"": ""string"",
                        ""description"": ""Color name (red, blue, green) or hex value (#RRGGBB).""
                    }
                },
                ""required"": [
                    ""name"",
                    ""color""
                ]
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""remove_interactable"",
            ""description"": ""Remove an interactable object from the scene and whitelist."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": {
                        ""type"": ""string"",
                        ""description"": ""Name of the interactable object to remove.""
                    }
                },
                ""required"": [
                    ""name""
                ]
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""set_position"",
            ""description"": ""Move an interactable object to a new world position."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": {
                        ""type"": ""string"",
                        ""description"": ""Name of the interactable object.""
                    },
                    ""position"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" }
                        },
                        ""required"": [ ""x"", ""y"", ""z"" ]
                    }
                },
                ""required"": [ ""name"", ""position"" ]
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""set_velocity"",
            ""description"": ""Set the velocity of an interactable object that has a Rigidbody."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""velocity"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" }
                        },
                        ""required"": [ ""x"", ""y"", ""z"" ]
                    }
                },
                ""required"": [ ""name"", ""velocity"" ]
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""set_rotation"",
            ""description"": ""Set the world rotation of an interactable object using a Quaternion."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""rotation"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" },
                            ""w"": { ""type"": ""number"" }
                        },
                        ""required"": [ ""x"", ""y"", ""z"", ""w"" ]
                    }
                },
                ""required"": [ ""name"", ""rotation"" ]
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""set_scale"",
            ""description"": ""Set the local scale of an interactable object."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""scale"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" }
                        },
                        ""required"": [ ""x"", ""y"", ""z"" ]
                    }
                },
                ""required"": [ ""name"", ""scale"" ]
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""create_cube"",
            ""description"": ""Spawn a cube interactable from the prefab template (with Rigidbody)."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""position"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" }
                        }
                    },
                    ""rotation"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" },
                            ""w"": { ""type"": ""number"" }
                        }
                    },
                    ""scale"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" }
                        }
                    },
                    ""velocity"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""x"": { ""type"": ""number"" },
                            ""y"": { ""type"": ""number"" },
                            ""z"": { ""type"": ""number"" }
                        }
                    },
                    ""color"": { ""type"": ""string"" }
                }
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""create_cylinder"",
            ""description"": ""Spawn a cylinder interactable from the prefab template (with Rigidbody)."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""position"": { ""type"": ""object"" },
                    ""rotation"": { ""type"": ""object"" },
                    ""scale"": { ""type"": ""object"" },
                    ""velocity"": { ""type"": ""object"" },
                    ""color"": { ""type"": ""string"" }
                }
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""create_ball"",
            ""description"": ""Spawn a soccer-ball-style sphere interactable from the prefab template (with Rigidbody)."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""position"": { ""type"": ""object"" },
                    ""rotation"": { ""type"": ""object"" },
                    ""scale"": { ""type"": ""object"" },
                    ""velocity"": { ""type"": ""object"" },
                    ""color"": { ""type"": ""string"" }
                }
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""create_disc"",
            ""description"": ""Spawn a thin disc interactable (curling material) from the prefab template (with Rigidbody)."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""position"": { ""type"": ""object"" },
                    ""rotation"": { ""type"": ""object"" },
                    ""scale"": { ""type"": ""object"" },
                    ""velocity"": { ""type"": ""object"" },
                    ""color"": { ""type"": ""string"" }
                }
            }
        }
    },
    {
        ""type"": ""function"",
        ""function"": {
            ""name"": ""create_mammae"",
            ""description"": ""Spawn a long, thin cylinder (mammae) from the prefab template (with Rigidbody)."",
            ""parameters"": {
                ""type"": ""object"",
                ""properties"": {
                    ""name"": { ""type"": ""string"" },
                    ""position"": { ""type"": ""object"" },
                    ""rotation"": { ""type"": ""object"" },
                    ""scale"": { ""type"": ""object"" },
                    ""velocity"": { ""type"": ""object"" },
                    ""color"": { ""type"": ""string"" }
                }
            }
        }
    }
]
";


    public bool includeTools = true;

    [Header("Networking")]
    public int timeoutSeconds = 30;

    // ===== OpenAI DTOs =====

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
    public class ChatMessage
    {
        public string role;
        public string content;
        public ToolCall[] tool_calls;
    }

    [System.Serializable]
    public class Choice
    {
        public ChatMessage message;
    }

    [System.Serializable]
    public class ChatCompletionResponse
    {
        public Choice[] choices;
    }

    // ===== Events =====

    public UnityEvent<string> OnAssistantMessage;
    public UnityEvent<ToolCall[]> OnToolCalls;
    public UnityEvent<ToolCall> OnBlockToolCall;

    [Header("Block Tool Names")]
    public string[] blockToolNames =
    {
        "recolor_object",
        "set_position",
        "set_rotation",
        "set_scale",
        "set_velocity",
        "create_cube",
        "create_cylinder",
        "create_ball",
        "create_disc",
        "create_mammae",
        "remove_interactable",
    };

    private bool _isRequestRunning;

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void SendUserMessage(string userText, string interactables)
    {
        if (_isRequestRunning)
        {
            Debug.LogWarning("[Gpt] Request already running.");
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogError("[Gpt] API key missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(userText))
        {
            Debug.LogWarning("[Gpt] Empty user input.");
            return;
        }

        var messages = BuildMessages(userText);
        StartCoroutine(SendRequest(messages));
    }

    // =========================================================
    // MESSAGE BUILDING
    // =========================================================

    private List<Message> BuildMessages(string userText)
    {
        var messages = new List<Message>();

        var systemBuilder = new StringBuilder();
        systemBuilder.Append(systemPrompt);

        if (injectSceneContext && interaction != null)
        {
            systemBuilder.Append("\n\nCurrent scene state:\n");
            systemBuilder.Append(interaction.Editor_ListInteractables_ForGPT());
        }

        messages.Add(new Message
        {
            role = "system",
            content = systemBuilder.ToString()
        });

        messages.Add(new Message
        {
            role = "user",
            content = $"사용자의 말: \"{userText}\"\n\n존재하는 오브젝트: ```{interaction.Editor_ListInteractables_ForGPT()}```"
        });

        return messages;
    }

    // =========================================================
    // REQUEST
    // =========================================================

    private IEnumerator SendRequest(List<Message> messages)
    {
        _isRequestRunning = true;

        var body = BuildRequestBody(messages);
        Debug.LogWarning("[Gpt] Request Body: " + body);
        var bodyRaw = Encoding.UTF8.GetBytes(body);

        using (var req = new UnityWebRequest(apiUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            req.timeout = timeoutSeconds;

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Gpt] Error: {req.error}\n{req.downloadHandler.text}");
                _isRequestRunning = false;
                yield break;
            }

            HandleResponse(req.downloadHandler.text);
        }

        _isRequestRunning = false;
    }

    // =========================================================
    // RESPONSE
    // =========================================================

    private void HandleResponse(string json)
    {
        var response = JsonUtility.FromJson<ChatCompletionResponse>(json);

        if (response?.choices == null || response.choices.Length == 0)
        {
            Debug.LogWarning("[Gpt] Empty response.");
            return;
        }

        var msg = response.choices[0].message;

        if (!string.IsNullOrEmpty(msg.content))
        {
            OnAssistantMessage?.Invoke(msg.content);
        }

        if (msg.tool_calls != null && msg.tool_calls.Length > 0)
        {
            OnToolCalls?.Invoke(msg.tool_calls);

            var blockSet = new HashSet<string>(blockToolNames);
            foreach (var call in msg.tool_calls)
            {
                if (call?.function != null &&
                    blockSet.Contains(call.function.name))
                {
                    OnBlockToolCall?.Invoke(call);
                }
            }
        }
    }

    // =========================================================
    // JSON
    // =========================================================

    private string BuildRequestBody(List<Message> messages)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"model\":\"").Append(model).Append("\",");
        sb.Append("\"messages\":[");

        for (int i = 0; i < messages.Count; i++)
        {
            sb.Append(JsonUtility.ToJson(messages[i]));
            if (i < messages.Count - 1) sb.Append(",");
        }

        sb.Append("]");

        if (includeTools && !string.IsNullOrWhiteSpace(toolsJson))
        {
            sb.Append($",\"tools\": {toolsJson}");
            sb.Append(",\"tool_choice\":\"auto\"");
        }

        sb.Append("}");
        return sb.ToString();
    }
}
