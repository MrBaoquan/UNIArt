using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.SceneManagement;

namespace UNIHper.Art.Editor
{
    internal class WorkflowUtility
    {
        public class DOCreatePrefab : EndNameEditAction
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
                ScriptableObject.CreateInstance<DOCreatePrefab>(),
                "NewUI.prefab",
                EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D,
                "ArtAssets/UI Pages"
            );
        }

        //[MenuItem("UNIArt/显示界面列表", priority = 102)]
        public static void ShowUIList()
        {
            var _uiPrefabsFolder = AssetDatabase.LoadAssetAtPath(
                "Assets/ArtAssets/UI Pages",
                typeof(Object)
            );
            AssetDatabase.OpenAsset(_uiPrefabsFolder);
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
        }
    }
}
