using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public class RenameSVNRepoWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<RenameSVNRepoWindow>();
            window.titleContent = new GUIContent("SVN 仓库重命名");
            window.minSize = new Vector2(400, 180);
            window.maxSize = window.minSize;
        }

        public void CreateGUI()
        {
            var viewAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.parful.uniart/Editor/SVN/UXML/RenameSVNRepoWindow.uxml"
            );
            rootVisualElement.Add(viewAsset.Instantiate());

            var _newRepoName = rootVisualElement.Q<TextField>("input_newRepoName");
            var _curRepoName = rootVisualElement.Q<Label>("label_curRepoName");

            var _confirmBtn = rootVisualElement.Q<Button>("btn_confirm");

            var _projectRepoUrl = SVNConextMenu.ProjectRepoUrl();
            var _projectName = Path.GetFileName(_projectRepoUrl);
            _curRepoName.text = _projectName;
            _newRepoName.value = _projectName;

            _confirmBtn.RegisterCallback<ClickEvent>(evt =>
            {
                if (RenameSVNRemotePath(_projectRepoUrl, _newRepoName.value))
                    Close();
            });

            var _cancelBtn = rootVisualElement.Q<Button>("btn_cancel");
            _cancelBtn.RegisterCallback<ClickEvent>(evt => Close());
        }

        private bool RenameSVNRemotePath(string oldRepoUrl, string newRepoName)
        {
            var oldRepoName = Path.GetFileName(oldRepoUrl);
            var newRepoUrl = oldRepoUrl.Replace(oldRepoName, newRepoName);

            // 校验输入
            if (string.IsNullOrEmpty(oldRepoUrl) || string.IsNullOrEmpty(newRepoUrl))
            {
                EditorUtility.DisplayDialog("错误", "仓库地址不能为空！", "确定");
                return false;
            }

            if (oldRepoName == newRepoName)
            {
                EditorUtility.DisplayDialog("错误", "新仓库地址不能与旧仓库地址相同！", "确定");
                return false;
            }

            var _notifyAuthorList = SVNConextMenu.GetRepoAuthors().Keys.Select(_ => $"@{_} ");
            var _notifyAuthorStr = string.Join(" ", _notifyAuthorList);

            EditorUtility.DisplayProgressBar("SVN 仓库重命名", "正在重命名仓库...", 0);
            var logMsg = $"仓库由 [{oldRepoName}] 重命名为 [{newRepoName}], 请及时切换仓库地址。{_notifyAuthorStr}";
            var result = ShellUtils.ExecuteCommand(
                "svn",
                $"rename \"{oldRepoUrl}\" \"{newRepoUrl}\" -m \"{logMsg}\"",
                true
            );
            EditorUtility.DisplayProgressBar("SVN 仓库重命名", "正在重新关联新地址...", 0.5f);
            if (result.HasErrors)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("错误1", result.Error, "确定");
                Debug.LogWarning(result.Error);
                return false;
            }

            // 将本地仓库切换到新地址
            result = ShellUtils.ExecuteCommand("svn", $"switch \"{newRepoUrl}\"", true);
            EditorUtility.ClearProgressBar();
            if (result.HasErrors)
            {
                EditorUtility.DisplayDialog("错误2", result.Error, "确定");
                Debug.LogWarning(result.Error);
                return false;
            }

            EditorUtility.DisplayDialog("提示", "仓库重命名成功！", "确定");
            return true;
        }
    }
}
