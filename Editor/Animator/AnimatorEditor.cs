using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UNIArt.Editor
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
            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(target, true);
        }

        List<AnimClipView> clipViews = new List<AnimClipView>();

        private void listAnimationClips()
        {
            var _clipRoot = root.Q<VisualElement>("clips-root");
            var _animator = target as Animator;
            if (_animator == null)
            {
                _clipRoot.style.display = DisplayStyle.None;
                return;
            }

            var controller = _animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                _clipRoot.style.display = DisplayStyle.None;
                return;
            }

            var _animationClips = controller.animationClips
                .Where(_ => !builtInAnimClips.Contains(_.name))
                .Distinct();

            _clipRoot.Clear();
            _clipRoot.style.display =
                _animationClips.Count() > 0 ? DisplayStyle.Flex : DisplayStyle.None;

            clipViews.Clear();
            var _stateMachine = controller.layers[0].stateMachine;
            foreach (AnimationClip clip in _animationClips)
            {
                var _clipView = new AnimClipView() { Clip = clip };
                _clipRoot.Add(_clipView);
                clipViews.Add(_clipView);

                _clipView.SetDefaultAnim(_stateMachine.defaultState.motion == clip);

                _clipView.BtnName.RegisterCallback<ClickEvent>(e =>
                {
                    clipViews.ForEach(_ => _.Deselect());
                    _clipView.Select();
                    AnimCtrlTarget.Value = _clipView;
                    DoAnimPreview();
                });

                _clipView.OnOpenAnimationEditor.AddListener(clipView =>
                {
                    // clipViews.ForEach(_ => _.Deselect());
                    StopAnimPreview();
                });

                _clipView.OnDeleted.AddListener(clipView =>
                {
                    _clipRoot.Remove(_clipView);
                    clipViews.First().Select();
                    AnimCtrlTarget.Value = clipViews.First();
                });

                _clipView.OnRenamed.AddListener(
                    (oldName, newName) =>
                    {
                        // controller.
                        var _stateMachine = controller.layers[0].stateMachine;

                        var _state = _stateMachine.states
                            .Where(_ => _.state.motion == clip)
                            .First();
                        _state.state.name = newName;
                    }
                );

                _clipView
                    .Q<Button>("btn_option")
                    .RegisterCallback<ClickEvent>(_ =>
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(
                            new GUIContent("重命名"),
                            false,
                            () =>
                            {
                                _clipView.DoNameEdit();
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
                                clipViews.ForEach(_clip => _clip.SetDefaultAnim(false));
                                _clipView.SetDefaultAnim(true);
                            }
                        );
                        menu.AddItem(
                            new GUIContent("创建反转动画"),
                            false,
                            () =>
                            {
                                var _reversedClip = ReverseAnimationContext.ReverseClip(clip);
                                AnimatorExt.AddClipToController(controller, _reversedClip);
                                listAnimationClips();
                            }
                        );

                        menu.ShowAsContext();
                    });
            }

            AnimCtrlTarget.Value = clipViews.FirstOrDefault();
        }

        VisualElement root;
        SliderInt _sliderFrame;

        ReactiveProperty<AnimClipView> AnimCtrlTarget = new ReactiveProperty<AnimClipView>();

        public override VisualElement CreateInspectorGUI()
        {
            var _animator = (Animator)target;

            root = new VisualElement();

            var defaultInspector = new IMGUIContainer(OnInspectorGUI);
            root.Add(defaultInspector);

            var _animatorExtView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.parful.uniart/Editor/Animator/AnimClipControl.uxml"
            );

            _animatorExtView.CloneTree(root);
            _sliderFrame = root.Q<SliderInt>("slider_frame");

            listAnimationClips();

            _sliderFrame.RegisterCallback<ChangeEvent<int>>(e =>
            {
                if (playDisposable != null)
                {
                    playDisposable.Cancel();
                    playDisposable = null;
                }
                var _clipView = AnimCtrlTarget.Value;
                var _duration = e.newValue * _clipView.Clip.length / _sliderFrame.highValue;
                _clipView.Clip.SampleAnimation(_animator.gameObject, _duration);
                updateSlider(_duration);
            });

            root.Q<Button>("btn_pause")
                .RegisterCallback<ClickEvent>(_ =>
                {
                    PauseAnimPreview();
                });

            root.Q<Button>("btn_play")
                .RegisterCallback<ClickEvent>(_ =>
                {
                    DoAnimPreview();
                });
            root.Q<Button>("btn_stop")
                .RegisterCallback<ClickEvent>(_ =>
                {
                    StopAnimPreview();
                });

            var _btnAdd = root.Q<Button>("btn_addAnim");
            var _inputAddAnim = root.Q<TextField>("input_addAnim");
            _btnAdd.RegisterCallback<ClickEvent>(e =>
            {
                _btnAdd.style.display = DisplayStyle.None;
                _inputAddAnim.style.display = DisplayStyle.Flex;
                _inputAddAnim.value = "新动画";
                _inputAddAnim.Focus();
            });

            string _inputFlag = "";
            _inputAddAnim.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    _inputFlag = "confirm";
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    _inputFlag = "cancel";
                }
            });

            _inputAddAnim.RegisterCallback<BlurEvent>(e =>
            {
                _btnAdd.style.display = DisplayStyle.Flex;
                _inputAddAnim.style.display = DisplayStyle.None;
                if (_inputFlag == "cancel" || string.IsNullOrEmpty(_inputAddAnim.value))
                    return;

                var controller = AnimatorExt.CreateController((Animator)target);

                var _newAnimName = _inputAddAnim.value;
                _inputAddAnim.value = "";

                _newAnimName = Utils.GenerateUniqueSubAssetName<AnimationClip>(
                    AssetDatabase.GetAssetPath(controller),
                    _newAnimName
                );

                AnimationClip clip = new AnimationClip();
                clip.name = _newAnimName;
                AssetDatabase.AddObjectToAsset(clip, controller);
                AssetDatabase.SaveAssetIfDirty(controller);
                AnimatorExt.AddClipToController(controller, clip);
                listAnimationClips();
            });

            AnimCtrlTarget.OnValueChanged.AddListener(_clipView =>
            {
                _clipView?.Select();
                root.Q<VisualElement>("ctrl_root").style.display = _clipView is null
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;

                updateSlider(0);
            });

            AnimCtrlTarget.SetValueAndForceNotify(clipViews.FirstOrDefault());
            return root;
        }

        CancellationTokenSource playDisposable = null;

        private void updateSlider(float _duration)
        {
            var _clipView = AnimCtrlTarget.Value;
            var _curFrame = 0;

            var _frameCount = 0;
            if (_clipView != null)
            {
                _curFrame = Mathf.FloorToInt(_duration * _clipView.Clip.frameRate);
                _frameCount = Mathf.FloorToInt(_clipView.Clip.length * _clipView.Clip.frameRate);
            }
            _curFrame = Mathf.Clamp(_curFrame, 0, _frameCount);

            _sliderFrame.lowValue = 0;
            _sliderFrame.highValue = _frameCount;

            _sliderFrame.label = $"{_curFrame:000}/{_sliderFrame.highValue:000}";
            _sliderFrame.SetValueWithoutNotify(_curFrame);
        }

        private void DoAnimPreview()
        {
            playDisposable?.Cancel();
            var _animator = (Animator)target;
            if (_animator.runtimeAnimatorController == null)
                return;

            var _clipView = AnimCtrlTarget.Value;

            _animator.Play(_clipView.Clip.name, 0, 0f);
            _clipView.Clip.SampleAnimation(_animator.gameObject, 0);
            _animator.Update(0);

            Utils.HookUpdateOnce(() =>
            {
                var _duration = 0f;
                var _playButton = root.Q<Button>("btn_play");
                _playButton.AddToClassList("selected");
                Action _finished = () =>
                {
                    _playButton.RemoveFromClassList("selected");
                };
                var _clipLoop = _clipView.Loop;
                playDisposable = Utils.UpdateWhile(
                    () =>
                    {
                        _clipView.Clip.SampleAnimation(_animator.gameObject, Time.deltaTime);
                        _duration += Time.deltaTime;
                        if (_clipLoop)
                            _duration %= _clipView.Clip.length;
                        updateSlider(_duration);
                        _animator.Update(Time.deltaTime);
                    },
                    () => _clipLoop ? true : _duration <= _clipView.Clip.length,
                    _finished,
                    _finished
                );
            });
        }

        public void StopAnimPreview()
        {
            var _clipView = AnimCtrlTarget.Value;
            var _animator = (Animator)target;

            playDisposable?.Cancel();
            playDisposable = null;

            if (_clipView == null)
                return;
            _animator.Play(_clipView.Clip.name, 0, 0f);
            _animator.Update(0);
            updateSlider(0);
        }

        public void PauseAnimPreview()
        {
            if (playDisposable != null)
            {
                playDisposable.Cancel();
                playDisposable = null;
            }
        }
    }
}
