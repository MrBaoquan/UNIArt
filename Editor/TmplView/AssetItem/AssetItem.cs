using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.EditorCoroutines.Editor;
using UnityEngine.Events;
using System;
using System.IO;

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

        public float Width => resolvedStyle.width;

        public void RebuildPreview()
        {
            previewTex = UIPreviewer.CreateAssetPreview(RawAssetObject, PreviewPath);
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
                if (UNIArtSettings.PsdRawExists(rawAssetPath))
                {
                    defaultPrefabIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Packages/com.parful.uniart/Assets/Icon/ps-prefab.png"
                    );
                }
            }
            else if (previewTex == null)
            {
                if (File.Exists(PreviewPath))
                    previewTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PreviewPath);
                else
                    RebuildPreview();
            }

            if (UNIArtSettings.IsProjectUIPageAsset(rawAssetPath) && RawAssetObject is GameObject)
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

            _assetType.style.backgroundImage = defaultPrefabIcon;
        }

        private void ChangeName(string newName)
        {
            var _ret = AssetDatabase.RenameAsset(rawAssetPath, newName);
            if (!string.IsNullOrEmpty(_ret))
            {
                Debug.LogWarning(_ret);
                return;
            }
            rawAssetPath = AssetDatabase.GetAssetPath(AssetObject);
            SetAssetName(newName);
            return;
        }

        public string PreviewPath => UNIArtSettings.GetPreviewPathByAsset(AssetPath);

        public bool NonPSDPrefab => !IsPSDPrefab;

        public bool IsPrefab => RawAssetObject is GameObject;

        public bool IsPSD => rawAssetPath.EndsWith(".psd");
        public bool IsPSDPrefab => rawAssetPath.EndsWith("#psd.prefab");

        public bool HasPSDEnity => IsPSD && UNIArtSettings.PsdEntityExists(rawAssetPath);
        public bool HasPSDRaw => !IsPSD && UNIArtSettings.PsdRawExists(rawAssetPath);

        public bool ShouldHidden => IsPSDPrefab && HasPSDRaw;

        public int Index = -1;

        public UnityEvent<string, string> OnConfirmInput = new UnityEvent<string, string>();

        public AssetItem()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/AssetItem/AssetItem.uxml")
            );

            visualTree.CloneTree(this);

            handleHoverEvent();
            PrepareStartDrag();
            handleDoubleClick(() =>
            {
                if (RawAssetObject is GameObject || IsPSD)
                {
                    AssetDatabase.OpenAsset(RawAssetObject.GetInstanceID());
                    return;
                }

                Utils.FocusProjectBrowser();
                EditorGUIUtility.PingObject(RawAssetObject);
            });

            var _inputField = this.Q<TextField>("input");

            _inputField.RegisterCallback<FocusOutEvent>(e =>
            {
                if (string.IsNullOrEmpty(_inputField.value))
                    return;

                DoText();
                if (_inputField.value == Path.GetFileNameWithoutExtension(rawAssetPath))
                {
                    return;
                }

                var _oldVal = rawAssetPath;
                var _newName = _inputField.value;
                _inputField.value = string.Empty;
                OnConfirmInput.Invoke(_oldVal, _newName);
                ChangeName(_newName);
            });
        }

        private EditorCoroutine hoverCoroutine;
        private Vector2 lastMousePosition;

        public UnityEvent<Texture2D, Vector2> OnShowPreview = new UnityEvent<Texture2D, Vector2>();
        public UnityEvent<AssetItem> OnHidePreview = new UnityEvent<AssetItem>();

        public void DoEdit()
        {
            var _inputField = this.Q<TextField>("input");
            _inputField.style.display = DisplayStyle.Flex;

            var _label = this.Q<Label>("name");
            _label.style.display = DisplayStyle.None;

            _inputField.value = Path.GetFileNameWithoutExtension(rawAssetPath);
            _inputField.Focus();
        }

        public void DoText()
        {
            var _inputField = this.Q<TextField>("input");
            _inputField.style.display = DisplayStyle.None;

            var _label = this.Q<Label>("name");
            _label.style.display = DisplayStyle.Flex;
        }

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

                OnStartDrag.Invoke(this);
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

        public void Delete()
        {
            AssetDatabase.DeleteAsset(rawAssetPath);
        }
    }
}
