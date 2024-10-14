using PluginMaster;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class PrefabComponentCopier
{
    // 将源预制体的所有子物体及其组件复制到目标预制体
    public static void CopyComponentsAndChildren(GameObject sourcePrefab, GameObject targetPrefab)
    {
        if (sourcePrefab == null || targetPrefab == null)
        {
            Debug.LogError("源预制体或目标预制体为空！");
            return;
        }

        CopyChildrenRecursive(sourcePrefab.transform, targetPrefab.transform);
    }

    // 递归复制子物体及其组件
    private static void CopyChildrenRecursive(Transform sourceTransform, Transform targetTransform)
    {
        for (int i = 0; i < sourceTransform.childCount; i++)
        {
            Transform sourceChild = sourceTransform.GetChild(i);
            Transform targetChild = targetTransform.Find(sourceChild.name);

            if (targetChild == null)
            {
                targetChild = GameObject
                    .Instantiate(sourceChild.gameObject, targetTransform)
                    .transform;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(targetChild.gameObject);
                targetChild.gameObject.name = sourceChild.name;
                targetChild.SetSiblingIndex(i);
            }
            else
            {
                targetChild.SetSiblingIndex(i);
                CopyComponents(sourceChild.gameObject, targetChild.gameObject);
                CopyChildrenRecursive(sourceChild, targetChild);
            }
            targetChild.gameObject.SetActive(sourceChild.gameObject.activeSelf);
            // Debug.Log($"{i}/{sourceTransform.childCount} 复制子物体：{sourceChild.name}");
        }
    }

    private static void CopyComponents(GameObject source, GameObject target)
    {
        Component[] sourceComponents = source.GetComponents<Component>();

        foreach (Component sourceComponent in sourceComponents)
        {
            if (
                sourceComponent is Transform
                || sourceComponent is PsGroup
                || sourceComponent is Image
                || sourceComponent is null
            )
            {
                continue;
            }

            Component targetComponent = target.GetComponent(sourceComponent.GetType());

            if (targetComponent == null)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(target);
            }
            else
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent);
                UnityEditorInternal.ComponentUtility.PasteComponentValues(targetComponent);
            }
        }
    }
}
