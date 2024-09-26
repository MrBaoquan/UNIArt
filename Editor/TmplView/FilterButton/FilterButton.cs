using System.Collections.Generic;
using UnityEditor;
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
        public const string AllFilterID = "全部";

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

        public FilterButton()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                Utils.PackageAssetPath("Editor/TmplView/FilterButton/FilterButton.uxml")
            );

            visualTree.CloneTree(this);
            this.Q<Label>("title").text = FilterID;
        }
    }
}
