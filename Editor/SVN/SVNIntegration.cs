using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UNIArt.Editor
{
    public class SVNIntegration /*: UnityEditor.AssetModificationProcessor*/
    {
        #region SVN CLI Definitions
        public static string ProjectRootNative => Path.GetDirectoryName(Application.dataPath);
        private static readonly Dictionary<char, VCFileStatus> m_FileStatusMap = new Dictionary<
            char,
            VCFileStatus
        >
        {
            { ' ', VCFileStatus.Normal },
            { 'A', VCFileStatus.Added },
            { 'C', VCFileStatus.Conflicted },
            { 'D', VCFileStatus.Deleted },
            { 'I', VCFileStatus.Ignored },
            { 'M', VCFileStatus.Modified },
            { 'R', VCFileStatus.Replaced },
            { '?', VCFileStatus.Unversioned },
            { '!', VCFileStatus.Missing },
            { 'X', VCFileStatus.External },
            { '~', VCFileStatus.Obstructed },
        };

        private static readonly Dictionary<char, VCSwitchedExternal> m_SwitchedExternalStatusMap =
            new Dictionary<char, VCSwitchedExternal>
            {
                { ' ', VCSwitchedExternal.Normal },
                { 'S', VCSwitchedExternal.Switched },
                { 'X', VCSwitchedExternal.External },
            };

        private static readonly Dictionary<char, VCLockStatus> m_LockStatusMap = new Dictionary<
            char,
            VCLockStatus
        >
        {
            { ' ', VCLockStatus.NoLock },
            { 'K', VCLockStatus.LockedHere },
            { 'O', VCLockStatus.LockedOther },
            { 'T', VCLockStatus.LockedButStolen },
            { 'B', VCLockStatus.BrokenLock },
        };

        private static readonly Dictionary<char, VCPropertiesStatus> m_PropertyStatusMap =
            new Dictionary<char, VCPropertiesStatus>
            {
                { ' ', VCPropertiesStatus.Normal },
                { 'C', VCPropertiesStatus.Conflicted },
                { 'M', VCPropertiesStatus.Modified },
            };

        private static readonly Dictionary<char, VCTreeConflictStatus> m_ConflictStatusMap =
            new Dictionary<char, VCTreeConflictStatus>
            {
                { ' ', VCTreeConflictStatus.Normal },
                { 'C', VCTreeConflictStatus.TreeConflict },
            };

        private static readonly Dictionary<char, VCRemoteFileStatus> m_RemoteStatusMap =
            new Dictionary<char, VCRemoteFileStatus>
            {
                { ' ', VCRemoteFileStatus.None },
                { '*', VCRemoteFileStatus.Modified },
            };

        private static readonly Dictionary<
            UpdateResolveConflicts,
            string
        > m_UpdateResolveConflictsMap = new Dictionary<UpdateResolveConflicts, string>
        {
            { UpdateResolveConflicts.Postpone, "postpone" },
            { UpdateResolveConflicts.Working, "working" },
            { UpdateResolveConflicts.Base, "base" },
            { UpdateResolveConflicts.MineConflict, "mine-conflict" },
            { UpdateResolveConflicts.TheirsConflict, "theirs-conflict" },
            { UpdateResolveConflicts.MineFull, "mine-full" },
            { UpdateResolveConflicts.TheirsFull, "theirs-full" },
            { UpdateResolveConflicts.Edit, "edit" },
            { UpdateResolveConflicts.Launch, "launch" },
        };

        private static readonly Dictionary<char, LogPathChange> m_LogPathChangesMap =
            new Dictionary<char, LogPathChange>
            {
                { 'A', LogPathChange.Added },
                { 'D', LogPathChange.Deleted },
                { 'R', LogPathChange.Replaced },
                { 'M', LogPathChange.Modified },
            };

		#endregion
        internal const int COMMAND_TIMEOUT = 20000; // Milliseconds
        internal const int ONLINE_COMMAND_TIMEOUT = 45000; // Milliseconds

        private static string SVN_Command => "svn";

        [NonSerialized]
        private static string m_LastDisplayedError = string.Empty;

        public static bool Silent => m_SilenceCount > 0;

        private static int m_SilenceCount = 0;

        private static string SVNFormatPath(string path)
        {
            // NOTE: @ is added at the end of path, to avoid problems when file name contains @, and SVN mistakes that as "At revision" syntax".
            //		https://stackoverflow.com/questions/757435/how-to-escape-characters-in-subversion-managed-file-names
            return path + "@";
        }

        public static string AssetPathToURL(string assetPath)
        {
            var result = ShellUtils.ExecuteCommand(
                "svn",
                $"info \"{SVNFormatPath(assetPath)}\"",
                COMMAND_TIMEOUT
            );
            if (result.HasErrors)
                return string.Empty;

            return ExtractLineValue("URL:", result.Output);
        }

        private static StatusOperationResult ParseCommonStatusError(string error)
        {
            // svn: warning: W155007: '...' is not a working copy!
            // This can be returned when project is not a valid svn checkout. (Probably)
            if (error.Contains("W155007"))
                return StatusOperationResult.NotWorkingCopy;

            // System.ComponentModel.Win32Exception (0x80004005): ApplicationName='...', CommandLine='...', Native error= The system cannot find the file specified.
            // Could not find the command executable. The user hasn't installed their CLI (Command Line Interface) so we're missing an "svn.exe" in the PATH environment.
            if (error.Contains("0x80004005"))
                return StatusOperationResult.ExecutableNotFound;

            // User needs to log in using normal SVN client and save their authentication.
            // svn: E170013: Unable to connect to a repository at URL '...'
            // svn: E230001: Server SSL certificate verification failed: issuer is not trusted
            // svn: E215004: No more credentials or we tried too many times.
            // Authentication failed
            if (error.Contains("E230001") || error.Contains("E215004"))
                return StatusOperationResult.AuthenticationFailed;

            // Unable to connect to repository indicating some network or server problems.
            // svn: E170013: Unable to connect to a repository at URL '...'
            // svn: E731001: No such host is known.
            if (error.Contains("E170013") || error.Contains("E731001"))
                return StatusOperationResult.UnableToConnectError;

            // Operation took too long, shell utils time out kicked in.
            if (error.Contains(ShellUtils.TIME_OUT_ERROR_TOKEN))
                return StatusOperationResult.Timeout;

            return StatusOperationResult.UnknownError;
        }

        public static StatusOperationResult GetStatuses(
            string path,
            bool recursive,
            bool offline,
            List<SVNStatusData> resultEntries,
            bool fetchLockDetails = false,
            int timeout = ONLINE_COMMAND_TIMEOUT,
            IShellMonitor shellMonitor = null
        )
        {
            var depth = recursive ? "infinity" : "empty";
            var offlineArg = offline ? string.Empty : "-u";

            var result = ShellUtils.ExecuteCommand(
                SVN_Command,
                $"status --depth={depth} {offlineArg} \"{SVNFormatPath(path)}\"",
                timeout,
                shellMonitor
            );

            if (result.HasErrors)
            {
                // svn: warning: W155010: The node '...' was not found.
                // This can be returned when path is under unversioned directory. In that case we consider it is unversioned as well.
                if (result.Error.Contains("W155010"))
                {
                    resultEntries.Add(
                        new SVNStatusData()
                        {
                            Path = path,
                            Status = VCFileStatus.Unversioned,
                            LockDetails = LockDetails.Empty
                        }
                    );
                    return StatusOperationResult.Success;
                }

                return ParseCommonStatusError(result.Error);
            }

            // If -u is used, an additional line is added toward the end of the output but _before_ any change list entries:
            // Status against revision:     14
            //
            // --- Changelist 'Scene Changes':
            // M                4   Assets\Scenes\SampleScene.unity
            //
            // Remove the first line if it begins with "Status" in this case so that the check for an empty output works in the cases where
            // there are one or more change list entries.
            if (
                !offline
                && result.Output != null
                && result.Output.StartsWith("Status", StringComparison.Ordinal)
            )
            {
                using var sr = new StringReader(result.Output);
                sr.ReadLine();
                result.Output = sr.ReadToEnd();
            }

            bool emptyOutput = string.IsNullOrWhiteSpace(result.Output);

            // Empty result could also mean: file doesn't exist.
            // Note: svn-deleted files still have svn status, so always check for status before files on disk.
            if (emptyOutput)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return StatusOperationResult.TargetPathNotFound;
            }

            // If no info is returned for path, the status is normal. Reflect this when searching for Empty depth.
            if (!recursive && emptyOutput)
            {
                resultEntries.Add(
                    new SVNStatusData()
                    {
                        Status = VCFileStatus.Normal,
                        Path = path,
                        LockDetails = LockDetails.Empty
                    }
                );
                return StatusOperationResult.Success;
            }

            resultEntries.AddRange(
                ExtractStatuses(
                    result.Output,
                    recursive,
                    offline,
                    fetchLockDetails,
                    timeout,
                    shellMonitor
                )
            );
            return StatusOperationResult.Success;
        }

        public static bool IsHiddenPath(string path)
        {
            for (int i = 0, len = path.Length; i < len - 1; ++i)
            {
                if (path[i + 1] == '.' && (path[i] == '/' || path[i] == '\\'))
                    return true;
            }

            return false;
        }

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

        public static LockDetails FetchLockDetails(
            string path,
            int timeout = ONLINE_COMMAND_TIMEOUT,
            IShellMonitor shellMonitor = null
        )
        {
            string url;
            LockDetails lockDetails = LockDetails.Empty;

            path = path.Replace('\\', '/');

            // Only files can be locked.
            // If repository is out of date it may receive entries for existing files on the server that are not present locally yet.
            if (!File.Exists(path))
            {
                lockDetails.Path = path;
                lockDetails.OperationResult = StatusOperationResult.TargetPathNotFound;
                return lockDetails;
            }

            //
            // Find the repository url of the path.
            // We need to call "svn info [repo-url]" in order to get up to date repository information.
            // NOTE: Project url can be cached and prepended to path, but externals may have different base url.
            //
            {
                var result = ShellUtils.ExecuteCommand(
                    SVN_Command,
                    $"info \"{SVNFormatPath(path)}\"",
                    timeout,
                    shellMonitor
                );

                url = ExtractLineValue("URL:", result.Output);

                if (result.HasErrors || string.IsNullOrEmpty(url))
                {
                    // svn: warning: W155010: The node '...' was not found.
                    // This can be returned when path is under unversioned directory. In that case we consider it is unversioned as well.
                    if (result.Error.Contains("W155010"))
                    {
                        lockDetails.Path = path; // LockDetails is still valid, just no lock.
                        return lockDetails;
                    }

                    // svn: warning: W155007: '...' is not a working copy!
                    // This can be returned when project is not a valid svn checkout. (Probably)
                    if (result.Error.Contains("W155007"))
                    {
                        lockDetails.OperationResult = StatusOperationResult.NotWorkingCopy;
                        return lockDetails;
                    }

                    // System.ComponentModel.Win32Exception (0x80004005): ApplicationName='...', CommandLine='...', Native error= The system cannot find the file specified.
                    // Could not find the command executable. The user hasn't installed their CLI (Command Line Interface) so we're missing an "svn.exe" in the PATH environment.
                    if (result.Error.Contains("0x80004005"))
                    {
                        lockDetails.OperationResult = StatusOperationResult.ExecutableNotFound;
                        return lockDetails;
                    }

                    // Operation took too long, shell utils time out kicked in.
                    if (result.Error.Contains(ShellUtils.TIME_OUT_ERROR_TOKEN))
                    {
                        lockDetails.OperationResult = StatusOperationResult.Timeout;
                        return lockDetails;
                    }

                    lockDetails.OperationResult = StatusOperationResult.UnknownError;
                    lockDetails.m_GotEmptyResponse =
                        string.IsNullOrEmpty(result.Output) && string.IsNullOrEmpty(result.Error);
                    return lockDetails;
                }
            }

            //
            // Get the actual owner from the repository (using the url).
            //
            {
                var result = ShellUtils.ExecuteCommand(
                    SVN_Command,
                    $"info \"{SVNFormatPath(url)}\"",
                    timeout,
                    shellMonitor
                );

                lockDetails.Owner = ExtractLineValue("Lock Owner:", result.Output);

                if (result.HasErrors || string.IsNullOrEmpty(lockDetails.Owner))
                {
                    // Owner will be missing if there is no lock. If true, just find something familiar to confirm it was not an error.
                    if (result.Output.IndexOf("URL:", StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        lockDetails.Path = path; // LockDetails is still valid, just no lock.
                        return lockDetails;
                    }

                    lockDetails.OperationResult = ParseCommonStatusError(result.Error);
                    lockDetails.m_GotEmptyResponse =
                        string.IsNullOrEmpty(result.Output) && string.IsNullOrEmpty(result.Error);
                    return lockDetails;
                }

                lockDetails.Path = path;
                lockDetails.Date = ExtractLineValue("Lock Created:", result.Output);

                // Locked message looks like this:
                // Lock Comment (4 lines):
                // Foo
                // Bar
                // ...
                // The number of lines is arbitrary. If there is no comment, this section is omitted.
                var lockMessageLineIndex = result.Output.IndexOf(
                    "Lock Comment",
                    StringComparison.OrdinalIgnoreCase
                );
                if (lockMessageLineIndex != -1)
                {
                    var lockMessageStart =
                        result.Output.IndexOf(
                            "\n",
                            lockMessageLineIndex,
                            StringComparison.OrdinalIgnoreCase
                        ) + 1;
                    lockDetails.Message = result.Output
                        .Substring(lockMessageStart)
                        .Replace("\r", "");
                    // Fuck '\r'
                }
            }

            return lockDetails;
        }

        private static IEnumerable<SVNStatusData> ExtractStatuses(
            string output,
            bool recursive,
            bool offline,
            bool fetchLockDetails,
            int timeout,
            IShellMonitor shellMonitor = null
        )
        {
            using (var sr = new StringReader(output))
            {
                string line = string.Empty;
                string nextLine = sr.ReadLine();

                while (true)
                {
                    line = nextLine;
                    if (line == null) // End of reader reached.
                        break;

                    nextLine = sr.ReadLine();

                    var lineLen = line.Length;

                    // Last status was deleted / added+, so this is telling us where it moved to / from. Skip it.
                    if (lineLen > 8 && line[8] == '>')
                        continue;

                    // Tree conflict "local dir edit, incoming dir delete or move upon switch / update" or similar.
                    if (lineLen > 6 && line[6] == '>')
                        continue;

                    // If there are any conflicts, the report will have two additional lines like this after any changelists:
                    // Summary of conflicts:
                    // Text conflicts: 1
                    if (line.StartsWith("Summary", StringComparison.Ordinal))
                        break;

                    // If -u is used, additional line is added at the end:
                    // Status against revision:     14
                    if (line.StartsWith("Status", StringComparison.Ordinal))
                        continue;

                    // All externals append separate sections with their statuses:
                    // Performing status on external item at '...':
                    if (line.StartsWith("Performing status", StringComparison.Ordinal))
                        continue;

                    // If the user has files in one or more change lists such as the TortoiseSVN-reserved "ignore-on-commit"
                    // change list and/or user-created change lists, these are added at the end plus an empty line per change
                    // list:
                    // --- Changelist 'Script Changes':
                    // A       Assets\Scripts\NewScript.cs
                    // A       Assets\Scripts\NewScript.cs.meta
                    //
                    // --- Changelist 'ignore-on-commit':
                    // A       Assets\Scripts\OtherScript.cs
                    // A       Assets\Scripts\OtherScript.cs.meta
                    //
                    // --- Changelist 'Scene Changes':
                    // M       Assets\Scenes\SampleScene.unity
                    //
                    // Skip the header and blank lines but get the statuses.
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.StartsWith("---", StringComparison.Ordinal))
                        continue;

                    // Rules are described in "svn help status".
                    var statusData = new SVNStatusData();
                    statusData.Status = m_FileStatusMap[line[0]];
                    statusData.PropertiesStatus = m_PropertyStatusMap[line[1]];
                    statusData.SwitchedExternalStatus = m_SwitchedExternalStatusMap[line[4]];
                    statusData.LockStatus = m_LockStatusMap[line[5]];
                    statusData.TreeConflictStatus = m_ConflictStatusMap[line[6]];
                    statusData.LockDetails = LockDetails.Empty;

                    // Last status was deleted / added+, so this is telling us where it moved to / from.
                    if (nextLine != null && nextLine.Length > 8 && nextLine[8] == '>')
                    {
                        if (statusData.Status == VCFileStatus.Deleted)
                        {
                            int movedPathStartIndex = "        > moved to ".Length;
                            statusData.MovedTo = nextLine
                                .Substring(movedPathStartIndex)
                                .Replace('\\', '/');
                        }
                        if (statusData.Status == VCFileStatus.Added)
                        {
                            int movedPathStartIndex = "        > moved from ".Length;
                            statusData.MovedFrom = nextLine
                                .Substring(movedPathStartIndex)
                                .Replace('\\', '/');
                        }
                    }

                    // 7 columns statuses + space;
                    int pathStart = 7 + 1;

                    if (!offline)
                    {
                        // + remote status + revision
                        pathStart += 13;
                        statusData.RemoteStatus = m_RemoteStatusMap[line[8]];
                    }

                    statusData.Path = line.Substring(pathStart).Replace('\\', '/');

                    // NOTE: If you pass absolute path to svn, the output will be with absolute path -> always pass relative path and we'll be good.
                    // If path is not relative, make it.
                    //if (!statusData.Path.StartsWith("Assets", StringComparison.Ordinal)) {
                    //	// Length+1 to skip '/'
                    //	statusData.Path = statusData.Path.Remove(0, ProjectRoot.Length + 1);
                    //}

                    if (IsHiddenPath(statusData.Path))
                        continue;

                    if (!offline && fetchLockDetails)
                    {
                        if (
                            statusData.LockStatus != VCLockStatus.NoLock
                            && statusData.LockStatus != VCLockStatus.BrokenLock
                        )
                        {
                            statusData.LockDetails = FetchLockDetails(
                                statusData.Path,
                                timeout,
                                shellMonitor
                            );

                            // HACK: sometimes "svn info ..." commands return empty results (empty lock details) after assembly reload.
                            //		 if that happens, try a few more times.
                            // NOTE: This may have been fixed by the proper closing of the streams in the ShellUtils.
                            for (int i = 0; i < 3 && statusData.LockDetails.m_GotEmptyResponse; ++i)
                            {
                                System.Threading.Thread.Sleep(20);
                                statusData.LockDetails = FetchLockDetails(
                                    statusData.Path,
                                    timeout,
                                    shellMonitor
                                );
                                //Debug.LogError($"Attempt {i} {statusData.LockDetails.m_GotEmptyResponse} {statusData.Path}");
                            }
                        }
                    }

                    yield return statusData;
                }
            }
        }

        public static RevertOperationResult Revert(
            IEnumerable<string> assetPaths,
            bool includeMeta,
            bool recursive,
            bool removeAdded,
            string targetsFileToUse = "",
            int timeout = -1,
            IShellMonitor shellMonitor = null
        )
        {
            targetsFileToUse = string.IsNullOrEmpty(targetsFileToUse)
                ? FileUtil.GetUniqueTempPathInProject()
                : targetsFileToUse;
            if (includeMeta)
            {
                assetPaths = assetPaths.Select(path => path + ".meta").Concat(assetPaths);
            }
            File.WriteAllLines(targetsFileToUse, assetPaths.Select(SVNFormatPath));

            var depth = recursive ? "infinity" : "empty";
            var removeAddedArg = removeAdded ? "--remove-added" : "";

            var result = ShellUtils.ExecuteCommand(
                SVN_Command,
                $"revert --targets \"{targetsFileToUse}\" --depth {depth} {removeAddedArg}",
                timeout,
                shellMonitor
            );
            if (result.HasErrors)
            {
                // Operation took too long, shell utils time out kicked in.
                if (result.Error.Contains(ShellUtils.TIME_OUT_ERROR_TOKEN))
                    return RevertOperationResult.Timeout;

                return RevertOperationResult.UnknownError;
            }

            return RevertOperationResult.Success;
        }

        internal static void LogStatusErrorHint(StatusOperationResult result, string suffix = null)
        {
            if (result == StatusOperationResult.Success)
                return;

            string displayMessage;

            switch (result)
            {
                case StatusOperationResult.NotWorkingCopy:
                    displayMessage = string.Empty;
                    break;

                case StatusOperationResult.TargetPathNotFound:
                    // We can be checking moved-to path, that shouldn't exist, so this is normal.
                    //displayMessage = "Target file/folder not found.";
                    displayMessage = string.Empty;
                    break;

                case StatusOperationResult.AuthenticationFailed:
                    displayMessage =
                        $"SVN Error: Trying to reach server repository failed because authentication is needed!";
                    break;

                case StatusOperationResult.UnableToConnectError:
                    displayMessage =
                        "SVN Error: Unable to connect to SVN repository server. Check your network connection. Overlay icons may not work correctly.";
                    break;

                case StatusOperationResult.ExecutableNotFound:

                    displayMessage = $"SVN CLI (Command Line Interface) not found by WiseSVN. ";
                    break;

                default:
                    displayMessage =
                        $"SVN \"{result}\" error occurred while processing the assets. Check the logs for more info.";
                    break;
            }

            if (
                !string.IsNullOrEmpty(displayMessage)
                && !Silent
                && m_LastDisplayedError != displayMessage
            )
            {
                Debug.LogError($"{displayMessage} {suffix}\n");
                m_LastDisplayedError = displayMessage;
                //DisplayError(displayMessage);	// Not thread-safe.
            }
        }

        public static SVNStatusData GetStatus(
            string path,
            bool logErrorHint = true,
            IShellMonitor shellMonitor = null
        )
        {
            List<SVNStatusData> resultEntries = new List<SVNStatusData>();

            // Optimization: empty depth will return nothing if status is normal.
            // If path is modified, added, deleted, unversioned, it will return proper value.
            StatusOperationResult result = GetStatuses(
                path,
                false,
                true,
                resultEntries,
                false,
                COMMAND_TIMEOUT,
                shellMonitor
            );

            if (logErrorHint)
            {
                LogStatusErrorHint(result);
            }

            SVNStatusData statusData = resultEntries.FirstOrDefault();

            // If no path was found, error happened.
            if (!statusData.IsValid || result != StatusOperationResult.Success)
            {
                // Fallback to unversioned as we don't touch them.
                statusData.Status = VCFileStatus.Unversioned;
            }

            return statusData;
        }

        public static bool IsWorkingCopy(string path)
        {
            var result = ShellUtils.ExecuteCommand(
                "svn",
                $"info \"{SVNFormatPath(path)}\"",
                COMMAND_TIMEOUT
            );

            return !result.HasErrors
                && !string.IsNullOrEmpty(ExtractLineValue("URL:", result.Output));
        }

        public static bool AddToWorkspace(IEnumerable<string> assetPaths, bool includeMeta = true)
        {
            var metaPaths = includeMeta
                ? assetPaths.Select(path => path + ".meta")
                : new List<string>();

            var _targetPaths = assetPaths.Concat(metaPaths);
            var _targetPathsStr = string.Join(" ", _targetPaths);

            var result = ShellUtils.ExecuteCommand(SVN_Command, $"add {_targetPathsStr}", true);
            if (result.HasErrors)
            {
                Debug.LogError(result.Error);
                return false;
            }

            return !result.HasErrors;
        }

        public static bool Commit(IEnumerable<string> assetPaths, bool includeMeta = true)
        {
            var metaPaths = includeMeta
                ? assetPaths.Select(path => path + ".meta")
                : new List<string>();

            var _targetPaths = assetPaths.Concat(metaPaths);
            var _targetPathsStr = string.Join(" ", _targetPaths);

            var result = ShellUtils.ExecuteCommand(
                SVN_Command,
                $"commit -m \"UNIArt Auto Commit\" {_targetPathsStr}",
                true
            );

            if (result.HasErrors)
            {
                Debug.LogError(result.Error);
                return false;
            }

            return true;
        }

        public static List<(string Dir, string Url)> GetExternals(string path)
        {
            var result = ShellUtils.ExecuteCommand(
                SVN_Command,
                $"propget svn:externals \"{path}\"",
                Encoding.GetEncoding(936),
                true
            );

            if (result.HasErrors)
            {
                return new List<(string, string)>();
            }

            var externals = new List<(string, string)>();
            foreach (var line in result.Output.Split('\n'))
            {
                if (line.StartsWith("svn: "))
                    continue;

                var parts = line.Split(' ');
                if (parts.Length < 2)
                {
                    continue;
                }

                var externalPath = parts[0];
                var externalUrl = parts[1];

                externals.Add((externalPath, externalUrl));
            }

            return externals;
        }

        public static bool HasExternal(string externalPath, string ExternalDir)
        {
            var _externals = GetExternals(externalPath);
            return _externals.Any(x => x.Dir == ExternalDir);
        }

        // 添加external
        public static void AddExternal(string externalPath, string externalUrl)
        {
            // 如果external不是工作副本,则使用svn add将其添加到工作副本中
            if (!IsWorkingCopy(externalPath))
            {
                AddToWorkspace(new List<string> { externalPath });
                Commit(new List<string> { externalPath });
            }

            var _externals = GetExternals(externalPath);

            if (_externals.Exists(_ => _.Url == externalUrl))
            {
                return;
            }

            // 设置外部
            var _folderName = Path.GetFileName(externalUrl);
            _externals.Add((_folderName, externalUrl));

            var _externalsStr = string.Join(
                Environment.NewLine,
                _externals.Select(x => $"\"{x.Item1}\" \"{x.Item2}\"")
            );

            var _args = $"propset svn:externals \"{_externalsStr}\" {externalPath}";

            var result = ShellUtils.ExecuteCommand("svn", _args, Encoding.GetEncoding(936), true);
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
                return;
            }

            // 更新外部资源
            var workingDir = $"{SVNConextMenu.ProjectRootUnity}/{externalPath}";
            result = ShellUtils.ExecuteCommand(
                "svn",
                $"update",
                workingDir,
                Encoding.GetEncoding(936)
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
                return;
            }

            AssetDatabase.Refresh();
        }

        public static void RemoveExternal(string externalPath, string externalUrl)
        {
            var _externals = GetExternals(externalPath);

            var _folderName = Path.GetFileName(externalUrl);
            var _external = _externals.FirstOrDefault(x => x.Dir == _folderName);
            if (_external == default)
            {
                return;
            }

            _externals.Remove(_external);

            var _externalsStr = string.Join(
                Environment.NewLine,
                _externals.Select(x => $"\"{x.Item1}\" \"{x.Item2}\"")
            );

            var _args = $"propset svn:externals \"{_externalsStr}\" {externalPath}";

            var result = ShellUtils.ExecuteCommand("svn", _args, Encoding.GetEncoding(936), true);
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
                return;
            }

            var workingDir = $"{SVNConextMenu.ProjectRootUnity}/{externalPath}";
            result = ShellUtils.ExecuteCommand(
                "svn",
                $"update",
                workingDir,
                Encoding.GetEncoding(936)
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
                return;
            }

            var removeTarget = $"{workingDir}/{_folderName}";

            // 移除 .meta
            var metaPath = removeTarget + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }

        public static bool Update(string workingDir)
        {
            if (!IsWorkingCopy(workingDir))
            {
                return false;
            }

            var result = ShellUtils.ExecuteCommand(
                "svn",
                $"update",
                workingDir,
                Encoding.GetEncoding(936)
            );
            if (result.HasErrors)
            {
                Debug.LogError($"SVN Error: {result.Error}");
                return false;
            }
            return true;
        }
    }
}
