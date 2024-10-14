using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

namespace UNIArt.Editor
{
    public class UNIArtSettings : ScriptableObject
    {
#region  用户偏好设置
        public class Editor
        {
            const string editorPrefsPrefix = "UNIArt_";
            private const string enableHierarchyIcon = editorPrefsPrefix + "enableHierarchyIcon";
            private const string enableHierarchyCheckbox =
                editorPrefsPrefix + "enableHierarchyCheckbox";
            private const string delayRetry = editorPrefsPrefix + "uniart_delayRetry";

            public static ReactiveProperty<float> DelayRetry = new ReactiveProperty<float>(
                EditorPrefs.GetFloat(delayRetry, 0.2f)
            );

            public static ReactiveProperty<bool> EnableHierarchyIcon = new ReactiveProperty<bool>(
                EditorPrefs.GetBool(enableHierarchyIcon, true)
            );

            public static ReactiveProperty<bool> EnableHierarchyCheckbox =
                new ReactiveProperty<bool>(EditorPrefs.GetBool(enableHierarchyCheckbox, true));

            public static ReactiveProperty<bool> EnableHierarchyItemGUI =
                new ReactiveProperty<bool>(
                    EnableHierarchyCheckbox.Value || EnableHierarchyIcon.Value
                );

            static Editor()
            {
                DelayRetry.Value = EditorPrefs.GetFloat(delayRetry, 0.2f);
                EnableHierarchyIcon.Value = EditorPrefs.GetBool(enableHierarchyIcon, true);
                EnableHierarchyCheckbox.Value = EditorPrefs.GetBool(enableHierarchyCheckbox, true);

                Action refreshProperty = () =>
                {
                    EnableHierarchyItemGUI.Value =
                        EnableHierarchyCheckbox.Value || EnableHierarchyIcon.Value;
                };

                EnableHierarchyItemGUI.OnValueChanged.AddListener(
                    (value) =>
                    {
                        Utils.ForceRecompile();
                    }
                );

                DelayRetry.OnValueChanged.AddListener(
                    (value) =>
                    {
                        EditorPrefs.SetFloat(delayRetry, value);
                    }
                );

                EnableHierarchyIcon.OnValueChanged.AddListener(
                    (value) =>
                    {
                        EditorPrefs.SetBool(enableHierarchyIcon, value);
                        refreshProperty();
                    }
                );

                EnableHierarchyCheckbox.OnValueChanged.AddListener(
                    (value) =>
                    {
                        EditorPrefs.SetBool(enableHierarchyCheckbox, value);
                        refreshProperty();
                    }
                );
            }
        }
#endregion

#region UNIArt 项目设置
        public static List<string> excludeHierarchyMethods => new List<string> { "OnItemGUI" };

        public string TemplateSVNRepo = "http://svn.andcrane.com/repo/UNIArtTemplates";
        public string TemplateLocalFolder = "Assets/ArtAssets/#Templates";

        // 默认是否安装Standard模板
        public bool InstallStandardDefault = true;

        internal string ArtFolder = "Assets/ArtAssets";

        // 依赖文件排除文件夹
        public List<string> dependencyExcludeFolders = new List<string> { "Assets/TextMesh Pro", };

        // 依赖文件排除文件
        internal List<string> dependencyExcludeFiles = new List<string>
        {
            "Assets/ArtAssets/Fonts/DefaultTMPFont.asset",
        };

        [Serializable]
        public class PSDImportArgs
        {
            [HideInInspector]
            public string psdPath;

            public string PSDEntityPath => PsdFileToPrefabPath(psdPath);

            public Texture2D OriginPSFile => AssetDatabase.LoadAssetAtPath<Texture2D>(psdPath);
            public float Scale = 1.0f;
            public bool ImportOnlyVisibleLayers = false;
            public bool CreateAtlas = false;
            public int MaxAtlasSize = 4096;
            public bool AddPSLayer = true;
            public bool RestoreEntity = true;

            public PSDImportArgs ShallowCopy(string psdFilePath)
            {
                var _args = (PSDImportArgs)this.MemberwiseClone();
                _args.psdPath = psdFilePath;
                return _args;
            }
        }

        [HideInInspector]
        public List<PSDImportArgs> PSDImportOptions = new List<PSDImportArgs>();

        public PSDImportArgs PSImportDefaultOptions = new PSDImportArgs();

        public static PSDImportArgs GetPSDImportArgs(string psdPath)
        {
            if (!Project.PSDImportOptions.Exists(_args => _args.psdPath == psdPath))
            {
                Project.PSDImportOptions.Add(Project.PSImportDefaultOptions.ShallowCopy(psdPath));
            }
            return Project.PSDImportOptions.Find(x => x.psdPath == psdPath);
        }

        [Serializable]
        public class PSDEntityInstance
        {
            public string origin;
            public string instancePath;
            public GameObject instanceObject =>
                AssetDatabase.LoadAssetAtPath<GameObject>(instancePath);
            public bool IsMissing => instanceObject == null;
        }

        public List<PSDEntityInstance> PSDEntityInstances = new List<PSDEntityInstance>();

        public static List<PSDEntityInstance> GetPSDEntityInstances(string psdPath)
        {
            return Project.PSDEntityInstances.FindAll(x => x.origin == psdPath);
        }

        public static GameObject GetPSDEntityInstance(string psdPath)
        {
            return Project.PSDEntityInstances
                .Where(x => x.origin == psdPath)
                .LastOrDefault()
                ?.instanceObject;
        }

        public static void AddPSDEntityInstance(string psdPath, string instance)
        {
            var _instance = new PSDEntityInstance() { origin = psdPath, instancePath = instance };
            Project.PSDEntityInstances.Add(_instance);
            Project.PSDEntityInstances = Project.PSDEntityInstances
                .Where(x => !x.IsMissing)
                .ToList();
        }

        public static string GetExternalTemplateFolderUrl(string templateName)
        {
            return Project.TemplateSVNRepo + "/" + templateName;
        }

        public static string GetExternalTemplateFolder(string templateName)
        {
            return Project.TemplateLocalFolder + "/" + templateName;
        }

        public static bool IsTemplateAsset(string assetPath)
        {
            return assetPath.ToForwardSlash().StartsWith(Project.TemplateLocalFolder + "/");
        }

        public static bool IsProjectUIPageAsset(string assetPath)
        {
            return assetPath.ToForwardSlash().StartsWith(Project.ArtFolder + "/UI Prefabs/Windows");
        }

        public static bool IsProjectUIComponentAsset(string assetPath)
        {
            return assetPath.ToForwardSlash().StartsWith(Project.ArtFolder + "/UI Prefabs/Widgets");
        }

        public static string GetTemplateNameBySubAsset(string assetPath)
        {
            var _path = assetPath.ToForwardSlash();
            string _pattern = @"^Assets/ArtAssets/\#Templates/(?<templateName>[^/]+)(/.*)?$";
            var _match = Regex.Match(_path, _pattern);
            if (_match.Success)
            {
                return _match.Groups["templateName"].Value;
            }
            return string.Empty;
        }

        public static string GetPreviewPathByAsset(string assetPath)
        {
            var TemplateRootFolder = GetExternalTemplateRootBySubAsset(assetPath);
            var TemplatePreviewFolder = TemplateRootFolder + "/Previews";
            var _fileName = assetPath
                .ToForwardSlash()
                .Replace(TemplateRootFolder + "/", "")
                .Replace("/", "_")
                .Replace(".prefab", ".png");
            return TemplatePreviewFolder + "/" + _fileName;
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

        public static bool IsSampleTemplateAsset(string path1, string path2)
        {
            var _path1 = path1.ToForwardSlash();
            var _path2 = path2.ToForwardSlash();
            return GetTemplateNameBySubAsset(_path1) == GetTemplateNameBySubAsset(_path2);
        }

        public static string PsdFileToPrefabPath(string psdPath)
        {
            return psdPath.Replace(".psd", "#psd.prefab");
        }

        public static string PrefabPathToPsdFile(string prefabPath)
        {
            return prefabPath.Replace("#psd.prefab", ".psd");
        }

        public static bool IsPSDFile(string assetPath)
        {
            return assetPath.ToForwardSlash().EndsWith(".psd");
        }

        public static bool PsdEntityExists(string psdPath)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PsdFileToPrefabPath(psdPath)) != null;
        }

        public static bool PsdRawExists(string entityPath)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PrefabPathToPsdFile(entityPath))
                != null;
        }

        public static bool IsPsdEntity(string assetPath)
        {
            return assetPath.ToForwardSlash().EndsWith("#psd.prefab");
        }

        [Serializable]
        public class TemplateCache
        {
            public string TemplateName;
            public bool KeepTop = false;
        }

        [HideInInspector]
        public List<TemplateCache> TemplateCaches = new List<TemplateCache>();

        [HideInInspector]
        public string LastSelectedTemplateID = TmplButton.LocalTemplateTitle;

        public void SetTemplateTop(string templateName, bool isTop)
        {
            var _cache = TemplateCaches.FirstOrDefault(x => x.TemplateName == templateName);
            if (_cache == null)
            {
                _cache = new TemplateCache() { TemplateName = templateName };
                TemplateCaches.Add(_cache);
            }
            _cache.KeepTop = isTop;
        }

        public bool GetTemplateTop(string templateName)
        {
            var _cache = TemplateCaches.FirstOrDefault(x => x.TemplateName == templateName);
            if (_cache == null)
            {
                return false;
            }
            return _cache.KeepTop;
        }

        private List<SVNIntegration.ExternalProperty> externals =
            new List<SVNIntegration.ExternalProperty>();

        public void PullExternals()
        {
            externals = SVNIntegration.GetExternals(UNIArtSettings.Project.TemplateLocalFolder);
        }

        public bool HasExternal(string templateName)
        {
            return externals.Any(x => x.Dir == templateName);
        }

        private static UNIArtSettings instance;
        public static UNIArtSettings Project
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

#endregion

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

                    EditorGUI.indentLevel += 2;

                    var _defaultLabelWidth = EditorGUIUtility.labelWidth;

                    EditorGUIUtility.labelWidth = 260;
                    // 绘制分组标题
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    GUILayout.Label("Hierarchy View", EditorStyles.boldLabel);
                    GUILayout.EndHorizontal();

                    Editor.EnableHierarchyCheckbox.Value = EditorGUILayout.Toggle(
                        "Component Checkbox",
                        Editor.EnableHierarchyCheckbox.Value
                    );

                    Editor.EnableHierarchyIcon.Value = EditorGUILayout.Toggle(
                        "Component Icon",
                        Editor.EnableHierarchyIcon.Value
                    );
                    EditorGUIUtility.labelWidth = _defaultLabelWidth;
                    EditorGUI.indentLevel -= 2;
                },
                keywords = new[] { "Custom", "UNIArt" }
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
                settingsObject = new SerializedObject(UNIArtSettings.Project);
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
