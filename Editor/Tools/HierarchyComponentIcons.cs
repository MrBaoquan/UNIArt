using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using PluginMaster;
using UNIArt.Runtime;
using System.Linq;

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
            { typeof(Toggle), "开关" },
            { typeof(ToggleGroup), "开关组" }
        };

        static HierarchyComponentIcons()
        {
            componentTypes = new List<Type>
            {
                typeof(AnimationClip),
                typeof(Animator),
                typeof(Button),
                typeof(Toggle),
                typeof(ToggleGroup),
                typeof(GameObject),
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

        private static void drawOptionGUI(int instanceID, Rect selectionRect)
        {
            if (selectionRect.Contains(Event.current.mousePosition))
            {
                GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                if (prefabStageRootNames.Contains(obj.name))
                    return;

                Rect _optionRect = new Rect(selectionRect.xMax - 16, selectionRect.y, 16, 16);
                if (_optionRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(_optionRect, new Color(0.3f, 0.5f, 1.0f, 0.3f)); // 设置高亮颜色
                }

                var _optionIcon = EditorGUIUtility.IconContent("d__Menu").image as Texture2D;
                GUI.DrawTexture(_optionRect, _optionIcon);

                if (
                    Event.current.type == EventType.MouseDown
                    && _optionRect.Contains(Event.current.mousePosition)
                )
                {
                    addGenericMenu(obj);
                }
            }
        }

        private static void addGenericMenu(GameObject obj)
        {
            GenericMenu menu = new GenericMenu();

            // 添加空物体菜单
            var _addEmptyMenu = new GUIContent(
                "创建 UI 空物体",
                EditorGUIUtility.IconContent("d__Menu").image
            );

            menu.AddItem(
                _addEmptyMenu,
                false,
                () =>
                {
                    GameObject _newObj = new GameObject();
                    _newObj.transform.SetParent(obj.transform, false);
                    _newObj.name = "UI 对象";
                    if (_newObj.GetComponentInParent<RectTransform>() != null)
                    {
                        var _rectTrans = _newObj.AddComponent<RectTransform>();
                        _rectTrans.sizeDelta = Vector2.zero;
                    }
                    Selection.activeGameObject = _newObj;
                }
            );
            menu.AddSeparator("");
            drawAddComponentsMenus(obj, menu);
            drawComponentConvertMenus(obj, menu);
            menu.AddSeparator("");
            if (obj.GetComponentInChildren<PsGroup>(true) != null)
            {
                menu.AddItem(
                    new GUIContent("移除PS组件 (含子节点)"),
                    false,
                    () =>
                    {
                        PSUtils.RemovePSLayer(obj);
                        EditorUtility.SetDirty(obj);
                    }
                );
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("移除PS组件 (含子节点)"));
            }

            // 移除丢失引用脚本
            var _removeMissingScriptMenu = new GUIContent(
                "移除丢失引用脚本",
                EditorGUIUtility.IconContent("d__Menu").image
            );

            if (HasMissingScripts(obj))
            {
                menu.AddItem(
                    _removeMissingScriptMenu,
                    false,
                    () =>
                    {
                        if (Selection.objects.Contains(obj))
                        {
                            foreach (var _obj in Selection.objects.OfType<GameObject>())
                            {
                                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(_obj);
                            }
                        }
                        else
                        {
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                        }
                    }
                );
            }
            else
            {
                menu.AddDisabledItem(_removeMissingScriptMenu);
            }

            menu.AddSeparator("");
            if (
                !PrefabUtility.IsPartOfAnyPrefab(obj)
                && Selection.objects.Contains(obj)
                && Selection.objects.Length == 1
            )
            {
                menu.AddItem(
                    new GUIContent("保存为预制体"),
                    false,
                    () =>
                    {
                        var _path = Utils.GetPrefabAssetPathByAnyGameObject(obj);
                        var _saveRoot = UNIArtSettings.Project.ArtFolder;
                        var _folder = "UI Prefabs/Widgets";
                        if (UNIArtSettings.IsTemplateAsset(_path))
                        {
                            _saveRoot = UNIArtSettings.GetExternalTemplateRootBySubAsset(_path);
                            _folder = "Prefabs/自定义组件";
                        }
                        var _savePath = Path.Combine(_saveRoot, _folder, obj.name + ".prefab")
                            .ToForwardSlash();
                        Utils.CreateFolderIfNotExist(Path.GetDirectoryName(_savePath));
                        // _savePath = AssetDatabase.GenerateUniqueAssetPath(_savePath);

                        var _newPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                            obj,
                            _savePath,
                            InteractionMode.AutomatedAction
                        );
                        ProjectWindowUtil.ShowCreatedAsset(_newPrefab);
                    }
                );
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("保存为预制体"));
            }

            menu.ShowAsContext();
            Event.current.Use();
        }

        // 添加组件
        private static void drawAddComponentsMenus(GameObject obj, GenericMenu menu)
        {
            var _addImageMenu = new GUIContent(
                "添加组件/图片",
                EditorGUIUtility.IconContent("d__Menu").image
            );

            appendMenu(
                obj,
                menu,
                _addImageMenu,
                () => obj.GetComponent<Image>() == null,
                _obj => _obj.AddComponent<Image>()
            );

            var _addButtonMenu = new GUIContent(
                "添加组件/按钮",
                EditorGUIUtility.IconContent("d__Menu").image
            );

            appendMenu(
                obj,
                menu,
                _addButtonMenu,
                () => obj.GetComponent<Selectable>() == null,
                _obj =>
                {
                    _obj.AddOrGetComponent<Image>();
                    _obj.AddComponent<Button>();
                }
            );

            var _addToggleMenu = new GUIContent(
                "添加组件/开关",
                EditorGUIUtility.IconContent("d__Menu").image
            );

            appendMenu(
                obj,
                menu,
                _addToggleMenu,
                () => obj.GetComponent<Selectable>() == null,
                _obj =>
                {
                    _obj.AddOrGetComponent<Image>();
                    _obj.AddComponent<Toggle>();
                }
            );

            var _addAnimMenu = new GUIContent(
                "添加组件/动画",
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/com.parful.uniart/Assets/Icon/筛选.png"
                )
            );

            appendMenu(
                obj,
                menu,
                _addAnimMenu,
                () => obj.GetComponent<Animator>() == null,
                _obj =>
                {
                    var _animator = _obj.AddComponent<Animator>();
                    var _controller = AnimatorExt.CreateController(_animator);
                    AnimatorExt.AddClipToController(_controller, "出现");
                },
                () =>
                {
                    Utils.HookUpdateOnce(() =>
                    {
                        AnimatorEditor.RefreshAnimationList();
                    });
                }
            );
        }

        private static void drawComponentConvertMenus(GameObject obj, GenericMenu menu)
        {
            var _convertToToggleMenu = new GUIContent(
                "组件转换/转换为开关组件",
                EditorGUIUtility.IconContent("d__Menu").image
            );

            appendMenu(
                obj,
                menu,
                _convertToToggleMenu,
                () => obj.GetComponent<Button>() != null,
                convertButtonToToggle
            );

            var _convertToButtonMenu = new GUIContent(
                "组件转换/转换为按钮组件",
                EditorGUIUtility.IconContent("d__Menu").image
            );

            appendMenu(
                obj,
                menu,
                _convertToButtonMenu,
                () => obj.GetComponent<Toggle>() != null,
                convertToggleToButton
            );
        }

        private static void convertButtonToToggle(GameObject obj)
        {
            var _button = obj.GetComponent<Button>();
            if (_button == null)
                return;

            if (_button.transition == Selectable.Transition.SpriteSwap)
            {
                var _normalSP =
                    _button.spriteState.highlightedSprite == null
                        ? obj.GetComponent<Image>().sprite
                        : _button.spriteState.highlightedSprite;

                var _pressedSP = _button.spriteState.pressedSprite;
                GameObject.DestroyImmediate(_button);

                var _toggleImage = obj.AddOrGetComponent<ToggleImage>();
                var _toggle = obj.GetComponent<Toggle>();
                _toggle.transition = Selectable.Transition.SpriteSwap;
                SpriteState _spriteState = new SpriteState
                {
                    highlightedSprite = _normalSP,
                    pressedSprite = _pressedSP
                };
                _toggle.spriteState = _spriteState;
            }
            else
            {
                GameObject.DestroyImmediate(_button);
                obj.AddOrGetComponent<Toggle>();
            }
        }

        private static void convertToggleToButton(GameObject obj)
        {
            var _toggle = obj.GetComponent<Toggle>();
            if (_toggle == null)
                return;

            if (_toggle.transition == Selectable.Transition.SpriteSwap)
            {
                var _normalSP = _toggle.spriteState.highlightedSprite;
                var _pressedSP = _toggle.spriteState.pressedSprite;

                var _toggleImage = obj.GetComponent<ToggleImage>();
                if (_toggleImage != null)
                    GameObject.DestroyImmediate(_toggleImage);
                GameObject.DestroyImmediate(_toggle);

                var _button = obj.AddOrGetComponent<Button>();
                _button.transition = Selectable.Transition.SpriteSwap;
                SpriteState _spriteState = new SpriteState
                {
                    highlightedSprite = _normalSP,
                    pressedSprite = _pressedSP
                };
                _button.spriteState = _spriteState;
            }
            else
            {
                var _toggleImage = obj.GetComponent<ToggleImage>();
                if (_toggleImage != null)
                    GameObject.DestroyImmediate(_toggleImage);
                GameObject.DestroyImmediate(_toggle);
                obj.AddOrGetComponent<Button>();
            }
        }

        private static void appendMenu(
            GameObject obj,
            GenericMenu menu,
            GUIContent content,
            Func<bool> condition,
            Action<GameObject> action,
            Action complete = null
        )
        {
            if (condition())
            {
                menu.AddItem(
                    content,
                    false,
                    () =>
                    {
                        if (Selection.objects.Contains(obj))
                        {
                            foreach (var _obj in Selection.objects.OfType<GameObject>())
                            {
                                action?.Invoke(_obj);
                            }
                        }
                        else
                        {
                            action?.Invoke(obj);
                        }
                        complete?.Invoke();
                    }
                );
            }
            else
            {
                menu.AddDisabledItem(content);
            }
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null)
                return;

            drawOptionGUI(instanceID, selectionRect);

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
            Rect iconRect = new Rect(selectionRect.xMax - 46, selectionRect.y, 16, 16);

            var _safeX = selectionRect.xMin + selectionRect.width / 2;

            var _idx = 0;
            componentTypes.ForEach(_componentType =>
            {
                iconRect.x = selectionRect.xMax - 46 - _idx * 20;
                _idx++;

                if (iconRect.x < _safeX)
                    return;

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
                else if (_componentType == typeof(GameObject))
                {
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(obj))
                    {
                        GUI.DrawTexture(
                            iconRect,
                            EditorGUIUtility.LoadRequired("d_Prefab Icon") as Texture
                        );
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
