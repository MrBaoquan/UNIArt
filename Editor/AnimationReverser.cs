using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UNIHper.Art.Editor
{
    public static class ReverseAnimationContext
    {
        [MenuItem("Assets/Create/Reversed Clip", false, 14)]
        private static void ReverseClip()
        {
            ReverseClip(GetSelectedClip());
        }

        [MenuItem("Assets/Create/Reversed Clip", true)]
        static bool ReverseClipValidation()
        {
            return Selection.activeObject.GetType() == typeof(AnimationClip);
        }

        // create reverse clip from an animation clip
        public static AnimationClip ReverseClip(AnimationClip originalClip)
        {
            string directoryPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(originalClip));
            string fileName = Path.GetFileName(AssetDatabase.GetAssetPath(originalClip));
            // csharpier-ignore
            string fileExtension = Path.GetExtension(AssetDatabase.GetAssetPath(originalClip));
            fileName = Path.GetFileNameWithoutExtension(fileName);
            // csharpier-ignore
            string copiedFilePath = directoryPath + Path.DirectorySeparatorChar + fileName + "_Reversed" + fileExtension;

            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(originalClip), copiedFilePath);
            var reversedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(copiedFilePath);
            // csharpier-ignore
            if (reversedClip == null) return null;
            float clipLength = reversedClip.length;
            var curves = AnimationUtility.GetCurveBindings(reversedClip);
            reversedClip.ClearCurves();

            foreach (EditorCurveBinding binding in curves)
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
                    K.time = clipLength - K.time;
                    float tmp = -K.inTangent;
                    K.inTangent = -K.outTangent;
                    K.outTangent = tmp;
                    keys[i] = K;
                }
                curve.keys = keys;
                reversedClip.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }
            var events = AnimationUtility.GetAnimationEvents(reversedClip);
            if (events.Length > 0)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    events[i].time = clipLength - events[i].time;
                }
                AnimationUtility.SetAnimationEvents(reversedClip, events);
            }
            return reversedClip;
        }

        public static AnimationClip GetSelectedClip()
        {
            return Selection.GetFiltered(typeof(AnimationClip), SelectionMode.Assets).FirstOrDefault() as AnimationClip;
        }
    }
}
