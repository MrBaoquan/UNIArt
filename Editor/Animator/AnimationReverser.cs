using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UNIArt.Editor
{
    public static class ReverseAnimationContext
    {
        private static void Test() { }

        [MenuItem("Assets/Create/反转动画", false, 33)]
        private static void ReverseClip()
        {
            ReverseClip(GetSelectedClip());
        }

        [MenuItem("Assets/Create/反转动画", true)]
        static bool ReverseClipValidation()
        {
            return Selection.activeObject
                && Selection.activeObject.GetType() == typeof(AnimationClip);
        }

        // create reverse clip from an animation clip
        public static AnimationClip ReverseClip(AnimationClip originalClip)
        {
            AnimationClip _reversedClip = null;
            var _assetPath = AssetDatabase.GetAssetPath(originalClip);

            if (AssetDatabase.IsSubAsset(originalClip))
            {
                var _childAnims = AssetDatabase
                    .LoadAllAssetRepresentationsAtPath(_assetPath)
                    .Where(_ => _ is AnimationClip)
                    .OfType<AnimationClip>();

                var _reversedClipName = originalClip.name + "_Reversed";
                if (originalClip.name == "出现")
                {
                    _reversedClipName = "消失";
                }
                else if (originalClip.name == "消失")
                {
                    _reversedClipName = "出现";
                }

                _reversedClip = _childAnims
                    .Where(_ => _.name == _reversedClipName)
                    .FirstOrDefault();
                if (_reversedClip is not null)
                {
                    AssetDatabase.RemoveObjectFromAsset(_reversedClip);
                    Object.DestroyImmediate(_reversedClip);
                    AssetDatabase.SaveAssets();
                }
                _reversedClip = Object.Instantiate<AnimationClip>(originalClip);
                _reversedClip.name = _reversedClipName;
                AssetDatabase.AddObjectToAsset(_reversedClip, _assetPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                var _copiedAnimPath = AssetDatabase.GenerateUniqueAssetPath(
                    _assetPath.Replace(".anim", "_Reversed.anim")
                );
                AssetDatabase.CopyAsset(_assetPath, _copiedAnimPath);
                _reversedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_copiedAnimPath);
            }

            if (_reversedClip is null)
                return null;

            float _clipLength = _reversedClip.length;
            var _curves = AnimationUtility.GetCurveBindings(_reversedClip);
            _reversedClip.ClearCurves();

            foreach (EditorCurveBinding binding in _curves)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(originalClip, binding);
                Keyframe[] keys = curve.keys;
                int keyCount = keys.Length;
                WrapMode postWrapMode = curve.postWrapMode;
                curve.postWrapMode = curve.preWrapMode;
                curve.preWrapMode = postWrapMode;
                for (int i = 0; i < keyCount; i++)
                {
                    Keyframe K = keys[i];
                    K.time = _clipLength - K.time;
                    float tmp = -K.inTangent;
                    K.inTangent = -K.outTangent;
                    K.outTangent = tmp;
                    keys[i] = K;
                }
                curve.keys = keys;
                _reversedClip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
            var events = AnimationUtility.GetAnimationEvents(_reversedClip);
            if (events.Length > 0)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    events[i].time = _clipLength - events[i].time;
                }
                AnimationUtility.SetAnimationEvents(_reversedClip, events);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return _reversedClip;
        }

        public static AnimationClip GetSelectedClip()
        {
            return Selection
                .GetFiltered<AnimationClip>(SelectionMode.Editable | SelectionMode.Deep)
                .FirstOrDefault();
        }
    }
}
