using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public class TmplBrowser : EditorWindow
    {
        const string BuiltInTemplateID = "Standard";

        [MenuItem("Window/UNIArt 工作台 &1", priority = 1399)] //1499
        public static void ShowExample()
        {
            TmplBrowser wnd = GetWindow<TmplBrowser>();
            wnd.titleContent = new GUIContent(
                "UNIArt 工作台",
                EditorGUIUtility.IconContent("Folder Icon").image
            );
            wnd.minSize = new Vector2(640, 360);
        }

        public int selectedTemplateID = 0;
        public TmplButton selectedTemplateButton =>
            selectedTemplateID < templateButtons.Count ? templateButtons[selectedTemplateID] : null;

        public int selectedFilterID = 0;
        public FilterButton selectedFilterButton =>
            selectedFilterID < filterButtons.Count ? filterButtons[selectedFilterID] : null;

        public void CreateGUI()
        {
            buildUI();
            registerUIEvents();
            Refresh();
            selectTemplateID(0);
        }

        private void buildUI()
        {
            VisualElement root = rootVisualElement;
            var m_VisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/TmplBroswer.uxml")
            );

            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            var _builtinTemplateButton = new TmplButton();
            _builtinTemplateButton.TemplateID = BuiltInTemplateID;

            _builtinTemplateButton.Q<Label>("title").text = "公用模板";
            _builtinTemplateButton.style.flexGrow = 0;
            _builtinTemplateButton.style.flexShrink = 0;
            var _templateListRoot = labelFromUXML.Q<VisualElement>("template-list");
            _templateListRoot.parent.Insert(0, _builtinTemplateButton);
            templateButtons.Add(_builtinTemplateButton);

            if (!Directory.Exists(UNIArtSettings.Project.TemplateLocalFolder))
            {
                Directory.CreateDirectory(UNIArtSettings.Project.TemplateLocalFolder);
                AssetDatabase.Refresh();
            }

            _builtinTemplateButton.Refresh();
            if (!_builtinTemplateButton.IsInstalled)
            {
                Debug.Log($"尝试安装公用模板...");
                SVNIntegration.AddExternal(
                    UNIArtSettings.Project.TemplateLocalFolder,
                    UNIArtSettings.GetExternalTemplateFolderUrl(BuiltInTemplateID)
                );
                SVNIntegration.Update(UNIArtSettings.Project.TemplateLocalFolder);
                _builtinTemplateButton.Refresh();
                if (_builtinTemplateButton.IsInstalled)
                {
                    Debug.Log($"公用模板安装成功.");
                }
                else
                {
                    Debug.LogWarning($"公用模板安装失败.");
                }
            }
        }

        private void registerUIEvents()
        {
            var root = rootVisualElement;

            // 窗口高度自适应
            root.Q<VisualElement>("root").style.height = position.height;
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                root.Q<VisualElement>("root").style.height = position.height;
            });

            // 安装模板
            root.Q<Button>("btn_install")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    SVNIntegration.AddExternal(
                        UNIArtSettings.Project.TemplateLocalFolder,
                        selectedTemplateButton.ExternalRepoUrl
                    );
                    selectedTemplateButton.Refresh();
                    refreshTemplateContent();
                });

            // 移除模板
            root.Q<Button>("btn_uninstall")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    SVNIntegration.RemoveExternal(
                        UNIArtSettings.Project.TemplateLocalFolder,
                        UNIArtSettings.GetExternalTemplateFolderUrl(
                            selectedTemplateButton.TemplateID
                        )
                    );
                    selectedTemplateButton.Refresh();
                    refreshTemplateContent();
                });

            // 资源视图缩放
            root.Q<Slider>("asset_zoom")
                .RegisterValueChangedCallback<float>(evt =>
                {
                    var _val = evt.newValue;
                    assetItems.ForEach(_asset => _asset.SetZoom(_val));
                });
            root.Q<Slider>("asset_zoom").SetValueWithoutNotify(50f);

            root.Q<Button>("btn_updateAll")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    var _externals = SVNIntegration.GetExternals(
                        UNIArtSettings.Project.TemplateLocalFolder
                    );
                    templateButtons
                        .Where(_button => !_externals.Any(_ => _.Dir == _button.TemplateID))
                        .ToList()
                        .ForEach(_ => _.CleanDir());
                    if (SVNIntegration.Update(UNIArtSettings.Project.TemplateLocalFolder))
                    {
                        var lastTemplateID = selectedTemplateID;
                        Refresh();
                        selectTemplateID(lastTemplateID);
                        Debug.Log("模板库更新完成.");
                    }
                });

            // 更新模板资源
            root.Q<Button>("btn_update")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (selectedTemplateButton?.Pull() ?? false)
                    {
                        SVNIntegration.AddExternal(
                            UNIArtSettings.Project.TemplateLocalFolder,
                            selectedTemplateButton.ExternalRepoUrl
                        );
                        selectTemplateID(selectedTemplateID);
                        Debug.Log($"模板资源[{selectedTemplateButton.TemplateID}]更新完成.");
                    }
                    else
                    {
                        Debug.LogWarning($"模板资源[{selectedTemplateButton.TemplateID}]更新失败.");
                    }
                });

            // 新建UI页面
            root.Q<Button>("btn_newUI")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    WorkflowUtility.CreateUIPrefab();
                });

            // UI页面列表
            root.Q<Button>("btn_uiList")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    WorkflowUtility.ShowUIList();
                });

            Action<PrefabStage> refreshLocationButtonState = (_) =>
            {
                var _prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                root.Q<Button>("btn_location").SetEnabled(_prefabStage != null);
            };

            PrefabStage.prefabStageOpened += refreshLocationButtonState;
            PrefabStage.prefabStageClosing += refreshLocationButtonState;
            refreshLocationButtonState(null);

            root.Q<Button>("btn_location")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    WorkflowUtility.LocationPrefab();
                });

            root.RegisterCallback<MouseDownEvent>(evt =>
            {
                selectedAssetItem?.Deselect();
            });

            rootVisualElement.RegisterCallback<WheelEvent>(evt =>
            {
                if ((evt.modifiers & EventModifiers.Control) != 0)
                {
                    float delta = evt.delta.y;
                    root.Q<Slider>("asset_zoom").value -= delta * 1.6f; // 调整灵敏度
                }
            });

            var contentView = rootVisualElement.Q<VisualElement>("template-content");
            contentView.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            contentView.RegisterCallback<DragPerformEvent>(OnDragPerform);

            contentView.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.R && evt.ctrlKey)
                {
                    Debug.LogWarning("Refreshing...");
                    refreshTemplateAssets();
                }
            });
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Length < 0)
            {
                return;
            }
            var _currentTmplPath = CurrentTmplPath;

            if (
                DragAndDrop.paths.Any(
                    _ =>
                        _ == selectedAssetItem?.AssetPath
                        || Path.GetDirectoryName(_).ToForwardSlash()
                            == _currentTmplPath.TrimEnd('/')
                )
            )
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy; // 允许拖拽复制操作
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            var _currentTmplPath = CurrentTmplPath;
            if (string.IsNullOrEmpty(_currentTmplPath))
            {
                return;
            }

            Utils.MoveAssetsWithDependencies(DragAndDrop.paths, _currentTmplPath, true);
            refreshTemplateAssets();
            DragAndDrop.AcceptDrag();
        }

        public static int CalculatePreviewDir(
            Vector2 windowSize,
            Vector2 previewSize,
            Vector2 mousePosition
        )
        {
            Vector2 previewPosition = mousePosition;
            int _xDir = -1,
                _yDir = -1;

            float spaceAbove = mousePosition.y;
            float spaceBelow = windowSize.y - mousePosition.y;
            float spaceLeft = mousePosition.x;
            float spaceRight = windowSize.x - mousePosition.x;

            if (spaceAbove < spaceBelow)
            {
                _yDir = 1;
            }
            if (spaceLeft < spaceRight)
            {
                _xDir = 1;
            }
            if (_xDir == -1 && _yDir == -1)
            {
                return 1;
            }
            else if (_xDir == 1 && _yDir == -1)
            {
                return 2;
            }
            else if (_xDir == -1 && _yDir == 1)
            {
                return 3;
            }
            else if (_xDir == 1 && _yDir == 1)
            {
                return 4;
            }
            return 4;
        }

        public void ShowAssetPreviewTooltip(Texture2D preview, Vector2 position)
        {
            if (preview == null)
            {
                ClearAssetPreviewTooltip();
                return;
            }
            Vector2 mousePosition = position;

            Vector2 windowSize = rootVisualElement.contentRect.size;

            // 修补大小
            int fixedSize = 19;

            var _maxSize = windowSize * 0.5f;

            var _previewTex = rootVisualElement.Q<VisualElement>("img_preview");

            var imageSize = Utils.CalculateScaledImageSize(
                _maxSize.x,
                _maxSize.y,
                preview.width,
                preview.height
            );

            _previewTex.style.width = imageSize.width;
            _previewTex.style.height = imageSize.height;

            var dir = CalculatePreviewDir(
                windowSize,
                new Vector2(imageSize.width, imageSize.height),
                mousePosition
            );

            Vector2 padding = Vector2.one * 2;
            if (dir == 3) // 左下
            {
                _previewTex.style.left = StyleKeyword.Auto;
                _previewTex.style.right = windowSize.x - mousePosition.x + padding.x;
                _previewTex.style.top = mousePosition.y + padding.y - fixedSize;
                _previewTex.style.bottom = StyleKeyword.Auto;
            }
            else if (dir == 4) // 右下
            {
                _previewTex.style.left = mousePosition.x + padding.x;
                _previewTex.style.right = StyleKeyword.Auto;
                _previewTex.style.top = mousePosition.y + padding.y - fixedSize;
                _previewTex.style.bottom = StyleKeyword.Auto;
            }
            else if (dir == 1) // 左上
            {
                _previewTex.style.left = StyleKeyword.Auto;
                _previewTex.style.right = windowSize.x - mousePosition.x + padding.x;
                _previewTex.style.top = StyleKeyword.Auto;
                _previewTex.style.bottom = windowSize.y - mousePosition.y + padding.y + fixedSize;
            }
            else if (dir == 2) // 右上
            {
                _previewTex.style.left = mousePosition.x + padding.x;
                _previewTex.style.right = StyleKeyword.Auto;
                _previewTex.style.top = StyleKeyword.Auto;
                _previewTex.style.bottom = windowSize.y - mousePosition.y + padding.y + fixedSize;
            }

            _previewTex.style.display = DisplayStyle.Flex;
            _previewTex.style.backgroundImage = preview;
        }

        public void ClearAssetPreviewTooltip()
        {
            var _previewTex = rootVisualElement.Q<VisualElement>("img_preview");
            _previewTex.style.display = DisplayStyle.None;
        }

        private void selectTemplateID(int id)
        {
            selectedTemplateID = id;
            var _templateButton = templateButtons[id];

            templateButtons.ForEach(_t => _t.Deselect());
            _templateButton.Select();

            refreshTemplateContent();
            selectFilterID(0);
        }

        private void selectFilterID(int id)
        {
            if (!selectedTemplateButton.IsInstalled)
                return;

            selectedFilterID = id;
            var _filterButton = filterButtons[id];

            filterButtons.ForEach(_f => _f.Deselect());
            _filterButton.Select();
            refreshTemplateAssets();
        }

        // 刷新模板内容
        private void refreshTemplateContent()
        {
            var _templateContent = rootVisualElement.Q<VisualElement>("template-content");
            var _templateMgr = rootVisualElement.Q<VisualElement>("template-mgr");

            if (selectedTemplateButton.IsInstalled)
            {
                _templateContent.style.display = DisplayStyle.Flex;
                _templateMgr.style.display = DisplayStyle.None;

                refreshTemplateFilters();
            }
            else
            {
                _templateContent.style.display = DisplayStyle.None;
                _templateMgr.style.display = DisplayStyle.Flex;
            }
        }

        // 模板库按钮列表
        public List<TmplButton> templateButtons = new List<TmplButton>();

        // 模板筛选按钮列表
        public List<FilterButton> filterButtons = new List<FilterButton>();

        private void refreshTemplateMenuList()
        {
            var _svnTemplateList = SVNConextMenu
                .GetRepoFolders("http://svn.andcrane.com/repo/UNIArtTemplates")
                .Except(new string[] { BuiltInTemplateID })
                .ToList();

            var _templateListRoot = rootVisualElement.Q<VisualElement>("template-list");
            _templateListRoot.Clear();
            templateButtons.RemoveRange(1, templateButtons.Count - 1);

            _svnTemplateList.ForEach(_item =>
            {
                var _templateButton = new TmplButton();
                _templateButton.Q<Label>("title").text = _item;
                _templateButton.TemplateID = _item;
                _templateListRoot.Add(_templateButton);
                templateButtons.Add(_templateButton);
            });

            templateButtons.ForEach(
                _t =>
                    _t.RegisterCallback<MouseDownEvent>(
                        evt => selectTemplateID(templateButtons.IndexOf(_t))
                    )
            );
        }

        private void validateTemplateID()
        {
            if (selectedTemplateID < 0 || selectedTemplateID >= templateButtons.Count)
            {
                selectedTemplateID = 0;
            }
        }

        private void validateFilterID()
        {
            if (selectedFilterID < 0 || selectedFilterID >= filterButtons.Count)
            {
                selectedFilterID = 0;
            }
        }

        // 刷新模板筛选列表
        private void refreshTemplateFilters()
        {
            validateTemplateID();
            var _templateRoot =
                $"{UNIArtSettings.GetExternalTemplateFolder(selectedTemplateButton.TemplateID)}/Prefabs";

            var _templateAssetTypes = Directory.Exists(_templateRoot)
                ? Directory
                    .GetDirectories(_templateRoot, "*", SearchOption.TopDirectoryOnly)
                    .Select(_ => Path.GetFileName(_))
                    .Where(_ => !_.StartsWith("."))
                    .ToList()
                : new List<string>();

            var _filterTags = rootVisualElement.Q<VisualElement>("filter-tags");
            _filterTags.Clear();
            filterButtons.Clear();

            _templateAssetTypes.Insert(0, "全部");
            _templateAssetTypes.ForEach(_dir =>
            {
                var _filterButton = new FilterButton() { FilterID = _dir };
                _filterButton.Q<Label>("title").text = _dir;
                _filterTags.Add(_filterButton);
                filterButtons.Add(_filterButton);
            });

            filterButtons.ForEach(_filterButton =>
            {
                _filterButton.RegisterCallback<MouseDownEvent>(evt =>
                {
                    selectFilterID(filterButtons.IndexOf(_filterButton));
                });
            });

            validateFilterID();
            selectFilterID(selectedFilterID);

            rootVisualElement
                .Q<Button>("btn_uninstall")
                .SetEnabled(!selectedTemplateButton.IsBuiltIn);
        }

        List<AssetItem> assetItems = new List<AssetItem>();

        // 当前选中的资源项
        public static AssetItem selectedAssetItem = null;

        public string CurrentTmplPath => currentTmplPath();

        private string currentTmplPath()
        {
            var _tmplRoot = selectedTemplateButton.RootFolder;
            var _filterPath = selectedFilterButton.FilterPath;
            return $"{_tmplRoot}/Prefabs/{_filterPath}";
        }

        private void refreshTemplateAssets()
        {
            var _templateID = selectedTemplateButton.TemplateID;
            validateFilterID();
            var _filterID = selectedFilterButton.FilterID;
            _filterID = _filterID == "全部" ? string.Empty : _filterID;
            var buttonContainer = rootVisualElement.Q<VisualElement>("asset-list");
            buttonContainer.Clear();
            assetItems.Clear();

            var _templateRoot =
                $"{UNIArtSettings.Project.TemplateLocalFolder}/{_templateID}/Prefabs/{_filterID}";

            if (!Directory.Exists(_templateRoot))
            {
                Directory.CreateDirectory(_templateRoot);
                AssetDatabase.Refresh();
            }

            var _assets = AssetDatabase
                .FindAssets("t:prefab", new string[] { _templateRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            _assets.ForEach(_path =>
            {
                var _obj = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                var _assetItem = new AssetItem() { AssetPath = _path };
                buttonContainer.Add(_assetItem);
                assetItems.Add(_assetItem);

                _assetItem.RegisterCallback<MouseDownEvent>(evt =>
                {
                    evt.StopPropagation();
                    assetItems.ForEach(_ => _.Deselect());
                    _assetItem.Select();
                    selectedAssetItem = _assetItem;
                });

                _assetItem.OnShowPreview.AddListener(
                    (_tex, _pos) => ShowAssetPreviewTooltip(_tex, _pos)
                );
                _assetItem.OnHidePreview.AddListener(_ => ClearAssetPreviewTooltip());
            });
        }

        public void Refresh()
        {
            refreshTemplateMenuList();
            refreshTemplateFilters();
            refreshTemplateAssets();
        }

        private void OnFocus() { }

        private void OnLostFocus()
        {
            selectedAssetItem?.Deselect();
            selectedAssetItem = null;
        }
    }
}
