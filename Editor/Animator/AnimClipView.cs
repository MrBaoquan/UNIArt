using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public class AnimClipView : SelectableView
    {
        AnimationClip _clip;
        public AnimationClip Clip
        {
            get => _clip;
            set
            {
                _clip = value;
                BtnName.text = _clip.name;

                var _clipSettings = AnimationUtility.GetAnimationClipSettings(_clip);
                bool _loop = _clipSettings.loopTime;
                this.Q<Toggle>("toggle_loop").SetValueWithoutNotify(_loop);
            }
        }

        public bool Loop
        {
            get
            {
                var _clipSettings = AnimationUtility.GetAnimationClipSettings(Clip);
                return _clipSettings.loopTime;
            }
        }

        public Button BtnName;

        public void DoNameEdit()
        {
            this.Q<Button>("btn_clipName").style.display = DisplayStyle.None;
            this.Q<TextField>("input_clipName").style.display = DisplayStyle.Flex;
            this.Q<TextField>("input_clipName").value = _clip.name;
            this.Q<TextField>("input_clipName").Focus();
        }

        public void SetDefaultAnim(bool bDefault)
        {
            this.Q<Label>("indicator").style.visibility = bDefault
                ? Visibility.Visible
                : Visibility.Hidden;
        }

        public void DoNameDisplay()
        {
            this.Q<Button>("btn_clipName").style.display = DisplayStyle.Flex;
            this.Q<TextField>("input_clipName").style.display = DisplayStyle.None;
        }

        public UnityEvent<string, string> OnRenamed = new UnityEvent<string, string>();
        public UnityEvent<AnimClipView> OnOpenAnimationEditor = new UnityEvent<AnimClipView>();
        public UnityEvent<AnimClipView> OnDeleted = new UnityEvent<AnimClipView>();

        public AnimClipView()
        {
            BindView(Utils.PackageAssetPath("Editor/Animator/AnimClipView.uxml"));
            BtnName = this.Q<Button>("btn_clipName");
            SetSelectTarget(BtnName);

            this.Q<Button>("btn_locate")
                .RegisterCallback<ClickEvent>(_ =>
                {
                    EditorGUIUtility.PingObject(Clip);
                });

            this.Q<Button>("btn_edit")
                .RegisterCallback<ClickEvent>(_ =>
                {
                    AssetDatabase.OpenAsset(Clip);
                    AnimationWindow animWindow = EditorWindow.GetWindow<AnimationWindow>();

                    animWindow.animationClip = Clip;
                    OnOpenAnimationEditor.Invoke(this);
                });

            this.Q<Toggle>("toggle_loop")
                .RegisterCallback<ChangeEvent<bool>>(_ =>
                {
                    var _clipSettings = AnimationUtility.GetAnimationClipSettings(Clip);
                    _clipSettings.loopTime = _.newValue;
                    AnimationUtility.SetAnimationClipSettings(Clip, _clipSettings);
                });

            this.Q<Button>("btn_delete")
                .RegisterCallback<ClickEvent>(_ =>
                {
                    if (
                        EditorUtility.DisplayDialog(
                            "确认删除",
                            $"确定要删除动画[{Clip.name}]吗，该操作不可恢复",
                            "是",
                            "取消"
                        )
                    )
                    {
                        if (AssetDatabase.IsSubAsset(Clip))
                        {
                            AssetDatabase.RemoveObjectFromAsset(Clip);
                            UnityEngine.Object.DestroyImmediate(Clip);
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            OnDeleted.Invoke(this);
                        }
                        else
                        {
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(Clip));
                            OnDeleted.Invoke(this);
                        }
                    }
                });

            var _blurType = "";
            var inputField = this.Q<TextField>("input_clipName");
            inputField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Return)
                {
                    if (string.IsNullOrEmpty(inputField.value))
                        return;
                    _blurType = "confirm";
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    _blurType = "cancel";
                }
            });

            inputField.RegisterCallback<BlurEvent>(_ =>
            {
                DoNameDisplay();
                if (_blurType == "cancel" || string.IsNullOrEmpty(inputField.value))
                    return;

                var _newName = inputField.value;
                inputField.value = "";
                var _oldName = Clip.name;
                string path = AssetDatabase.GetAssetPath(Clip);
                if (AssetDatabase.IsSubAsset(Clip))
                {
                    var _mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
                    _newName = Utils.GenerateUniqueSubAssetName<AnimationClip>(path, _newName);
                    Clip.name = _newName;
                    AssetDatabase.SaveAssetIfDirty(_mainAsset);
                    BtnName.text = Clip.name;
                    OnRenamed.Invoke(_oldName, Clip.name);
                }
                else
                {
                    _newName = AssetDatabase.GenerateUniqueAssetPath(
                        path.Replace(_oldName, _newName)
                    );
                    AssetDatabase.RenameAsset(path, _newName);
                    BtnName.text = Clip.name;
                    OnRenamed.Invoke(_oldName, Clip.name);
                }
            });
        }
    }
}
