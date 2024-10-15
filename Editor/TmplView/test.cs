using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class GenericMenuWithUIToolkit : EditorWindow
{
    [MenuItem("Tools/UIToolkit GenericMenu Example")]
    public static void ShowWindow()
    {
        // 显示窗口
        GenericMenuWithUIToolkit wnd = GetWindow<GenericMenuWithUIToolkit>();
        wnd.titleContent = new GUIContent("GenericMenu with UIToolkit");
    }

    public void CreateGUI()
    {
        // 创建按钮
        Button dropdownButton = null;
        dropdownButton = new Button(() =>
        {
            // 按钮点击时显示 GenericMenu
            ShowGenericMenu(dropdownButton);
        })
        {
            text = "Show Menu"
        };

        // 将按钮添加到根 VisualElement
        rootVisualElement.Add(dropdownButton);
    }

    private void ShowGenericMenu(Button dropdownButton)
    {
        // 创建 GenericMenu
        GenericMenu menu = new GenericMenu();

        // 添加菜单项
        menu.AddItem(new GUIContent("Option 1"), false, () => Debug.Log("Option 1 selected"));
        menu.AddItem(new GUIContent("Option 2"), false, () => Debug.Log("Option 2 selected"));
        menu.AddItem(new GUIContent("Option 3"), false, () => Debug.Log("Option 3 selected"));

        // 获取按钮在屏幕中的位置信息
        var worldBound = dropdownButton.worldBound;
        Vector2 menuPosition = GUIUtility.GUIToScreenPoint(
            new Vector2(worldBound.xMin, worldBound.yMax)
        );

        // 自定义菜单的显示位置，确保它在按钮下方
        Rect rect = new Rect(menuPosition, Vector2.zero);
        menu.DropDown(rect);
    }
}
