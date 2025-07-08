using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;

namespace UNIArt.Editor
{
    [InitializeOnLoad]
    public static class DragDropHandler
    {
        static DragDropHandler()
        {
            // 注册层次视图拖拽事件的回调
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
            // EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemOnGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            DragAndDrop.AddDropHandler(HierarchyDropHandler);
            DragAndDrop.AddDropHandler(SceneDropHandler);
            DragAndDrop.AddDropHandler(ProjectBrowserDropHandler);
        }

        static bool isDragPerform = false;
        static bool isCtrlPressed = false;

        // static DragAndDropVisualMode InspectorDropHandler(
        //     UnityEngine.Object[] targets,
        //     bool perform
        // )
        // {
        //     return DragAndDropVisualMode.None;
        // }

        static DragAndDropVisualMode HierarchyDropHandler(
            int dropTargetInstanceID,
            HierarchyDropFlags dropMode,
            Transform parentForDraggedObjects,
            bool perform
        )
        {
            if (perform)
            {
                isCtrlPressed = Event.current.modifiers == EventModifiers.Control;
                isDragPerform = true;
            }
            return DragAndDropVisualMode.None;
        }

        static DragAndDropVisualMode SceneDropHandler(
            UnityEngine.Object dropUpon,
            Vector3 worldPosition,
            Vector2 viewportPosition,
            Transform parentForDraggedObjects,
            bool perform
        )
        {
            if (perform)
            {
                isCtrlPressed = Event.current.modifiers == EventModifiers.Control;
                isDragPerform = true;
            }
            return DragAndDropVisualMode.None;
        }

        private static void createSequenceImageAnimation(GameObject target, List<Sprite> sprites)
        {
            var _imageComp = target.AddOrGetComponent<Image>();
            _imageComp.sprite = sprites[0];
            _imageComp.SetNativeSize();

            var _animator = target.AddOrGetComponent<Animator>();
            var _controller = AnimatorExt.CreateController(_animator);
            var _animationClip = SpriteSheetTool.CreateSequenceImageAnimation(sprites);

            AnimatorExt.AddClipToController(_controller, _animationClip);
            Event.current.Use();
            DragAndDrop.SetGenericData("ArtDragOrigin", null);
            Selection.activeGameObject = target;

            AnimatorEditor.RefreshAnimationList();
        }

        private static void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragPerform && selectionRect.Contains(evt.mousePosition))
            {
                GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

                var _textures = DragAndDrop.objectReferences.OfType<Texture2D>().ToList();

                if (_textures.Count > 1)
                {
                    var _sprites = DragAndDrop.paths
                        .Select(_ => AssetDatabase.LoadAssetAtPath<Sprite>(_))
                        .OfType<Sprite>()
                        .ToList();
                    createSequenceImageAnimation(obj, _sprites);
                }
                else if (_textures.Count == 1)
                {
                    var _assetPath = AssetDatabase.GetAssetPath(_textures.First());
                    // 获取子资源
                    var _subSprites = AssetDatabase
                        .LoadAllAssetsAtPath(_assetPath)
                        .OfType<Sprite>()
                        .ToList();

                    if (_subSprites.Count > 1)
                    {
                        createSequenceImageAnimation(obj, _subSprites);
                    }
                }
            }
            handleDropEvent();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            handleDropEvent();
        }

        // private static void OnProjectWindowItemOnGUI(string guid, Rect selectionRect)
        // {
        //     selectionRect.Contains(evt.mousePosition)
        // }

        private static void handleDropEvent()
        {
            Event evt = Event.current;

            if (evt.type == EventType.DragExited && Utils.IsDragFromUNIArt() && isDragPerform)
            {
                isDragPerform = false;

                if (DragAndDrop.objectReferences.Length > 1)
                {
                    return;
                }

                var _newObj = Selection.activeGameObject;
                if (_newObj == null)
                    return;

                if (!isCtrlPressed)
                {
                    // 取消解预制体
                    // if (PrefabUtility.IsAnyPrefabInstanceRoot(_newObj))
                    // {
                    //     PrefabUtility.UnpackPrefabInstance(
                    //         _newObj,
                    //         PrefabUnpackMode.Completely,
                    //         InteractionMode.AutomatedAction
                    //     );
                    // }
                }

                AssetItem dragAsset = DragAndDrop.GetGenericData("ArtDragOrigin") as AssetItem;
                if (dragAsset == null)
                {
                    Debug.LogWarning("Dragged item is not a valid asset.");
                    return;
                }

                if (dragAsset.AssetObject is Texture2D)
                {
                    GameObject.DestroyImmediate(_newObj.GetComponent<SpriteRenderer>());
                    _newObj.AddComponent<RectTransform>();
                    var _imageComponent = _newObj.AddComponent<Image>();
                    _imageComponent.color = Color.white;
                    _imageComponent.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                        dragAsset.AssetPath
                    );
                    _newObj.GetComponent<Image>().SetNativeSize();
                }

                evt.Use();
                DragAndDrop.SetGenericData("ArtDragOrigin", null);
            }
        }

        static DragAndDropVisualMode ProjectBrowserDropHandler(
            int dragInstanceId,
            string dropUponPath,
            bool perform
        )
        {
            // 1. 从工作台拖拽到项目文件夹 需要处理依赖
            // 2. 项目文件夹内按住alt拖拽到项目文件夹 需要处理依赖
            if (
                perform
                && (
                    (
                        Event.current.modifiers == EventModifiers.Alt
                        && (
                            dropUponPath.StartsWith(UNIArtSettings.Project.ArtFolder)
                            || UNIArtSettings.IsTemplateAsset(dropUponPath)
                        )
                    ) || Utils.IsDragFromUNIArt()
                )
            )
            {
                Utils.MoveAssetsWithDependencies(DragAndDrop.paths, dropUponPath, false);
                EditorApplication.delayCall += () =>
                {
                    TmplBrowser.RefreshContentView();
                };

                return DragAndDropVisualMode.None;
            }

            return DragAndDropVisualMode.None;
        }
    }
}
