using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UNIArt.Editor
{
    public static class AnimatorExt
    {
        public static AnimatorController CreateController(Animator animator)
        {
            if (animator.runtimeAnimatorController == null)
            {
                var _gameObject = animator.gameObject;

                var _WorkFolder = "Assets/ArtAssets";
                var _assetPath = Utils.GetPrefabAssetPathByAnyGameObject(_gameObject);
                if (UNIArtSettings.IsTemplateAsset(_assetPath))
                {
                    var _templateRoot = UNIArtSettings.GetExternalTemplateRootBySubAsset(
                        _assetPath
                    );
                    if (!string.IsNullOrEmpty(_templateRoot))
                    {
                        _WorkFolder = _templateRoot;
                    }
                }

                var _animationFolder = Path.Combine(_WorkFolder, "Animations").ToForwardSlash();

                if (!AssetDatabase.IsValidFolder(_animationFolder))
                    AssetDatabase.CreateFolder(_WorkFolder, "Animations");

                var _controller = AnimatorController.CreateAnimatorControllerAtPath(
                    AssetDatabase.GenerateUniqueAssetPath(
                        $"{_animationFolder}/{animator.gameObject.name}_Controller.controller"
                    )
                );
                animator.runtimeAnimatorController = _controller;
                EditorUtility.SetDirty(animator);
                AssetDatabase.SaveAssetIfDirty(animator);
                AssetDatabase.Refresh();
                Selection.activeGameObject = animator.gameObject;
            }
            return animator.runtimeAnimatorController as AnimatorController;
        }

        public static void AddClipToController(AnimatorController controller, string animName)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = animName;
            AssetDatabase.AddObjectToAsset(clip, controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AddClipToController(controller, clip);
        }

        public static void AddClipToController(AnimatorController controller, AnimationClip clip)
        {
            if (controller.layers.Length <= 0)
            {
                controller.AddLayer("Base Layer");
            }
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
