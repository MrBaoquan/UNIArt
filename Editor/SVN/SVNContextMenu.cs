using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UNIHper.Art.Editor
{
    public class SVNConextMenu
    {
        private const string ClientCommand = "TortoiseProc.exe";

#region SVN菜单栏

        [MenuItem("Assets/\u2935   SVN 更新资源", priority = 61)]
        public static void UpdateAll()
        {
            // It is recommended to freeze Unity while updating.
            // If SVN downloads files while Unity is crunching assets, GUID database may get corrupted.
            // TortoiseSVN handles nested repositories gracefully and updates them one after another. SnailSVN - not so much. :(
            Update(GetRootAssetPath(), false, wait: true);
        }

        [MenuItem("Assets/\u2197  SVN 提交资源", priority = 62)]
        public static void CommitAll()
        {
            var paths = GetSelectedAssetPaths().ToList();
            if (paths.Count == 1 && paths[0].StartsWith("Assets/ArtAssets/#Templates/"))
            {
                var _selectedPath = paths[0];
                var _external = SVNIntegration
                    .GetExternals("Assets/ArtAssets/#Templates")
                    .Select(_ => _.Dir)
                    .Where(x => _selectedPath.StartsWith($"Assets/ArtAssets/#Templates/{x}"))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(_external))
                {
                    CommitExternal($"Assets/ArtAssets/#Templates/{_external}");
                    return;
                }
            }

            // TortoiseSVN handles nested repositories gracefully. SnailSVN - not so much. :(
            Commit(GetRootAssetPath(), false);
        }

        const int SVNMenuOrderOffset = 62;

        [MenuItem("Assets/Tortoise SVN/\u2197  提交", false, SVNMenuOrderOffset + 1)]
        public static void CommitSelected()
        {
            var paths = GetSelectedAssetPaths().ToList();
            if (paths.Count == 1)
            {
                if (paths[0] == "Assets")
                {
                    // Special case for the "Assets" folder as it doesn't have a meta file and that kind of breaks the TortoiseSVN.
                    CommitAll();
                    return;
                }

                // TortoiseSVN shows "(multiple targets selected)" for commit path when more than one was specified.
                // Don't specify the .meta unless really needed to.
                var statusData = SVNIntegration.GetStatus(paths[0] + ".meta");
                if (statusData.Status == VCFileStatus.Normal && !statusData.IsConflicted)
                {
                    Commit(paths, false);
                    return;
                }
            }

            Commit(paths, true);
        }

        [MenuItem("Assets/Tortoise SVN/✏️  修改仓库名", false, SVNMenuOrderOffset + 15)]
        public static void RenameSVNRemote()
        {
            RenameSVNRepoWindow.ShowWindow();
        }

        [MenuItem("Assets/Tortoise SVN/🔄  切换仓库源", false, SVNMenuOrderOffset + 16)]
        public static void SwitchSVNRemote()
        {
            SwitchSVNRepoWindow.ShowWindow();
        }

        [MenuItem("Assets/Tortoise SVN/\U0001F50D  检查修改", false, SVNMenuOrderOffset + 31)]
        public static void CheckChangesSelected()
        {
            if (Selection.assetGUIDs.Length > 1)
            {
                CheckChanges(GetSelectedAssetPaths(), true);
            }
            else if (Selection.assetGUIDs.Length == 1)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);

                var isFolder = System.IO.Directory.Exists(assetPath);

                if (isFolder || (!DiffAsset(assetPath) && !DiffAsset(assetPath + ".meta")))
                {
                    CheckChanges(GetSelectedAssetPaths(), true);
                }
            }
        }

        [MenuItem("Assets/Tortoise SVN/\U0001F50D  检查全部修改", false, SVNMenuOrderOffset + 32)]
        public static void CheckChangesAll()
        {
            // TortoiseSVN handles nested repositories gracefully. SnailSVN - not so much. :(
            CheckChanges(GetRootAssetPath(), false);
        }

        // 还原
        [MenuItem("Assets/Tortoise SVN/  \u21A9   还原", false, SVNMenuOrderOffset + 33)]
        public static void RevertSelected()
        {
            var paths = GetSelectedAssetPaths().ToList();
            if (paths.Count == 1)
            {
                if (paths[0] == "Assets")
                {
                    // Special case for the "Assets" folder as it doesn't have a meta file and that kind of breaks the TortoiseSVN.
                    RevertAll();
                    return;
                }

                // TortoiseSVN shows the meta file for revert even if it has no changes.
                // Don't specify the .meta unless really needed to.
                var statusData = SVNIntegration.GetStatus(paths[0] + ".meta");
                if (statusData.Status == VCFileStatus.Normal && !statusData.IsConflicted)
                {
                    if (Directory.Exists(paths[0]))
                    {
                        Revert(paths, false);
                    }
                    else
                    {
                        if (
                            EditorUtility.DisplayDialog(
                                "Revert File?",
                                $"Are you sure you want to revert this file and it's meta?\n\"{paths[0]}\"",
                                "Yes",
                                "No",
                                DialogOptOutDecisionType.ForThisSession,
                                "WiseSVN.RevertConfirm"
                            )
                        )
                        {
                            SVNIntegration.Revert(paths, false, true, false);
                            AssetDatabase.Refresh();
                        }
                    }
                    return;
                }
            }

            Revert(GetSelectedAssetPaths(), true, true);
        }

        // 还原全部
        [MenuItem("Assets/Tortoise SVN/  \u21A9   还原全部", false, SVNMenuOrderOffset + 34)]
        public static void RevertAll()
        {
            // TortoiseSVN handles nested repositories gracefully. SnailSVN - not so much. :(
            Revert(GetRootAssetPath(), false, true);
        }

        [MenuItem("Assets/Tortoise SVN/\u26A0  解决全部", false, SVNMenuOrderOffset + 51)]
        private static void ResolveAllMenu()
        {
            ResolveAll(false);
        }

        [MenuItem("Assets/Tortoise SVN/\U0001F9F9  清理", false, SVNMenuOrderOffset + 52)]
        public static void Cleanup()
        {
            Cleanup(true);
        }

        [MenuItem("Assets/Tortoise SVN/\U0001F512  获取锁定", false, SVNMenuOrderOffset + 71)]
        public static void GetLocksSelected()
        {
            if (!TryShowLockDialog(GetSelectedAssetPaths().ToList(), GetLocks, false))
            {
                // This will include the meta which is rarely what you want.
                GetLocks(GetSelectedAssetPaths(), true, false);
            }
        }

        [MenuItem("Assets/Tortoise SVN/\U0001F513  解除锁定", false, SVNMenuOrderOffset + 72)]
        public static void ReleaseLocksSelected()
        {
            if (!TryShowLockDialog(GetSelectedAssetPaths().ToList(), ReleaseLocks, true))
            {
                // No locked assets, show nothing.
            }
        }

        // 打开版本库浏览器
        [MenuItem("Assets/Tortoise SVN/\U0001F4C1  版本库浏览器", priority = SVNMenuOrderOffset + 91)]
        public static void RepoBrowserSelected()
        {
            var _assetRepoUrl = GetSelectedAssetPaths()
                .Select(SVNIntegration.AssetPathToURL)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(_assetRepoUrl))
            {
                _assetRepoUrl = ProjectRepoUrl();
            }
            RepoBrowser(_assetRepoUrl);
        }

        // 复制仓库地址
        [MenuItem("Assets/Tortoise SVN/ 🔗  复制仓库地址", priority = SVNMenuOrderOffset + 92)]
        public static void CopySVNURL()
        {
            var repoUrl = ProjectRepoUrl();

            EditorGUIUtility.systemCopyBuffer = repoUrl;
            EditorUtility.DisplayDialog("SVN 仓库地址已复制到剪贴板", repoUrl, "OK");
        }

        [MenuItem("Assets/Tortoise SVN/  \u2631   显示日志", false, SVNMenuOrderOffset + 93)]
        public static void ShowLogSelected()
        {
            ShowLog(GetSelectedAssetPaths().FirstOrDefault());
        }

        [MenuItem("Assets/Tortoise SVN/  \u2631   显示全部日志", false, SVNMenuOrderOffset + 94)]
        public static void ShowLogAll()
        {
            ShowLog(GetRootAssetPath().First());
        }

        [MenuItem("Assets/\u2935   SVN 更新资源", true)]
        [MenuItem("Assets/\u2197  SVN 提交资源", true)]
        [MenuItem("Assets/Tortoise SVN/\u2197  提交", true)]
        [MenuItem("Assets/Tortoise SVN/\U0001F50D  检查修改", true)]
        [MenuItem("Assets/Tortoise SVN/\U0001F50D  检查全部修改", true)]
        [MenuItem("Assets/Tortoise SVN/  \u21A9   还原", true)]
        [MenuItem("Assets/Tortoise SVN/  \u21A9   还原全部", true)]
        [MenuItem("Assets/Tortoise SVN/\u26A0  解决全部", true)]
        [MenuItem("Assets/Tortoise SVN/\U0001F9F9  清理", true)]
        [MenuItem("Assets/Tortoise SVN/\U0001F512  获取锁定", true)]
        [MenuItem("Assets/Tortoise SVN/\U0001F513  解除锁定", true)]
        [MenuItem("Assets/Tortoise SVN/  \u2631   显示日志", true)]
        [MenuItem("Assets/Tortoise SVN/  \u2631   显示全部日志", true)]
        [MenuItem("Assets/Tortoise SVN/ 🔗  复制仓库地址", true)]
        [MenuItem("Assets/Tortoise SVN/\U0001F4C1  版本库浏览器", true)]
        private static bool checkIfSVNRepo()
        {
            var _repoPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), ".svn");
            return Directory.Exists(_repoPath);
        }

#endregion
        public static void Update(
            IEnumerable<string> assetPaths,
            bool includeMeta,
            bool wait = false
        )
        {
            if (!assetPaths.Any())
                return;

            string pathsArg = AssetPathsToContextPaths(assetPaths, includeMeta);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:update /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                // Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void Commit(
            IEnumerable<string> assetPaths,
            bool includeMeta,
            bool wait = false
        )
        {
            if (!assetPaths.Any())
                return;

            string pathsArg = AssetPathsToContextPaths(assetPaths, includeMeta);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:commit /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                // Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void CommitExternal(string externalPath)
        {
            // if (string.IsNullOrEmpty(pathsArg))
            //     return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:commit /path:\".\"",
                externalPath,
                Encoding.GetEncoding(936)
            );
            if (result.HasErrors)
            {
                // Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        private static IEnumerable<string> GetRootAssetPath()
        {
            yield return "."; // The root folder of the project (not the Assets folder).
        }

        protected static string FileArgumentsSeparator => "*";
        protected static bool FileArgumentsSurroundQuotes => false;

        private static string PreparePathArg(string path)
        {
            return FileArgumentsSurroundQuotes ? '"' + path + '"' : path;
        }

        protected static string AssetPathToContextPaths(string assetPath, bool includeMeta)
        {
            if (string.IsNullOrEmpty(assetPath))
                return PreparePathArg(Path.GetDirectoryName(Application.dataPath));

            // Because svn doesn't like it when you pass ignored files to some operations, like commit.
            string paths = "";
            if (SVNIntegration.GetStatus(assetPath).Status != VCFileStatus.Ignored)
            {
                paths = PreparePathArg(assetPath);
            }

            if (
                includeMeta
                && SVNIntegration.GetStatus(assetPath + ".meta").Status != VCFileStatus.Ignored
            )
            {
                paths += FileArgumentsSeparator + PreparePathArg(assetPath + ".meta");
            }

            return paths;
        }

        public static string AssetPathsToContextPaths(
            IEnumerable<string> assetPaths,
            bool includeMeta
        )
        {
            if (!assetPaths.Any())
                return string.Empty;

            return string.Join(
                FileArgumentsSeparator,
                assetPaths.Select(path => AssetPathToContextPaths(path, includeMeta))
            );
        }

        public static void Revert(
            IEnumerable<string> assetPaths,
            bool includeMeta,
            bool wait = false
        )
        {
            if (!assetPaths.Any())
                return;

            string pathsArg = AssetPathsToContextPaths(assetPaths, includeMeta);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:revert /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                // Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void ShowLog(string assetPath, bool wait = false)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            string pathsArg = AssetPathToContextPaths(assetPath, false);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:log /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        private static string SVNFormatPath(string path)
        {
            // NOTE: @ is added at the end of path, to avoid problems when file name contains @, and SVN mistakes that as "At revision" syntax".
            //		https://stackoverflow.com/questions/757435/how-to-escape-characters-in-subversion-managed-file-names
            return path + "@";
        }

        internal const int COMMAND_TIMEOUT = 20000; // Milliseconds
        internal const int ONLINE_COMMAND_TIMEOUT = 45000; // Milliseconds

        private static string ExtractLineValue(string pattern, string str)
        {
            var lineIndex = str.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (lineIndex == -1)
                return string.Empty;

            var valueStartIndex = lineIndex + pattern.Length + 1;
            var lineEndIndex = str.IndexOf(
                "\n",
                valueStartIndex,
                StringComparison.OrdinalIgnoreCase
            );
            if (lineEndIndex == -1)
            {
                lineEndIndex = str.Length - 1;
            }

            // F@!$!#@!#!
            if (str[lineEndIndex - 1] == '\r')
            {
                lineEndIndex--;
            }

            return str.Substring(valueStartIndex, lineEndIndex - valueStartIndex);
        }

        private static IEnumerable<string> GetSelectedAssetPaths()
        {
            string[] guids = Selection.assetGUIDs;
            for (int i = 0; i < guids.Length; ++i)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (string.IsNullOrEmpty(path))
                    continue;

                // All direct folders in packages (the package folder) are returned with ToLower() by Unity.
                // If you have a custom package in development and your folder has upper case letters, they need to be restored.
                if (path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                {
                    path = Path.GetFullPath(path)
                        .Replace("\\", "/")
                        .Replace(ProjectRootUnity + "/", "");

                    // If this is a normal package (not a custom one in development), returned path points to the "Library" folder.
                    if (!path.StartsWith("Packages", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                yield return path;
            }
        }

        public static string ProjectRootUnity =>
            Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");

        public static void Cleanup(bool wait = false)
        {
            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:cleanup /path:\"{SVNIntegration.ProjectRootNative}\"",
                wait
            );
            if (result.HasErrors)
            {
                // Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void ResolveAll(bool wait = false)
        {
            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:resolve /path:\"{SVNIntegration.ProjectRootNative}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void Resolve(string assetPath, bool wait = false)
        {
            if (System.IO.Directory.Exists(assetPath))
            {
                var resolveResult = ShellUtils.ExecuteCommand(
                    ClientCommand,
                    $"/command:resolve /path:\"{AssetPathToContextPaths(assetPath, false)}\"",
                    wait
                );
                if (!string.IsNullOrEmpty(resolveResult.Error))
                {
                    Debug.LogError($"SVN Error: {resolveResult.Error}");
                }

                return;
            }

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:conflicteditor /path:\"{AssetPathToContextPaths(assetPath, false)}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void CheckChanges(
            IEnumerable<string> assetPaths,
            bool includeMeta,
            bool wait = false
        )
        {
            if (!assetPaths.Any())
                return;

            string pathsArg = AssetPathsToContextPaths(assetPaths, includeMeta);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:repostatus /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static bool DiffAsset(string assetPath)
        {
            var statusData = SVNIntegration.GetStatus(assetPath);

            var isModified =
                statusData.Status != VCFileStatus.Normal
                && statusData.Status != VCFileStatus.Unversioned
                && statusData.Status != VCFileStatus.Conflicted;
            isModified |= statusData.PropertiesStatus == VCPropertiesStatus.Modified;

            if (isModified)
            {
                DiffChanges(assetPath, false);
                return true;
            }

            if (
                statusData.Status == VCFileStatus.Conflicted
                || statusData.PropertiesStatus == VCPropertiesStatus.Conflicted
            )
            {
                Resolve(assetPath, false);
                return true;
            }

            return false;
        }

        public static void DiffChanges(string assetPath, bool wait = false)
        {
            string pathsArg = AssetPathToContextPaths(assetPath, false);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:diff /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        private static bool TryShowLockDialog(
            List<string> selectedPaths,
            Action<IEnumerable<string>, bool, bool> operationHandler,
            bool onlyLocked
        )
        {
            if (selectedPaths.Count == 0)
                return true;

            if (selectedPaths.All(p => Directory.Exists(p)))
            {
                operationHandler(selectedPaths, false, false);
                return true;
            }

            bool hasModifiedPaths = false;
            var modifiedPaths = new List<string>();
            foreach (var path in selectedPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);

                var countPrev = modifiedPaths.Count;
                // modifiedPaths.AddRange(
                //     SVNStatusesDatabase.Instance
                //         .GetAllKnownStatusData(guid, false, true, true)
                //         .Where(sd => sd.Status != VCFileStatus.Unversioned)
                //         .Where(
                //             sd =>
                //                 sd.Status != VCFileStatus.Normal
                //                 || sd.LockStatus != VCLockStatus.NoLock
                //         )
                //         .Where(
                //             sd =>
                //                 !onlyLocked
                //                 || (
                //                     sd.LockStatus != VCLockStatus.NoLock
                //                     && sd.LockStatus != VCLockStatus.LockedOther
                //                 )
                //         )
                //         .Select(sd => sd.Path)
                // );

                // No change in asset or meta -> just add the asset as it was selected by the user anyway.
                if (modifiedPaths.Count == countPrev)
                {
                    if (!onlyLocked || Directory.Exists(path))
                    {
                        modifiedPaths.Add(path);
                    }
                }
                else
                {
                    hasModifiedPaths = true;
                }
            }

            if (hasModifiedPaths)
            {
                operationHandler(modifiedPaths, false, false);
                return true;
            }

            return false;
        }

        public static void GetLocks(
            IEnumerable<string> assetPaths,
            bool includeMeta,
            bool wait = false
        )
        {
            if (!assetPaths.Any())
                return;

            string pathsArg = AssetPathsToContextPaths(assetPaths, includeMeta);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:lock /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void ReleaseLocks(
            IEnumerable<string> assetPaths,
            bool includeMeta,
            bool wait = false
        )
        {
            if (!assetPaths.Any())
                return;

            string pathsArg = AssetPathsToContextPaths(assetPaths, includeMeta);
            if (string.IsNullOrEmpty(pathsArg))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:unlock /path:\"{pathsArg}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static void RepoBrowser(string url, bool wait = false)
        {
            if (string.IsNullOrEmpty(url))
                return;

            var result = ShellUtils.ExecuteCommand(
                ClientCommand,
                $"/command:repobrowser /path:\"{url}\"",
                wait
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
            }
        }

        public static string ProjectRepoUrl()
        {
            var _assetRepoUrl = SVNIntegration.AssetPathToURL("Assets");
            _assetRepoUrl = Uri.UnescapeDataString(_assetRepoUrl);
            return _assetRepoUrl.Substring(0, _assetRepoUrl.LastIndexOf("/"));
        }

        // 查看SVN仓库文件夹列表
        public static List<string> GetRepoFolders(string repoUrl)
        {
            var result = ShellUtils.ExecuteCommand(
                "svn",
                $"list {repoUrl}",
                Encoding.GetEncoding(936)
            );

            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
                return new List<string>();
            }

            return result.Output
                .Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Where(_ => _.EndsWith("/"))
                .Select(_ => _.TrimEnd('/'))
                .ToList();
        }

        // 查看仓库作者列表
        public static Dictionary<string, int> GetRepoAuthors()
        {
            var result = ShellUtils.ExecuteCommand("svn", $"log --quiet --xml", true);
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
                return new Dictionary<string, int>();
            }
            return ParseSVNLog(result.Output)
                .OrderByDescending(x => x.Value)
                .Take(10)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        private static Dictionary<string, int> ParseSVNLog(string logOutput)
        {
            var authorStats = new Dictionary<string, int>();

            var xmlDoc = new System.Xml.XmlDocument();
            xmlDoc.LoadXml(logOutput);

            var logEntries = xmlDoc.SelectNodes("//logentry/author");
            foreach (System.Xml.XmlNode authorNode in logEntries)
            {
                var author = authorNode.InnerText;
                if (authorStats.ContainsKey(author))
                    authorStats[author]++;
                else
                    authorStats[author] = 1;
            }

            return authorStats;
        }
    }
}
