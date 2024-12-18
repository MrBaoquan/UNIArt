﻿/*
Copyright (c) 2020 Omar Duarte
Unauthorized copying of this file, via any medium is strictly prohibited.
Writen by Omar Duarte, 2020.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UNIArt.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PluginMaster
{
    public class PsLayerCreator
    {
        private TextureUtils.OutputObjectType _outputType;
        private bool _importIntoSelectedObject;
        private string _rootName;
        private float _pixelsPerUnit;
        private SpriteAlignment _spriteAlignment;
        private Vector2 _spritePivot;
        private PsdFile _psdFile;
        private int _lastSortingOrder;
        private bool _createAtlas;
        private int _atlasMaxSize;
        private string _outputFolder;
        private bool _importOnlyVisibleLayers;
        private bool _addPsComponents;
        private PsGroup.BlendingShaderType _blendingShader;
        private Dictionary<int, Tuple<Texture2D, Rect>> _layerTextures;
        private float _scale;

        private GameObject _lastObject = null;

        public PsLayerCreator(
            TextureUtils.OutputObjectType outputType,
            bool importIntoSelectedObject,
            string rootName,
            float pixelsPerUnit,
            SpriteAlignment spriteAlignment,
            Vector2 customPivot,
            PsdFile psdFile,
            int lastSortingOrder,
            bool createAtlas,
            int atlasMaxSize,
            string outputFolder,
            bool importOnlyVisibleLayers,
            bool addPsComponents,
            PsGroup.BlendingShaderType blendingShader,
            Dictionary<int, Tuple<Texture2D, Rect>> layerTextures,
            float scale
        )
        {
            _outputType = outputType;
            _importIntoSelectedObject = importIntoSelectedObject;
            _rootName = rootName;
            _pixelsPerUnit = pixelsPerUnit;
            _spriteAlignment = spriteAlignment;
            _spritePivot = customPivot;
            _psdFile = psdFile;
            _lastSortingOrder = lastSortingOrder;
            _createAtlas = createAtlas;
            _atlasMaxSize = atlasMaxSize;
            _outputFolder = outputFolder;
            _importOnlyVisibleLayers = importOnlyVisibleLayers;
            _addPsComponents = addPsComponents;
            _blendingShader = blendingShader;
            _layerTextures = layerTextures;
            _scale = scale;
        }

        public static Texture2D[] CreatePngFiles(
            string outputFolder,
            Dictionary<int, Tuple<Texture2D, Rect>> layerTextures,
            PsdFile psdFile,
            float scale,
            float pixelsPerUnit,
            SpriteAlignment spriteAlignment,
            Vector2 customPivot,
            bool createAtlas,
            int atlasMaxSize,
            bool importOnlyVisibleLayers
        )
        {
            var textures = new List<Texture2D>(layerTextures.Select(obj => obj.Value.Item1));
            var namesAndTexturesList = new List<Tuple<string, Texture2D>>();
            foreach (var item in layerTextures.ToList())
            {
                if (item.Value.Item1 == null)
                    continue;
                var layer = psdFile.GetLayer(item.Key);
                if (importOnlyVisibleLayers && !(layer.Visible && layer.VisibleInHierarchy))
                    continue;
                var layerName = layer.Name;
                var scaledRect = new Rect(
                    item.Value.Item2.x * scale,
                    item.Value.Item2.y * scale,
                    item.Value.Item2.width * scale,
                    item.Value.Item2.height * scale
                );
                var scaledTexture = TextureUtils.GetScaledTexture(
                    item.Value.Item1,
                    (int)scaledRect.width,
                    (int)scaledRect.height
                );

                if (createAtlas)
                {
                    namesAndTexturesList.Add(
                        new Tuple<string, Texture2D>(
                            item.Key.ToString("D4") + "_" + layerName,
                            scaledTexture
                        )
                    );
                }
                else
                {
                    var fileName =
                        Path.GetFileNameWithoutExtension(psdFile.Path)
                        + "_"
                        + item.Key.ToString("D4")
                        + "_"
                        + layerName;
                    TextureUtils.SavePngAsset(
                        scaledTexture,
                        outputFolder + fileName + ".png",
                        pixelsPerUnit,
                        spriteAlignment,
                        customPivot
                    );
                }
            }
            if (createAtlas)
            {
                var texture = TextureUtils.CreateAtlas(
                    namesAndTexturesList.ToArray(),
                    atlasMaxSize,
                    outputFolder + Path.GetFileNameWithoutExtension(psdFile.Path) + ".png",
                    pixelsPerUnit,
                    spriteAlignment,
                    customPivot
                );
                textures.Add(texture);
            }
            return textures.ToArray();
        }

        public GameObject CreateGameObjets(Vector2 anchorMin, Vector2 anchorMax, Vector2 uiPivot)
        {
            foreach (var item in _layerTextures.ToList())
            {
                if (item.Value.Item1 == null)
                    continue;
                var scaledRect = new Rect(
                    item.Value.Item2.x * _scale,
                    item.Value.Item2.y * _scale,
                    item.Value.Item2.width * _scale,
                    item.Value.Item2.height * _scale
                );
                var scaledTexture = TextureUtils.GetScaledTexture(
                    item.Value.Item1,
                    (int)scaledRect.width,
                    (int)scaledRect.height
                );
                _layerTextures[item.Key] = new Tuple<Texture2D, Rect>(scaledTexture, scaledRect);
            }

            Transform rootParent = null;
            if (_importIntoSelectedObject)
            {
                rootParent = Selection.activeTransform;
            }
            GameObject root = new GameObject(_rootName);
            root.transform.parent = rootParent;
            _lastObject = root;

            if (_outputType == TextureUtils.OutputObjectType.UI_IMAGE)
            {
                if (root.transform.GetComponentInParent<Canvas>() == null)
                {
                    var canvas = root.AddComponent<Canvas>();
                    var scaler = root.AddComponent<CanvasScaler>();
                    scaler.referencePixelsPerUnit = _pixelsPerUnit;
                    root.AddComponent<GraphicRaycaster>();
                }
                else
                {
                    root.transform.GetComponentInParent<Canvas>().renderMode =
                        _blendingShader == PsGroup.BlendingShaderType.FAST
                            ? RenderMode.ScreenSpaceCamera
                            : RenderMode.ScreenSpaceOverlay;
                    var parentTransform = root.transform.GetComponentInParent<RectTransform>();
                    var rectTransform = root.AddComponent<RectTransform>();
                    rectTransform.localScale = Vector3.one;
                    rectTransform.localPosition = new Vector3(
                        0, //parentTransform.rect.width / 2f,
                        0, //-parentTransform.rect.height / 2f,
                        0f
                    );
                    rectTransform.anchorMax = anchorMax;
                    rectTransform.anchorMin = anchorMin;
                    rectTransform.pivot = uiPivot;
                    rectTransform.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        _psdFile.BaseLayer.Rect.width
                    );
                    rectTransform.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        _psdFile.BaseLayer.Rect.height
                    );
                }
                if (_blendingShader == PsGroup.BlendingShaderType.FAST)
                {
                    var canvas = root.transform.GetComponentInParent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = Camera.main;
                    canvas.sortingOrder = 20000;
                }
                else
                {
                    root.transform.GetComponentInParent<Canvas>().renderMode =
                        RenderMode.ScreenSpaceOverlay;
                }
            }

            var objectsAndTexturesList = new List<Tuple<string, GameObject, Texture2D, Rect>>();
            CreateHierarchy(
                root.transform,
                _psdFile.RootLayers.ToArray(),
                _lastSortingOrder,
                out _lastSortingOrder,
                ref objectsAndTexturesList,
                anchorMin,
                anchorMax,
                uiPivot
            );
            // root.transform.localPosition = Vector3.zero;

            if (_createAtlas)
            {
                TextureUtils.CreateAtlas(
                    _outputType,
                    objectsAndTexturesList.ToArray(),
                    _atlasMaxSize,
                    _outputFolder + Path.GetFileNameWithoutExtension(_psdFile.Path) + ".png",
                    _pixelsPerUnit,
                    _spriteAlignment,
                    _spritePivot
                );
            }
            return root;
        }

        private void CreateHierarchy(
            Transform parentTransform,
            Layer[] children,
            int initialSortingOrder,
            out int lastSortingOrder,
            ref List<Tuple<string, GameObject, Texture2D, Rect>> objectsAndTexturesList,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 uiPivot
        )
        {
            if (_outputType == TextureUtils.OutputObjectType.UI_IMAGE)
            {
                Array.Reverse(children);
            }
            lastSortingOrder = initialSortingOrder;
            foreach (var childLayer in children)
            {
                if (
                    _importOnlyVisibleLayers
                    && !(childLayer.Visible && childLayer.VisibleInHierarchy)
                )
                    continue;

                GameObject childGameObject = null;

                string objName = childLayer.Name;

                if (childLayer is LayerGroup)
                {
                    childGameObject = new GameObject(objName);
                    childGameObject.transform.parent = parentTransform.transform;
                    _lastObject = childGameObject;
                    CreateHierarchy(
                        childGameObject.transform,
                        ((LayerGroup)childLayer).Children,
                        lastSortingOrder,
                        out lastSortingOrder,
                        ref objectsAndTexturesList,
                        anchorMin,
                        anchorMax,
                        uiPivot
                    );
                    if (_addPsComponents)
                    {
                        PsGroup groupComp = childGameObject.AddComponent<PsGroup>();
                        groupComp.Initialize(
                            (PsdBlendModeType)childLayer.BlendModeKey,
                            childLayer.Alpha,
                            childLayer.Visible,
                            childLayer.VisibleInHierarchy,
                            _blendingShader
                        );
                    }
                    if (_outputType == TextureUtils.OutputObjectType.UI_IMAGE)
                    {
                        var rectTransform = childGameObject.AddComponent<RectTransform>();
                        rectTransform.localScale = Vector3.one;
                        rectTransform.localPosition = Vector3.zero;

                        rectTransform.anchorMax = anchorMax;
                        rectTransform.anchorMin = anchorMin;
                        rectTransform.pivot = uiPivot;

                        rectTransform.SetSizeWithCurrentAnchors(
                            RectTransform.Axis.Horizontal,
                            childLayer.Rect.width
                        );
                        rectTransform.SetSizeWithCurrentAnchors(
                            RectTransform.Axis.Vertical,
                            childLayer.Rect.height
                        );
                    }
                    continue;
                }

                Rect layerRect = _layerTextures[childLayer.Id].Item2;
                Texture2D texture = _layerTextures[childLayer.Id].Item1;
                if (texture == null)
                    continue;

                childGameObject = new GameObject(objName);
                childGameObject.transform.parent = parentTransform.transform;

                lastSortingOrder--;

                SpriteRenderer renderer = null;
                Image image = null;
                if (_outputType == TextureUtils.OutputObjectType.SPRITE_RENDERER)
                {
                    childGameObject.transform.position = new Vector3(
                        (layerRect.width / 2 + layerRect.x) / _pixelsPerUnit,
                        -(layerRect.height / 2 + layerRect.y) / _pixelsPerUnit,
                        0
                    );
                    renderer = childGameObject.AddComponent<SpriteRenderer>();
                    renderer.sortingOrder = lastSortingOrder;
                }
                else
                {
                    image = childGameObject.AddComponent<Image>();
                    image.rectTransform.localScale = Vector3.one;

                    image.rectTransform.localPosition = new Vector3(
                        layerRect.x
                            + layerRect.width * uiPivot.x
                            - _psdFile.BaseLayer.Rect.width * 0.5f * _scale,
                        -layerRect.y
                            + layerRect.height * (uiPivot.y - 1f)
                            + _psdFile.BaseLayer.Rect.height * 0.5f * _scale,
                        0f
                    );

                    image.rectTransform.anchorMax = anchorMax;
                    image.rectTransform.anchorMin = anchorMin;
                    image.rectTransform.pivot = uiPivot;
                    image.rectTransform.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        layerRect.width
                    );
                    image.rectTransform.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        layerRect.height
                    );
                }

                if (!_createAtlas)
                {
                    var _psdFileName = Path.GetFileNameWithoutExtension(_psdFile.Path);
                    var _layerID = childLayer.Id.ToString("D3");
                    var _layerName = childLayer.Name;

                    var fileName = $"{_layerName}_{_psdFileName}_{_layerID}";
                    fileName = Utils.ReplaceInvalidFileNameChars(fileName);

                    Sprite childSprite = TextureUtils.SavePngAsset(
                        texture,
                        _outputFolder + fileName + ".png",
                        _pixelsPerUnit,
                        _spriteAlignment,
                        _spritePivot
                    );
                    if (_outputType == TextureUtils.OutputObjectType.SPRITE_RENDERER)
                        renderer.sprite = childSprite;
                    else
                        image.sprite = childSprite;
                }
                if (_addPsComponents)
                {
                    PsLayer layerComp = null;
                    if (_outputType == TextureUtils.OutputObjectType.SPRITE_RENDERER)
                    {
                        layerComp = childGameObject.AddComponent<PsLayerSprite>();
                    }
                    else
                    {
                        layerComp = childGameObject.AddComponent<PsLayerImage>();
                    }
                    layerComp.Initialize(
                        (PsdBlendModeType)childLayer.BlendModeKey,
                        childLayer.Alpha,
                        childLayer.Visible,
                        childLayer.VisibleInHierarchy,
                        _blendingShader
                    );
                }
                var tuple = new Tuple<string, GameObject, Texture2D, Rect>(
                    childLayer.Id.ToString("D4") + "_" + objName,
                    childGameObject,
                    texture,
                    layerRect
                );
                objectsAndTexturesList.Add(tuple);
            }
        }
    }
}
#endif
