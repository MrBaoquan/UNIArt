using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.SceneManagement;
using TMPro;
using TMPro.EditorUtilities;

namespace UNIHper.Art.Editor
{
    internal class WorkflowUtility
    {
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

                _newTempGO.AddComponent<Animator>();

                var _prefabObj = PrefabUtility.SaveAsPrefabAsset(_newTempGO, pathName);
                ProjectWindowUtil.ShowCreatedAsset(_prefabObj);
                GameObject.DestroyImmediate(_newTempGO);

                PrefabStageUtility.OpenPrefab(pathName);
                SceneView.lastActiveSceneView.FrameSelected();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public class DOCopyUIPage : EndNameEditAction
        {
            public static string originalPrefabPath = string.Empty;

            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                if (string.IsNullOrEmpty(originalPrefabPath))
                {
                    Debug.LogError("Nothing to copy!");
                    return;
                }
                var _selectedPath = originalPrefabPath;
                if (!AssetDatabase.CopyAsset(_selectedPath, pathName))
                {
                    Debug.LogError("Copy UIPage failed!");
                    return;
                }

                var _newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathName);

                var _animator = _newPrefab.GetComponent<Animator>();
                if (_animator == null)
                {
                    _animator = _newPrefab.AddComponent<Animator>();
                }
                if (_animator.runtimeAnimatorController != null)
                {
                    var _controllerPath = AssetDatabase.GetAssetPath(
                        _animator.runtimeAnimatorController
                    );
                    var _copiedControllerPath = _controllerPath.Replace(
                        Path.GetFileName(_controllerPath),
                        Path.GetFileNameWithoutExtension(pathName) + "_Controller.controller"
                    );
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

                ProjectWindowUtil.ShowCreatedAsset(_newPrefab);

                PrefabStageUtility.OpenPrefab(pathName);
                //SceneView.lastActiveSceneView.FrameSelected();

                Selection.activeGameObject = _newPrefab;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("Assets/Create/UIPage 预制体", priority = 30)]
        public static void CreateUIPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ArtAssets/UI Prefabs/Windows"))
            {
                AssetDatabase.CreateFolder("Assets/ArtAssets/UI Prefabs", "Windows");
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath(
                "Assets/ArtAssets/UI Prefabs/Windows",
                typeof(Object)
            );

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                ScriptableObject.CreateInstance<DOCreateUIPage>(),
                "NewUI.prefab",
                EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D,
                "ArtAssets/UI Pages"
            );
        }

        [MenuItem("Assets/Create/复制 UIPage预制体 (含页面动画) %w", priority = 31)]
        public static void CreateUIPrefabCopy()
        {
            var _selected = Selection.activeObject;
            if (_selected == null)
            {
                return;
            }

            DOCopyUIPage.originalPrefabPath = AssetDatabase.GetAssetPath(
                Selection.activeGameObject
            );

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0,
                ScriptableObject.CreateInstance<DOCopyUIPage>(),
                "NewUI.prefab",
                EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D,
                "ArtAssets/UI Prefabs/Windows"
            );
        }

        [MenuItem("Assets/Create/复制 UIPage预制体 (含页面动画) %w", true)]
        public static bool CreateUIPrefabCopyValidate()
        {
            var _selected = Selection.activeGameObject;
            if (_selected == null)
            {
                return false;
            }
            var _selectedPath = AssetDatabase.GetAssetPath(_selected);
            if (!_selectedPath.StartsWith("Assets/ArtAssets/UI Prefabs/Windows"))
            {
                return false;
            }

            return true;
        }

        [MenuItem("Assets/转到UI界面列表 %g", priority = 50)]
        public static void ShowUIList()
        {
            var _uiPrefabsFolder = AssetDatabase.LoadAssetAtPath(
                "Assets/ArtAssets/UI Prefabs/Windows",
                typeof(Object)
            );

            EditorGUIUtility.PingObject(_uiPrefabsFolder);
            if (Selection.activeObject == _uiPrefabsFolder)
            {
                AssetDatabase.OpenAsset(_uiPrefabsFolder);
            }

            Selection.activeObject = _uiPrefabsFolder;
        }

        // create ArtAssets folder if not exist
        [InitializeOnLoadMethod]
        public static void CreateArtAssetsFolder()
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
