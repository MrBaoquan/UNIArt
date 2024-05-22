using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Kamgam.UIPreview
{
    public enum LogLevel
    {
        Log = 0,
        Warning = 1,
        Error = 2,
        NoLogs = 99
    }

    public class UIPreviewSettings : ScriptableObject
    {
        public const string Version = "1.2.0";
        public const string SettingsFilePath = "Assets/UIPreviewSettings.asset";

        [SerializeField, Tooltip(_LogLevelTooltip)]
        public LogLevel LogLevel;
        public const string _LogLevelTooltip = "Log levels to determine how many log messages will be shown (Log = all message, Error = only critical errors).";

        [Tooltip(_IgnoreErrorsTooltip)]
        public bool IgnoreErrors = false;
        public const string _IgnoreErrorsTooltip = "Should exceptions while rendering the preview be shown in the console or just ignored? Enable if you see error logs by some third party asset during preview.";

        [Tooltip(_DisableCustomScriptsInPreview)]
        public bool DisableCustomScriptsInPreview = true;
        public const string _DisableCustomScriptsInPreview = "Disable custom scripts in while rendering the preview. Helps to avoid side effects of [ExecuteAlways] scripts.";

        [Tooltip(_DisableCustomScriptsExclusions)]
        public string[] DisableCustomScriptsExclusions = new string[] {};
        public const string _DisableCustomScriptsExclusions = "If 'DisableCustomScripts' in ON then you can still exclude some namespaces from being excluded. Any component in any of these namespaces will not be excluded. Any component having this exact name will not be excluded.";

        [Tooltip(_ExecuteInPlayModeTooltip)]
        public bool ExecuteInPlayMode = false;
        public const string _ExecuteInPlayModeTooltip = "Should the preview be shown during play mode too?\nThe preview has to load and render the asset. Having two versions of an asset loaded can lead to lower performance or side effects. That's why it is disabled by default.";

        [Tooltip(_PreviewTextureResolution)]
        public int PreviewTextureResolution = 256;
        public const string _PreviewTextureResolution = "Side length of the square preview render texture.";

        [Header("Canvas Scaler")]
        [Tooltip(_UseCanvasScalerTooltip)]
        public bool UseCanvasScaler = true;
        public const string _UseCanvasScalerTooltip = "Should a canvas scaler be used for the rendering of the UI?";

        public CanvasScaler.ScaleMode UIScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        public Vector2 ReferenceResolution = new Vector2(800, 600);

        public CanvasScaler.ScreenMatchMode ScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        [Range(0f, 1f)]
        public float MatchWidthOrHeight = 0.5f;

        public int ReferencePixelsPerUnit = 100;

        [Range(0f, 20f)]
        public float ScaleFactor = 1f;

        public CanvasScaler.Unit PhysicalUnit = CanvasScaler.Unit.Points;
        public int FallbackScreenDPI = 96;
        public int DefaultSpriteDPI = 96;

        protected static UIPreviewSettings cachedSettings;

        public static UIPreviewSettings GetOrCreateSettings()
        {
            if (cachedSettings == null)
            {
                cachedSettings = AssetDatabase.LoadAssetAtPath<UIPreviewSettings>(SettingsFilePath);
                if (cachedSettings == null)
                {
                    cachedSettings = ScriptableObject.CreateInstance<UIPreviewSettings>();

                    cachedSettings.LogLevel = LogLevel.Warning;
                    cachedSettings.ExecuteInPlayMode = false;
                    cachedSettings.IgnoreErrors = false;

                    AssetDatabase.CreateAsset(cachedSettings, SettingsFilePath);
                    AssetDatabase.SaveAssets();
                }
            }
            return cachedSettings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        // settings
        public static void SelectSettings()
        {
            var settings = UIPreviewSettings.GetOrCreateSettings();
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "UIPreviewSettings settings could not be found or created.", "Ok");
            }
        }

        [MenuItem("Tools/UI Preview/Open Settings", priority = 200)]
        public static void OpenSettings()
        {
            var settings = GetOrCreateSettings();
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Settings could not be found or created.", "Ok");
            }
        }

        [MenuItem("Tools/UI Preview/Open Manual", priority = 201)]
        public static void OpenManual()
        {
            EditorUtility.OpenWithDefaultApp("Assets/UIPreview/UIPreviewManual.pdf");
        }

        [MenuItem("Tools/UI Preview/Please leave a review :-)", priority = 500)]
        public static void Review()
        {
            Application.OpenURL("https://assetstore.unity.com/packages/slug/226906?aid=1100lqC54&pubref=asset");
        }

        [MenuItem("Tools/UI Preview/More Asset by KAMGAM", priority = 501)]
        public static void MoreAssets()
        {
            Application.OpenURL("https://assetstore.unity.com/publishers/37829?aid=1100lqC54&pubref=asset");
        }

        [MenuItem("Tools/UI Preview/UI Preview Version:  " + UIPreviewSettings.Version, priority = 512)]
        public static void VersionLog()
        {
            Debug.Log("UI Preview Version: " + Version);
        }
    }

    [CustomEditor(typeof(UIPreviewSettings))]
    public class UIPreviewSettingsEditor : Editor
    {
        UIPreviewSettings settings;

        public void Awake()
        {
            settings = target as UIPreviewSettings;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty serializedProperty = serializedObject.GetIterator();
            serializedProperty.NextVisible(enterChildren: true); // skip script
            while (serializedProperty.NextVisible(enterChildren: false))
            {
                EditorGUILayout.PropertyField(serializedProperty);
            }

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();

                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.Repaint();
                }
            }
        }
    }

    static class PivotCursorToolSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreatePivotCursorToolSettingsProvider()
        {
            var provider = new SettingsProvider("Project/UI Preview", SettingsScope.Project)
            {
                label = "UI Preview",
                guiHandler = (searchContext) =>
                {
                    var serializedSettings = UIPreviewSettings.GetSerializedSettings();
                    var settings = serializedSettings.targetObject as UIPreviewSettings;

                    beginHorizontalIndent(10);

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Version: " + UIPreviewSettings.Version + "  (" + UIPreviewSettings.SettingsFilePath + ")");
                    if (drawButton(" Manual ", icon: "_Help", options: GUILayout.Width(80)))
                    {
                        UIPreviewSettings.OpenManual();
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(5);
                    drawField(serializedSettings, "LogLevel", "Log level:", UIPreviewSettings._LogLevelTooltip);
                    drawField(serializedSettings, "IgnoreErrors", "Ignore Errors:", UIPreviewSettings._IgnoreErrorsTooltip);
                    drawField(serializedSettings, "DisableCustomScriptsInPreview", "Disable Custom Scripts:", UIPreviewSettings._DisableCustomScriptsInPreview);
                    drawField(serializedSettings, "DisableCustomScriptsExclusions", "Disable Custom Scripts Exclusions:", UIPreviewSettings._DisableCustomScriptsExclusions);
                    drawField(serializedSettings, "ExecuteInPlayMode", "Exec In Play Mode:", UIPreviewSettings._ExecuteInPlayModeTooltip);
                    drawField(serializedSettings, "PreviewTextureResolution", "Preview Texture Resolution:", UIPreviewSettings._PreviewTextureResolution);
                    drawField(serializedSettings, "UseCanvasScaler", "Use Canvas Scaler:", UIPreviewSettings._UseCanvasScalerTooltip);

                    if (settings.UseCanvasScaler)
                    {
                        drawField(serializedSettings, "UIScaleMode", "UI Scale Mode");

                        if (settings.UIScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
                        {
                            drawField(serializedSettings, "ScaleFactor", "Scale Factor");
                            drawField(serializedSettings, "ReferencePixelsPerUnit", "Reference Pixels Per Unit");
                        }
                        else if (settings.UIScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                        {
                            drawField(serializedSettings, "ReferenceResolution", "Reference Resolution");
                            drawField(serializedSettings, "ScreenMatchMode", "Screen Match Mode");
                            drawField(serializedSettings, "MatchWidthOrHeight", "Match Width Or Height");
                            drawField(serializedSettings, "ReferencePixelsPerUnit", "Reference Pixels Per Unit");
                        }
                        else if (settings.UIScaleMode == CanvasScaler.ScaleMode.ConstantPhysicalSize)
                        {
                            drawField(serializedSettings, "PhysicalUnit", "Physical Unit");
                            drawField(serializedSettings, "FallbackScreenDPI", "Fallback Screen DPI");
                            drawField(serializedSettings, "DefaultSpriteDPI", "Default Sprite DPI");
                            drawField(serializedSettings, "ReferencePixelsPerUnit", "Reference Pixels Per Unit");
                        }
                    }
                    GUILayout.Space(5);

                    endHorizontalIndent();

                    serializedSettings.ApplyModifiedProperties();
                },

                // Populate the search keywords to enable smart search filtering and label highlighting.
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "ui", "preview", "canvas", "editor", "inspector", "ui preview" })
            };

            return provider;
        }

        static bool drawButton(string text, string tooltip = null, string icon = null, params GUILayoutOption[] options)
        {
            GUIContent content;

            // icon
            if (!string.IsNullOrEmpty(icon))
                content = EditorGUIUtility.IconContent(icon);
            else
                content = new GUIContent();

            // text
            content.text = text;

            // tooltip
            if (!string.IsNullOrEmpty(tooltip))
                content.tooltip = tooltip;

            return GUILayout.Button(content, options);
        }

        static void drawField(SerializedObject settings, string fieldPropertyName, string label, string tooltip = null)
        {
            EditorGUILayout.PropertyField(settings.FindProperty(fieldPropertyName), new GUIContent(label));
            if (!string.IsNullOrEmpty(tooltip))
            {
                var style = new GUIStyle(GUI.skin.label);
                style.wordWrap = true;
                var col = style.normal.textColor;
                col.a = 0.5f;
                style.normal.textColor = col;

                beginHorizontalIndent(10);
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(tooltip, style);
                GUILayout.EndVertical();
                endHorizontalIndent();
            }
            GUILayout.Space(5);
        }
        static void beginHorizontalIndent(int indentAmount = 10, bool beginVerticalInside = true)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(indentAmount);
            if (beginVerticalInside)
                GUILayout.BeginVertical();
        }

        static void endHorizontalIndent(float indentAmount = 10, bool begunVerticalInside = true, bool bothSides = false)
        {
            if (begunVerticalInside)
                GUILayout.EndVertical();
            if (bothSides)
                GUILayout.Space(indentAmount);
            GUILayout.EndHorizontal();
        }
    }
}