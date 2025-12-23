using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Text;

/// <summary>
/// Bridges Gpt tool-calls to predefined scene blocks.
/// - Only the whitelisted interactables are exposed to ChatGPT/LLM.
/// - Provides lightweight state summaries the model can consume.
/// - Applies actions from tool calls (e.g., recolor).
/// </summary>
public class Interaction : MonoBehaviour
{
    public Recorder recorder;
    public string responseText;

    [Serializable]
    public class Interactable
    {
        public string name;
        public GameObject target;
    }

    [Serializable]
    public class InteractableTemplate
    {
        [Tooltip("Key used by the tool name (e.g., cube, cylinder, ball).")]
        public string templateId;

        [Tooltip("Optional friendly base-name used when auto-naming new instances.")]
        public string defaultName = "object";

        [Tooltip("Prefab that already contains a collider, Rigidbody, and grab component.")]
        public GameObject prefab;

        [Tooltip("Default position used when the tool does not provide one.")]
        public Vector3 defaultSpawnPosition = new Vector3(0f, 1f, 0f);

        [Tooltip("Default scale applied to the spawned object if the prefab scale is 1.")]
        public Vector3 defaultScale = Vector3.one;

        [Tooltip("Default initial velocity (applied to the object's Rigidbody).")]
        public Vector3 defaultVelocity = Vector3.zero;
    }

    [Header("References")]
    [Tooltip("The GPT chat client that raises block tool calls.")]
    public Gpt gpt;

    [Header("Interactables")]
    [Tooltip("Only these objects will be visible/controllable to the LLM tools.")]
    public List<Interactable> interactables = new List<Interactable>();

    [Header("Templates")]
    [Tooltip("Prefab templates that can be spawned at runtime via GPT tools.")]
    public List<InteractableTemplate> templates = new List<InteractableTemplate>();

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
    private class PositionArgs
    {
        public string name;
        public Vector3 position;
    }

    [Serializable]
    private class RotationArgs
    {
        public string name;
        public Quaternion rotation;
    }

    [Serializable]
    private class ScaleArgs
    {
        public string name;
        public Vector3 scale;
    }

    [Serializable]
    private class VelocityArgs
    {
        public string name;
        public Vector3 velocity;
    }

    [Serializable]
    private class StateArgs
    {
        public string name;
    }

    [Serializable]
    private class GrabbedArgs
    {
        public bool onlyGrabbed = true;
    }

    [Serializable]
    private class CreateArgs
    {
        public string name;
        public SerializableVector3 position;
        public SerializableQuaternion rotation;
        public SerializableVector3 scale;
        public SerializableVector3 velocity;
        public string color;
    }

    [Serializable]
    private class SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    private class SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    private readonly Dictionary<string, Interactable> _interactableLookup = new Dictionary<string, Interactable>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InteractableTemplate> _templateLookup = new Dictionary<string, InteractableTemplate>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        RebuildInteractableLookup();
        EnsureTemplateLookup();

        if (gpt != null)
        {
            gpt.OnBlockToolCall.AddListener(HandleToolCall);
        }
        else
        {
            Debug.LogWarning("[KARVIS][Interaction] GPT 참조가 설정되지 않았습니다; 도구 호출이 무시됩니다.");
        }
    }

    private void OnDestroy()
    {
        if (gpt != null)
        {
            gpt.OnBlockToolCall.RemoveListener(HandleToolCall);
        }
    }

    private void RebuildInteractableLookup()
    {
        _interactableLookup.Clear();
        foreach (var interactable in interactables)
        {
            if (interactable == null || interactable.target == null || string.IsNullOrWhiteSpace(interactable.name))
            {
                continue;
            }

            if (!_interactableLookup.ContainsKey(interactable.name))
            {
                _interactableLookup.Add(interactable.name, interactable);
            }
        }
    }

    private void EnsureTemplateLookup()
    {
        EnsureDefaultTemplates();
        _templateLookup.Clear();
        foreach (var template in templates)
        {
            if (template == null || string.IsNullOrWhiteSpace(template.templateId))
            {
                continue;
            }

            var key = template.templateId.Trim();
            if (!_templateLookup.ContainsKey(key))
            {
                if (template.prefab == null)
                {
                    var resourcePath = $"InteractableTemplates/{key}";
                    var loaded = Resources.Load<GameObject>(resourcePath);
                    if (loaded != null)
                    {
                        template.prefab = loaded;
                    }
                }

                _templateLookup.Add(key, template);
            }
        }
    }

    private void EnsureDefaultTemplates()
    {
        MaybeAddTemplate("cube", "큐브", new Vector3(0f, 0f, 0f), Vector3.one);
        MaybeAddTemplate("cylinder", "원기둥", new Vector3(0f, 0f, 0f), Vector3.one);
        MaybeAddTemplate("ball", "공", new Vector3(0f, 0f, 0f), Vector3.one);
        MaybeAddTemplate("disc", "디스크", new Vector3(0f, 0f, 0f), new Vector3(1f, 0.1f, 1f));
        MaybeAddTemplate("mammae", "맴매", new Vector3(0f, 0f, 0f), new Vector3(0.15f, 4f, 0.15f));
    }

    private void MaybeAddTemplate(string id, string defaultName, Vector3 defaultVelocity, Vector3 defaultScale)
    {
        if (templates.Exists(t => t != null && string.Equals(t.templateId, id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var prefab = Resources.Load<GameObject>($"InteractableTemplates/{id}");
        templates.Add(new InteractableTemplate
        {
            templateId = id,
            defaultName = defaultName,
            prefab = prefab,
            defaultScale = defaultScale,
            defaultVelocity = defaultVelocity
        });
    }

    private void HandleToolCall(Gpt.ToolCall call)
    {
        if (call == null || call.function == null)
        {
            return;
        }

        switch (call.function.name)
        {
            case "get_interactable_state":
                HandleGetState(call.function.arguments);
                break;

            case "recolor_object":
                HandleRecolor(call.function.arguments);
                break;

            case "set_position":
                HandleSetPosition(call.function.arguments);
                break;
            case "set_rotation":
                HandleSetRotation(call.function.arguments);
                break;
            case "set_scale":
                HandleSetScale(call.function.arguments);
                break;
            case "set_velocity":
                HandleSetVelocity(call.function.arguments);
                break;

            case "get_grabbed_interactables":
                HandleGetGrabbed(call.function.arguments);
                break;

            case "create_cube":
                HandleCreate(call.function.arguments, "cube");
                break;

            case "create_cylinder":
                HandleCreate(call.function.arguments, "cylinder");
                break;

            case "create_ball":
                HandleCreate(call.function.arguments, "ball");
                break;

            case "create_disc":
                HandleCreate(call.function.arguments, "disc");
                break;

            case "create_mammae":
                HandleCreate(call.function.arguments, "mammae");
                break;

            default:
                SendFeedback($"'{call.function.name}'는 지원되지 않는 기능입니다.");
                break;
        }
    }

    private void HandleGetState(string argsJson)
    {
        var args = JsonUtility.FromJson<StateArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name))
        {
            SendFeedback("이름이 누락되어 명령을 처리할 수 없습니다.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"'{args.name}'을 찾을 수 없거나 화이트리스트에 없습니다.");
            return;
        }

        SendFeedback(BuildSingleSummary(interactable));
    }

    private void HandleRecolor(string argsJson)
    {
        var args = JsonUtility.FromJson<RecolorArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name) || string.IsNullOrWhiteSpace(args.color))
        {
            SendFeedback("이름 또는 색상이 누락되어 명령을 처리할 수 없습니다.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"'{args.name}'을 찾을 수 없거나 화이트리스트에 없습니다.");
            return;
        }

        var renderers = interactable.target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            SendFeedback($"'{args.name}'에 렌더러가 없습니다.");
            return;
        }

        if (!TryParseColor(args.color, out var parsed))
        {
            SendFeedback($"색상 '{args.color}'을 구문 분석할 수 없습니다. 이름이나 헥스(#RRGGBB)를 사용하세요.");
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

        SendFeedback($"'{args.name}'이 {ColorUtility.ToHtmlStringRGB(parsed)}으로 설정되었습니다.");
    }

    private void HandleSetPosition(string argsJson)
    {
        var args = JsonUtility.FromJson<PositionArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name))
        {
            SendFeedback("이름 또는 위치가 누락되어 명령을 처리할 수 없습니다.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"'{args.name}'을 찾을 수 없거나 화이트리스트에 없습니다.");
            return;
        }

        interactable.target.transform.position = args.position;
        SendFeedback($"'{args.name}'이 ({args.position.x:F2},{args.position.y:F2},{args.position.z:F2})로 이동되었습니다.");
    }

    private void HandleSetRotation(string argsJson)
    {
        var args = JsonUtility.FromJson<RotationArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name))
        {
            SendFeedback("이름 또는 회전이 누락되어 명령을 처리할 수 없습니다.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"'{args.name}'을 찾을 수 없거나 화이트리스트에 없습니다.");
            return;
        }

        interactable.target.transform.rotation = args.rotation;
        SendFeedback($"'{args.name}'이 ({args.rotation.x:F4},{args.rotation.y:F4},{args.rotation.z:F4},{args.rotation.w:F4})로 회전되었습니다.");
    }

    private void HandleSetScale(string argsJson)
    {
        var args = JsonUtility.FromJson<ScaleArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name))
        {
            SendFeedback(" 이름 또는 크기가 누락되어 명령을 처리할 수 없습니다.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"'{args.name}'을 찾을 수 없거나 화이트리스트에 없습니다.");
            return;
        }

        interactable.target.transform.localScale = args.scale;
        SendFeedback($"'{args.name}'이 ({args.scale.x:F2},{args.scale.y:F2},{args.scale.z:F2})로 크기가 조정되었습니다.");
    }

    private void HandleSetVelocity(string argsJson)
    {
        var args = JsonUtility.FromJson<VelocityArgs>(argsJson);
        if (args == null || string.IsNullOrWhiteSpace(args.name))
        {
            SendFeedback("이름 또는 속도가 누락되어 명령을 처리할 수 없습니다.");
            return;
        }

        var interactable = FindInteractable(args.name);
        if (interactable == null)
        {
            SendFeedback($"'{args.name}'을 찾을 수 없거나 화이트리스트에 없습니다.");
            return;
        }

        var rb = interactable.target.GetComponent<Rigidbody>();
        if (rb == null)
        {
            SendFeedback($"'{args.name}'에 Rigidbody가 없습니다.");
            return;
        }

        rb.linearVelocity = args.velocity;
        SendFeedback($"'{args.name}'의 속도가 ({args.velocity.x:F2},{args.velocity.y:F2},{args.velocity.z:F2})로 설정되었습니다.");
    }

    private void HandleGetGrabbed(string argsJson)
    {
        // If args parsing fails, default to only returning grabbed ones.
        var args = JsonUtility.FromJson<GrabbedArgs>(string.IsNullOrEmpty(argsJson) ? "{}" : argsJson);
        bool filterGrabbed = args == null || args.onlyGrabbed;

        var sb = new StringBuilder();
        sb.Append("{\"interactables\":[");

        bool first = true;
        foreach (var interactable in interactables)
        {
            if (interactable == null || interactable.target == null)
            {
                continue;
            }

            if (filterGrabbed && !IsGrabbed(interactable.target))
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
        SendFeedback(sb.ToString());
    }

    private void HandleCreate(string argsJson, string templateId)
    {
        var args = JsonUtility.FromJson<CreateArgs>(string.IsNullOrEmpty(argsJson) ? "{}" : argsJson);
        if (!_templateLookup.TryGetValue(templateId, out var template) || template.prefab == null)
        {
            SendFeedback($"'{templateId}' 템플릿 프리팹을 찾을 수 없습니다.");
            return;
        }

        var instance = Instantiate(template.prefab);
        var desiredName = string.IsNullOrWhiteSpace(args?.name) ? template.defaultName : args.name.Trim();
        var finalName = GenerateUniqueName(string.IsNullOrWhiteSpace(desiredName) ? templateId : desiredName);
        instance.name = finalName;

        ApplyCreateArgs(instance, template, args);
        EnsurePhysicsAndGrab(instance);

        var interactable = new Interactable { name = finalName, target = instance };
        interactables.Add(interactable);
        _interactableLookup[finalName] = interactable;

        SendFeedback($"'{finalName}'이(가) 생성되었습니다.");
    }

    private void ApplyCreateArgs(GameObject instance, InteractableTemplate template, CreateArgs args)
    {
        var t = instance.transform;
        t.position = template.defaultSpawnPosition;
        t.rotation = Quaternion.identity;
        t.localScale = template.defaultScale == Vector3.zero ? Vector3.one : template.defaultScale;

        Debug.Log(args.scale.ToVector3());
        if (args != null)
        {
            if (args.position.ToVector3() != Vector3.zero)
            {
                t.position = args.position.ToVector3();
            }

            if (args.rotation.ToQuaternion() != Quaternion.identity)
            {
                t.rotation = args.rotation.ToQuaternion();
            }

            if (args.scale.ToVector3() != Vector3.zero)
            {
                t.localScale = args.scale.ToVector3();
            }
        }

        var renderer = instance.GetComponentInChildren<Renderer>();
        if (renderer != null && !string.IsNullOrWhiteSpace(args?.color) && TryParseColor(args.color, out var parsedColor))
        {
            var mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i].color = parsedColor;
            }
        }

        var rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = template.defaultVelocity;
            if (args?.velocity != null)
            {
                rb.linearVelocity = args.velocity.ToVector3();
            }
        }
    }

    private void EnsurePhysicsAndGrab(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.useGravity = true;
        }

        if (!HasGrabComponent(go))
        {
            TryAddGrabComponent(go);
        }
    }

    private bool HasGrabComponent(GameObject go)
    {
        return
            GetComponentByName(go, "Meta.XR.Interaction.HandGrab.HandGrabInteractable") != null ||
            GetComponentByName(go, "Oculus.Interaction.HandGrab.HandGrabInteractable") != null ||
            GetComponentByName(go, "OVRGrabbable") != null ||
            GetComponentByName(go, "Meta.XR.BuildingBlocks.Grabbable") != null;
    }

    private void TryAddGrabComponent(GameObject go)
    {
        var candidateTypes = new[]
        {
            "Meta.XR.BuildingBlocks.Grabbable, Meta.XR.BuildingBlocks",
            "Meta.XR.Interaction.HandGrab.HandGrabInteractable, Meta.XR.Interaction",
            "Oculus.Interaction.HandGrab.HandGrabInteractable, Oculus.Interaction",
            "OVRGrabbable"
        };

        foreach (var typeName in candidateTypes)
        {
            var type = ResolveType(typeName);
            if (type == null)
            {
                continue;
            }

            if (go.GetComponent(type) == null)
            {
                go.AddComponent(type);
                return;
            }
        }

        Debug.LogWarning("[KARVIS][Interaction] 그랩 가능한 컴포넌트를 찾을 수 없습니다. MetaXR/Oculus 패키지가 설치되어 있는지 확인하세요.");
    }

    private Interactable FindInteractable(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        _interactableLookup.TryGetValue(name, out var interactable);
        return interactable;
    }

    private string GenerateUniqueName(string desiredName)
    {
        var baseName = string.IsNullOrWhiteSpace(desiredName) ? "object" : desiredName.Trim();
        var candidate = baseName;
        var counter = 1;

        while (_interactableLookup.ContainsKey(candidate))
        {
            candidate = $"{baseName}_{counter}";
            counter++;
        }

        return candidate;
    }

    private string BuildSceneSummary()
    {
        var sb = new StringBuilder();
        sb.Append("[");

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

        sb.Append("]");
        return sb.ToString();
    }

    private string BuildSingleSummary(Interactable interactable)
    {
        var t = interactable.target.transform;
        var renderer = interactable.target.GetComponentInChildren<Renderer>();
        string colorHex = "unknown";
        if (renderer != null && renderer.sharedMaterial != null)
        {
            colorHex = ColorUtility.ToHtmlStringRGB(renderer.sharedMaterial.color);
        }
        var rb = interactable.target.GetComponent<Rigidbody>();
        var velocity = rb != null ? rb.linearVelocity : Vector3.zero;

        bool grabbed = IsGrabbed(interactable.target);

        return
            $"{{" +
            $"\"type\":\"available_objects\"," +
            $"\"name\":\"{interactable.name}\"," +

            $"\"position\":{{" +
                $"\"x\":{t.position.x:F2}," +
                $"\"y\":{t.position.y:F2}," +
                $"\"z\":{t.position.z:F2}" +
            $"}}," +

            $"\"rotation\":{{" +
                $"\"x\":{t.rotation.x:F4}," +
                $"\"y\":{t.rotation.y:F4}," +
                $"\"z\":{t.rotation.z:F4}," +
                $"\"w\":{t.rotation.w:F4}" +
            $"}}," +

            $"\"scale\":{{" +
                $"\"x\":{t.localScale.x:F2}," +
                $"\"y\":{t.localScale.y:F2}," +
                $"\"z\":{t.localScale.z:F2}" +
            $"}}," +

            $"\"velocity\":{{" +
                $"\"x\":{velocity.x:F2}," +
                $"\"y\":{velocity.y:F2}," +
                $"\"z\":{velocity.z:F2}" +
            $"}}," +

            $"\"color\":\"#{colorHex}\"," +
            $"\"grabbed\":{grabbed.ToString().ToLowerInvariant()}" +
            $"}}";

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

    private static bool IsGrabbed(GameObject go)
    {
        // Prefer MetaXR Building Blocks style hand grab interactable (if present).
        var metaHandGrab = GetComponentByName(go, "Meta.XR.Interaction.HandGrab.HandGrabInteractable");
        if (metaHandGrab != null && HasSelectingPoints(metaHandGrab))
        {
            return true;
        }

        // Oculus Interaction hand grab (common in MetaXR packages).
        var oculusHandGrab = GetComponentByName(go, "Oculus.Interaction.HandGrab.HandGrabInteractable");
        if (oculusHandGrab != null && HasSelectingPoints(oculusHandGrab))
        {
            return true;
        }

        // OVRGrabbable fallback.
        var ovr = GetComponentByName(go, "OVRGrabbable");
        if (ovr != null && HasBoolProperty(ovr, "isGrabbed"))
        {
            return true;
        }

        return false;
    }

    private static Component GetComponentByName(GameObject go, string typeName)
    {
        var type = ResolveType(typeName);
        return type == null ? null : go.GetComponent(type);
    }

    private static bool HasSelectingPoints(Component comp)
    {
        var type = comp.GetType();
        var prop = type.GetProperty("SelectingPointsCount");
        if (prop != null)
        {
            try
            {
                var value = prop.GetValue(comp, null);
                if (value is int i && i > 0) return true;
            }
            catch (Exception)
            {
                // ignore
            }
        }

        return HasBoolProperty(comp, "isGrabbed");
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        if (_typeCache.TryGetValue(typeName, out var cached))
        {
            return cached;
        }

        var type = Type.GetType(typeName);
        if (type != null)
        {
            _typeCache[typeName] = type;
            return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
            {
                _typeCache[typeName] = type;
                return type;
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var candidate in assembly.GetTypes())
            {
                if (string.Equals(candidate.Name, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    _typeCache[typeName] = candidate;
                    return candidate;
                }
            }
        }

        _typeCache[typeName] = null;
        return null;
    }

    private static bool HasBoolProperty(Component comp, string propertyName)
    {
        var type = comp.GetType();
        var prop = type.GetProperty(propertyName);
        if (prop != null)
        {
            try
            {
                var value = prop.GetValue(comp, null);
                if (value is bool b && b) return true;
            }
            catch (Exception)
            {
                // ignore
            }
        }

        return false;
    }

    private void SendFeedback(string text)
    {
        OnToolActionFeedback?.Invoke(text);
        Debug.Log($"[KARVIS][Interaction] {text}");
        recorder.OnGptResponseReceived("명령 수행이 완료되었습니다.");
    }

    // ===== Editor / Debug API =====

    public void Editor_ListInteractables()
    {
        SendFeedback(BuildSceneSummary());
    }

    public void Editor_Recolor(string name, string color)
    {
        HandleRecolor(JsonUtility.ToJson(new RecolorArgs
        {
            name = name,
            color = color
        }));
    }

    public void Editor_SetPosition(string name, Vector3 position)
    {
        HandleSetPosition(JsonUtility.ToJson(new PositionArgs
        {
            name = name,
            position = position
        }));
    }

    public void Editor_SetRotation(string name, Quaternion rotation)
    {
        HandleSetRotation(JsonUtility.ToJson(new RotationArgs
        {
            name = name,
            rotation = rotation
        }));
    }

    public void Editor_SetScale(string name, Vector3 scale)
    {
        HandleSetScale(JsonUtility.ToJson(new ScaleArgs
        {
            name = name,
            scale = scale
        }));
    }

    public void Editor_SetVelocity(string name, Vector3 velocity)
    {
        HandleSetVelocity(JsonUtility.ToJson(new VelocityArgs
        {
            name = name,
            velocity = velocity
        }));
    }

    public void Editor_GetGrabbed()
    {
        HandleGetGrabbed("{}");
    }

    public string Editor_ListInteractables_ForGPT()
    {
        return BuildSceneSummary();
    }

}
