using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.SceneManagement;
using TMPro;
using TMPro.EditorUtilities;
using System;

namespace UNIArt.Editor
{
    internal class WorkflowUtility
    {
        const string UIPagePrefabRoot = "Assets/ArtAssets/UI Prefabs";
        static string UIPageFolder => $"{UIPagePrefabRoot}/Windows";
        static string UIComponentFolder => $"{UIPagePrefabRoot}/Widgets";

        public class DOCreateUIPage : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var _newTempGO = new GameObject(Path.GetFileNameWithoutExtension(pathName));
                var _rectTrans = _newTempGO.AddComponent<RectTransform>();

                _rectTrans.anchorMin = Vector2.zero;
                _rectTrans.anchorMax = Vector2.one;
                _rectTrans.offsetMin = Vector2.zero;
                _rectTrans.offsetMax = Vector2.zero;

                _newTempGO.AddComponent<CanvasRenderer>();
                var _image = _newTempGO.AddComponent<Image>();
                _image.color = Color.white;

                var _animator = _newTempGO.AddComponent<Animator>();
                var _controller = AnimatorExt.CreateController(_animator);

                AnimatorExt.AddClipToController(_controller, "显示");

                var _prefabObj = PrefabUtility.SaveAsPrefabAsset(_newTempGO, pathName);
                DestroyImmediate(_newTempGO);

                PrefabStageUtility.OpenPrefab(pathName);
                SceneView.lastActiveSceneView.FrameSelected();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                ProjectWindowUtil.ShowCreatedAsset(_prefabObj);
                Selection.activeObject = _prefabObj;
            }
        }

        public class DOCopyUIPrefab : EndNameEditAction
        {
            public static string originalPrefabPath = string.Empty;

            public override void Action(int instanceId, string newFilePath, string resourceFile)
            {
                if (string.IsNullOrEmpty(originalPrefabPath))
                {
                    Debug.LogError("Nothing to copy!");
                    return;
                }
                var _selectedPath = originalPrefabPath;
                if (!AssetDatabase.CopyAsset(_selectedPath, newFilePath))
                {
                    Debug.LogError("Copy UIPage failed!");
                    return;
                }

                var _newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newFilePath);

                var _animator = _newPrefab.GetComponent<Animator>();
                if (_animator != null)
                {
                    _animator = _newPrefab.GetComponent<Animator>();
                    if (_animator.runtimeAnimatorController != null)
                    {
                        var _controllerPath = AssetDatabase.GetAssetPath(
                            _animator.runtimeAnimatorController
                        );

                        var _animationFolder = Utils.GetAnimationFolderByPath(newFilePath);

                        var _copiedControllerPath =
                            $"{_animationFolder}/{Path.GetFileNameWithoutExtension(newFilePath)}_Controller.controller";

                        if (!AssetDatabase.CopyAsset(_controllerPath, _copiedControllerPath))
                        {
                            Debug.LogError("Copy AnimatorController failed!");
                            return;
                        }
                        _animator.runtimeAnimatorController =
                            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                                _copiedControllerPath
                            );
                    }
                }

                PrefabStageUtility.OpenPrefab(newFilePath);
                SceneView.lastActiveSceneView.FrameSelected();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (UNIArtSettings.IsPsdEntity(originalPrefabPath))
                {
                    UNIArtSettings.AddPSDEntityInstance(originalPrefabPath, newFilePath);
                }

                Utils.Delay(
                    () =>
                    {
                        ProjectWindowUtil.ShowCreatedAsset(_newPrefab);
                        Selection.activeObject = _newPrefab;
                    },
                    UNIArtSettings.Editor.DelayRetry
                );
            }
        }

        [MenuItem("Assets/Create/UIPage 预制体", priority = 30)]
        public static void CreateUIPrefab()
        {
            if (!AssetDatabase.IsValidFolder(UIPageFolder))
            {
                Utils.CreateFolderIfNotExist(UIPageFolder);
            }

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                ScriptableObject.CreateInstance<DOCreateUIPage>(),
                $"{UIPageFolder}/新页面.prefab",
                EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D,
                string.Empty
            );
        }

        [MenuItem("Assets/Create/复制 UIPage预制体 (含页面动画) %w", priority = 31)]
        public static void CreateUIPageCopy()
        {
            var selectedObject =
                TmplBrowser.selectedAsset?.AssetObject ?? Selection.activeGameObject;

            if (selectedObject == null)
            {
                return;
            }
            var _selectedPath = AssetDatabase.GetAssetPath(selectedObject);
            CopyPrefab(AssetDatabase.GetAssetPath(selectedObject), _selectedPath);
        }

        public static void CopyPrefabToUIPage(string assetPath)
        {
            CopyPrefab(assetPath, $"{UIPageFolder}/{Path.GetFileName(assetPath)}");
        }

        public static void CopyPrefabToUIComponent(string assetPath)
        {
            CopyPrefab(assetPath, $"{UIComponentFolder}/{Path.GetFileName(assetPath)}");
        }

        public static void CopyPrefab(string assetPath, string destFile)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            // 移除#PSD
            destFile = destFile.Replace("#psd", string.Empty);

            var _asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (_asset == null)
            {
                return;
            }

            DOCopyUIPrefab.originalPrefabPath = assetPath;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                ScriptableObject.CreateInstance<DOCopyUIPrefab>(),
                destFile,
                EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D,
                string.Empty
            );
        }

        [MenuItem("Assets/Create/复制 UIPage预制体 (含页面动画) %w", true)]
        public static bool CreateUIPrefabCopyValidate()
        {
            var _gameObject = Selection.activeGameObject;

            if (_gameObject == null)
            {
                return false;
            }
            // var _selectedPath = AssetDatabase.GetAssetPath(_gameObject);

            // if (!_selectedPath.StartsWith(UIPagePrefabRoot))
            // {
            //     return false;
            // }

            return true;
        }

        [MenuItem("Assets/转到UI界面列表 &2", priority = 50)]
        public static void ShowUIList()
        {
            FocusProjectBrowser();
            var _uiPrefabsFolder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UIPageFolder);
            AssetDatabase.OpenAsset(_uiPrefabsFolder);
            Utils.Delay(
                () => AssetDatabase.OpenAsset(_uiPrefabsFolder),
                UNIArtSettings.Editor.DelayRetry
            );
        }

        [MenuItem("Assets/定位正在编辑的预制体 &3", priority = 51)]
        public static void LocationStagePrefab()
        {
            FocusProjectBrowser();
            var _assetPath = Utils.PrefabStageAssetPath();
            if (!string.IsNullOrEmpty(_assetPath))
            {
                ProjectWindowUtil.ShowCreatedAsset(
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_assetPath)
                );
            }
        }

        public static void FocusProjectBrowser()
        {
            Type projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
            EditorWindow.GetWindow(projectBrowserType).Focus();
            EditorWindow.GetWindow(projectBrowserType).Repaint();
        }

        public static void FocusInspector()
        {
            Type inspectorType = Type.GetType("UnityEditor.InspectorWindow, UnityEditor");
            EditorWindow.GetWindow<EditorWindow>(inspectorType).Focus();
            EditorWindow.GetWindow<EditorWindow>("Inspector").Repaint();
        }

        // create ArtAssets basic layout
        [InitializeOnLoadMethod]
        public static void InitWorkLayout()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ArtAssets"))
            {
                AssetDatabase.CreateFolder("Assets", "ArtAssets");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            bool _dirty = false;
            new List<string> { "UI Prefabs", "Textures", "Audios", "Fonts", "Animations" }.ForEach(
                (folderName) =>
                {
                    if (!AssetDatabase.IsValidFolder($"Assets/ArtAssets/{folderName}"))
                    {
                        AssetDatabase.CreateFolder("Assets/ArtAssets", folderName);
                        _dirty = true;
                    }
                }
            );
            if (_dirty)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            importTMPEssentialResourcesIfNotExists();
            KeepHierarchyGUIPriority();
        }

        private static void KeepHierarchyGUIPriority()
        {
            if (!UNIArtSettings.Editor.EnableHierarchyItemGUI.Value)
                return;

            Utils.RemoveHierarchyWindowItemOnGUI(UNIArtSettings.excludeHierarchyMethods);
            Utils.Delay(
                () =>
                {
                    Utils.RemoveHierarchyWindowItemOnGUI(UNIArtSettings.excludeHierarchyMethods);
                },
                0.5f
            );
        }

        private static void importTMPEssentialResourcesIfNotExists()
        {
            string[] _settings = AssetDatabase.FindAssets("t:TMP_Settings");
            if (_settings.Length <= 0)
            {
                string packageFullPath = TMP_EditorUtility.packageFullPath;
                //TMP Menu import way: TMP_PackageUtilities.ImportProjectResourcesMenu();
                AssetDatabase.ImportPackage(
                    packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage",
                    false
                );

                AssetDatabase.importPackageCompleted += importTMPCallback;
            }
            else
            {
                setDefaultTMPFont();
            }
        }

        private static void importTMPCallback(string packageName)
        {
            setDefaultTMPFont();
            AssetDatabase.importPackageCompleted -= importTMPCallback;
        }

        private static void setDefaultTMPFont()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/ArtAssets/Fonts/DefaultTMPFont.asset"
            );
            if (fontAsset == null)
            {
                return;
            }
            // TMPro 3.2版本前defaultFontAsset未公开，且3.2版本未发布，暂时使用反射设置
            System.Type _type = typeof(TMP_Settings);
            var _field = _type.GetField(
                "m_defaultFontAsset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            _field.SetValue(TMP_Settings.instance, fontAsset);
        }
    }
}
