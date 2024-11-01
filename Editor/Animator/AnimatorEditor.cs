using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    [CustomEditor(typeof(Animator))]
    public class AnimatorEditor : UnityEditor.Editor
    {
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

        private void OnDisable()
        {
            PauseAnimPreview();
        }

        List<AnimClipView> clipViews = new List<AnimClipView>();

        private void listAnimationClips(bool autoSelectIfNone = true)
        {
            var _clipRoot = root.Q<VisualElement>("clips-root");

            if (animator == null)
            {
                _clipRoot.style.display = DisplayStyle.None;
                return;
            }

            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                _clipRoot.style.display = DisplayStyle.None;
                return;
            }

            var _animationClips = controller.animationClips
                .Where(_ => !builtInAnimClips.Contains(_.name))
                .OrderBy(_ => _.name)
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
                    StopAnimPreview();
                });

                _clipView.OnDeleted.AddListener(clipView =>
                {
                    listAnimationClips();
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

            if (autoSelectIfNone)
            {
                var _animTarget = clipViews.FirstOrDefault();
                if (AnimCtrlTarget.Value != null)
                {
                    _animTarget = clipViews
                        .Where(_ => _.Clip.name == AnimCtrlTarget.Value.ClipName)
                        .FirstOrDefault();
                }
                AnimCtrlTarget.Value = _animTarget;
            }
        }

        VisualElement root;
        SliderInt _sliderFrame;
        VisualElement ctrlRoot;

        ReactiveProperty<AnimClipView> AnimCtrlTarget = new ReactiveProperty<AnimClipView>();

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            var defaultInspector = new IMGUIContainer(OnInspectorGUI);
            root.Add(defaultInspector);

            var _animatorExtView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.parful.uniart/Editor/Animator/AnimClipControl.uxml"
            );

            _animatorExtView.CloneTree(root);
            _sliderFrame = root.Q<SliderInt>("slider_frame");
            ctrlRoot = root.Q<VisualElement>("ctrl_root");

            listAnimationClips();

            _sliderFrame.RegisterCallback<ChangeEvent<int>>(e =>
            {
                if (playDisposable != null)
                {
                    playDisposable.Cancel();
                    playDisposable = null;
                }
                if (!CanControl())
                    return;

                var _duration = e.newValue * curClipView.Clip.length / _sliderFrame.highValue;
                curClipView.Clip.SampleAnimation(animator.gameObject, _duration);
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
                updateCtrlStyle();
                StopAnimPreview();
            });

            var _lastAnimClip = clipViews.FirstOrDefault();
            if (lastAnimationTimes.TryGetValue(target.GetInstanceID(), out AnimData animData))
            {
                _lastAnimClip = clipViews
                    .Where(_clipView => _clipView.ClipName == animData.name)
                    .FirstOrDefault();
            }

            _lastAnimClip?.Select();
            AnimCtrlTarget.SetValueWithoutNotify(_lastAnimClip);
            updateCtrlStyle();
            updateSlider();
            return root;
        }

        CancellationTokenSource playDisposable = null;

        private void updateCtrlStyle()
        {
            ctrlRoot.style.display = CanControl() ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void updateSlider()
        {
            if (lastAnimationTimes.ContainsKey(target.GetInstanceID()))
            {
                updateSlider(lastAnimationTimes[target.GetInstanceID()].time);
            }
            else
            {
                updateSlider(0);
            }
        }

        class AnimData
        {
            public string name;
            public float time;
        }

        static Dictionary<int, AnimData> lastAnimationTimes = new Dictionary<int, AnimData>();

        private void updateSlider(float _duration)
        {
            var _curFrame = 0;

            if (!lastAnimationTimes.ContainsKey(target.GetInstanceID()))
            {
                lastAnimationTimes.Add(target.GetInstanceID(), new AnimData());
            }
            var _animData = lastAnimationTimes[target.GetInstanceID()];
            _animData.name = curClipView?.ClipName;
            _animData.time = _duration;

            var _frameCount = 0;
            if (curClipView != null)
            {
                _curFrame = Mathf.FloorToInt(_duration * curClipView.Clip.frameRate);
                _frameCount = Mathf.FloorToInt(
                    curClipView.Clip.length * curClipView.Clip.frameRate
                );
            }
            // _curFrame = Mathf.Clamp(_curFrame, 0, _frameCount);

            _sliderFrame.lowValue = 0;
            _sliderFrame.highValue = _frameCount;

            _sliderFrame.label = $"{_curFrame:000}/{_sliderFrame.highValue:000}";
            _sliderFrame.SetValueWithoutNotify(_curFrame);
        }

        Animator animator => target as Animator;
        AnimClipView curClipView => AnimCtrlTarget.Value;

        private void DoAnimPreview()
        {
            playDisposable?.Cancel();

            if (!CanControl())
                return;

            animator.Play(curClipView.Clip.name, -1, 0f);
            curClipView.Clip.SampleAnimation(animator.gameObject, 0);
            animator.Update(0);

            var _duration = 0f;
            var _playButton = root.Q<Button>("btn_play");
            _playButton.AddToClassList("selected");
            Action _finishPreview = () =>
            {
                _playButton.RemoveFromClassList("selected");
                curClipView.Clip.SampleAnimation(animator.gameObject, curClipView.Clip.length);
            };

            playDisposable = Utils.UpdateWhile(
                () =>
                {
                    var _clipLoop = curClipView.Loop;
                    _duration += Time.deltaTime;
                    if (_clipLoop)
                        _duration %= curClipView.Clip.length;

                    curClipView.Clip.SampleAnimation(animator.gameObject, _duration);
                    updateSlider(_duration);
                    // _animator.Update(Time.deltaTime);
                },
                () =>
                    (curClipView.Loop ? true : _duration <= curClipView.Clip.length)
                    && CanControl(),
                _finishPreview,
                _finishPreview
            );
        }

        private bool CanControl()
        {
            return animator
                && animator.enabled
                && animator.runtimeAnimatorController
                && AnimCtrlTarget.Value != null
                && animator.gameObject.activeInHierarchy;
        }

        public void StopAnimPreview()
        {
            playDisposable?.Cancel();
            playDisposable = null;

            if (!CanControl())
                return;

            animator.Play(curClipView.Clip.name, -1, 0f);
            animator.Update(0);
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
