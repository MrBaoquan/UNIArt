using UnityEditor;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public class SelectableView : VisualElement
    {
        public void Select()
        {
            if (!root.ClassListContains("selected"))
            {
                root.AddToClassList("selected");
            }
        }

        public void Deselect()
        {
            if (root.ClassListContains("selected"))
            {
                root.RemoveFromClassList("selected");
            }
        }

        public void Toggle()
        {
            if (IsSelected)
            {
                Deselect();
            }
            else
            {
                Select();
            }
        }

        public bool IsSelected => root.ClassListContains("selected");

        VisualElement _root;

        public void SetSelectTarget(VisualElement target)
        {
            _root = target;
        }

        VisualElement root
        {
            get
            {
                if (_root == null)
                {
                    SetSelectTarget(this.Q<VisualElement>("root"));
                }
                return _root;
            }
        }

        protected void BindView(string view)
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(view);
            visualTree.CloneTree(this);
        }
    }
}
