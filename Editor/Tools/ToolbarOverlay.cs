using UnityEngine;
using UnityEditor.Toolbars;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEditor;
using R2D;
using UNIArt.Editor;

[EditorToolbarElement(id, typeof(SceneView))]
class ToggleRuler : EditorToolbarToggle
{
    public const string id = "UNIArtTools/R2DToggle";

    public ToggleRuler()
    {
        tooltip = "标尺工具";
        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Packages/com.parful.uniart/Assets/Icon/标尺.png"
        );
        this.RegisterValueChangedCallback(OnStateChange);
    }

    void OnStateChange(ChangeEvent<bool> evt)
    {
        if (evt.newValue)
        {
            EditorWindow.GetWindow(typeof(R2DE_EditorWindow));
        }
        else
        {
            R2DE_EditorWindow r2dWindow = (R2DE_EditorWindow)
                EditorWindow.GetWindow(typeof(R2DE_EditorWindow));
            r2dWindow.Close();
        }
    }
}

[Overlay(typeof(SceneView), "UNIArt Tools")]
[Icon("Packages/com.parful.uniart/Assets/Icon/艺术.png")]
public class UNIArtEditorToolbar : ToolbarOverlay
{
    UNIArtEditorToolbar()
        : base(ToggleRuler.id) { }

    public override void OnCreated()
    {
        Utils.HookUpdateOnce(() => displayed = true);
    }
}
