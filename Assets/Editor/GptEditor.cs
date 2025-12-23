#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class GptEditor : EditorWindow
{
    public Gpt gpt;
    public Interaction interaction;

    private string gptInput = "";

    private string selectedName = "";
    private string colorInput = "red";
    private Vector3 positionInput;
    private Vector3 velocityInput;

    [MenuItem("Tools/GPT + Interaction Controller")]
    public static void Open()
    {
        GetWindow<GptEditor>("GPT Interaction");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            gpt = FindFirstObjectByType<Gpt>();
            interaction = FindFirstObjectByType<Interaction>();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("GPT + Interaction Controller", EditorStyles.boldLabel);

        // ===== References =====
        GUILayout.Space(5);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Mode에서만 동작합니다.", MessageType.Warning);
            return;
        }

        if (interaction == null)
        {
            EditorGUILayout.HelpBox("Interaction.cs 참조 필요", MessageType.Error);
            return;
        }

        GUILayout.Space(10);

        // ===== GPT 테스트 =====
        GUILayout.Label("GPT Test (ToolCall 경유)", EditorStyles.boldLabel);
        gptInput = EditorGUILayout.TextArea(gptInput, GUILayout.Height(50));

        using (new EditorGUI.DisabledScope(gpt == null || string.IsNullOrWhiteSpace(gptInput)))
        {
            if (GUILayout.Button("Send To GPT"))
            {
                gpt.SendUserMessage(gptInput, interaction.Editor_ListInteractables_ForGPT());
                gptInput = "";
            }
        }

        GUILayout.Space(10);

        // ===== Interaction 직접 제어 =====
        GUILayout.Label("Interaction Direct Control", EditorStyles.boldLabel);

        selectedName = EditorGUILayout.TextField("Interactable Name", selectedName);

        GUILayout.Space(5);

        // Recolor
        EditorGUILayout.BeginHorizontal();
        colorInput = EditorGUILayout.TextField("Color", colorInput);
        if (GUILayout.Button("Recolor", GUILayout.Width(80)))
        {
            interaction.Editor_Recolor(selectedName, colorInput);
        }
        EditorGUILayout.EndHorizontal();

        // Position
        positionInput = EditorGUILayout.Vector3Field("Position", positionInput);
        if (GUILayout.Button("Set Position"))
        {
            interaction.Editor_SetPosition(selectedName, positionInput);
        }

        // Velocity
        velocityInput = EditorGUILayout.Vector3Field("Velocity", velocityInput);
        if (GUILayout.Button("Set Velocity"))
        {
            interaction.Editor_SetVelocity(selectedName, velocityInput);
        }

        GUILayout.Space(5);

        if (GUILayout.Button("List Interactables"))
        {
            interaction.Editor_ListInteractables();
        }

        if (GUILayout.Button("Get Grabbed Interactables"))
        {
            interaction.Editor_GetGrabbed();
        }
    }
}
#endif