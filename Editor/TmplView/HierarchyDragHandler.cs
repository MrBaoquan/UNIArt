using UnityEngine;
using UnityEditor;

namespace UNIArt.Editor
{
    [InitializeOnLoad]
    public static class HierarchyDragHandler
    {
        static HierarchyDragHandler()
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
        static bool isAltPressed = false;

        static DragAndDropVisualMode HierarchyDropHandler(
            int dropTargetInstanceID,
            HierarchyDropFlags dropMode,
            Transform parentForDraggedObjects,
            bool perform
        )
        {
            if (perform)
            {
                isAltPressed = Event.current.modifiers == EventModifiers.Alt;
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
                isAltPressed = Event.current.modifiers == EventModifiers.Alt;
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

                if (!isAltPressed)
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
            return DragAndDrop.paths != null
                && DragAndDrop.paths.Length > 0
                && DragAndDrop.paths[0].StartsWith("Assets/ArtAssets/#Templates");
        }

        static DragAndDropVisualMode ProjectBrowserDropHandler(
            int dragInstanceId,
            string dropUponPath,
            bool perform
        )
        {
            if (!perform || !dropUponPath.StartsWith(UNIArtSettings.Project.ArtFolder))
                return DragAndDropVisualMode.None;

            Utils.MoveAssetsWithDependencies(DragAndDrop.paths, dropUponPath, false);
            return DragAndDropVisualMode.None;
        }
    }
}
