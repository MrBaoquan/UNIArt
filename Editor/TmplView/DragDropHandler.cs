using UnityEngine;
using UnityEditor;

namespace UNIArt.Editor
{
    [InitializeOnLoad]
    public static class DragDropHandler
    {
        static DragDropHandler()
        {
            // 注册层次视图拖拽事件的回调
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyDrag;
            // EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemOnGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            DragAndDrop.AddDropHandler(HierarchyDropHandler);
            DragAndDrop.AddDropHandler(SceneDropHandler);
            DragAndDrop.AddDropHandler(ProjectBrowserDropHandler);
        }

        static bool isDragPerform = false;
        static bool isCtrlPressed = false;

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

        private static void OnHierarchyDrag(int instanceID, Rect selectionRect)
        {
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
            if (evt.type == EventType.DragExited && isArtAssetDrag() && isDragPerform)
            {
                isDragPerform = false;
                var _newObj = Selection.activeGameObject;
                if (_newObj == null)
                    return;

                if (!isCtrlPressed)
                {
                    if (PrefabUtility.IsPartOfAnyPrefab(_newObj))
                    {
                        PrefabUtility.UnpackPrefabInstance(
                            _newObj,
                            PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction
                        );
                    }
                }

                evt.Use();
            }
        }

        private static bool isArtAssetDrag()
        {
            return DragAndDrop.GetGenericData("AssetItem") != null;
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
                    ) || isArtAssetDrag()
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
