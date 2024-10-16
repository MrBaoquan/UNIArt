using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public enum ButtonState
    {
        Normal,
        Hover,
    }

    public class FilterButton : VisualElement
    {
        public const string CreateNewText = "___New Folder";
        public bool IsNew => titleLabel.text == CreateNewText;
        public bool IsAll => FilterID == string.Empty;
        private static Dictionary<string, string> filterTextMap = new Dictionary<string, string>()
        {
            { string.Empty, "全部" },
            { "Widgets", "UI组件" },
            { "Windows", "UI页面" },
        };

        public string assetGUID = string.Empty;
        private string filterID = string.Empty;

        public string FilterID
        {
            get => filterID;
            set
            {
                filterID = value;
                titleLabel.text = filterTextMap.ContainsKey(value) ? filterTextMap[value] : value;
                if (IsNew)
                {
                    this.style.display = DisplayStyle.None;
                }
            }
        }

        public int OrderID
        {
            get
            {
                if (filterID == string.Empty)
                {
                    return 0;
                }
                if (filterID == "Windows")
                {
                    return 1;
                }
                if (filterID == "Widgets")
                {
                    return 2;
                }
                else if (filterID == CreateNewText)
                {
                    return 10000;
                }
                return 10;
            }
        }

        public void Select()
        {
            if (!this.Q<VisualElement>("root").ClassListContains("selected"))
            {
                this.Q<VisualElement>("root").AddToClassList("selected");
            }
        }

        public void Deselect()
        {
            if (this.Q<VisualElement>("root").ClassListContains("selected"))
            {
                this.Q<VisualElement>("root").RemoveFromClassList("selected");
            }
        }

        public void DoEdit()
        {
            if (IsNew)
            {
                this.style.display = DisplayStyle.Flex;
            }
            var _inputField = this.Q<TextField>("input");
            _inputField.style.display = DisplayStyle.Flex;

            var _label = this.Q<Label>("title");
            _label.style.display = DisplayStyle.None;

            _inputField.value = IsNew ? "新建文件夹" : FilterID;
            _inputField.Focus();
        }

        public void DoText()
        {
            if (IsNew)
            {
                this.style.display = DisplayStyle.None;
            }
            inputField.style.display = DisplayStyle.None;
            titleLabel.style.display = DisplayStyle.Flex;
        }

        public UnityEvent<string, string> OnConfirmInput = new UnityEvent<string, string>();

        private Label titleLabel;
        private TextField inputField;

        public FilterButton()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/FilterButton/FilterButton.uxml")
            );

            visualTree.CloneTree(this);

            titleLabel = this.Q<Label>("title");
            titleLabel.text = FilterID;

            inputField = this.Q<TextField>("input");
            inputField.style.display = DisplayStyle.None;

            string _blurType = "";
            // esc取消事件
            inputField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Return)
                {
                    if (string.IsNullOrEmpty(inputField.value))
                        return;
                    _blurType = "confirm";
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    _blurType = "cancel";
                    // Debug.LogWarning("cancel");
                    // DoText();
                }
            });

            // blur
            inputField.RegisterCallback<BlurEvent>(e =>
            {
                if (_blurType != "cancel")
                {
                    DoText();
                    if (inputField.value == "")
                    {
                        return;
                    }

                    var _oldVal = FilterID;
                    var _newVal = inputField.value;
                    inputField.value = string.Empty;

                    if (_newVal == _oldVal && !IsNew)
                        return;

                    if (!IsNew)
                        FilterID = _newVal;

                    OnConfirmInput.Invoke(_oldVal, _newVal);
                }
                else
                {
                    DoText();
                }
            });
        }
    }
}
