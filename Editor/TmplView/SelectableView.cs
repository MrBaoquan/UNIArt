using UnityEditor;
using UnityEngine.UIElements;

namespace UNIHper.Art.Editor
{
    public class SelectableView : VisualElement
    {
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

        protected void BindView(string view)
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(view);
            visualTree.CloneTree(this);
        }
    }
}
