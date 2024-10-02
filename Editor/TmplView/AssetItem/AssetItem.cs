using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.EditorCoroutines.Editor;
using UnityEngine.Events;
using System;
using System.IO;
using Sirenix.Utilities.Editor;
using System.Diagnostics;

namespace UNIArt.Editor
{
    public class AssetItem : SelectableView
    {
        const string previewFolderName = "Previews";
        internal string rawAssetPath;
        private Texture2D defaultPrefabIcon;
        public UnityEngine.Object RawAssetObject = null;
        public UnityEngine.Object AssetObject
        {
            get
            {
                if (IsPSD && HasPSDEnity)
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
                }
                return RawAssetObject;
            }
            set { RawAssetObject = value; }
        }
        public Texture2D previewTex = null;
        public string TemplateID = string.Empty; // 所属模板ID
        public string TemplateRootFolder => UNIArtSettings.GetExternalTemplateFolder(TemplateID);
        public string TemplatePreviewFolder => $"{TemplateRootFolder}/{previewFolderName}";

        public string AssetPath
        {
            get
            {
                if (IsPSD && HasPSDEnity)
                {
                    return UNIArtSettings.PsdFileToPrefabPath(rawAssetPath);
                }
                return rawAssetPath;
            }
            set
            {
                rawAssetPath = value;
                TemplateID = UNIArtSettings.GetTemplateNameBySubAsset(value);
                SetAssetName(Path.GetFileNameWithoutExtension(value));
                RawAssetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
                // if (ShouldHidden)
                // {
                //     this.style.display = DisplayStyle.None;
                // }
                RefreshPreview();
            }
        }

        private void SetAssetName(string assetName)
        {
            this.Q<Label>("name").text = assetName;
        }

        public void SetZoom(float zoom)
        {
            zoom = (zoom - 50) / 50f;
            var _root = this.Q<VisualElement>("root");
            _root.style.width = 80 + zoom * 30;
            _root.style.height = 80 + zoom * 30;
        }

        public void RefreshPreview()
        {
            defaultPrefabIcon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;

            if (RawAssetObject is Texture2D)
            {
                previewTex = RawAssetObject as Texture2D;
            }

            if (rawAssetPath.EndsWith("#psd.prefab"))
            {
                previewTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    UNIArtSettings.PrefabPathToPsdFile(rawAssetPath)
                );
            }
            else if (previewTex == null && UNIArtSettings.IsTemplateAsset(rawAssetPath))
            {
                previewTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PreviewPath);
            }
            else if (
                UNIArtSettings.IsProjectUIPageAsset(rawAssetPath) && RawAssetObject is GameObject
            )
            {
                defaultPrefabIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/com.parful.uniart/Assets/Icon/UIPage.png"
                );
            }
            else if (UNIArtSettings.IsProjectUIComponentAsset(rawAssetPath))
            {
                defaultPrefabIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/com.parful.uniart/Assets/Icon/组件.png"
                );
            }

            this.Q<VisualElement>("preview").style.backgroundImage =
                previewTex ?? defaultPrefabIcon;

            refreshAssetIcon();
        }

        private void refreshAssetIcon()
        {
            var _assetType = this.Q<VisualElement>("asset-type");
            if (this.Q<VisualElement>("preview").style.backgroundImage == defaultPrefabIcon)
            {
                _assetType.style.backgroundImage = null;
                return;
            }
            if (RawAssetObject is Texture2D)
            {
                if (IsPSD)
                {
                    _assetType.style.backgroundImage = HasPSDEnity
                        ? AssetDatabase.LoadAssetAtPath<Texture2D>(
                            "Packages/com.parful.uniart/Assets/Icon/ps-active.png"
                        )
                        : AssetDatabase.LoadAssetAtPath<Texture2D>(
                            "Packages/com.parful.uniart/Assets/Icon/ps-disabled.png"
                        );
                    return;
                }
                _assetType.style.backgroundImage =
                    EditorGUIUtility.IconContent("Image Icon").image as Texture2D;
                return;
            }

            _assetType.style.backgroundImage =
                EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
        }

        public string PreviewPath => UNIArtSettings.GetPreviewPathByAsset(AssetPath);

        public bool IsPSD => rawAssetPath.EndsWith(".psd");
        public bool IsPSDPrefab => rawAssetPath.EndsWith("#psd.prefab");

        public bool HasPSDEnity => IsPSD && UNIArtSettings.PsdEntityExists(rawAssetPath);
        public bool HasPSDRaw => !IsPSD && UNIArtSettings.PsdRawExists(rawAssetPath);

        public bool ShouldHidden => IsPSDPrefab && HasPSDRaw;

        public int Index = -1;

        public AssetItem()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/AssetItem/AssetItem.uxml")
            );

            visualTree.CloneTree(this);
            // Func<DropdownMenuAction, DropdownMenuAction.Status> activeIfGameObject = (action) =>
            // {
            //     return (HasPSDEnity || assetObject is GameObject)
            //         ? DropdownMenuAction.Status.Normal
            //         : DropdownMenuAction.Status.Disabled;
            // };

            // Func<DropdownMenuAction, DropdownMenuAction.Status> activeIfThumb = (action) =>
            // {
            //     return (HasPSDEnity || assetObject is GameObject) && !IsPSD && !IsPSDPrefab
            //         ? DropdownMenuAction.Status.Normal
            //         : DropdownMenuAction.Status.Disabled;
            // };

            // this.AddManipulator(
            //     new ContextualMenuManipulator(
            //         (evt) =>
            //         {
            //             evt.menu.AppendAction(
            //                 "复制到项目 [UI页面]",
            //                 (x) =>
            //                 {
            //                     WorkflowUtility.CopyPrefabToUIPage(AssetPath);
            //                 },
            //                 activeIfGameObject
            //             );
            //             evt.menu.AppendAction(
            //                 "复制到项目 [UI组件]",
            //                 (x) =>
            //                 {
            //                     WorkflowUtility.CopyPrefabToUIComponent(AssetPath);
            //                 },
            //                 activeIfGameObject
            //             );
            //             evt.menu.AppendSeparator();

            //             evt.menu.AppendAction(
            //                 "打开所在文件夹",
            //                 (x) =>
            //                 {
            //                     EditorUtility.RevealInFinder(rawAssetPath);
            //                 }
            //             );

            //             evt.menu.AppendAction(
            //                 "在资源视图中显示",
            //                 (x) =>
            //                 {
            //                     Utils.FocusProjectBrowser();
            //                     EditorGUIUtility.PingObject(assetObject);
            //                 }
            //             );

            //             evt.menu.AppendSeparator();
            //             evt.menu.AppendAction(
            //                 "重新导入",
            //                 (x) =>
            //                 {
            //                     if (IsPSD)
            //                     {
            //                         if (HasPSDEnity)
            //                         {
            //                             AssetDatabase.DeleteAsset(AssetPath);
            //                             AssetDatabase.Refresh();
            //                         }
            //                     }
            //                     AssetDatabase.ImportAsset(rawAssetPath);
            //                 }
            //             );
            //             evt.menu.AppendSeparator();
            //             evt.menu.AppendAction(
            //                 "选取缩略图",
            //                 (x) =>
            //                 {
            //                     AssetDatabase.OpenAsset(assetObject);
            //                     SceneViewCapture.OnCapture(_rect =>
            //                     {
            //                         var _savedPath = PreviewPath;
            //                         var _savedFolder = Path.GetDirectoryName(_savedPath);
            //                         if (!Directory.Exists(_savedFolder))
            //                         {
            //                             Directory.CreateDirectory(_savedFolder);
            //                         }

            //                         SceneViewCapture.TakeScreenshot(
            //                             _rect,
            //                             PreviewPath,
            //                             () =>
            //                             {
            //                                 RefreshPreview();
            //                             }
            //                         );
            //                     });
            //                     SceneViewCapture.ShowCapture();
            //                 },
            //                 activeIfThumb
            //             );
            //         }
            //     )
            // );

            handleHoverEvent();
            PrepareStartDrag();
            handleDoubleClick(() =>
            {
                if (
                    !UNIArtSettings.IsTemplateAsset(rawAssetPath) && RawAssetObject is GameObject
                    || IsPSD
                )
                {
                    AssetDatabase.OpenAsset(RawAssetObject.GetInstanceID());
                    return;
                }

                Utils.FocusProjectBrowser();
                EditorGUIUtility.PingObject(RawAssetObject);
            });
        }

        private EditorCoroutine hoverCoroutine;
        private Vector2 lastMousePosition;

        public UnityEvent<Texture2D, Vector2> OnShowPreview = new UnityEvent<Texture2D, Vector2>();
        public UnityEvent<AssetItem> OnHidePreview = new UnityEvent<AssetItem>();

        private void handleHoverEvent()
        {
            Action preparePreview = () =>
            {
                if (hoverCoroutine != null)
                {
                    OnHidePreview.Invoke(this);
                    EditorCoroutineUtility.StopCoroutine(hoverCoroutine);
                }
                hoverCoroutine = EditorCoroutineUtility.StartCoroutine(
                    ShowPreviewAfterDelay(),
                    this
                );
            };

            this.RegisterCallback<MouseEnterEvent>(evt =>
            {
                evt.StopPropagation();
                lastMousePosition = evt.mousePosition;
                preparePreview();
            });

            this.RegisterCallback<MouseMoveEvent>(evt =>
            {
                evt.StopPropagation();
                var _deltaLength = (evt.mousePosition - lastMousePosition).sqrMagnitude;
                lastMousePosition = evt.mousePosition;
                if (_deltaLength < 4)
                {
                    return;
                }
                preparePreview();
                evt.StopPropagation();
            });

            // 注册鼠标移出事件
            this.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                evt.StopPropagation();
                if (hoverCoroutine != null)
                {
                    EditorCoroutineUtility.StopCoroutine(hoverCoroutine);
                    OnHidePreview.Invoke(this);
                }
            });

            this.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (hoverCoroutine != null)
                    EditorCoroutineUtility.StopCoroutine(hoverCoroutine);
                OnHidePreview.Invoke(this);
            });

            this.RegisterCallback<MouseUpEvent>(evt =>
            {
                OnHidePreview.Invoke(this);
            });
        }

        private IEnumerator ShowPreviewAfterDelay()
        {
            yield return new EditorWaitForSeconds(0.45f);
            OnShowPreview.Invoke(previewTex, lastMousePosition);
        }

        public UnityEvent<AssetItem> OnStartDrag = new UnityEvent<AssetItem>();

        private void PrepareStartDrag()
        {
            bool isMouseDown = false;
            Vector2 mouseDownPosition = Vector2.zero;
            this.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                isMouseDown = true;

                mouseDownPosition = evt.mousePosition;
                // evt.StopPropagation();
            });

            this.RegisterCallback<MouseUpEvent>(evt =>
            {
                isMouseDown = false;
                // evt.StopPropagation();
            });

            this.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (evt.button != 0 || !isMouseDown)
                    return;
                var _delta = (evt.mousePosition - mouseDownPosition).magnitude;

                var dragable =
                    Event.current.type == EventType.MouseDown
                    || Event.current.type == EventType.MouseDrag;

                if (_delta < 5f || !dragable)
                    return;
                isMouseDown = false;

                var _dragTarget = AssetObject;

                if (_dragTarget is Texture2D)
                {
                    _dragTarget = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Packages/com.parful.uniart/Assets/Prefabs/图片.prefab"
                    );
                    _dragTarget.name = Path.GetFileNameWithoutExtension(rawAssetPath);
                }

                OnStartDrag.Invoke(this);
                // DragAndDrop.PrepareStartDrag();
                // DragAndDrop.objectReferences = new UnityEngine.Object[] { AssetObject };
                // DragAndDrop.paths = new string[] { assetPath };

                // DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                // DragAndDrop.StartDrag("Drag Asset");
                // DragAndDrop.SetGenericData("AssetItem", this);

                evt.StopPropagation();
            });
        }

        private void handleDoubleClick(Action onDoubleClick)
        {
            int _clickCount = 0;
            double _lastClickTime = 0;
            const double DoubleClickThreshold = 0.3f; // 双击的时间间隔阈值

            this.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // 检查是否为左键点击
                {
                    double timeSinceLastClick = EditorApplication.timeSinceStartup - _lastClickTime;

                    if (timeSinceLastClick < DoubleClickThreshold)
                    {
                        _clickCount++;

                        if (_clickCount == 2)
                        {
                            onDoubleClick();
                            _clickCount = 0;
                            evt.StopPropagation();
                        }
                    }
                    else
                    {
                        _clickCount = 1;
                    }

                    _lastClickTime = EditorApplication.timeSinceStartup;
                }
            });
        }
    }
}
