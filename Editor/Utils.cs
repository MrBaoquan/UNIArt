using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UNIHper.Art.Editor
{
    public static class Utils
    {
        const string packageName = "com.parful.uniart";

        public static string ToForwardSlash(this string path)
        {
            return path.Replace("\\", "/");
        }

        public static string PackageAssetPath(string assetPath)
        {
            return $"Packages/{packageName}/{assetPath}";
        }

        public static void RemoveHierarchyWindowItemOnGUI(List<string> methods)
        {
            FieldInfo eventField = typeof(EditorApplication).GetField(
                "hierarchyWindowItemOnGUI",
                BindingFlags.Static | BindingFlags.Public
            );

            if (eventField == null)
                return;

            Delegate eventDelegate = (Delegate)eventField.GetValue(null);
            if (eventDelegate == null)
                return;

            Delegate[] subscribers = eventDelegate.GetInvocationList();

            subscribers
                .Where(_ => methods.Contains(_.Method.Name))
                .ToList()
                .ForEach(_ =>
                {
                    EditorApplication.hierarchyWindowItemOnGUI -=
                        (EditorApplication.HierarchyWindowItemCallback)_;
                });
        }

        public static void Delay(Action callback, float duration)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(
                DelayCoroutine(() => callback?.Invoke(), duration)
            );
        }

        private static IEnumerator DelayCoroutine(Action callback, float duration)
        {
            yield return new EditorWaitForSeconds(duration);
            callback();
        }

        public static string PrefabStageAssetPath()
        {
            var _prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (_prefabStage == null)
                return string.Empty;

            return _prefabStage.assetPath;
        }

        public static string GetPrefabAssetPathByAnyGameObject(GameObject selected)
        {
            if (selected == null)
                return string.Empty;

            if (PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                return AssetDatabase.GetAssetPath(selected);
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
                return string.Empty;

            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(selected);
            if (prefabRoot != null)
            {
                return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
            }

            if (selected.transform.IsChildOf(prefabStage.prefabContentsRoot.transform))
            {
                return prefabStage.assetPath;
            }

            return string.Empty;
        }

        public static (float width, float height) CalculateScaledImageSize(
            float windowWidth,
            float windowHeight,
            float imageWidth,
            float imageHeight
        )
        {
            // 如果图片尺寸小于窗口尺寸，返回原始图片尺寸
            if (imageWidth <= windowWidth && imageHeight <= windowHeight)
            {
                return (imageWidth, imageHeight);
            }

            // 计算图片和窗口的宽高比
            float imageAspectRatio = imageWidth / imageHeight;
            float windowAspectRatio = windowWidth / windowHeight;

            // 目标尺寸
            float targetWidth,
                targetHeight;

            // 判断根据宽度或高度进行缩放
            if (imageAspectRatio > windowAspectRatio)
            {
                // 以窗口宽度为基准进行缩放
                targetWidth = windowWidth;
                targetHeight = windowWidth / imageAspectRatio;
            }
            else
            {
                // 以窗口高度为基准进行缩放
                targetHeight = windowHeight;
                targetWidth = windowHeight * imageAspectRatio;
            }

            // 返回缩放后的宽高
            return (targetWidth, targetHeight);
        }
    }
}
