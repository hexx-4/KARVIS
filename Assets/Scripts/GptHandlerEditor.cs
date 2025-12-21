#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Meta.XR.BuildingBlocks.AIBlocks
{
    [CustomEditor(typeof(GptHandler))]
    public class GptHandlerEditor : UnityEditor.Editor
    {
        private SerializedProperty _userInput,
            _selectedPrompt,
            _includeImage,
            _imageSource,
            _promptImage,
            _promptImageUrl,
            _useEditorFakeCameraPreview;

        private void OnEnable()
        {
            _userInput = serializedObject.FindProperty("userInput");
            _selectedPrompt = serializedObject.FindProperty("selectedPrompt");
            _includeImage = serializedObject.FindProperty("includeImage");
            _imageSource = serializedObject.FindProperty("imageSource");
            _promptImage = serializedObject.FindProperty("promptImage");
            _promptImageUrl = serializedObject.FindProperty("promptImageUrl");
            _useEditorFakeCameraPreview = serializedObject.FindProperty("useEditorFakeCameraPreview");
        }

        public override void OnInspectorGUI()
        {
            var helper = (GptHandler)target;
            serializedObject.Update();

            EditorGUILayout.PropertyField(_userInput, new GUIContent("User Input"));
            EditorGUILayout.PropertyField(_selectedPrompt, new GUIContent("Default Prompt"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_includeImage, new GUIContent("Include Image"));

            if (_includeImage.boolValue)
            {
                EditorGUILayout.PropertyField(_imageSource, new GUIContent("Image Source"));
                var src = (PromptImageSource)_imageSource.enumValueIndex;
                switch (src)
                {
                    case PromptImageSource.InspectorTexture:
                        EditorGUILayout.PropertyField(_promptImage, new GUIContent("Prompt Image"));
                        break;

                    case PromptImageSource.ImageUrl:
                        EditorGUILayout.PropertyField(_promptImageUrl, new GUIContent("Prompt Image URL"));
                        break;

                    case PromptImageSource.Camera:
                        if (_useEditorFakeCameraPreview != null)
                        {
                            EditorGUILayout.PropertyField(_useEditorFakeCameraPreview, new GUIContent("Use Editor Fake Camera"));
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Send Prompt"))
                {
                    helper.SendPrompt();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
