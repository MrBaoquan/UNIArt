using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UNIHper.Art.Editor
{
    public static class SpriteSheetTool
    {
        [MenuItem("Assets/Create/UI Image 序列帧", priority = 35)]
        public static void CreateUIImageAnimation()
        {
            CreateSpriteAnimation(typeof(Image));
        }

        [MenuItem("Assets/Create/2D Sprite 序列帧", priority = 36)]
        public static void CreateSpriteAnimation()
        {
            CreateSpriteAnimation(typeof(SpriteRenderer));
        }

        [MenuItem("Assets/Create/UI Image 序列帧", true)]
        [MenuItem("Assets/Create/2D Sprite 序列帧", true)]
        public static bool CreateSpriteSheetAnimationValidation()
        {
            return GetFilteredSprites().Count > 0;
        }

        public static void CreateSpriteAnimation(Type spriteType)
        {
            var _sprites = GetFilteredSprites().OrderBy(_sp => _sp.name).ToArray();

            string path = EditorUtility.SaveFilePanelInProject(
                "保存序列图动画",
                "New AnimationClip",
                "anim",
                "保存序列图动画"
            );
            if (string.IsNullOrEmpty(path))
                return;
            AnimationClip animationClip = new AnimationClip();
            animationClip.frameRate = 25;

            EditorCurveBinding spriteBinding = new EditorCurveBinding();
            spriteBinding.type = spriteType;
            spriteBinding.path = "";
            spriteBinding.propertyName = "m_Sprite";

            ObjectReferenceKeyframe[] spriteKeyframes = new ObjectReferenceKeyframe[
                _sprites.Length
            ];
            for (int i = 0; i < _sprites.Length; i++)
            {
                spriteKeyframes[i] = new ObjectReferenceKeyframe();
                spriteKeyframes[i].time = i / animationClip.frameRate;
                spriteKeyframes[i].value = _sprites[i];
            }

            AnimationUtility.SetObjectReferenceCurve(animationClip, spriteBinding, spriteKeyframes);

            AssetDatabase.CreateAsset(animationClip, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ProjectWindowUtil.ShowCreatedAsset(animationClip);
        }

        private static List<Sprite> GetFilteredSprites()
        {
            return Selection
                .GetFiltered<Texture2D>(SelectionMode.TopLevel)
                .SelectMany(
                    _tex =>
                        AssetDatabase
                            .LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(_tex))
                            .OfType<Sprite>()
                )
                .ToList();
        }
    }
}
