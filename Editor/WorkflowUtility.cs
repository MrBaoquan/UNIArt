using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.SceneManagement;
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
                        Path.GetFileNameWithoutExtension(pathName) + ".controller"
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
            if (!AssetDatabase.IsValidFolder("Assets/ArtAssets/UI Pages"))
            {
                AssetDatabase.CreateFolder("Assets/ArtAssets", "UI Pages");
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath(
                "Assets/ArtAssets/UI Pages",
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
                "ArtAssets/UI Pages"
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
            if (!_selectedPath.StartsWith("Assets/ArtAssets/UI Pages"))
            {
                return false;
            }

            return true;
        }

        [MenuItem("Assets/转到UI界面列表 %g", priority = 102)]
        public static void ShowUIList()
        {
            var _uiPrefabsFolder = AssetDatabase.LoadAssetAtPath(
                "Assets/ArtAssets/UI Pages",
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
            new List<string> { "UI Pages", "Textures", "Audios", "Fonts", "Animations" }.ForEach(
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
            if (_settings.Length > 0)
                return;
            string packageFullPath = TMP_EditorUtility.packageFullPath;

            //TMP Menu import way: TMP_PackageUtilities.ImportProjectResourcesMenu();

            AssetDatabase.ImportPackage(
                packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage",
                false
            );
        }
    }
}
