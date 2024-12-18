using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public enum AssetFilterMode
    {
        All,
        Prefab,
        Texture,
        None
    }

    public class TmplButton : SelectableView
    {
        public const string BuiltInTemplateID = "Standard";
        public const string LocalTemplateTitle = "本地项目";
        public bool IsBuiltIn => TemplateID == BuiltInTemplateID;
        public bool IsLocal => TemplateID == LocalTemplateTitle;

        public bool IsInScrollView => !IsBuiltIn && !IsLocal && !IsTop;

        public bool IsTop
        {
            set
            {
                UNIArtSettings.Project.SetTemplateTop(templateID, value);
                RefreshTopIcon();
            }
            get
            {
                if (IsLocal || IsBuiltIn)
                    return true;
                return UNIArtSettings.Project.GetTemplateTop(templateID);
            }
        }

        public ReactiveProperty<AssetFilterMode> FilterMode = new ReactiveProperty<AssetFilterMode>(
            AssetFilterMode.None
        );

        // 遍历Top文件夹
        public bool SearchTopFolderOnly
        {
            get { return UNIArtSettings.Project.GetSearchTopFolderOnly(templateID); }
            set { UNIArtSettings.Project.SetSearchTopFolderOnly(templateID, value); }
        }

        // 模板ID
        private string templateID = string.Empty;
        public string TemplateID
        {
            get { return templateID; }
            set
            {
                templateID = value;
                this.Q<Label>("title").text = templateID == "Standard" ? "基础组件" : templateID;
                initDefaultVariables();
                Refresh();
            }
        }

        private void initDefaultVariables()
        {
            var _filterMode = UNIArtSettings.Project.GetTemplateFilterMode(templateID);
            if (_filterMode == AssetFilterMode.None)
            {
                _filterMode = (IsBuiltIn || IsLocal) ? AssetFilterMode.All : AssetFilterMode.Prefab;
            }
            FilterMode.Value = _filterMode;
        }

        private List<string> filterDirs()
        {
            if (IsLocal)
            {
                return new List<string> { "UI Prefabs", "Textures" };
            }
            return new List<string> { "Prefabs", "Textures" };
        }

        public string PrefabRootDir => $"{RootFolder}/{filterDirs()[0]}";
        public string TextureRootDir => $"{RootFolder}/{filterDirs()[1]}";

        public bool Removeable
        {
            get
            {
                if (IsLocal)
                {
                    return false;
                }
                if (IsBuiltIn && UNIArtSettings.Project.InstallStandardDefault)
                {
                    return false;
                }
                return true;
            }
        }

        public string filterArgs()
        {
            List<string> assetTypes = new List<string> { "Prefab", "Texture" };
            if (FilterMode.Value == AssetFilterMode.Texture)
            {
                assetTypes.Remove("Prefab");
            }
            else if (FilterMode.Value == AssetFilterMode.Prefab)
            {
                assetTypes.Remove("Texture");
            }
            return string.Join(" ", assetTypes.Select(t => $"t:{t}"));
        }

        public List<string> FilterTags()
        {
            return filterDirs()
                .Where(_ => Directory.Exists(RootFolder + "/" + _))
                .SelectMany(
                    _topDir =>
                        Directory
                            .GetDirectories(
                                $"{RootFolder}/{_topDir}",
                                "*",
                                SearchTopFolderOnly
                                    ? SearchOption.TopDirectoryOnly
                                    : SearchOption.AllDirectories
                            )
                            .Select(_dir => _dir.ToForwardSlash())
                            .Select(
                                _dir =>
                                    _dir.ToForwardSlash().Replace($"{RootFolder}/{_topDir}/", "")
                            )
                )
                .Where(_ => !_.StartsWith("."))
                .Distinct()
                .OrderBy(_ => _)
                .ToList();
        }

        // 拥有资源的FilterTags
        public List<string> FilterTagsWithAsset()
        {
            if (FilterMode.Value == AssetFilterMode.All)
            {
                return FilterTags();
            }

            var _findArgs = "";
            var _rootDir = string.Empty;

            if (FilterMode.Value == AssetFilterMode.Prefab)
            {
                _findArgs = "t:Prefab";
                _rootDir = PrefabRootDir;
            }
            else if (FilterMode.Value == AssetFilterMode.Texture)
            {
                _findArgs = "t:Texture";
                _rootDir = TextureRootDir;
            }
            return FilterTags()
                .Where(
                    _filter =>
                        AssetDatabase.IsValidFolder($"{_rootDir}/{_filter}")
                        && AssetDatabase
                            .FindAssets(_findArgs, new string[] { $"{_rootDir}/{_filter}" })
                            .Length > 0
                )
                .ToList();
        }

        public string[] ValidFilterRootPaths(string filterTag)
        {
            if (!IsInstalled || !AssetDatabase.IsValidFolder(RootFolder))
            {
                return new string[0];
            }

            var _filterRoots = filterDirs().Select(_ => $"{RootFolder}/{_}");
            _filterRoots
                .Where(_ => !AssetDatabase.IsValidFolder(_))
                .ToList()
                .ForEach(_ => Utils.CreateFolderIfNotExist(_));

            return _filterRoots
                .Select(_rootDir => $"{_rootDir}/{filterTag}")
                .Where(_ => AssetDatabase.IsValidFolder(_))
                .ToArray();
        }

        public string RootPrefabFilterPath(string filterTag)
        {
            return $"{RootFolder}/{filterDirs()[0]}/{filterTag}";
        }

        public string RootTextureFilterPath(string filterTag)
        {
            return $"{RootFolder}/{filterDirs()[1]}/{filterTag}";
        }

        private bool isInstalled = false;
        public bool IsInstalled
        {
            get => isInstalled;
            protected set
            {
                isInstalled = value;
                RefreshInstallIcon();
            }
        }

        public int FilterID
        {
            get { return UNIArtSettings.Project.GetTemplateFilterID(TemplateID); }
            set { UNIArtSettings.Project.SetTemplateFilterID(TemplateID, value); }
        }
        public ReactiveProperty<string> SearchFilter = new ReactiveProperty<string>(string.Empty);

        public string RootFolder =>
            IsLocal
                ? UNIArtSettings.Project.ArtFolder
                : UNIArtSettings.GetExternalTemplateFolder(TemplateID);
        public string ExternalRepoUrl => UNIArtSettings.GetExternalTemplateFolderUrl(TemplateID);

        public int OrderID
        {
            get
            {
                if (IsLocal)
                {
                    return 100;
                }
                if (IsBuiltIn)
                {
                    return 90;
                }
                if (IsInstalled)
                {
                    return 50;
                }
                return 0;
            }
        }

        public void Refresh()
        {
            if (IsLocal)
            {
                IsInstalled = true;
                IsTop = true;
                return;
            }
            IsInstalled = UNIArtSettings.Project.HasExternal(TemplateID);
            IsTop = UNIArtSettings.Project.GetTemplateTop(TemplateID);
            RefreshStyle();
        }

        // 是否拥有本地资源
        public bool HasLocalEntity => AssetDatabase.IsValidFolder(RootFolder);

        public bool AssetReady => IsInstalled && HasLocalEntity;

        public void RefreshStyle()
        {
            RefreshInstallIcon();
            RefreshTopIcon();
        }

        public void RefreshInstallIcon()
        {
            if (parent == null)
                return;
            if (IsInstalled)
                this.Q<VisualElement>("icon_status").RemoveFromClassList("not-installed");
            else
            {
                this.Q<VisualElement>("icon_status").AddToClassList("not-installed");
            }
        }

        public void RefreshTopIcon()
        {
            if (parent == null)
                return;
            if (IsTop && !IsLocal && !IsBuiltIn)
            {
                this.Q<VisualElement>("top-icon").AddToClassList("menu-keep-top");
            }
            else
            {
                this.Q<VisualElement>("top-icon").RemoveFromClassList("menu-keep-top");
            }
        }

        public void Commit()
        {
            if (IsLocal)
            {
                SVNConextMenu.Commit(SVNConextMenu.GetRootAssetPath(), false);
                return;
            }
            // 将版本设置为最新版
            SVNIntegration.AddOrUpdateExternal(
                UNIArtSettings.Project.TemplatePropTarget,
                ExternalRepoUrl,
                -1,
                false
            );

            SVNConextMenu.CommitExternal(RootFolder);

            // 指定为最新版本号
            SVNIntegration.AddOrUpdateExternal(
                UNIArtSettings.Project.TemplatePropTarget,
                ExternalRepoUrl,
                0,
                false
            );
        }

        // 拉取最新资源
        public bool Pull()
        {
            if (IsLocal)
            {
                SVNConextMenu.UpdateAll();
                return true;
            }
            if (!IsInstalled)
                return false;
            if (SVNIntegration.IsWorkingCopy(RootFolder))
            {
                if (SVNIntegration.Update(RootFolder))
                {
                    AssetDatabase.Refresh();
                    return true;
                }
                return false;
            }

            if (!AssetDatabase.DeleteAsset(RootFolder))
            {
                Debug.LogWarning("Failed to delete external template folder.");
                return false;
            }

            if (!SVNIntegration.Update(UNIArtSettings.Project.TemplateRelativeRoot))
            {
                Debug.LogWarning("Failed to checkout external template.");
                return false;
            }
            AssetDatabase.Refresh();
            return true;
        }

        private bool keeptopable => !IsTop && !IsBuiltIn && !IsLocal;
        private bool unkeeptopable => IsTop && !IsBuiltIn && !IsLocal;

        public UnityEvent<bool> onTopChanged = new UnityEvent<bool>();

        public TmplButton()
        {
            BindView(Utils.PackageAssetPath("Editor/TmplView/TmplButton/TmplButton.uxml"));

            this.AddManipulator(
                new ContextualMenuManipulator(
                    (evt) =>
                    {
                        evt.menu.AppendAction(
                            "置顶该资源库",
                            (x) =>
                            {
                                IsTop = true;
                                onTopChanged.Invoke(true);
                            },
                            _ =>
                                keeptopable
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );
                        evt.menu.AppendAction(
                            "取消置顶",
                            (x) =>
                            {
                                IsTop = false;
                                onTopChanged.Invoke(false);
                            },
                            _ =>
                                unkeeptopable
                                    ? DropdownMenuAction.Status.Normal
                                    : DropdownMenuAction.Status.Disabled
                        );
                    }
                )
            );
        }

        public bool CleanDir()
        {
            if (IsLocal)
                return true;
            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                return true;
            }
            return AssetDatabase.DeleteAsset(RootFolder);
        }
    }
}
