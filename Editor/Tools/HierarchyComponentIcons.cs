using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UNIHper.Art.Editor
{
    [InitializeOnLoad]
    public class HierarchyComponentIcons
    {
        // 保存组件类型和对应图标的列表
        private static List<Type> componentTypes;

        // 组件中文名
        static Dictionary<Type, string> componentNames = new Dictionary<Type, string>
        {
            { typeof(Animator), "动画控制器" },
            { typeof(Button), "按钮" },
            { typeof(Canvas), "画布" },
            { typeof(Camera), "相机" },
        };

        static HierarchyComponentIcons()
        {
            // 初始化组件图标列表
            componentTypes = new List<Type>
            {
                typeof(AnimationClip),
                typeof(Animator),
                typeof(Button),
                typeof(Canvas),
                typeof(Camera),
                typeof(MonoScript)
            };

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

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            // 根据 instanceID 获取 GameObject
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null)
                return;

            // 设置初始图标位置
            Rect iconRect = new Rect(selectionRect.xMax - 30, selectionRect.y, 16, 16);

            var _idx = 0;
            componentTypes.ForEach(_componentType =>
            {
                // 计算图标的位置
                iconRect.x = selectionRect.xMax - 30 - _idx * 20;
                _idx++;

                // 检查 GameObject 是否激活
                Color originalColor = GUI.color;
                if (!obj.activeInHierarchy)
                {
                    // 将图标颜色设置为灰色
                    GUI.color = Color.gray;
                }

                var _icon = $"d_{_componentType.Name} Icon";
                if (_componentType.IsSubclassOf(typeof(Component)))
                {
                    var _iconTexture = (Texture)EditorGUIUtility.LoadRequired(_icon);
                    var _component = obj.GetComponent(_componentType);

                    // 如果对象拥有该组件，则绘制图标
                    if (_component != null)
                    {
                        GUI.DrawTexture(iconRect, _iconTexture);

                        // 添加鼠标悬停提示
                        AddTooltip(iconRect, $"{componentNames[_componentType]}");
                    }
                }
                else if (_componentType == typeof(AnimationClip))
                {
                    var _iconTexture = (Texture)EditorGUIUtility.LoadRequired(_icon);
                    if (!HasAnimation(obj))
                    {
                        // 恢复原始颜色
                        GUI.color = originalColor;
                        return;
                    }

                    var _animator = obj.GetComponent<Animator>();
                    var _clipLength = _animator.runtimeAnimatorController.animationClips.Length;
                    if (_clipLength <= 0)
                    {
                        // 恢复原始颜色
                        GUI.color = originalColor;
                        return;
                    }

                    // 绘制动画图标
                    // iconRect.y += 1;
                    GUI.DrawTexture(iconRect, _iconTexture);
                    // iconRect.y -= 1;

                    // 绘制动画片段数量标签
                    Rect labelRect = new Rect(iconRect.x + 15, iconRect.y - 4, 16, 16);
                    GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = 10,
                        fontStyle = FontStyle.Normal,
                        normal = { textColor = Color.cyan },
                        hover = { textColor = Color.cyan },
                    };
                    GUI.Label(labelRect, $"x{_clipLength}", labelStyle);

                    // 添加鼠标悬停提示
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
                // 恢复原始颜色
                GUI.color = originalColor;
            });
        }

        private static void AddTooltip(Rect rect, string tooltip)
        {
            // 显示工具提示信息
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Arrow);
            GUIContent content = new GUIContent("", tooltip);
            GUI.Label(rect, content);
        }
    }
}
