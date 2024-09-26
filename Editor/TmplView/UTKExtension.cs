using UnityEngine.UIElements;

public static class UTKExtension
{
    public static void ApplyScaleOne(this VisualElement element)
    {
        if (element.ClassListContains("scale-zero"))
        {
            element.RemoveFromClassList("scale-zero");
        }
        if (element.ClassListContains("scale-one"))
            return;
        element.AddToClassList("scale-one");
    }

    public static void ApplyScaleZero(this VisualElement element)
    {
        if (element.ClassListContains("scale-one"))
        {
            element.RemoveFromClassList("scale-one");
        }
        if (element.ClassListContains("scale-zero"))
            return;
        element.AddToClassList("scale-zero");
    }
}
