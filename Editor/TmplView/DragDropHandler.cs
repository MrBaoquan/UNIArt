using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Linq;

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

        private static void OnHierarchyItemGUI(int instanceID, Rect selectionRect)
        {
            Event evt = Event.current;
            if (evt.type == EventType.DragPerform && selectionRect.Contains(evt.mousePosition))
            {
                GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                var _imageComp = obj.GetComponent<Image>();
                if (DragAndDrop.paths.Count() > 1 && _imageComp != null)
                {
                    _imageComp.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DragAndDrop.paths[0]);
                    _imageComp.SetNativeSize();

                    var _animator = obj.GetComponent<Animator>();
                    if (_animator == null)
                        _animator = obj.AddComponent<Animator>();

                    AssetDatabase.SaveAssets();
                    var _controller = AnimatorEditor.CreateController(_animator);

                    var _animationClip = SpriteSheetTool.CreateSequenceImageAnimation(
                        typeof(Image),
                        DragAndDrop.paths.ToList()
                    );
                    AnimatorEditor.AddClipToController(_controller, _animationClip);

                    evt.Use();
                    return;
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
                var _newObj = Selection.activeGameObject;
                if (_newObj == null)
                    return;

                if (!isCtrlPressed)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(_newObj))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            _newObj,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                }

                AssetItem dragAsset = DragAndDrop.GetGenericData("OriginAsset") as AssetItem;
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
                        && dropUponPath.StartsWith(UNIArtSettings.Project.ArtFolder)
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
