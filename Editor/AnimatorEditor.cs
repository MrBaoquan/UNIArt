using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UNIHper.Art.Editor
{
    public enum EditAction
    {
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
        private string defaultSaveDir = Application.dataPath;

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
                foreach (AnimationClip clip in controller.animationClips)
                {
                    EditorGUILayout.BeginHorizontal();


                    var _stateMachine = controller.layers[0].stateMachine;
                    var _placeHolder = _stateMachine.defaultState.motion == clip ? "*" : "";

                    GUI.contentColor = Color.green;
                    GUILayout.Label(_placeHolder, GUILayout.Width(10));
                    GUI.contentColor = Color.white;

                    string clipName = clip.name;
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
                                AssetDatabase.RenameAsset(path, editTarget.content);
                            }
                            clipName = editTarget.content;
                            editTarget.action = EditAction.None;
                        }
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.ObjectField(clip, typeof(AnimationClip), true, GUILayout.Width(120));
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
                            EditorUtility.DisplayDialog("确认删除", $"确定要删除动画[{clip.name}]吗，该操作不可恢复", "是", "取消")
                        )
                        {
                            Undo.RecordObject(clip, "删除动画 " + clip.name);
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(clip));
                        }
                    }

                    if (GUILayout.Button("...", GUILayout.Width(25)))
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("重命名"), false, () =>
                        {
                            editTarget.clip = clip;
                            editTarget.action = EditAction.Rename;
                            editTarget.content = clipName;
                        });
                        menu.AddItem(new GUIContent("设为默认动画"), false, () =>
                            {
                                var _stateMachine = controller.layers[0].stateMachine;
                                var _animationClips = controller.animationClips;
                                _stateMachine.defaultState = _stateMachine.states
                                    .Where(_ => _.state.motion == clip)
                                    .First()
                                    .state;
                            });
                        menu.AddItem(new GUIContent("创建反转动画"), false, () =>
                        {
                            var _reversedClip = ReverseAnimationContext.ReverseClip(clip);
                            onNewAnimationCreated(controller, _reversedClip);
                        });

                        menu.ShowAsContext();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();
            // Add a button to create a new animation clip
            if (GUILayout.Button("新建动画"))
            {
                AnimationClip clip = new AnimationClip();
                clip.name = "New Animation";


                string _animSavePath = EditorUtility.SaveFilePanel("保存动画", defaultSaveDir, "新动画", "anim");

                if (!string.IsNullOrEmpty(_animSavePath))
                {
                    var _projectDir = Path.GetDirectoryName(Application.dataPath).Replace("\\", "/") + "/";
                    if (!_animSavePath.StartsWith(_projectDir))
                    {
                        EditorUtility.DisplayDialog("错误", "动画文件必须保存在项目目录下", "确定");
                        return;
                    }

                    defaultSaveDir = Path.GetDirectoryName(_animSavePath).Replace("\\", "/");

                    _animSavePath = _animSavePath.Replace(_projectDir, "");

                    AssetDatabase.CreateAsset(clip, _animSavePath);

                    if (controller is null)
                    {
                        var _controllerPath = Path.Combine(Path.GetDirectoryName(_animSavePath), animator.gameObject.name + ".controller");
                        controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(_controllerPath, clip);
                        animator.runtimeAnimatorController = controller;
                    }
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    onNewAnimationCreated(controller, clip);
                }
            }
        }

        private void onNewAnimationCreated(AnimatorController controller, AnimationClip clip)
        {
            var _stateMachine = controller.layers[0].stateMachine;

            var _animationClips = controller.animationClips;
            if (_stateMachine.states.Where(_ => _.state.motion == clip).Count() == 0)
            {
                var _state = _stateMachine.AddState(clip.name);
                _state.motion = clip;
            }
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.OpenAsset(clip);
            AnimationWindow animWindow = EditorWindow.GetWindow<AnimationWindow>();
            animWindow.animationClip = clip;
        }
    }
}