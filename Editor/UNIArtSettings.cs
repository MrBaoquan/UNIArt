using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

namespace UNIArt.Editor
{
    public class UNIArtSettings : ScriptableObject
    {
        const string editorPrefsPrefix = "UNIArt_";
        private const string enableHierarchyIcon = editorPrefsPrefix + "enableHierarchyIcon";
        private const string enableHierarchyCheckbox =
            editorPrefsPrefix + "enableHierarchyCheckbox";

        private const string delayRetry = editorPrefsPrefix + "uniart_delayRetry";
        public static float DelayRetry
        {
            get => EditorPrefs.GetFloat(delayRetry, 0.2f);
            set => EditorPrefs.SetFloat(delayRetry, value);
        }

        public static bool EnableHierachyIcon
        {
            get => EditorPrefs.GetBool(enableHierarchyIcon, true);
            set => EditorPrefs.SetBool(enableHierarchyIcon, value);
        }

        public static bool EnableHierachyCheckbox
        {
            get => EditorPrefs.GetBool(enableHierarchyCheckbox, true);
            set => EditorPrefs.SetBool(enableHierarchyCheckbox, value);
        }

        public static bool EnableHierachyItemGUI => EnableHierachyIcon || EnableHierachyCheckbox;

        public static List<string> excludeHierarchyMethods => new List<string> { "OnItemGUI" };

        public string TemplateSVNRepo = "http://svn.andcrane.com/repo/UNIArtTemplates";
        public string TemplateLocalFolder = "Assets/ArtAssets/#Templates";

        public static string GetExternalTemplateFolderUrl(string templateName)
        {
            return DefaultSettings.TemplateSVNRepo + "/" + templateName;
        }

        public static string GetExternalTemplateFolder(string templateName)
        {
            return DefaultSettings.TemplateLocalFolder + "/" + templateName;
        }

        public static bool IsTemplateAsset(string assetPath)
        {
            return assetPath.ToForwardSlash().StartsWith(DefaultSettings.TemplateLocalFolder + "/");
        }

        public static string GetTemplateNameBySubAsset(string assetPath)
        {
            var _path = assetPath.ToForwardSlash();
            string _pattern = @"^Assets/ArtAssets/\#Templates/(?<templateName>[^/]+)/.*$";
            var _match = Regex.Match(_path, _pattern);
            if (_match.Success)
            {
                return _match.Groups["templateName"].Value;
            }
            return string.Empty;
        }

        // 根据资源名获取模板文件夹根目录
        public static string GetExternalTemplateRootBySubAsset(string assetPath)
        {
            var _templateName = GetTemplateNameBySubAsset(assetPath);
            if (string.IsNullOrEmpty(_templateName))
            {
                return string.Empty;
            }
            return GetExternalTemplateFolder(_templateName);
        }

        private static UNIArtSettings instance;
        public static UNIArtSettings DefaultSettings
        {
            get
            {
                if (instance != null)
                    return instance;

                var _default = AssetDatabase
                    .FindAssets("t:UNIArtSettings")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<UNIArtSettings>)
                    .FirstOrDefault();

                if (_default != null)
                {
                    instance = _default;
                    return instance;
                }

                instance = CreateInstance<UNIArtSettings>();
                AssetDatabase.CreateAsset(instance, "Assets/Resources/UNIArt Settings.asset");
                AssetDatabase.Refresh();
                return instance;
            }
        }

        // 创建一个设置提供者
        [SettingsProvider]
        public static SettingsProvider CreatePreferencesProvider()
        {
            var provider = new SettingsProvider("Preferences/UNIArt", SettingsScope.User)
            {
                label = "UNIArt",
                // 绘制偏好设置界面
                guiHandler = (searchContext) =>
                {
                    GUILayout.Space(16);
                    EditorGUI.indentLevel++;
                    var _defaultLabelWidth = EditorGUIUtility.labelWidth;
                    // EditorGUILayout.LabelField("Custom Preferences", EditorStyles.boldLabel);
                    // 设置文本标签的宽度
                    EditorGUIUtility.labelWidth = 250;
                    EnableHierachyCheckbox = EditorGUILayout.Toggle(
                        "Enable Hirarchy Checkbox",
                        EnableHierachyCheckbox,
                        // 指定宽度
                        GUILayout.Width(300)
                    );

                    EnableHierachyIcon = EditorGUILayout.Toggle(
                        "Enable Hirarchy Icon",
                        EnableHierachyIcon
                    );
                    EditorGUIUtility.labelWidth = _defaultLabelWidth;
                    EditorGUI.indentLevel--;
                },
                keywords = new[] { "Custom", "Preferences", "Feature" }
            };

            return provider;
        }
    }

    public class UNIArtSettingsProvider : SettingsProvider
    {
        private SerializedObject settingsObject;

        public UNIArtSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        public override void OnGUI(string searchContext)
        {
            if (settingsObject == null)
            {
                settingsObject = new SerializedObject(UNIArtSettings.DefaultSettings);
            }

            if (settingsObject != null)
            {
                settingsObject.Update();

                // 自动生成与所有字段相关的 UI
                SerializedProperty property = settingsObject.GetIterator();
                property.NextVisible(true); // 跳过 `m_Script` 字段

                while (property.NextVisible(false)) // 遍历所有属性
                {
                    EditorGUILayout.PropertyField(property, true);
                }
                if (settingsObject.hasModifiedProperties)
                {
                    Debug.LogWarning(
                        "UNIArtSettings has been modified, please restart the editor to take effect."
                    );
                }

                settingsObject.ApplyModifiedProperties();
            }
            else
            {
                GUILayout.Label("No AutoGeneratedSettings asset found.");
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateAutoGeneratedSettingsProvider()
        {
            return new UNIArtSettingsProvider("Project/UNIArt", SettingsScope.Project)
            {
                keywords = new[] { "auto", "settings", "generated" }
            };
        }
    }
}
