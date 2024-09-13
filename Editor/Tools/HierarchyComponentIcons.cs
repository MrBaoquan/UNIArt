using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace UNIArt.Editor
{
    [InitializeOnLoad]
    public class HierarchyComponentIcons
    {
        private static List<Type> componentTypes;

        static Dictionary<Type, string> componentNames = new Dictionary<Type, string>
        {
            { typeof(Animator), "动画控制器" },
            { typeof(Button), "按钮" },
            { typeof(Canvas), "画布" },
            { typeof(Camera), "相机" },
        };

        static HierarchyComponentIcons()
        {
            componentTypes = new List<Type>
            {
                typeof(AnimationClip),
                typeof(Animator),
                typeof(Button),
                typeof(Canvas),
                typeof(Camera),
                typeof(MonoScript)
            };

            if (!UNIArtSettings.Editor.EnableHierarchyItemGUI.Value)
                return;

            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
        }

        private static bool HasAnimation(GameObject obj)
        {
            var _animator = obj.GetComponent<Animator>();
            if (_animator == null)
                return false;
            var _controller = _animator.runtimeAnimatorController;
            if (_controller == null)
            {
                return false;
            }
            return _controller.animationClips.Length > 0;
        }

        private static bool HasMissingScripts(GameObject gameObject)
        {
            return GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0;
        }

        static List<string> prefabStageRootNames = new List<string>
        {
            "Prefab Mode in Context",
            "Canvas (Environment)"
        };

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null)
                return;

            if (UNIArtSettings.Editor.EnableHierarchyCheckbox.Value)
            {
                if (!prefabStageRootNames.Contains(obj.name) && Utils.IsPrefabStage())
                {
                    Rect toggleRect = new Rect(selectionRect.xMin, selectionRect.y, 16, 16); // Toggle 的位置

                    bool isVisible = obj.activeSelf;
                    bool newVisibility = EditorGUI.Toggle(toggleRect, isVisible);
                    if (newVisibility != isVisible)
                    {
                        Undo.RecordObject(obj, $"Toggle {obj.name} Visibility");
                        obj.SetActive(newVisibility);
                        EditorUtility.SetDirty(obj);
                    }
                }
            }

            if (!UNIArtSettings.Editor.EnableHierarchyIcon.Value)
                return;

            // 设置初始图标位置
            Rect iconRect = new Rect(selectionRect.xMax - 30, selectionRect.y, 16, 16);

            var _idx = 0;
            componentTypes.ForEach(_componentType =>
            {
                iconRect.x = selectionRect.xMax - 30 - _idx * 20;
                _idx++;

                Color originalColor = GUI.color;
                if (!obj.activeInHierarchy || prefabStageRootNames.Contains(obj.name))
                {
                    GUI.color = Color.gray;
                }

                var _icon = $"d_{_componentType.Name} Icon";
                if (_componentType.IsSubclassOf(typeof(Component)))
                {
                    var _iconTexture = (Texture)EditorGUIUtility.LoadRequired(_icon);
                    var _component = obj.GetComponent(_componentType);

                    if (_component != null)
                    {
                        GUI.DrawTexture(iconRect, _iconTexture);
                        AddTooltip(iconRect, $"{componentNames[_componentType]}");
                    }
                }
                else if (_componentType == typeof(AnimationClip))
                {
                    var _iconTexture = (Texture)EditorGUIUtility.LoadRequired(_icon);
                    if (!HasAnimation(obj))
                    {
                        GUI.color = originalColor;
                        return;
                    }

                    var _animator = obj.GetComponent<Animator>();
                    var _clipLength = _animator.runtimeAnimatorController.animationClips.Length;
                    if (_clipLength <= 0)
                    {
                        GUI.color = originalColor;
                        return;
                    }

                    // 绘制动画图标
                    GUI.DrawTexture(iconRect, _iconTexture);

                    Rect labelRect = new Rect(iconRect.x + 15, iconRect.y - 4, 16, 16);
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 10,
                        fontStyle = FontStyle.Normal,
                        normal = { textColor = Color.cyan },
                        hover = { textColor = Color.cyan },
                    };
                    GUI.Label(labelRect, $"x{_clipLength}", labelStyle);

                    AddTooltip(iconRect, $"该对象包含 {_clipLength} 个动画");
                }
                else if (_componentType == typeof(MonoScript))
                {
                    if (HasMissingScripts(obj))
                    {
                        // 绘制丢失脚本图标
                        GUI.DrawTexture(
                            iconRect,
                            EditorGUIUtility.LoadRequired("d_console.warnicon.sml") as Texture
                        );
                        AddTooltip(iconRect, "该对象包含丢失引用的脚本");
                    }
                }

                GUI.color = originalColor;
            });
        }

        private static void AddTooltip(Rect rect, string tooltip)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Arrow);
            GUIContent content = new GUIContent("", tooltip);
            GUI.Label(rect, content);
        }
    }
}
