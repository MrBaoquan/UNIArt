using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.EditorCoroutines.Editor;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public class TmplBrowser : EditorWindow
    {
        const string EditorSelectTemplateIDKey = "LastSelectedTemplateID";

        // const string BuiltInTemplateID = "Standard";

        [MenuItem("Window/UNIArt 工作台 &1", priority = 1399)] //1499
        public static void ShowUNIArtWindow()
        {
            TmplBrowser wnd = GetWindow<TmplBrowser>();
            wnd.titleContent = new GUIContent(
                "UNIArt 工作台",
                EditorGUIUtility.IconContent("Folder Icon").image
            );
            wnd.minSize = new Vector2(640, 360);
        }

        public static void RefreshContentView()
        {
            TmplBrowser wnd = GetWindow<TmplBrowser>(false, "UNIArt 共创平台", false);
            wnd.RefreshTemplateFilters();
        }

        public int SelectedTemplateID
        {
            get => SessionState.GetInt(EditorSelectTemplateIDKey, 1);
            set => SessionState.SetInt(EditorSelectTemplateIDKey, value);
        }
        public TmplButton selectedTemplateButton =>
            SelectedTemplateID < templateButtons.Count ? templateButtons[SelectedTemplateID] : null;

        public int selectedFilterID = 0;
        public FilterButton selectedFilterButton =>
            selectedFilterID < filterButtons.Count ? filterButtons[selectedFilterID] : null;

        public void CreateGUI()
        {
            buildUI();
            registerUIEvents();
            Refresh();
            selectTemplate(SelectedTemplateID);
        }

        private void buildUI()
        {
            VisualElement root = rootVisualElement;
            var m_VisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/TmplBroswer.uxml")
            );

            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            var _templateListRoot = labelFromUXML.Q<VisualElement>("template-list");

            var _localTemplateButton = new TmplButton();
            _localTemplateButton.TemplateID = TmplButton.LocalTemplateTitle;
            _localTemplateButton.style.flexGrow = 0;
            _localTemplateButton.style.flexShrink = 0;
            _templateListRoot.parent.Insert(0, _localTemplateButton);
            templateButtons.Add(_localTemplateButton);

            var _builtinTemplateButton = new TmplButton();
            _builtinTemplateButton.TemplateID = TmplButton.BuiltInTemplateID;
            _builtinTemplateButton.style.flexGrow = 0;
            _builtinTemplateButton.style.flexShrink = 0;

            _templateListRoot.parent.Insert(1, _builtinTemplateButton);
            templateButtons.Add(_builtinTemplateButton);

            if (!Directory.Exists(UNIArtSettings.Project.TemplateLocalFolder))
            {
                Directory.CreateDirectory(UNIArtSettings.Project.TemplateLocalFolder);
                AssetDatabase.Refresh();
            }

            _builtinTemplateButton.Refresh();
            if (
                UNIArtSettings.Project.InstallStandardDefault && !_builtinTemplateButton.IsInstalled
            )
            {
                Debug.Log($"尝试安装公用模板...");
                SVNIntegration.AddExternal(
                    UNIArtSettings.Project.TemplateLocalFolder,
                    UNIArtSettings.GetExternalTemplateFolderUrl(TmplButton.BuiltInTemplateID)
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
            previewTex = rootVisualElement.Q<VisualElement>("img_preview");

            ToolbarSearchField searchField = rootVisualElement.Q<ToolbarSearchField>(
                "asset-search"
            );

            EditorCoroutine searchCoroutine = null;
            searchField.RegisterValueChangedCallback(evt =>
            {
                // selectedTemplateButton.SearchFilter.Value = evt.newValue;
                if (searchCoroutine != null)
                {
                    EditorCoroutineUtility.StopCoroutine(searchCoroutine);
                }
                searchCoroutine = EditorCoroutineUtility.StartCoroutine(
                    delaySearchFilter(
                        evt.newValue,
                        () =>
                        {
                            searchCoroutine = null;
                        }
                    ),
                    this
                );
            });

            var toolbarMenu = rootVisualElement.Q<ToolbarMenu>("type-filter-menu");

            // // 添加菜单项
            toolbarMenu.menu.AppendAction(
                "全部",
                action => selectedTemplateButton.FilterMode.Value = AssetFilterMode.All,
                action =>
                    selectedTemplateButton.FilterMode.Value == AssetFilterMode.All
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            toolbarMenu.menu.AppendAction(
                "预制体",
                action => selectedTemplateButton.FilterMode.Value = AssetFilterMode.Prefab,
                action =>
                    selectedTemplateButton.FilterMode.Value == AssetFilterMode.Prefab
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );
            toolbarMenu.menu.AppendAction(
                "图片",
                action => selectedTemplateButton.FilterMode.Value = AssetFilterMode.Texture,
                action =>
                    selectedTemplateButton.FilterMode.Value == AssetFilterMode.Texture
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            var filterDirButton = rootVisualElement.Q<ToolbarButton>("btn-filter-dir");
            filterDirButton.RegisterCallback<MouseUpEvent>(evt =>
            {
                selectedTemplateButton.SearchTopFolderOnly =
                    !selectedTemplateButton.SearchTopFolderOnly;

                refreshTemplateView();
            });
        }

        private void refreshFilterDirButtonStyle()
        {
            var filterDirButton = rootVisualElement.Q<ToolbarButton>("btn-filter-dir");
            if (selectedTemplateButton.SearchTopFolderOnly)
            {
                filterDirButton.RemoveFromClassList("filter-dir-off");
                filterDirButton.AddToClassList("filter-dir-on");
                filterDirButton.tooltip = "查看所有文件夹";
            }
            else
            {
                filterDirButton.RemoveFromClassList("filter-dir-on");
                filterDirButton.AddToClassList("filter-dir-off");
                filterDirButton.tooltip = "仅查看主要文件夹";
            }
        }

        private IEnumerator delaySearchFilter(string filter, Action finishCallback = null)
        {
            yield return new EditorWaitForSeconds(0.5f);
            selectedTemplateButton.SearchFilter.Value = filter;
            finishCallback?.Invoke();
        }

        private void syncToolbarMenuStatus()
        {
            var toolbarMenu = rootVisualElement.Q<ToolbarMenu>("type-filter-menu");
            toolbarMenu.RemoveFromClassList("filter-menu-all");
            toolbarMenu.RemoveFromClassList("filter-menu-prefab");
            toolbarMenu.RemoveFromClassList("filter-menu-texture");
            if (selectedTemplateButton.FilterMode.Value == AssetFilterMode.All)
            {
                toolbarMenu.AddToClassList("filter-menu-all");
            }
            else if (selectedTemplateButton.FilterMode.Value == AssetFilterMode.Prefab)
            {
                toolbarMenu.AddToClassList("filter-menu-prefab");
            }
            else if (selectedTemplateButton.FilterMode.Value == AssetFilterMode.Texture)
            {
                toolbarMenu.AddToClassList("filter-menu-texture");
            }

            ToolbarSearchField searchField = rootVisualElement.Q<ToolbarSearchField>(
                "asset-search"
            );
            searchField.SetValueWithoutNotify(selectedTemplateButton.SearchFilter.Value);
        }

        VisualElement previewTex = null;

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
                    refreshTemplateView();
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
                    refreshTemplateView();
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
                        var lastTemplateID = SelectedTemplateID;
                        Refresh();
                        selectTemplate(lastTemplateID);
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
                        selectTemplate(SelectedTemplateID);
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
                    WorkflowUtility.LocationStagePrefab();
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

            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.R && evt.ctrlKey)
                {
                    RefreshTemplateFilters();
                }
            });
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Length < 0)
            {
                return;
            }
            if (DragAndDrop.GetGenericData("AssetItem") != null)
            {
                return;
            }
            var _currentTmplPath = CurrentTmplPath;
            if (
                DragAndDrop.paths.Any(
                    _ => Path.GetDirectoryName(_).ToForwardSlash() == _currentTmplPath.TrimEnd('/')
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

            Utils.MoveAssetsWithDependencies(
                DragAndDrop.paths,
                _currentTmplPath.ToForwardSlash().TrimEnd('/'),
                true
            );
            DragAndDrop.AcceptDrag();
            RefreshTemplateFilters();
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

            var imageSize = Utils.CalculateScaledImageSize(
                _maxSize.x,
                _maxSize.y,
                preview.width,
                preview.height
            );

            var dir = CalculatePreviewDir(
                windowSize,
                new Vector2(imageSize.width, imageSize.height),
                mousePosition
            );

            Vector2 padding = Vector2.one * 2;

            if (dir == 3) // 左下
            {
                previewTex.style.transformOrigin = new TransformOrigin(
                    new Length(100, LengthUnit.Percent),
                    new Length(0, LengthUnit.Percent),
                    0
                );
                previewTex.style.left = StyleKeyword.Auto;
                previewTex.style.right = windowSize.x - mousePosition.x + padding.x;
                previewTex.style.top = mousePosition.y + padding.y - fixedSize;
                previewTex.style.bottom = StyleKeyword.Auto;
            }
            else if (dir == 4) // 右下
            {
                previewTex.style.transformOrigin = new TransformOrigin(
                    new Length(0, LengthUnit.Percent),
                    new Length(0, LengthUnit.Percent),
                    0
                );
                previewTex.style.left = mousePosition.x + padding.x;
                previewTex.style.right = StyleKeyword.Auto;
                previewTex.style.top = mousePosition.y + padding.y - fixedSize;
                previewTex.style.bottom = StyleKeyword.Auto;
            }
            else if (dir == 1) // 左上
            {
                previewTex.style.transformOrigin = new TransformOrigin(
                    new Length(100, LengthUnit.Percent),
                    new Length(100, LengthUnit.Percent),
                    0
                );

                previewTex.style.left = StyleKeyword.Auto;
                previewTex.style.right = windowSize.x - mousePosition.x + padding.x;
                previewTex.style.top = StyleKeyword.Auto;
                previewTex.style.bottom = windowSize.y - mousePosition.y + padding.y + fixedSize;
            }
            else if (dir == 2) // 右上
            {
                previewTex.style.transformOrigin = new TransformOrigin(
                    new Length(0, LengthUnit.Percent),
                    new Length(100, LengthUnit.Percent),
                    0
                );
                previewTex.style.left = mousePosition.x + padding.x;
                previewTex.style.right = StyleKeyword.Auto;
                previewTex.style.top = StyleKeyword.Auto;
                previewTex.style.bottom = windowSize.y - mousePosition.y + padding.y + fixedSize;
            }

            previewTex.style.width = imageSize.width;
            previewTex.style.height = imageSize.height;
            previewTex.style.backgroundImage = preview;
            previewTex.ApplyScaleOne();
        }

        public void ClearAssetPreviewTooltip()
        {
            previewTex.ApplyScaleZero();
        }

        private void selectTemplate(int id)
        {
            SelectedTemplateID = id;
            validateTemplateID();

            templateButtons.ForEach(_t => _t.Deselect());
            selectedTemplateButton.Select();

            refreshTemplateView();
        }

        private void setTemplateFilter(int filterID)
        {
            if (!selectedTemplateButton.IsInstalled && !selectedTemplateButton.IsLocal)
                return;

            selectedTemplateButton.FilterID = filterID;
            selectedFilterID = filterID;

            filterButtons.ForEach(_f => _f.Deselect());
            selectedFilterButton?.Select();
            refreshTemplateAssets();
        }

        // 刷新模板内容
        private void refreshTemplateView()
        {
            var _templateContent = rootVisualElement.Q<VisualElement>("template-content");
            var _templateMgr = rootVisualElement.Q<VisualElement>("template-mgr");

            if (selectedTemplateButton.IsInstalled || selectedTemplateButton.IsLocal)
            {
                _templateContent.style.display = DisplayStyle.Flex;
                _templateMgr.style.display = DisplayStyle.None;

                RefreshTemplateFilters();
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
                .Except(new string[] { TmplButton.BuiltInTemplateID })
                .ToList();

            var _templateListRoot = rootVisualElement.Q<VisualElement>("template-list");
            _templateListRoot.Clear();
            templateButtons.RemoveRange(2, templateButtons.Count - 2);

            _svnTemplateList.ForEach(_item =>
            {
                var _templateButton = new TmplButton();
                _templateButton.TemplateID = _item;
                _templateListRoot.Add(_templateButton);
                templateButtons.Add(_templateButton);
            });

            templateButtons.ForEach(
                _t =>
                    _t.RegisterCallback<MouseDownEvent>(
                        evt => selectTemplate(templateButtons.IndexOf(_t))
                    )
            );
        }

        private int validateTemplateID()
        {
            if (SelectedTemplateID < 0 || SelectedTemplateID >= templateButtons.Count)
            {
                SelectedTemplateID = 0;
            }
            return SelectedTemplateID;
        }

        private void validateFilterID()
        {
            if (selectedFilterID < 0 || selectedFilterID >= filterButtons.Count)
            {
                selectedFilterID = 0;
            }
        }

        // 刷新模板筛选列表
        public void RefreshTemplateFilters()
        {
            validateTemplateID();
            refreshFilterDirButtonStyle();

            var _filterTags = rootVisualElement.Q<VisualElement>("filter-tags");
            _filterTags.Clear();
            filterButtons.Clear();

            var _templateAssetTags = selectedTemplateButton.FilterTags();

            _templateAssetTags.Insert(0, string.Empty);
            _templateAssetTags.ForEach(_dir =>
            {
                var _filterButton = new FilterButton() { FilterID = _dir };
                _filterTags.Add(_filterButton);
                filterButtons.Add(_filterButton);
            });

            filterButtons.ForEach(_filterButton =>
            {
                _filterButton.RegisterCallback<MouseDownEvent>(evt =>
                {
                    setTemplateFilter(filterButtons.IndexOf(_filterButton));
                });
            });

            validateFilterID();
            setTemplateFilter(selectedTemplateButton.FilterID);

            rootVisualElement
                .Q<Button>("btn_uninstall")
                .SetEnabled(
                    !(
                        selectedTemplateButton.IsBuiltIn
                        && UNIArtSettings.Project.InstallStandardDefault
                    )
                );
            syncToolbarMenuStatus();

            selectedTemplateButton.FilterMode.OnValueChanged.RemoveAllListeners();
            selectedTemplateButton.FilterMode.OnValueChanged.AddListener(_ =>
            {
                syncToolbarMenuStatus();
                refreshTemplateAssets();
            });

            selectedTemplateButton.SearchFilter.OnValueChanged.RemoveAllListeners();
            selectedTemplateButton.SearchFilter.OnValueChanged.AddListener(_ =>
            {
                refreshTemplateAssets();
            });
        }

        List<AssetItem> assetItems = new List<AssetItem>();

        // 当前选中的资源项
        public static AssetItem selectedAssetItem = null;

        public string CurrentTmplPath => currentTmplPath();

        private string currentTmplPath()
        {
            var _tmplRoot = selectedTemplateButton.RootFolder;
            var _filterPath = selectedFilterButton.FilterID;
            return $"{_tmplRoot}/Prefabs/{_filterPath}".TrimEnd('/');
        }

        EditorCoroutine _refreshAssetsCoroutine = null;

        private void refreshTemplateAssets()
        {
            if (_refreshAssetsCoroutine != null)
            {
                EditorCoroutineUtility.StopCoroutine(_refreshAssetsCoroutine);
            }
            _refreshAssetsCoroutine = EditorCoroutineUtility.StartCoroutine(
                refreshTemplateAssetsAsync(),
                this
            );
        }

        private IEnumerator refreshTemplateAssetsAsync()
        {
            var _templateID = selectedTemplateButton.TemplateID;
            validateFilterID();
            var _filterID = selectedFilterButton.FilterID;

            var buttonContainer = rootVisualElement.Q<VisualElement>("asset-list");
            buttonContainer.Clear();
            assetItems.Clear();

            string pattern = @$".*{selectedTemplateButton.SearchFilter.Value}.*";

            var _assets = AssetDatabase
                .FindAssets(
                    selectedTemplateButton.filterArgs(),
                    selectedTemplateButton.FilterRootPaths(_filterID)
                )
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(
                    _path =>
                        string.IsNullOrEmpty(selectedTemplateButton.SearchFilter.Value)
                            ? true
                            : Regex.IsMatch(_path, pattern, RegexOptions.IgnoreCase)
                )
                .ToList();

            foreach (var _path in _assets)
            {
                var _obj = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                var _assetItem = new AssetItem() { AssetPath = _path };
                buttonContainer.Add(_assetItem);
                assetItems.Add(_assetItem);
                yield return new EditorWaitForSeconds(0.002f);
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
            }
        }

        public void Refresh()
        {
            refreshTemplateMenuList();
            RefreshTemplateFilters();
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
