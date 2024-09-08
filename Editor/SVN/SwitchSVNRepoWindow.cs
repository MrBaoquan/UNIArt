using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public class SwitchSVNRepoWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            var window = GetWindow<SwitchSVNRepoWindow>();
            window.titleContent = new GUIContent("SVN 切换仓库");
            window.minSize = new Vector2(600, 180);
            window.maxSize = window.minSize;
        }

        public void CreateGUI()
        {
            var viewAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.parful.uniart/Editor/SVN/UXML/SwitchSVNRepoWindow.uxml"
            );
            rootVisualElement.Add(viewAsset.Instantiate());

            var _newRepoName = rootVisualElement.Q<TextField>("input_newRepoUrl");
            var _curRepoName = rootVisualElement.Q<Label>("label_curRepoUrl");

            var _confirmBtn = rootVisualElement.Q<Button>("btn_confirm");

            var _projectRepoUrl = SVNConextMenu.ProjectRepoUrl();
            var _projectName = Path.GetFileName(_projectRepoUrl);
            _curRepoName.text = _projectRepoUrl;
            _newRepoName.value = _projectRepoUrl;

            _confirmBtn.RegisterCallback<ClickEvent>(evt =>
            {
                if (SwichSVNRepoUrl(_projectRepoUrl, _newRepoName.value))
                    Close();
            });

            var _cancelBtn = rootVisualElement.Q<Button>("btn_cancel");
            _cancelBtn.RegisterCallback<ClickEvent>(evt => Close());
        }

        private bool SwichSVNRepoUrl(string oldRepoUrl, string newRepoUrl)
        {
            var oldRepoName = Path.GetFileName(oldRepoUrl);

            // 校验输入
            if (string.IsNullOrEmpty(oldRepoUrl) || string.IsNullOrEmpty(newRepoUrl))
            {
                EditorUtility.DisplayDialog("错误", "仓库地址不能为空！", "确定");
                return false;
            }

            if (oldRepoUrl == newRepoUrl)
            {
                EditorUtility.DisplayDialog("错误", "新仓库地址不能与旧仓库地址相同！", "确定");
                return false;
            }

            var _notifyAuthorList = SVNConextMenu.GetRepoAuthors().Keys.Select(_ => $"@{_} ");
            var _notifyAuthorStr = string.Join(" ", _notifyAuthorList);

            EditorUtility.DisplayProgressBar("SVN 切换仓库地址", "正在切换仓库地址...", 0.5f);

            // 将本地仓库切换到新地址
            var result = ShellUtils.ExecuteCommand("svn", $"switch \"{newRepoUrl}\"", true);
            EditorUtility.ClearProgressBar();
            if (result.HasErrors)
            {
                EditorUtility.DisplayDialog("错误", result.Error, "确定");
                Debug.LogWarning(result.Error);
                return false;
            }

            EditorUtility.DisplayDialog("提示", "仓库地址切换成功！", "确定");
            return true;
        }
    }
}
