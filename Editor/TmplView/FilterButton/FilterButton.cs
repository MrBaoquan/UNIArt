using UnityEditor;
using UnityEngine.UIElements;

namespace UNIHper.Art.Editor
{
    public enum ButtonState
    {
        Normal,
        Hover,
    }

    public class FilterButton : VisualElement
    {
        public string assetGUID = string.Empty;
        public string FilterID = string.Empty;

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
