using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UNIArt.Editor
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

        public static AnimationClip CreateSequenceImageAnimation(List<string> imagePaths)
        {
            var _sprites = imagePaths
                .Select(_ => AssetDatabase.LoadAssetAtPath<Sprite>(_))
                .OfType<Sprite>()
                .OrderBy(_sp => _sp.name)
                .ToList();

            var _spPath = imagePaths.First();
            var _saveDir = $"{UNIArtSettings.Project.ArtFolder}/Animations";
            if (UNIArtSettings.IsTemplateAsset(_spPath))
            {
                _saveDir =
                    UNIArtSettings.GetExternalTemplateRootBySubAsset(_spPath) + "/Animations";
            }
            var regex = new Regex(@"(_)?(\d+)?$");
            var _animName = regex.Replace(Path.GetFileNameWithoutExtension(_spPath), "");

            var _animPath = Path.Combine(_saveDir, _animName + ".anim").ToForwardSlash();
            Utils.CreateFolderIfNotExist(_saveDir);
            _animPath = AssetDatabase.GenerateUniqueAssetPath(_animPath);
            return CreateSequenceAnimation(_animPath, typeof(Image), _sprites);
        }

        public static AnimationClip CreateSequenceImageAnimation(List<Sprite> sprites)
        {
            var _saveDir = $"{UNIArtSettings.Project.ArtFolder}/Animations";
            var _animName = sprites.First().name;
            var _animPath = Path.Combine(_saveDir, _animName + ".anim").ToForwardSlash();
            Utils.CreateFolderIfNotExist(_saveDir);
            _animPath = AssetDatabase.GenerateUniqueAssetPath(_animPath);
            return CreateSequenceAnimation(_animPath, typeof(Image), sprites);
        }

        public static AnimationClip CreateSequenceAnimation(
            string savePath,
            Type spriteType,
            List<Sprite> _sprites
        )
        {
            AnimationClip animationClip = new AnimationClip();
            animationClip.frameRate = 25;

            EditorCurveBinding spriteBinding = new EditorCurveBinding();
            spriteBinding.type = spriteType;
            spriteBinding.path = "";
            spriteBinding.propertyName = "m_Sprite";

            ObjectReferenceKeyframe[] spriteKeyframes = new ObjectReferenceKeyframe[_sprites.Count];
            for (int i = 0; i < _sprites.Count; i++)
            {
                spriteKeyframes[i] = new ObjectReferenceKeyframe();
                spriteKeyframes[i].time = i / animationClip.frameRate;
                spriteKeyframes[i].value = _sprites[i];
            }

            AnimationUtility.SetObjectReferenceCurve(animationClip, spriteBinding, spriteKeyframes);

            AssetDatabase.CreateAsset(animationClip, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return animationClip;
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
