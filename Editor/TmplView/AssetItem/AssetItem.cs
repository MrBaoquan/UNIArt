using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.EditorCoroutines.Editor;
using UnityEngine.Events;
using System;
using System.IO;

namespace UNIHper.Art.Editor
{
    public class AssetItem : SelectableView
    {
        const string previewFolderName = "Previews";
        private string assetPath;
        private Texture2D defaultPrefabIcon;
        public GameObject gameObject = null;
        public Texture2D previewTex = null;
        public string TemplateID = string.Empty; // 所属模板ID
        public string TemplateRootFolder => UNIArtSettings.GetExternalTemplateFolder(TemplateID);
        public string TemplatePreviewFolder => $"{TemplateRootFolder}/{previewFolderName}";

        public string AssetPath
        {
            get { return assetPath; }
            set
            {
                assetPath = value;
                TemplateID = UNIArtSettings.GetTemplateNameBySubAsset(value);
                SetAssetName(Path.GetFileNameWithoutExtension(value));
                gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(value);
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
            var _previewTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PreviewPath);
            if (_previewTex != null)
            {
                previewTex = _previewTex;
            }
            this.Q<VisualElement>("preview").style.backgroundImage =
                _previewTex ?? defaultPrefabIcon;
        }

        public string PreviewPath => _getPreviewPath();

        private string _getPreviewPath()
        {
            var _fileName = AssetPath
                .ToForwardSlash()
                .Replace(TemplateRootFolder + "/", "")
                .Replace("/", "_")
                .Replace(".prefab", ".png");
            return TemplatePreviewFolder + "/" + _fileName;
        }

        public AssetItem()
        {
            defaultPrefabIcon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/AssetItem/AssetItem.uxml")
            );

            visualTree.CloneTree(this);
            this.RegisterCallback<MouseDownEvent>(evt => OnMouseDown(evt));

            this.AddManipulator(
                new ContextualMenuManipulator(
                    (evt) =>
                    {
                        evt.menu.AppendAction(
                            "复制到项目 [UI页面]",
                            (x) =>
                            {
                                WorkflowUtility.CopyPrefabToUIPage(AssetPath);
                            },
                            DropdownMenuAction.AlwaysEnabled
                        );
                        evt.menu.AppendAction(
                            "复制到项目 [UI组件]",
                            (x) =>
                            {
                                WorkflowUtility.CopyPrefabToUIComponent(AssetPath);
                            },
                            DropdownMenuAction.AlwaysEnabled
                        );
                        evt.menu.AppendSeparator();
                        evt.menu.AppendAction(
                            "选取缩略图",
                            (x) =>
                            {
                                AssetDatabase.OpenAsset(gameObject);
                                SceneViewCapture.OnCapture(_rect =>
                                {
                                    var _savedPath = PreviewPath;
                                    var _savedFolder = Path.GetDirectoryName(_savedPath);
                                    if (!Directory.Exists(_savedFolder))
                                    {
                                        Directory.CreateDirectory(_savedFolder);
                                    }

                                    SceneViewCapture.TakeScreenshot(
                                        _rect,
                                        PreviewPath,
                                        () =>
                                        {
                                            RefreshPreview();
                                        }
                                    );
                                });
                                SceneViewCapture.ShowCapture();
                            },
                            DropdownMenuAction.AlwaysEnabled
                        );
                    }
                )
            );

            handleHoverEvent();
            handleDoubleClick(() =>
            {
                Type projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
                EditorWindow.GetWindow(projectBrowserType).Focus();

                EditorGUIUtility.PingObject(gameObject);
            });
        }

        private EditorCoroutine hoverCoroutine;
        private Vector2 lastMousePosition;

        public UnityEvent<Texture2D, Vector2> OnShowPreview = new UnityEvent<Texture2D, Vector2>();
        public UnityEvent<AssetItem> OnHidePreview = new UnityEvent<AssetItem>();

        private void handleHoverEvent()
        {
            this.RegisterCallback<MouseEnterEvent>(evt =>
            {
                lastMousePosition = evt.mousePosition;
            });

            this.RegisterCallback<MouseMoveEvent>(evt =>
            {
                var _deltaLength = (evt.mousePosition - lastMousePosition).sqrMagnitude;
                if (_deltaLength < 100)
                {
                    return;
                }

                lastMousePosition = evt.mousePosition;
                if (hoverCoroutine != null)
                {
                    OnHidePreview.Invoke(this);
                    EditorCoroutineUtility.StopCoroutine(hoverCoroutine);
                }
                hoverCoroutine = EditorCoroutineUtility.StartCoroutine(
                    ShowPreviewAfterDelay(),
                    this
                );
            });

            // 注册鼠标移出事件
            this.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                // 如果鼠标移出，停止协程并关闭预览窗口
                if (hoverCoroutine != null)
                {
                    EditorCoroutineUtility.StopCoroutine(hoverCoroutine);
                    OnHidePreview.Invoke(this);
                }
            });
        }

        private IEnumerator ShowPreviewAfterDelay()
        {
            yield return new EditorWaitForSeconds(0.45f);
            OnShowPreview.Invoke(previewTex, lastMousePosition);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            evt.StopPropagation();
            // 如果鼠标移出，停止协程并关闭预览窗口
            if (hoverCoroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(hoverCoroutine);
                EditorWindow.GetWindow<TmplBrowser>().ClearAssetPreviewTooltip();
            }
            if (evt.button == 0) // 左键点击
            {
                var _sp = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new UnityEngine.Object[] { _sp }; // 拖拽时不关联具体对象
                DragAndDrop.paths = new string[] { assetPath }; // 自定义拖拽路径

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                DragAndDrop.StartDrag("AssetItem");

                // 结束拖拽事件处理
                // evt.StopPropagation();
            }
        }

        private void handleDoubleClick(Action onDoubleClick)
        {
            int _clickCount = 0;
            double _lastClickTime = 0;
            const double DoubleClickThreshold = 0.3f; // 双击的时间间隔阈值

            this.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();
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
