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
        [MenuItem("Window/UNIArt 工作台 &1", priority = 1399)] //1499
        public static void ShowUNIArtWindow()
        {
            Instance.minSize = new Vector2(640, 360);
        }

        public static TmplBrowser Instance
        {
            get
            {
                TmplBrowser wnd = GetWindow<TmplBrowser>();
                wnd.titleContent = new GUIContent(
                    "UNIArt 工作台",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Packages/com.parful.uniart/Assets/Icon/艺术.png"
                    )
                );
                return wnd;
            }
        }

        public static bool IsWindowOpen => HasOpenInstances<TmplBrowser>();

        public static void RefreshContentView()
        {
            if (!IsWindowOpen)
            {
                return;
            }
            Instance.RefreshTemplateFilters();
        }

        public int SelectedTemplateID
        {
            get
            {
                var _idx = templateButtons.FindIndex(
                    _ => _.TemplateID == UNIArtSettings.Project.LastSelectedTemplateID
                );
                return _idx == -1 ? 0 : _idx;
            }
            set
            {
                if (value < 0 || value >= templateButtons.Count)
                {
                    return;
                }
                UNIArtSettings.Project.LastSelectedTemplateID = templateButtons[value].TemplateID;
            }
        }

        public TmplButton selectedTemplateButton =>
            SelectedTemplateID < templateButtons.Count ? templateButtons[SelectedTemplateID] : null;

        public int selectedFilterID => selectedTemplateButton?.FilterID ?? 0;
        public FilterButton selectedFilterButton =>
            selectedFilterID < filterButtons.Count ? filterButtons[selectedFilterID] : null;

        public List<AssetItem> selectedAssets => assetItems.Where(item => item.IsSelected).ToList();

#region 启动页面构造
        public void CreateGUI()
        {
            UNIArtSettings.Project.PullExternals();
            buildUI();
            registerUIEvents();
            Refresh();

            UPMUpdater.IsPackageLatest(
                "com.parful.uniart",
                (_isLatest, current, latest) =>
                {
                    rootVisualElement.Q<VisualElement>("update-dot").style.display = _isLatest
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                    rootVisualElement.Q<Button>("btn-version-update").tooltip = _isLatest
                        ? $"当前版本: {current}已是最新版本"
                        : $"当前版本: {current}\n最新版本: {latest}, 点击开始更新";
                }
            );
            // selectTemplate(SelectedTemplateID);
        }

        ReactiveProperty<string> templateFilter = new ReactiveProperty<string>();

        VisualElement dropView;
        VisualElement filterListRoot;

        ScrollView filterScrollView;

        ScrollView assetScrollView;
        ScrollView tmplScrollView;

        enum SelectType
        {
            Filter,
            Asset,
            None
        }

        SelectType lastSelect = SelectType.None;

        private EditorCoroutine installBuiltinCoroutine;

        private void buildUI()
        {
            VisualElement root = rootVisualElement;
            var m_VisualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/TmplBroswer.uxml")
            );

            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            dropView = root.Q<VisualElement>("drop-view");
            dropView.style.display = DisplayStyle.None;

            filterScrollView = rootVisualElement.Q<ScrollView>("tags-scrollView");
            filterListRoot = rootVisualElement.Q<VisualElement>("tag-list");
            assetScrollView = rootVisualElement.Q<ScrollView>("asset-scrollView");
            tmplScrollView = rootVisualElement.Q<ScrollView>("template-list");

            var _topListRoot = labelFromUXML.Q<VisualElement>("menu-top-list");

            var _localTemplateButton = new TmplButton()
            {
                TemplateID = TmplButton.LocalTemplateTitle
            };

            _localTemplateButton.style.flexGrow = 0;
            _localTemplateButton.style.flexShrink = 0;
            _topListRoot.Insert(0, _localTemplateButton);
            templateButtons.Add(_localTemplateButton);

            var _builtinTemplateButton = new TmplButton()
            {
                TemplateID = TmplButton.BuiltInTemplateID
            };

            _builtinTemplateButton.style.flexGrow = 0;
            _builtinTemplateButton.style.flexShrink = 0;

            _topListRoot.Insert(1, _builtinTemplateButton);
            templateButtons.Add(_builtinTemplateButton);

            installBuiltinCoroutine = EditorCoroutineUtility.StartCoroutine(
                delayInstallBuiltinTemplate(),
                this
            );

            previewTex = rootVisualElement.Q<VisualElement>("img_preview");

            ToolbarSearchField templateSearch = rootVisualElement.Q<ToolbarSearchField>(
                "template-search"
            );

            templateFilter.OnValueChanged.AddListener(_ =>
            {
                refreshTemplateMenuList();
            });
#region 资源区工具栏菜单
            var toolbarAddOrCreateMenu = rootVisualElement.Q<ToolbarMenu>("toolbar-add-menu");
            toolbarAddOrCreateMenu.menu.AppendAction(
                "新建文件夹",
                action =>
                {
                    // if (filterButtons.Count > 2)
                    // {
                    //     filterScrollView.ScrollTo(
                    //         filterButtons.Skip(filterButtons.Count - 2).Take(1).First()
                    //     );
                    // }
                    filterButtons.Last().DoEdit();
                    Utils.Delay(
                        () =>
                        {
                            filterScrollView.ScrollTo(filterButtons.Last());
                        },
                        0.1f
                    );
                }
            );
#endregion

            EditorCoroutine templateSearchCoroutine = null;
            templateSearch.RegisterValueChangedCallback(evt =>
            {
                if (templateSearchCoroutine != null)
                {
                    EditorCoroutineUtility.StopCoroutine(templateSearchCoroutine);
                }
                var _filter = evt.newValue;
                templateSearchCoroutine = EditorCoroutineUtility.StartCoroutine(
                    delaySearchFilter(() =>
                    {
                        templateFilter.Value = _filter;
                        templateSearchCoroutine = null;
                    }),
                    this
                );
            });

            ToolbarSearchField assetSearch = rootVisualElement.Q<ToolbarSearchField>(
                "asset-search"
            );

            EditorCoroutine assetSearchCoroutine = null;
            assetSearch.RegisterValueChangedCallback(evt =>
            {
                if (assetSearchCoroutine != null)
                {
                    EditorCoroutineUtility.StopCoroutine(assetSearchCoroutine);
                }
                var _filter = evt.newValue;
                assetSearchCoroutine = EditorCoroutineUtility.StartCoroutine(
                    delaySearchFilter(() =>
                    {
                        selectedTemplateButton.SearchFilter.Value = _filter;
                        assetSearchCoroutine = null;
                    }),
                    this
                );
            });

            var toolbarFilterMenu = rootVisualElement.Q<ToolbarMenu>("type-filter-menu");

            // // 添加菜单项
            toolbarFilterMenu.menu.AppendAction(
                "全部",
                action => selectedTemplateButton.FilterMode.Value = AssetFilterMode.All,
                action =>
                    selectedTemplateButton.FilterMode.Value == AssetFilterMode.All
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );

            toolbarFilterMenu.menu.AppendAction(
                "预制体",
                action => selectedTemplateButton.FilterMode.Value = AssetFilterMode.Prefab,
                action =>
                    selectedTemplateButton.FilterMode.Value == AssetFilterMode.Prefab
                        ? DropdownMenuAction.Status.Checked
                        : DropdownMenuAction.Status.Normal
            );
            toolbarFilterMenu.menu.AppendAction(
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

        IEnumerator delayInstallBuiltinTemplate()
        {
            yield return new EditorWaitForSeconds(0.5f);

            var _builtinTemplateButton = templateButtons.Skip(1).Take(1).FirstOrDefault();
            if (_builtinTemplateButton == null)
                yield break;

            UNIArtSettings.Project.PullExternals();
            _builtinTemplateButton.Refresh();

            if (UNIArtSettings.Project.InstallStandardDefault && !_builtinTemplateButton.AssetReady)
            {
                Utils.UnlockReloadDomain();
                // Debug.Log($"尝试安装基础组件库...");
                SVNIntegration.AddOrUpdateExternal(
                    UNIArtSettings.Project.TemplatePropTarget,
                    UNIArtSettings.GetExternalTemplateFolderUrl(TmplButton.BuiltInTemplateID)
                );
                if (_builtinTemplateButton.HasLocalEntity == false)
                {
                    // Debug.Log("基础组件库不存在, 尝试从远程拉取...");
                    SVNIntegration.Update(UNIArtSettings.Project.TemplatePropTarget);
                    yield return new EditorWaitForSeconds(0.1f);
                    AssetDatabase.Refresh();
                }

                _builtinTemplateButton.Refresh();
                if (_builtinTemplateButton.AssetReady)
                {
                    Debug.Log($"基础组件库安装成功.");
                }
            }
        }

#endregion

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

        private void refreshViewButtonStyle()
        {
            var _btnUpdate = rootVisualElement.Q<Button>("btn_update");
            var _btnCommit = rootVisualElement.Q<Button>("btn_commit");
            var _btnRevert = rootVisualElement.Q<Button>("btn_revert");
            if (selectedTemplateButton.IsLocal)
            {
                _btnCommit.tooltip = "提交项目资源";
                _btnUpdate.tooltip = "更新项目资源";
                _btnRevert.tooltip = "还原项目资源变更";
            }
            else
            {
                _btnCommit.tooltip = "提交资源库内容";
                _btnUpdate.tooltip = "更新资源库内容";
                _btnRevert.tooltip = "还原资源库内容变更";
            }
        }

        private IEnumerator delaySearchFilter(Action finishCallback = null)
        {
            yield return new EditorWaitForSeconds(0.5f);
            finishCallback?.Invoke();
        }

        private IEnumerator delayRebuildAssetPreview(AssetItem assetItem)
        {
            yield return new EditorWaitForSeconds(0.01f);
            assetItem?.RebuildPreview();
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

        private (bool HoverAsset, bool HoverFilter) GetHoverState(Vector2 evtPos)
        {
            var _originIsAsset = selectedAssets.Any(_ => _.worldBound.Contains(evtPos));
            var _originIsFilter =
                selectedFilterButton != null && selectedFilterButton.worldBound.Contains(evtPos);
            return (_originIsAsset, _originIsFilter);
        }

        public void ShowUIPageList()
        {
            selectTemplate(0);
            selectFilter(1);
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
                    Utils.LockReloadDomain();
                    SVNIntegration.AddOrUpdateExternal(
                        UNIArtSettings.Project.TemplatePropTarget,
                        selectedTemplateButton.ExternalRepoUrl
                    );

                    UNIArtSettings.Project.PullExternals();
                    refreshTemplateMenuList();
                    refreshTemplateView();
                    Utils.UnlockReloadDomain();

                    Utils.Delay(
                        () =>
                        {
                            if (selectedTemplateButton?.IsInScrollView ?? false)
                                tmplScrollView.ScrollTo(selectedTemplateButton);
                        },
                        0.1f
                    );
                });

            // 移除模板
            root.Q<Button>("btn_uninstall")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (SVNIntegration.IsWorkingCopyDirty(selectedTemplateButton.RootFolder))
                    {
                        // 弹出确认框
                        var _confirm = EditorUtility.DisplayDialog(
                            "确认",
                            $"[{selectedTemplateButton.TemplateID}]存在未提交资源，确定要移除吗？",
                            "确定",
                            "取消"
                        );
                        if (!_confirm)
                            return;
                    }

                    Utils.LockReloadDomain();
                    var _templateRootUrl = UNIArtSettings.GetExternalTemplateFolderUrl(
                        selectedTemplateButton.TemplateID
                    );
                    SVNIntegration.RemoveExternal(
                        UNIArtSettings.Project.TemplatePropTarget,
                        _templateRootUrl
                    );
                    UNIArtSettings.Project.PullExternals();
                    refreshTemplateMenuList();
                    refreshTemplateView();
                    Utils.UnlockReloadDomain();

                    Utils.Delay(
                        () =>
                        {
                            if (selectedTemplateButton?.IsInScrollView ?? false)
                                tmplScrollView.ScrollTo(selectedTemplateButton);
                        },
                        0.1f
                    );
                });

            // 资源视图缩放
            root.Q<Slider>("asset_zoom")
                .RegisterValueChangedCallback<float>(evt =>
                {
                    var _val = evt.newValue;
                    assetItems.ForEach(_asset => _asset.SetZoom(_val));
                });
            root.Q<Slider>("asset_zoom").SetValueWithoutNotify(50f);

            root.Q<Button>("btn-help")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    Application.OpenURL("http://wiki.andcrane.com:5152/zh/UNIArt");
                });

            root.Q<Button>("btn-version-update")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    UPMUpdater.UpdatePackage(
                        "com.parful.uniart",
                        () =>
                        {
                            Application.OpenURL(
                                "http://upm.andcrane.com:4873/-/web/detail/com.parful.uniart"
                            );
                        }
                    );
                });

            root.Q<Button>("btn_updateAll")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    UNIArtSettings.Project.PullExternals();
                    templateButtons
                        .Where(_ => !_.IsLocal)
                        .Where(_button => !UNIArtSettings.Project.HasExternal(_button.TemplateID))
                        .ToList()
                        .ForEach(_ => _.CleanDir());
                    if (SVNIntegration.Update(UNIArtSettings.Project.TemplatePropTarget))
                    {
                        var lastTemplateID = SelectedTemplateID;
                        Refresh();
                        selectTemplate(lastTemplateID);
                        Debug.Log("资源更新完成.");
                    }
                });

            root.Q<Button>("btn_revert")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    selectedTemplateButton.Revert();
                });

            root.Q<Button>("btn_commit")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    selectedTemplateButton.Commit();
                });

            root.Q<Button>("btn_resolve")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    selectedTemplateButton.Resolve();
                });

            root.Q<Button>("btn_clean")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    selectedTemplateButton.Clean();
                });

            root.Q<Label>("text_version")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    selectedTemplateButton.ShowLog();
                });

            // 更新模板资源
            root.Q<Button>("btn_update")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (selectedTemplateButton?.Pull() ?? false)
                    {
                        if (!selectedTemplateButton.IsLocal)
                        {
                            SVNIntegration.AddOrUpdateExternal(
                                UNIArtSettings.Project.TemplatePropTarget,
                                selectedTemplateButton.ExternalRepoUrl
                            );
                        }

                        selectTemplate(SelectedTemplateID);
                        Debug.Log($"资源[{selectedTemplateButton.TemplateID}]更新完成.");
                    }
                    else
                    {
                        Debug.LogWarning($"资源[{selectedTemplateButton.TemplateID}]更新失败.");
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
                    ShowUIPageList();
                });

            Action<PrefabStage> refreshLocationButtonState = (_prefabStage) =>
            {
                root.Q<Button>("btn_location")
                    .SetEnabled(PrefabStageUtility.GetCurrentPrefabStage() != null);

                if (_prefabStage is null || UNIArtSettings.Project.AutoUpdateUIPreview == false)
                    return;

                var _assetItem = assetItems.FirstOrDefault(
                    _ => _.AssetPath == _prefabStage.assetPath
                );
                if (_assetItem is null)
                    return;
                EditorCoroutineUtility.StartCoroutine(delayRebuildAssetPreview(_assetItem), this);
            };

            PrefabStage.prefabStageOpened += refreshLocationButtonState;
            PrefabStage.prefabStageClosing += refreshLocationButtonState;

            refreshLocationButtonState(null);

            root.Q<Button>("btn_location")
                .RegisterCallback<MouseUpEvent>(evt =>
                {
                    WorkflowUtility.LocationStagePrefab();
                });

#region 模板点击事件
            root.RegisterCallback<MouseDownEvent>(evt =>
            {
                var _hoverState = GetHoverState(evt.mousePosition);
                if (
                    evt.button == (int)MouseButton.RightMouse
                    && (_hoverState.HoverFilter || _hoverState.HoverAsset)
                )
                {
                    return;
                }

                selectedAssets.ForEach(_ => _.Deselect());
                selectedAsset = null;
            });
#endregion

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
            contentView.RegisterCallback<DragEnterEvent>(OnDragEnter);
            contentView.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            // contentView.RegisterCallback<MouseDownEvent>(evt =>
            // {
            //     // 右键菜单
            //     Debug.LogWarning("右键菜单");
            // });
#region 资源右键菜单
            Func<DropdownMenuAction, DropdownMenuAction.Status> activeIfGameObject = (action) =>
            {
                if (selectedAsset == null)
                    return DropdownMenuAction.Status.Disabled;
                return (selectedAsset.HasPSDEnity || selectedAsset.RawAssetObject is GameObject)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled;
            };

            Func<DropdownMenuAction, DropdownMenuAction.Status> activeIfThumb = (action) =>
            {
                if (selectedAsset == null)
                    return DropdownMenuAction.Status.Disabled;
                return selectedAsset.IsPrefab && selectedAsset.NonPSDPrefab
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled;
            };

            contentView.AddManipulator(
                new ContextualMenuManipulator(
                    (evt) =>
                    {
                        var _evtPos = evt.mousePosition;

                        var _hoverState = GetHoverState(_evtPos);
                        var _originIsAsset = _hoverState.HoverAsset;
                        var _originIsFilter = _hoverState.HoverFilter;

                        evt.menu.AppendAction(
                            "复制到项目/UI 页面",
                            (x) =>
                            {
                                WorkflowUtility.CopyPrefabToUIPage(selectedAsset.AssetPath);
                            },
                            activeIfGameObject
                        );
                        evt.menu.AppendSeparator("复制到项目/");
                        evt.menu.AppendAction(
                            "复制到项目/UI 组件",
                            (x) =>
                            {
                                WorkflowUtility.CopyPrefabToUIComponent(selectedAsset.AssetPath);
                            },
                            activeIfGameObject
                        );
                        evt.menu.AppendSeparator();

                        evt.menu.AppendAction(
                            "打开所在文件夹",
                            (x) =>
                            {
                                if (_originIsFilter)
                                {
                                    EditorUtility.RevealInFinder(CurrentRootPath);
                                    return;
                                }
                                EditorUtility.RevealInFinder(selectedAsset.rawAssetPath);
                            }
                        );

                        evt.menu.AppendAction(
                            "在资源视图中显示",
                            (x) =>
                            {
                                Utils.FocusProjectBrowser();
                                if (selectedAsset == null)
                                {
                                    EditorGUIUtility.PingObject(
                                        AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                                            CurrentRootPath
                                        )
                                    );
                                    return;
                                }
                                EditorGUIUtility.PingObject(selectedAsset.RawAssetObject);
                            }
                        );

                        evt.menu.AppendSeparator();

                        evt.menu.AppendAction(
                            "PS 文件/导入选项",
                            (x) =>
                            {
                                PSImportOptions.ShowPSOptions(selectedAsset.rawAssetPath);
                            },
                            _ =>
                                selectedAsset != null && selectedAsset.IsPSD
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );
                        evt.menu.AppendSeparator();
                        evt.menu.AppendAction(
                            "刷新 %R",
                            (x) =>
                            {
                                RefreshTemplateFilters();
                            },
                            DropdownMenuAction.AlwaysEnabled
                        );
                        evt.menu.AppendAction(
                            "重新导入",
                            (x) =>
                            {
                                selectedAssets.ForEach(_asset =>
                                {
                                    Utils.ReimportAsset(_asset.rawAssetPath);
                                });
                            },
                            _ =>
                                selectedAsset != null
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );

                        evt.menu.AppendSeparator();

                        evt.menu.AppendAction(
                            "重命名",
                            (x) =>
                            {
                                ClearAssetPreviewTooltip();
                                if (_originIsAsset)
                                {
                                    selectedAsset.DoEdit();
                                    return;
                                }

                                selectedFilterButton.DoEdit();
                            },
                            _ =>
                                _originIsAsset || _originIsFilter
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );

                        evt.menu.AppendAction(
                            "删除",
                            (x) =>
                            {
                                if (_originIsAsset)
                                {
                                    var _selectedAssets = selectedAssets.ToList();
                                    var _assetConfirmMsg = $"确认删除选中的{_selectedAssets.Count}个资源吗?";
                                    if (
                                        EditorUtility.DisplayDialog(
                                            "删除资源",
                                            _assetConfirmMsg,
                                            "确认",
                                            "取消"
                                        )
                                    )
                                    {
                                        _selectedAssets.ForEach(_ => _.Delete());
                                        refreshTemplateAssets();
                                    }
                                    return;
                                }

                                var _folderConfirm = $"确认删除文件夹[{selectedFilterButton.FilterID}]吗?";
                                if (
                                    EditorUtility.DisplayDialog("删除文件夹", _folderConfirm, "确认", "取消")
                                )
                                {
                                    List<string> fails = new List<string>();
                                    AssetDatabase.DeleteAssets(
                                        selectedTemplateButton.ValidFilterRootPaths(
                                            selectedFilterButton.FilterID
                                        ),
                                        fails
                                    );
                                    RefreshTemplateFilters();
                                }
                            },
                            _ =>
                                (_originIsAsset || !selectedFilterButton.IsAll)
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );

                        evt.menu.AppendSeparator();
                        evt.menu.AppendAction(
                            "缩略图/更新缩略图",
                            (x) =>
                            {
                                selectedAssets
                                    .Where(_ => _.NonPSDPrefab && _.IsPrefab)
                                    .ToList()
                                    .ForEach(_asset =>
                                    {
                                        _asset.RebuildPreview();
                                    });
                            },
                            _ =>
                                selectedAsset != null
                                    && selectedAsset.IsPrefab
                                    && selectedAsset.NonPSDPrefab
                                || selectedAssets.Where(_ => _.NonPSDPrefab && _.IsPrefab).Count()
                                    > 0
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );
                        evt.menu.AppendAction(
                            "缩略图/选取缩略图",
                            (x) =>
                            {
                                AssetDatabase.OpenAsset(selectedAsset.RawAssetObject);
                                SceneViewCapture.OnCapture(_rect =>
                                {
                                    var _savedPath = selectedAsset.PreviewPath;
                                    var _savedFolder = Path.GetDirectoryName(_savedPath);
                                    if (!Directory.Exists(_savedFolder))
                                    {
                                        Directory.CreateDirectory(_savedFolder);
                                    }

                                    SceneViewCapture.TakeScreenshot(
                                        _rect,
                                        selectedAsset.PreviewPath,
                                        () =>
                                        {
                                            selectedAsset.RefreshPreview();
                                        }
                                    );
                                });
                                SceneViewCapture.ShowCapture();
                            },
                            activeIfThumb
                        );

                        evt.menu.AppendSeparator();
                        // 查看属性
                        evt.menu.AppendAction(
                            "属性",
                            (x) =>
                            {
                                WorkflowUtility.FocusInspector();
                                Selection.activeObject = selectedAsset.RawAssetObject;
                            },
                            _ =>
                                _originIsAsset
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );
                    }
                )
            );
#endregion

#region 资源操作快捷键

            // ctrl+r 刷新视图
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.R && evt.ctrlKey)
                {
                    RefreshTemplateFilters();
                }

                if (evt.keyCode == KeyCode.A && evt.ctrlKey)
                {
                    assetItems.ForEach(_ => _.Select());
                }

                if (evt.keyCode == KeyCode.F5)
                {
                    // tmplScrollView.ScrollTo(selectedTemplateButton);
                    // Utils.Delay(
                    //     () =>
                    //     {
                    //         filterScrollView.ScrollTo(filterButtons.Last());
                    //     },
                    //     0.1f
                    // );
                }
            });

            // 上下左右键切换资源
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                bool validKey =
                    evt.keyCode == KeyCode.UpArrow
                    || evt.keyCode == KeyCode.DownArrow
                    || evt.keyCode == KeyCode.LeftArrow
                    || evt.keyCode == KeyCode.RightArrow
                    || evt.keyCode == KeyCode.F2
                    || evt.keyCode == KeyCode.Delete;
                if (!validKey)
                    return;
                var _selectedAssets = selectedAssets;
                var _isMultipleSelect = _selectedAssets.Count > 1;
                ClearAssetPreviewTooltip();
                if (evt.keyCode == KeyCode.F2)
                {
                    if (selectedAssets.Count == 1 && lastSelect == SelectType.Asset)
                    {
                        selectedAsset.DoEdit();
                    }
                    else if (lastSelect == SelectType.Filter && selectedFilterButton is not null)
                    {
                        selectedFilterButton.DoEdit();
                    }
                }

                if (evt.keyCode == KeyCode.Delete)
                {
                    if (assetItems.Count == 0 || selectedAsset == null)
                    {
                        return;
                    }

                    var _assetConfirmMsg = $"确认删除选中的{_selectedAssets.Count}个资源吗?";
                    if (EditorUtility.DisplayDialog("删除资源", _assetConfirmMsg, "确认", "取消"))
                    {
                        _selectedAssets.ForEach(_ => _.Delete());
                        refreshTemplateAssets();
                    }
                }

                if (assetItems.Count <= 0)
                    return;

                var assetView = rootVisualElement.Q<VisualElement>("asset-list");

                var _column = Mathf.FloorToInt(
                    (assetView.resolvedStyle.width - 32) / assetItems.First().Width
                );

                var _curID = -1000;
                if (evt.keyCode == KeyCode.UpArrow)
                {
                    _curID = _selectedAssets.Min(_ => _.Index) - _column;
                }
                else if (evt.keyCode == KeyCode.DownArrow)
                {
                    _curID = _selectedAssets.Max(_ => _.Index) + _column;
                }
                else if (evt.keyCode == KeyCode.LeftArrow)
                {
                    _curID = _selectedAssets.Min(_ => _.Index) - 1;
                }
                else if (evt.keyCode == KeyCode.RightArrow)
                {
                    _curID = _selectedAssets.Max(_ => _.Index) + 1;
                }
                if (_curID == -1000)
                    return;
                _curID = Mathf.Clamp(_curID, 0, assetItems.Count - 1);
                _selectedAssets.ForEach(_ => _.Deselect());
                selectedAsset = assetItems[_curID];
                selectedAsset.Select();
                lastSelect = SelectType.Asset;

                assetScrollView.ScrollTo(selectedAsset);
            });
        }
#endregion

#region 资源拖拽处理

        private void showDropView()
        {
            if (
                DragAndDrop.visualMode != DragAndDropVisualMode.Rejected
                && !Utils.IsDragFromUNIArt()
                && dropView.style.display != DisplayStyle.Flex
            )
                dropView.style.display = DisplayStyle.Flex;
        }

        private void hideDropView()
        {
            dropView.style.display = DisplayStyle.None;
        }

        private void OnDragEnter(DragEnterEvent evt)
        {
            showDropView();
        }

        private void OnDragLeave(DragLeaveEvent evt)
        {
            hideDropView();
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Length <= 0)
            {
                if (
                    DragAndDrop.objectReferences
                        .OfType<GameObject>()
                        .Any(_obj => PrefabUtility.IsAnyPrefabInstanceRoot(_obj))
                )
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                showDropView();
                return;
            }

            if (Utils.IsDragFromUNIArt())
            {
                return;
            }
            if (DragAndDrop.paths.Any(_ => Utils.IsExternalPath(_)))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                showDropView();
                return;
            }

            // 内部资源移动，限制源目录和目标目录为同一个文件夹的情况
            var _rootPaths = selectedTemplateButton.ValidFilterRootPaths(
                selectedFilterButton.FilterID
            );
            if (
                DragAndDrop.paths
                    .Select(_path => Path.GetDirectoryName(_path).ToForwardSlash())
                    .Any(_dir => _rootPaths.Contains(_dir))
            )
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy; // 允许拖拽复制操作
            showDropView();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            hideDropView();
            // 处理拖拽保存为预制体
            if (DragAndDrop.paths.Length <= 0 && DragAndDrop.objectReferences.Length > 0)
            {
                DragAndDrop.objectReferences
                    .OfType<GameObject>()
                    .ToList()
                    .ForEach(_obj =>
                    {
                        Utils.SaveNonPrefabObjectAsPrefab(
                            _obj,
                            $"{selectedTemplateButton.PrefabRootDir}/{selectedFilterButton.FilterID}"
                        );
                    });
                if (
                    selectedTemplateButton.FilterMode.Value != AssetFilterMode.All
                    && selectedTemplateButton.FilterMode.Value != AssetFilterMode.Prefab
                )
                {
                    selectedTemplateButton.FilterMode.Value = AssetFilterMode.Prefab;
                }
                refreshTemplateAssets();
                return;
            }

            // 导入外部资源
            if (DragAndDrop.paths.Any(_ => Utils.IsExternalPath(_)))
            {
                var _externalGroupPaths = DragAndDrop.paths
                    .Where(_ => Utils.IsExternalPath(_))
                    .Select(_path => (path: _path, targetRoot: TargetRootFolder(_path)))
                    .GroupBy(_ => _.targetRoot);
                _externalGroupPaths
                    .ToList()
                    .ForEach(_ =>
                    {
                        var _targetRoot = _.Key;
                        var _normalPaths = _.Select(_ => _.path)
                            .Where(_ => !_.EndsWith(".psd"))
                            .ToArray();
                        Utils.ImportExternalAssets(_normalPaths, _targetRoot);

                        var _psdFiles = _.Select(_ => _.path)
                            .Where(_ => _.EndsWith(".psd"))
                            .ToList();
                        _psdFiles.ForEach(_psdFile =>
                        {
                            var _psdRoot = _targetRoot;
                            var _psdFolder = Path.GetFileNameWithoutExtension(_psdFile);
                            if (!_psdRoot.EndsWith(_psdFolder))
                            {
                                _psdRoot = Path.Combine(_psdRoot, _psdFolder);
                            }

                            Utils.ImportExternalAssets(new[] { _psdFile }, _psdRoot);
                        });
                    });
                if (
                    selectedTemplateButton.FilterMode.Value != AssetFilterMode.All
                    && selectedTemplateButton.FilterMode.Value != AssetFilterMode.Texture
                )
                {
                    selectedTemplateButton.FilterMode.Value = AssetFilterMode.All;
                }
                RefreshTemplateFilters();
                return;
            }

            // 移动内部资源
            var _internalGroupPaths = DragAndDrop.paths
                .Where(_ => !Utils.IsExternalPath(_))
                .Select(_path => (path: _path, targetRoot: TargetRootFolder(_path)))
                .GroupBy(_ => _.targetRoot);
            _internalGroupPaths
                .ToList()
                .ForEach(_ =>
                {
                    var _targetRoot = _.Key;
                    var _paths = _.Select(_ => _.path).ToArray();
                    Utils.MoveAssetsWithDependencies(DragAndDrop.paths, _targetRoot, true);
                });

            RefreshTemplateFilters();

            DragAndDrop.AcceptDrag();
        }
#endregion

        public string TargetRootFolder(string assetPath)
        {
            var _type = GetAssetType(assetPath);
            var _rootDir = string.Empty;
            if (_type == typeof(GameObject))
            {
                _rootDir = selectedTemplateButton.PrefabRootDir;
            }
            else
            {
                _rootDir = selectedTemplateButton.TextureRootDir;
            }
            return $"{_rootDir}/{selectedFilterButton.FilterID}".TrimEnd('/');
        }

        public Type GetAssetType(string assetPath)
        {
            if (Utils.IsExternalPath(assetPath))
            {
                if (
                    new List<string>
                    {
                        ".jpg",
                        ".png",
                        ".jpeg",
                        ".gif",
                        ".psd",
                        ".tga",
                        ".exr",
                        ".hdr",
                        ".pic"
                    }.Contains(Path.GetExtension(assetPath).ToLower())
                )
                {
                    return typeof(Texture2D);
                }
                else if (Directory.Exists(assetPath)) // 直接拖文件夹的 默认为是图片资源
                {
                    return typeof(Texture2D);
                }
                else
                {
                    return typeof(GameObject);
                }
            }
            return AssetDatabase.GetMainAssetTypeAtPath(assetPath);
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

        private void selectFilter(int filterID)
        {
            if (!selectedTemplateButton.IsInstalled && !selectedTemplateButton.IsLocal)
                return;

            selectedTemplateButton.FilterID = filterID;

            filterButtons.ForEach(_f => _f.Deselect());
            selectedFilterButton?.Select();
            lastSelect = SelectType.Filter;
            refreshTemplateAssets();
        }

        // 刷新模板内容
        private void refreshTemplateView()
        {
            var _templateContent = rootVisualElement.Q<VisualElement>("template-content");
            var _templateMgr = rootVisualElement.Q<VisualElement>("template-mgr");

            refreshVersions();

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

#region 构建模板列表
        private void refreshTemplateMenuList()
        {
            var _svnTemplateList = SVNConextMenu
                .GetRepoFolders("http://svn.andcrane.com/repo/UNIArtTemplates")
                .Except(new string[] { TmplButton.BuiltInTemplateID })
                .ToList();

            var _topListRoot = rootVisualElement.Q<VisualElement>("menu-top-list");
            _topListRoot.Clear();

            var _pattern = $".*{templateFilter.Value}.*";

            var _normalListRoot = rootVisualElement.Q<VisualElement>("template-list");
            _normalListRoot.Clear();
            templateButtons.RemoveRange(2, templateButtons.Count - 2);

            templateButtons[1].Refresh();

            var _newTemplates = _svnTemplateList
                .Where(_templName => Regex.IsMatch(_templName, _pattern, RegexOptions.IgnoreCase))
                .Select(_item => new TmplButton() { TemplateID = _item });

            templateButtons
                .Take(2)
                .Concat(_newTemplates.Where(_ => _.IsTop))
                .ToList()
                .ForEach(_templdate =>
                {
                    _topListRoot.Add(_templdate);
                    templateButtons.Add(_templdate);
                });
            _newTemplates
                .Where(_ => !_.IsTop)
                .OrderByDescending(_ =>
                {
                    return _.OrderID;
                })
                .ToList()
                .ForEach(_templdate =>
                {
                    _normalListRoot.Add(_templdate);
                    templateButtons.Add(_templdate);
                });

            templateButtons.ForEach(_t =>
            {
                _t.RefreshStyle();
                _t.RegisterCallback<MouseDownEvent>(
                    evt => selectTemplate(templateButtons.IndexOf(_t))
                );

                _t.onTopChanged.AddListener(_ =>
                {
                    refreshTemplateMenuList();
                });
            });

            selectTemplate(SelectedTemplateID);
        }
#endregion

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
            if (selectedFilterID < 0 || selectedFilterID >= (filterButtons.Count - 1))
            {
                selectedTemplateButton.FilterID = 0;
            }
        }

        private void orderFilters()
        {
            filterButtons = filterButtons.OrderBy(_ => _.FilterID).OrderBy(_ => _.OrderID).ToList();
            filterListRoot.Clear();
            filterButtons.ForEach(_f => filterListRoot.Add(_f));
        }

        private FilterButton addFilter(string filterID)
        {
            if (filterButtons.Exists(_ => _.FilterID == filterID && !_.IsNew))
                return filterButtons.Find(_f => _f.FilterID == filterID && !_f.IsNew);

            var _filterButton = new FilterButton() { FilterID = filterID };
            filterButtons.Add(_filterButton);
            filterListRoot.Add(_filterButton);

            _filterButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                var _filterID = filterButtons.IndexOf(_filterButton);
                if (_filterID == selectedFilterID)
                {
                    return;
                }

                selectFilter(filterButtons.IndexOf(_filterButton));
            });

            _filterButton.OnConfirmInput.AddListener(
                (_old, _newFolderName) =>
                {
                    // Debug.Log($"Rename filter {_old} to {_newFolderName}");
                    var _filterName = _newFolderName;
                    if (_filterButton.IsNew)
                    {
                        var _rootDir = selectedTemplateButton.RootTextureFilterPath(string.Empty);
                        if (_newFolderName.Contains("/"))
                        {
                            var _newPath = Path.Combine(_rootDir, _newFolderName).ToForwardSlash();
                            _rootDir = Path.GetDirectoryName(_newPath).ToForwardSlash();
                            if (!Directory.Exists(_rootDir))
                            {
                                Directory.CreateDirectory(_rootDir);
                                AssetDatabase.Refresh();
                            }
                            _newFolderName = Path.GetFileName(_newPath);
                        }
                        _rootDir = _rootDir.TrimEnd('/');

                        var _folderGUID = AssetDatabase.CreateFolder(_rootDir, _newFolderName);
                        if (string.IsNullOrEmpty(_folderGUID))
                        {
                            Debug.LogError($"创建文件夹失败：{_newFolderName}");
                            return;
                        }
                        var _createdFolderPath = AssetDatabase.GUIDToAssetPath(_folderGUID);
                        var _newFilterButton = addFilter(
                            _filterName.Contains('/')
                                ? _filterName
                                : Path.GetFileName(_createdFolderPath)
                        );
                        orderFilters();

                        selectFilter(filterButtons.IndexOf(_newFilterButton));

                        Utils.Delay(
                            () =>
                            {
                                filterScrollView.ScrollTo(_newFilterButton);
                            },
                            0.1f
                        );

                        return;
                    }

                    selectedTemplateButton
                        .ValidFilterRootPaths(_old)
                        .ToList()
                        .ForEach(_oldPath =>
                        {
                            var _newName = Path.GetFileName(_newFolderName);
                            // Debug.LogWarning($"Rename filter {_old} to {_newName}");
                            var _msg = AssetDatabase.RenameAsset(_oldPath, _newName);
                            if (!string.IsNullOrEmpty(_msg))
                            {
                                Debug.LogWarning(_msg);
                            }
                        });

                    orderFilters();
                    // var _oldPath = selectedTemplateButton.RootTextureFilterPath(_old);
                }
            );
            return _filterButton;
        }

        // 刷新模板筛选列表
        public void RefreshTemplateFilters()
        {
            validateTemplateID();
            refreshFilterDirButtonStyle();
            refreshViewButtonStyle();

            filterListRoot.Clear();
            filterButtons.Clear();

            var _templateAssetTags = selectedTemplateButton.FilterTagsWithAsset();

            _templateAssetTags.Insert(0, string.Empty);
            _templateAssetTags.Add(FilterButton.CreateNewText);
            _templateAssetTags.ForEach(_dir => addFilter(_dir));

            orderFilters();

            validateFilterID();
            selectFilter(selectedTemplateButton.FilterID);

            rootVisualElement
                .Q<Button>("btn_uninstall")
                .SetEnabled(selectedTemplateButton.Removeable);
            syncToolbarMenuStatus();

            selectedTemplateButton.FilterMode.OnValueChanged.RemoveAllListeners();
            selectedTemplateButton.FilterMode.OnValueChanged.AddListener(_ =>
            {
                UNIArtSettings.Project.SetTemplateFilterMode(selectedTemplateButton.TemplateID, _);
                syncToolbarMenuStatus();
                RefreshTemplateFilters();
            });

            selectedTemplateButton.Version.OnValueChanged.RemoveAllListeners();
            selectedTemplateButton.Version.OnValueChanged.AddListener(_ =>
            {
                refreshVersions();
            });

            selectedTemplateButton.SearchFilter.OnValueChanged.RemoveAllListeners();
            selectedTemplateButton.SearchFilter.OnValueChanged.AddListener(_ =>
            {
                refreshTemplateAssets();
            });
        }

        List<AssetItem> assetItems = new List<AssetItem>();

        // 当前选中的资源项
        public static AssetItem selectedAsset = null;

        EditorCoroutine _refreshAssetsCoroutine = null;

        // 当前筛选目录
        public List<string> CurrentRootPaths
        {
            get
            {
                if (selectedTemplateButton == null)
                {
                    return new List<string>();
                }
                return selectedTemplateButton
                    .ValidFilterRootPaths(selectedFilterButton.FilterID)
                    .ToList();
            }
        }

        public string CurrentRootPath => CurrentRootPaths.FirstOrDefault();

        private void refreshVersions()
        {
            var _textVersion = rootVisualElement.Q<Label>("text_version");
            _textVersion.text = "Ver:" + selectedTemplateButton.Version.Value;
        }

        private void refreshTemplateAssets()
        {
            hideDropView();
            ClearAssetPreviewTooltip();
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

            Func<string, int> orderAssets = (assetPath) =>
            {
                if (assetPath.EndsWith(".prefab"))
                {
                    return 0;
                }
                else if (assetPath.EndsWith(".psd"))
                {
                    return 5;
                }
                return 1000;
            };

            var _filterRoots = selectedTemplateButton.ValidFilterRootPaths(_filterID);
            if (_filterRoots.Length <= 0)
            {
                yield break;
            }

            var _assets = AssetDatabase
                .FindAssets(selectedTemplateButton.filterArgs(), _filterRoots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(
                    _path =>
                        string.IsNullOrEmpty(selectedTemplateButton.SearchFilter.Value)
                            ? true
                            : Regex.IsMatch(_path, pattern, RegexOptions.IgnoreCase)
                )
                .OrderBy(orderAssets)
                .ToList();

            var _assetID = 0;
            foreach (var _path in _assets)
            {
                var _obj = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                var _assetItem = new AssetItem() { AssetPath = _path, Index = _assetID++ };
                buttonContainer.Add(_assetItem);
                assetItems.Add(_assetItem);
                yield return new EditorWaitForSeconds(0.001f);

                // 资源点击事件
                _assetItem.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (
                        evt.button == (int)MouseButton.RightMouse
                        && selectedAssets.Contains(_assetItem)
                    )
                    {
                        return;
                    }
                    // if (evt.button == (int)MouseButton.LeftMouse)
                    // {
                    //     evt.StopPropagation();
                    // }
                    evt.StopPropagation();

                    if (!evt.ctrlKey && !evt.shiftKey)
                    {
                        if (selectedAssets.Count <= 1)
                        {
                            assetItems.ForEach(_ => _.Deselect());
                        }

                        // _assetItem.Select();
                        // selectedAsset = _assetItem;
                    }
                });

                _assetItem.RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (
                        evt.button == (int)MouseButton.RightMouse
                        && selectedAssets.Contains(_assetItem)
                    )
                    {
                        return;
                    }

                    if (evt.button == (int)MouseButton.LeftMouse)
                    {
                        evt.StopPropagation();
                    }

                    if (evt.ctrlKey)
                    {
                        _assetItem.Toggle();
                    }
                    else if (evt.shiftKey)
                    {
                        int _min = _assetItem.Index,
                            _max = _assetItem.Index;
                        if (selectedAssets.Count > 0)
                        {
                            _min = selectedAssets.Min(_ => _.Index);
                            _max = selectedAssets.Max(_ => _.Index);
                            var _cur = _assetItem.Index;
                            if (_cur <= _max)
                                _min = _cur;
                            else
                                _max = _cur;
                        }

                        assetItems.ForEach(_ => _.Deselect());
                        assetItems
                            .Where(_ => _.Index >= _min && _.Index <= _max)
                            .ToList()
                            .ForEach(_ => _.Select());
                    }
                    else
                    {
                        assetItems.ForEach(_ => _.Deselect());
                        _assetItem.Select();
                        lastSelect = SelectType.Asset;
                    }

                    selectedAsset = _assetItem;
                });

                // 资源拖拽事件
                _assetItem.OnStartDrag.AddListener(_ =>
                {
                    _.Select();
                    var _selectedAssets = selectedAssets;

                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = _selectedAssets
                        .Select(_asset => _asset.AssetObject)
                        .ToArray();
                    DragAndDrop.paths = _selectedAssets
                        .Select(_asset => _asset.rawAssetPath)
                        .ToArray();

                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                    DragAndDrop.StartDrag("Drag Asset");
                    DragAndDrop.SetGenericData("ArtDragOrigin", _);
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
            // selectedAssetItem?.Deselect();
            // selectedAssetItem = null;
        }
    }
}
