using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UNIHper.Art.Editor
{
    public enum EditAction
    {
        Create,
        Rename,
        SetAsDefault,
        None
    }

    public class EditTarget
    {
        public AnimationClip clip = null;
        public EditAction action = EditAction.None;
        public string content = string.Empty;
    }

    [CustomEditor(typeof(Animator))]
    public class AnimatorEditor : UnityEditor.Editor
    {
        private EditTarget editTarget = new EditTarget();
        private static string defaultSaveDir = string.Empty;
        private static List<string> builtInAnimClips = new List<string>()
        {
            "UINone",
            "UIShow",
            "UIHide"
        };

        void OnEnable()
        {
            if (defaultSaveDir == string.Empty)
                defaultSaveDir = Application.dataPath;
            createController((Animator)target);
        }

        private void createController(Animator animator)
        {
            if (animator.runtimeAnimatorController == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/ArtAssets/Animations"))
                    AssetDatabase.CreateFolder("Assets/ArtAssets", "Animations");

                var _controller = AnimatorController.CreateAnimatorControllerAtPath(
                    AssetDatabase.GenerateUniqueAssetPath(
                        $"Assets/ArtAssets/Animations/{animator.gameObject.name}.controller"
                    )
                );
                animator.runtimeAnimatorController = _controller;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            // Get the target Animator component
            Animator animator = (Animator)target;
            // Display the list of animation clips in the current Animator controller
            AnimatorController controller =
                animator.runtimeAnimatorController as AnimatorController;
            if (controller != null)
            {
                var _animationClips = controller.animationClips
                    .Where(_ => !builtInAnimClips.Contains(_.name))
                    .Distinct();

                foreach (AnimationClip clip in _animationClips)
                {
                    EditorGUILayout.BeginHorizontal();

                    var _stateMachine = controller.layers[0].stateMachine;
                    var _placeHolder = _stateMachine.defaultState.motion == clip ? "*" : "";

                    GUI.contentColor = Color.green;
                    GUILayout.Label(_placeHolder, GUILayout.Width(10));
                    GUI.contentColor = Color.white;
                    if (editTarget.action == EditAction.Rename && editTarget.clip == clip)
                    {
                        EditorGUI.BeginChangeCheck();
                        editTarget.content = EditorGUILayout.TextField(
                            editTarget.content,
                            GUILayout.Width(120)
                        );

                        if (EditorGUI.EndChangeCheck())
                        {
                            editTarget.content = editTarget.content.Trim();
                        }

                        GUI.backgroundColor = Color.red;
                        if (GUILayout.Button("取消"))
                        {
                            editTarget.action = EditAction.None;
                        }

                        GUI.backgroundColor = Color.green;
                        if (GUILayout.Button("确认"))
                        {
                            if (!string.IsNullOrEmpty(editTarget.content))
                            {
                                string path = AssetDatabase.GetAssetPath(editTarget.clip);
                                if (AssetDatabase.IsSubAsset(clip))
                                {
                                    var _mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                                    clip.name = editTarget.content;
                                    AssetDatabase.SaveAssetIfDirty(_mainAsset);
                                }
                                else
                                {
                                    AssetDatabase.RenameAsset(path, editTarget.content);
                                }
                            }
                            editTarget.action = EditAction.None;
                            editTarget.content = string.Empty;
                        }
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(
                            clip,
                            typeof(AnimationClip),
                            true,
                            GUILayout.Width(120)
                        );
                        EditorGUI.EndDisabledGroup();
                    }

                    GUI.backgroundColor = Color.white;
                    GUILayout.Label("");
                    if (GUILayout.Button("编辑", GUILayout.Width(50)))
                    {
                        AssetDatabase.OpenAsset(clip);
                        AnimationWindow animWindow = EditorWindow.GetWindow<AnimationWindow>();
                        animWindow.animationClip = clip;
                    }

                    if (GUILayout.Button("删除", GUILayout.Width(50)))
                    {
                        if (
                            EditorUtility.DisplayDialog(
                                "确认删除",
                                $"确定要删除动画[{clip.name}]吗，该操作不可恢复",
                                "是",
                                "取消"
                            )
                        )
                        {
                            if (AssetDatabase.IsSubAsset(clip))
                            {
                                AssetDatabase.RemoveObjectFromAsset(clip);
                                Object.DestroyImmediate(clip);
                                AssetDatabase.SaveAssets();
                                AssetDatabase.Refresh();
                            }
                            else
                            {
                                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(clip));
                            }
                        }
                    }

                    if (GUILayout.Button("...", GUILayout.Width(25)))
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(
                            new GUIContent("重命名"),
                            false,
                            () =>
                            {
                                editTarget.clip = clip;
                                editTarget.content = clip.name;
                                editTarget.action = EditAction.Rename;
                            }
                        );
                        menu.AddItem(
                            new GUIContent("设为默认动画"),
                            false,
                            () =>
                            {
                                var _stateMachine = controller.layers[0].stateMachine;
                                var _animationClips = controller.animationClips;
                                _stateMachine.defaultState = _stateMachine.states
                                    .Where(_ => _.state.motion == clip)
                                    .First()
                                    .state;
                            }
                        );
                        menu.AddItem(
                            new GUIContent("创建反转动画"),
                            false,
                            () =>
                            {
                                var _reversedClip = ReverseAnimationContext.ReverseClip(clip);
                                onNewAnimationCreated(controller, _reversedClip);
                            }
                        );

                        menu.ShowAsContext();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // 创建新动画
                if (editTarget.action == EditAction.Create)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("", GUILayout.Width(10));
                    EditorGUI.BeginChangeCheck();
                    editTarget.content = EditorGUILayout.TextField(
                        editTarget.content,
                        GUILayout.Width(120)
                    );

                    if (EditorGUI.EndChangeCheck())
                    {
                        editTarget.content = editTarget.content.Trim();
                    }

                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("取消"))
                    {
                        editTarget.action = EditAction.None;
                    }

                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("确认"))
                    {
                        if (!string.IsNullOrEmpty(editTarget.content))
                        {
                            var _subAssetNames = AssetDatabase
                                .LoadAllAssetRepresentationsAtPath(
                                    AssetDatabase.GetAssetPath(controller)
                                )
                                .Where(_ => _.GetType() == typeof(AnimationClip))
                                .Select(_ => _.name)
                                .ToList();
                            // generate unique animation name
                            if (_subAssetNames.Contains(editTarget.content))
                            {
                                int i = 1;
                                while (_subAssetNames.Contains($"{editTarget.content} ({i})"))
                                {
                                    i++;
                                }
                                editTarget.content = $"{editTarget.content} ({i})";
                            }
                            AnimationClip clip = new AnimationClip();
                            clip.name = editTarget.content;
                            AssetDatabase.AddObjectToAsset(clip, controller);
                            AssetDatabase.SaveAssetIfDirty(controller);
                            onNewAnimationCreated(controller, clip);
                        }
                        editTarget.content = string.Empty;
                        editTarget.action = EditAction.None;
                    }
                    GUILayout.Label("", GUILayout.Width(50));
                    GUILayout.Label("", GUILayout.Width(50));
                    GUILayout.Label("", GUILayout.Width(25));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("新建动画", GUILayout.Height(25)))
            {
                editTarget.action = EditAction.Create;
                editTarget.content = "新动画";
                // AnimationClip clip = new AnimationClip();
                // clip.name = "New Animation";

                // string _animSavePath = EditorUtility.SaveFilePanel(
                //     "保存动画",
                //     defaultSaveDir,
                //     "新动画",
                //     "anim"
                // );

                // if (!string.IsNullOrEmpty(_animSavePath))
                // {
                //     var _projectDir =
                //         Path.GetDirectoryName(Application.dataPath).Replace("\\", "/") + "/";
                //     if (!_animSavePath.StartsWith(_projectDir))
                //     {
                //         EditorUtility.DisplayDialog("错误", "动画文件必须保存在工程目录下", "确定");
                //         return;
                //     }

                //     defaultSaveDir = Path.GetDirectoryName(_animSavePath).Replace("\\", "/");

                //     _animSavePath = _animSavePath.Replace(_projectDir, "");

                //     AssetDatabase.CreateAsset(clip, _animSavePath);

                //     if (controller is null)
                //     {
                //         var _controllerPath = Path.Combine(
                //             Path.GetDirectoryName(_animSavePath),
                //             animator.gameObject.name + ".controller"
                //         );
                //         controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(
                //             _controllerPath,
                //             clip
                //         );
                //         animator.runtimeAnimatorController = controller;
                //     }
                //     AssetDatabase.SaveAssets();
                //     AssetDatabase.Refresh();
                //     onNewAnimationCreated(controller, clip);
                // }
            }
        }

        private void onNewAnimationCreated(AnimatorController controller, AnimationClip clip)
        {
            var _stateMachine = controller.layers[0].stateMachine;
            var _animationClips = controller.animationClips;

            var _sameNameState = _stateMachine.states
                .Select(_ => _.state)
                .Where(_state => _state.name == clip.name)
                .FirstOrDefault();
            if (_sameNameState is null)
            {
                var _state = _stateMachine.AddState(clip.name);
                _state.motion = clip;
            }
            else if (_sameNameState.motion == null)
            {
                _sameNameState.motion = clip;
            }

            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.OpenAsset(clip);
            AnimationWindow animWindow = EditorWindow.GetWindow<AnimationWindow>();
            animWindow.animationClip = clip;
        }
    }
}
