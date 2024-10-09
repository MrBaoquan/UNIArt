using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Events;
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
        public bool IsAll => FilterID == string.Empty;
        private static Dictionary<string, string> filterTextMap = new Dictionary<string, string>()
        {
            { string.Empty, "全部" },
            { "Widgets", "UI组件" },
            { "Windows", "UI页面" }
        };

        public string assetGUID = string.Empty;
        private string filterID = string.Empty;
        public string FilterID
        {
            get => filterID;
            set
            {
                filterID = value;
                this.Q<Label>("title").text = filterTextMap.ContainsKey(value)
                    ? filterTextMap[value]
                    : value;
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
            var _inputField = this.Q<TextField>("input");
            _inputField.style.display = DisplayStyle.Flex;

            var _label = this.Q<Label>("title");
            _label.style.display = DisplayStyle.None;

            _inputField.value = FilterID;
            _inputField.Focus();
        }

        public void DoText()
        {
            var _inputField = this.Q<TextField>("input");
            _inputField.style.display = DisplayStyle.None;

            var _label = this.Q<Label>("title");
            _label.style.display = DisplayStyle.Flex;
        }

        public UnityEvent<string, string> OnConfirmInput = new UnityEvent<string, string>();

        public FilterButton()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/FilterButton/FilterButton.uxml")
            );

            visualTree.CloneTree(this);
            this.Q<Label>("title").text = FilterID;

            var _inputField = this.Q<TextField>("input");

            _inputField.RegisterCallback<FocusOutEvent>(e =>
            {
                if (string.IsNullOrEmpty(_inputField.value))
                    return;

                DoText();
                if (_inputField.value == filterID)
                {
                    return;
                }

                var _oldVal = FilterID;
                FilterID = _inputField.value;
                _inputField.value = string.Empty;
                OnConfirmInput.Invoke(_oldVal, FilterID);
            });
        }
    }
}
