using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using PluginMaster;
using UNIArt.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UNIArt.Editor
{
    public static class PSUtils
    {
        static Dictionary<string, PSDFileMgr> cachedPSDFiles = new Dictionary<string, PSDFileMgr>();

        public static void Dispose(string psdPath)
        {
            if (cachedPSDFiles.ContainsKey(psdPath))
            {
                cachedPSDFiles[psdPath].Dispose();
                cachedPSDFiles.Remove(psdPath);
            }
        }

        public static PSDFileMgr GetPSDFile(string psdPath)
        {
            // TODO 后续考虑缓存优化，暂时先每次都新建
            if (cachedPSDFiles.ContainsKey(psdPath))
            {
                cachedPSDFiles[psdPath].Dispose();
                cachedPSDFiles.Remove(psdPath);
            }
            var psdFileMgr = new PSDFileMgr();
            cachedPSDFiles.Add(psdPath, psdFileMgr);
            return cachedPSDFiles[psdPath];
        }

        [MenuItem("Tools/PSD Test")]
        private static void PsdCreateGameObjectTest()
        {
            PrefabComponentCopier.CopyComponentsAndChildren(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ArtAssets/UI Prefabs/Windows/二级 合并.prefab"
                ),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ArtAssets/UI Prefabs/Widgets/二级 合并#psd.prefab"
                )
            );
        }

        public static GameObject RemovePSLayer(GameObject target)
        {
            var _psLayers = target.GetComponentsInChildren<PsGroup>(true).ToList();

            _psLayers.ForEach(_ =>
            {
                var _imageComp = _.GetComponent<Image>();
                if (_imageComp != null)
                {
                    _imageComp.color = new Color(1, 1, 1, _.Opacity);
                    _imageComp.material = null;
                }
                GameObject.DestroyImmediate(_);
            });

            return target;
        }

        public static GameObject PostProcessPSDEntity(GameObject target)
        {
            // 将所有PSGroup visible为false的设置为true, 并将gameobject.active设置为false
            var _psGroups = target.GetComponentsInChildren<PsGroup>();
            foreach (var _psGroup in _psGroups)
            {
                if (!_psGroup.Visible)
                {
                    _psGroup.Visible = true;
                    _psGroup.gameObject.SetActive(false);
                }
            }

            // 筛选名称符合@动画或者@按钮的物体
            var _allChildren = target.GetComponentsInChildren<Transform>(true);
            var _compFlagRegex = @".+@(?<type>动画|默认)$";
            _allChildren
                .Where(t => Regex.IsMatch(t.name, _compFlagRegex))
                .ToList()
                .ForEach(_child =>
                {
                    var _type = Regex.Match(_child.name, _compFlagRegex).Groups["type"].Value;
                    if (_type == "动画")
                    {
                        _child.gameObject.AddOrGetComponent<Animator>();
                        _child.gameObject.name = _child.gameObject.name.Replace(
                            "@动画",
                            string.Empty
                        );
                    }
                    else if (_type == "默认")
                    {
                        var _curImage = _child.gameObject.GetComponent<Image>();

                        var _selectedName = _child.name.Replace("@默认", "@选中");
                        var _pressedName = _child.name.Replace("@默认", "@点击");

                        if (_child.transform.parent == null)
                            return;

                        var _pressedTransform = _child.parent.Find(_pressedName);
                        var _toggleTransfrom = _child.parent.Find(_selectedName);

                        if (_pressedTransform != null) // 视为按钮
                        {
                            var _button = _child.gameObject.AddOrGetComponent<Button>();
                            _button.transition = Selectable.Transition.SpriteSwap;
                            var _spriteState = _button.spriteState;
                            _spriteState.pressedSprite = _pressedTransform
                                .GetComponent<Image>()
                                ?.sprite;
                            _spriteState.highlightedSprite = _curImage.sprite;
                            _button.spriteState = _spriteState;

                            GameObject.DestroyImmediate(_pressedTransform.gameObject);
                            _child.gameObject.name = _child.gameObject.name.Replace("@默认", "按钮");
                        }
                        else if (_toggleTransfrom != null) // 视为开关
                        {
                            var _toggle = _child.gameObject.AddOrGetComponent<Toggle>();
                            var _toggleGroup =
                                _toggle.transform.parent.gameObject.AddOrGetComponent<ToggleGroup>();

                            _toggle.group = _toggleGroup;

                            _toggle.transition = Selectable.Transition.SpriteSwap;
                            var _spriteState = _toggle.spriteState;
                            _spriteState.pressedSprite = _toggleTransfrom
                                .GetComponent<Image>()
                                ?.sprite;
                            _spriteState.highlightedSprite = _curImage.sprite;
                            _toggle.spriteState = _spriteState;

                            _child.gameObject.AddOrGetComponent<ToggleImage>();

                            GameObject.DestroyImmediate(_toggleTransfrom.gameObject);
                            _child.gameObject.name = _child.gameObject.name.Replace(
                                "@默认",
                                string.Empty
                            );
                        }
                        else
                        {
                            _child.gameObject.AddOrGetComponent<Button>();
                            _child.gameObject.name = _child.gameObject.name.Replace(
                                "@默认",
                                string.Empty
                            );
                        }
                    }
                });

            return target;
        }

        public static void CreatePSDGameObject(
            string psdFilePath,
            Action<GameObject> callback = null
        )
        {
            var psdFileMgr = GetPSDFile(psdFilePath);
            Action createGameObjects = () =>
            {
                var _loadingTitle = $"Loading PS File:  {psdFilePath}";
                var _loadingInfo = string.Empty;
                bool _bFinished = false;

                var _psdImportArgs = UNIArtSettings.GetPSDImportArgs(psdFilePath);
                _loadingInfo = "PSD Entity Post Processing ...";

                var _layerCreator = new PsLayerCreator(
                    TextureUtils.OutputObjectType.UI_IMAGE,
                    true,
                    Path.GetFileNameWithoutExtension(psdFilePath),
                    100,
                    SpriteAlignment.Center,
                    new Vector2(0.5f, 0.5f),
                    psdFileMgr._psdFile,
                    psdFileMgr._previewLayers.Count,
                    _psdImportArgs.CreateAtlas,
                    _psdImportArgs.MaxAtlasSize,
                    Path.GetDirectoryName(psdFilePath).ToForwardSlash() + "/",
                    _psdImportArgs.ImportOnlyVisibleLayers,
                    true,
                    PsGroup.BlendingShaderType.GRAB_PASS,
                    psdFileMgr._layerTextures,
                    _psdImportArgs.Scale
                );

                var _psEntityObject = _layerCreator.CreateGameObjets(
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f)
                );

                Utils.UpdateWhile(
                    () =>
                    {
                        EditorUtility.DisplayProgressBar(_loadingTitle, _loadingInfo, 0.95f);
                    },
                    () => !_bFinished,
                    () =>
                    {
                        EditorUtility.ClearProgressBar();
                    }
                );

                Utils.Delay(
                    () =>
                    {
                        try
                        {
                            if (_psEntityObject.GetComponent<Canvas>() != null)
                            {
                                GameObject.DestroyImmediate(
                                    _psEntityObject.GetComponent<GraphicRaycaster>()
                                );
                                GameObject.DestroyImmediate(
                                    _psEntityObject.GetComponent<CanvasScaler>()
                                );
                                GameObject.DestroyImmediate(_psEntityObject.GetComponent<Canvas>());
                                var _rectTrans = _psEntityObject.GetComponent<RectTransform>();
                                _rectTrans.anchorMin = Vector2.one * 0.5f;
                                _rectTrans.anchorMax = Vector2.one * 0.5f;
                                _rectTrans.localPosition = Vector3.zero;
                            }

                            _psEntityObject = PostProcessPSDEntity(_psEntityObject);

                            if (_psdImportArgs.AddPSLayer == false)
                            {
                                RemovePSLayer(_psEntityObject);
                            }

                            _psEntityObject.AddOrGetComponent<Animator>();
                            if (_psdImportArgs.RestoreEntity)
                            {
                                var _psdEntity = UNIArtSettings.GetPSDEntityInstance(
                                    _psdImportArgs.PSDEntityPath
                                );

                                if (_psdEntity != null)
                                {
                                    PrefabComponentCopier.CopyComponentsAndChildren(
                                        _psdEntity,
                                        _psEntityObject
                                    );
                                }
                            }

                            var _savePath = psdFilePath.Replace(".psd", "#psd.prefab");
                            _savePath = AssetDatabase.GenerateUniqueAssetPath(_savePath);
                            var _newPrefab = PrefabUtility.SaveAsPrefabAsset(
                                _psEntityObject,
                                _savePath
                            );

                            AssetDatabase.SaveAssets();
                            if (!UNIArtSettings.Project.DebugMode)
                            {
                                GameObject.DestroyImmediate(_psEntityObject);
                            }

                            _bFinished = true;
                            // AssetDatabase.Refresh();
                            callback?.Invoke(_newPrefab);
                            // AssetDatabase.OpenAsset(_newPrefab);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError(e.Message);
                        }
                        finally
                        {
                            _bFinished = true;
                            EditorUtility.ClearProgressBar();
                        }
                    },
                    1f
                );
            };

            if (psdFileMgr.IsReady)
            {
                // Debug.LogWarning($"PSD file is already loaded: {psdFilePath}");
                createGameObjects();
            }
            else
            {
                psdFileMgr.onAllLayerLoadCompleted.AddListener(() =>
                {
                    try
                    {
                        createGameObjects();
                    }
                    catch (System.Exception e)
                    {
                        psdFileMgr.Dispose();
                        Debug.LogError(e.Message);
                        Debug.LogError(e.StackTrace);
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                });
                psdFileMgr.PreprePSD(psdFilePath);
            }
        }

        // [MenuItem("Tools/PSD Layer Import")]
        // public static void PsdImport()
        // {
        //     var psdFileMgr = new PSDFileMgr();
        //     psdFileMgr.onAllLayerLoadCompleted.AddListener(() =>
        //     {
        //         PsLayerCreator.CreatePngFiles(
        //             "Assets/ArtAssets/Textures/Psd/待机",
        //             psdFileMgr._layerTextures,
        //             psdFileMgr._psdFile,
        //             2,
        //             200,
        //             SpriteAlignment.Center,
        //             new Vector2(0.5f, 0.5f),
        //             false,
        //             4096,
        //             true
        //         );
        //         psdFileMgr.Dispose();
        //     });
        //     psdFileMgr.PreprePSD("Assets/ArtAssets/Textures/Psd/待机.12psd");
        // }
    }

    public class PSDFileMgr
    {
        private abstract class HierarchyItem
        {
            public readonly int Id = -1;

            protected HierarchyItem(int id) => (Id) = (id);
        }

        private class LayerItem : HierarchyItem
        {
            public readonly Texture2D TextureNoBorder = null;
            public readonly Texture2D TextureWithBorder = null;

            public LayerItem(int id, Texture2D textureNoBorder, Texture2D textureWithBorder)
                : base(id) =>
                (TextureNoBorder, TextureWithBorder) = (textureNoBorder, textureWithBorder);
        }

        private class GroupItem : HierarchyItem
        {
            public bool IsOpen { get; set; }

            public GroupItem(int id, bool isOpen)
                : base(id) => (IsOpen) = (isOpen);
        }

        public enum PSDStatus
        {
            NotLoaded,
            Loading,
            Loaded,
            Error,
            Disposed
        }

        public PSDStatus Status = PSDStatus.NotLoaded;
        public bool IsReady => Status == PSDStatus.Loaded;

        private class PendingData
        {
            public bool pending = false;
            public Layer layer = null;
            public Color32[] thumbnailPixels = null;
            public Color32[] thumbnailPixelsWithBorder = null;
            public Color32[] layerPixels = null;
            public Rect layerRect = Rect.zero;

            public void Reset()
            {
                pending = false;
                layer = null;
                thumbnailPixels = null;
                thumbnailPixelsWithBorder = null;
                layerPixels = null;
                layerRect = Rect.zero;
            }
        }

        private PendingData _pixelsPending = new PendingData();

        private string _psdPath = "";
        private Thread _psdFileThread = null;
        private float _tempProgress = 0f;
        private float _loadingProgress
        {
            get { return _tempProgress; }
            set { _tempProgress = Mathf.Clamp01(value); }
        }

        // private string _loadingTitle = "Loading";
        private string _loadingInfo = "";

        // private int _progressId = -1;

        private Queue<Layer> _textureLoadingPendingLayers = new Queue<Layer>();
        private Thread _loadThumbnailThread = null;

        private TextureUtils.LayerTextureLoader _textureLoader = null;

        public UnityEvent onPsdFileLoadCompleted = new UnityEvent();

        public UnityEvent onAllLayerLoadCompleted = new UnityEvent();

        public void LoadAllLayers()
        {
            _hierarchyItems.Clear();
            _textureLoadingPendingLayers.Clear();
            _layerTextures.Clear();

            CreateItemDictionary(_psdFile.RootLayers.ToArray());
            var _totalLayerCount = _textureLoadingPendingLayers.Count;

            LoadPendingTextures();

            Utils.UpdateWhile(
                () =>
                {
                    EditorUtility.DisplayProgressBar(
                        $"Loading PS File:  {_psdPath}",
                        _loadingInfo,
                        _loadingProgress
                    );
                    if (!(_pixelsPending.pending && _textureLoader == null))
                        return;
                    var layer = _pixelsPending.layer;
                    if (_layerTextures.ContainsKey(layer.Id))
                    {
                        LoadNextLayer();
                        _pixelsPending.pending = false;
                    }
                    if (_pixelsPending.thumbnailPixels == null)
                    {
                        _hierarchyItems.Add(layer.Id, new LayerItem(layer.Id, null, null));
                        _layerTextures.Add(layer.Id, new Tuple<Texture2D, Rect>(null, Rect.zero));
                        LoadNextLayer();
                        _pixelsPending.pending = false;
                        return;
                    }

                    var thumbnailTexture = new Texture2D(
                        (int)_previewRect.width,
                        (int)_previewRect.height,
                        TextureFormat.RGBA32,
                        true
                    );
                    thumbnailTexture.SetPixels32(_pixelsPending.thumbnailPixels);
                    thumbnailTexture.Apply();

                    var hierarchyThumbnailTexture = new Texture2D(
                        _hierarchyThumbnailW,
                        _hierarchyThumbnailH,
                        TextureFormat.RGBA32,
                        true
                    );
                    hierarchyThumbnailTexture.SetPixels32(_pixelsPending.thumbnailPixelsWithBorder);
                    hierarchyThumbnailTexture.Apply();
                    _hierarchyItems.Add(
                        layer.Id,
                        new LayerItem(layer.Id, thumbnailTexture, hierarchyThumbnailTexture)
                    );

                    var layerTexture = new Texture2D(
                        (int)_pixelsPending.layerRect.width,
                        (int)_pixelsPending.layerRect.height,
                        TextureFormat.RGBA32,
                        true
                    );
                    layerTexture.SetPixels32(_pixelsPending.layerPixels);
                    layerTexture.Apply();

                    _layerTextures.Add(
                        layer.Id,
                        new Tuple<Texture2D, Rect>(layerTexture, _pixelsPending.layerRect)
                    );

                    LoadNextLayer();

                    _pixelsPending.pending = false;
                    _pixelsPending.Reset();
                },
                () => _layerTextures.Count < _totalLayerCount && Status != PSDStatus.Disposed,
                () =>
                {
                    PreviewPanel();
                    Status = PSDStatus.Loaded;
                    try
                    {
                        onAllLayerLoadCompleted?.Invoke();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning(e.Message);
                    }
                    finally
                    {
                        onAllLayerLoadCompleted.RemoveAllListeners();
                        EditorUtility.ClearProgressBar();
                    }
                }
            );
        }

        private void PreviewPanel()
        {
            _previewLayers.Clear();
            foreach (var item in _hierarchyItems.Values)
            {
                if (!(item is LayerItem))
                    continue;
                if (((LayerItem)item).TextureNoBorder == null)
                    continue;
                var scaledTexture = TextureUtils.GetScaledTexture(
                    ((LayerItem)item).TextureNoBorder,
                    (int)_previewRect.width,
                    (int)_previewRect.height
                );
                var layer = _psdFile.GetLayer(item.Id);
                // UnityEngine.Debug.LogWarning($"add preview layer: {item.Id} {layer.Name}");
                _previewLayers.Add(
                    item.Id,
                    new PreviewLayer(
                        layer.Name,
                        scaledTexture,
                        layer.BlendModeInHierarchy,
                        layer.Visible && layer.VisibleInHierarchy,
                        layer.AlphaInHierarchy
                    )
                );
            }

            // _importWidth = Mathf.RoundToInt(_psdFile.BaseLayer.Rect.width);
            // _importHeight = Mathf.RoundToInt(_psdFile.BaseLayer.Rect.height);
            // _aspectRatio = _psdFile.BaseLayer.Rect.width / _psdFile.BaseLayer.Rect.height;

            _resultTexture = GetPreviewTexture((int)_previewRect.width, (int)_previewRect.height);
            _resultTexture = TextureUtils.SetTextureBorder(
                _resultTexture,
                (int)_previewRect.width,
                (int)_previewRect.height
            );
        }

        private Texture2D GetPreviewTexture(int width, int height)
        {
            var resultTexture = new Texture2D(width, height, TextureFormat.RGBA32, true);

            var resultTexturePixels = resultTexture.GetPixels();

            for (int i = 0; i < resultTexturePixels.Length; ++i)
            {
                var r = i / width;
                var c = i - r * width;
                resultTexturePixels[i] =
                    (r % 16 < 8) == (c % 16 < 8) ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            }

            for (int layerIdx = _previewLayers.Count - 1; layerIdx >= 0; --layerIdx)
            {
                var previewLayer = _previewLayers.ElementAt(layerIdx).Value;
                if (!previewLayer.Visible)
                    continue;
                var sourcePixels = previewLayer.Texture.GetPixels();
                for (int i = 0; i < resultTexturePixels.Length; ++i)
                {
                    resultTexturePixels[i] = PsdImportWindow.GetBlendedPixel(
                        resultTexturePixels[i],
                        sourcePixels[i],
                        previewLayer.Alpha,
                        previewLayer.BlendMode
                    );
                }
            }

            resultTexture.SetPixels(resultTexturePixels);
            resultTexture.Apply();
            return resultTexture;
        }

        public void PreprePSD(string psdFilePath)
        {
            onPsdFileLoadCompleted.AddListener(() =>
            {
                LoadAllLayers();
            });
            LoadFile(psdFilePath);
        }

        public void LoadFile(string path)
        {
            DestroyAllTextures();
            _psdPath = path;
            var _loadingError = false;
            var _loaded = false;
            Action<float> _onFileLoading = _progress =>
            {
                _loadingProgress = _progress * 0.3f;
                _loadingInfo = $"Loading PSD file: {_psdPath} - {_progress * 100:F1}%";
            };

            Action _onFileLoaded = null;
            Action<string> _onFileLoadingError = null;
            _onFileLoadingError = _error =>
            {
                _psdFile.OnProgressChanged -= _onFileLoading;
                _psdFile.OnDone -= _onFileLoaded;
                _psdFile.OnError -= _onFileLoadingError;
                _psdFile = null;
                _psdFileThread.Abort();
                _loadingError = true;
            };

            _onFileLoaded = () =>
            {
                _psdFile.OnProgressChanged -= _onFileLoading;
                _psdFile.OnDone -= _onFileLoaded;
                _psdFile.OnError -= _onFileLoadingError;

                var aspect = _psdFile.BaseLayer.Rect.width / _psdFile.BaseLayer.Rect.height;
                _hierarchyThumbnailW = (int)((float)_hierarchyThumbnailH * aspect);
                _previewRect = GetPreviewRect(
                    (int)_psdFile.BaseLayer.Rect.width,
                    (int)_psdFile.BaseLayer.Rect.height,
                    399,
                    200
                );
                _loaded = true;
            };

            Utils.UpdateWhile(
                () => { },
                () => !_loaded && !_loadingError && Status != PSDStatus.Disposed,
                () =>
                {
                    onPsdFileLoadCompleted.Invoke();
                    EditorUtility.ClearProgressBar();
                }
            );

            _psdFile = new PsdFile(path);
            _psdFile.OnProgressChanged += _onFileLoading;
            _psdFile.OnDone += _onFileLoaded;
            _psdFile.OnError += _onFileLoadingError;
            var threadDelegate = new ThreadStart(_psdFile.Load);
            _psdFileThread = new Thread(threadDelegate);
            _psdFileThread.Start();
            DestroyAllTextures();
            // #if UNITY_2020_1_OR_NEWER
            //             _progressId = Progress.Start("Loading");
            // #endif
        }

        private bool ProgressBar()
        {
            if (_loadingProgress < 1f)
            {
                // #if UNITY_2020_1_OR_NEWER
                //                 Progress.Report(_progressId, _loadingProgress);
                // #else
                // EditorUtility.DisplayProgressBar(
                //     "Loading",
                //     ((int)(_loadingProgress * 100)).ToString() + " %",
                //     _loadingProgress
                // );
                //#endif
                return false;
            }

            if (_pixelsPending.pending || _textureLoader != null)
                return false;
            return true;
        }

        private Rect GetPreviewRect(int sourceWidth, int sourceHeight, int maxWidth, int maxHeight)
        {
            var resultWidth = maxWidth;
            var aspectRatio = (float)sourceWidth / (float)sourceHeight;
            var resultHeight = (int)((float)resultWidth / aspectRatio);

            if (resultHeight > maxHeight)
            {
                resultHeight = maxHeight;
                resultWidth = (int)((float)resultHeight * aspectRatio);
            }

            if (resultWidth < sourceWidth && resultHeight < sourceHeight)
            {
                return new Rect(0, 0, resultWidth, resultHeight);
            }
            return new Rect(0, 0, sourceWidth, sourceHeight);
        }

        private void DestroyAllTextures()
        {
            var textures = UnityEngine.Object.FindObjectsOfType<Texture2D>();
            DestroyTextures(textures);
        }

        private void DestroyTextures(Texture2D[] textures)
        {
            foreach (var texture in textures)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            Resources.UnloadUnusedAssets();
        }

        private Rect _previewRect = Rect.zero;
        private const int _hierarchyThumbnailH = 32;
        private int _hierarchyThumbnailW = 32;

        UnityEvent<Layer> onLayerLoaded = new UnityEvent<Layer>();

        private void LoadPendingTextures()
        {
            _pixelsPending = new PendingData();
            if (_textureLoadingPendingLayers.Count == 0)
            {
                LoadNextLayer();
                return;
            }
            var layer = _textureLoadingPendingLayers.Dequeue();

            _textureLoader = new TextureUtils.LayerTextureLoader(
                layer,
                (int)_psdFile.BaseLayer.Rect.width,
                (int)_psdFile.BaseLayer.Rect.height,
                _hierarchyThumbnailW,
                _hierarchyThumbnailH,
                (int)_previewRect.width,
                (int)_previewRect.height
            );

            Action<float> _onTextureLoading = null;
            Action<Layer, Color32[], Color32[], Color32[], Rect> _onTextureLoadingComplete = null;
            Action _onTextureLoadingError = null;

            _onTextureLoading = progress =>
            {
                _loadingProgress =
                    0.3f
                    + (
                        progress
                        + (float)_textureCount
                        - (float)_textureLoadingPendingLayers.Count
                        - 1f
                    )
                        / (float)_textureCount
                        * 0.7f;
                _loadingInfo = $"Loading layer: {layer.Name} - {_loadingProgress * 100:F1}%";
            };

            _onTextureLoadingComplete = (
                Layer layer,
                Color32[] thumbnailPixels,
                Color32[] thumbnailPixelsWithBorder,
                Color32[] layerPixels,
                Rect layerRect
            ) =>
            {
                _textureLoader.ProgressChanged -= _onTextureLoading;
                _textureLoader.OnLoadingComplete -= _onTextureLoadingComplete;

                _pixelsPending.pending = true;
                _pixelsPending.layer = layer;
                _pixelsPending.thumbnailPixels = thumbnailPixels;
                _pixelsPending.thumbnailPixelsWithBorder = thumbnailPixelsWithBorder;
                _pixelsPending.layerPixels = layerPixels;
                _pixelsPending.layerRect = layerRect;

                _textureLoader = null;
                onLayerLoaded.Invoke(layer);
            };

            _onTextureLoadingError = () =>
            {
                _textureLoader.ProgressChanged -= _onTextureLoading;
                _textureLoader.OnLoadingComplete -= _onTextureLoadingComplete;
                _textureLoader.OnError -= _onTextureLoadingError;
                _textureLoader = null;
                // _loadingError = true;
                _loadThumbnailThread.Abort();
            };

            _textureLoader.ProgressChanged += _onTextureLoading;
            _textureLoader.OnLoadingComplete += _onTextureLoadingComplete;
            _textureLoader.OnError += _onTextureLoadingError;
            var threadDelegate = new ThreadStart(_textureLoader.LoadLayerPixels);
            _loadThumbnailThread = new Thread(threadDelegate);
            _loadThumbnailThread.Name = layer.Name;
            _loadThumbnailThread.Start();
        }

        private void LoadNextLayer()
        {
            if (_textureLoadingPendingLayers.Count == 0)
            {
                // Debug.LogWarning("All layers loaded");
                _loadingProgress = 1f;
                // _updatePreview = true;
                // #if UNITY_2020_1_OR_NEWER
                //                 _progressId = Progress.Remove(_progressId);
                // #else
                EditorUtility.ClearProgressBar();
                //#endif
                DestroyUnusedTextures();
            }
            else
            {
                LoadPendingTextures();
            }
        }

        private int _textureCount = 0;

        private Texture2D _resultTexture = null;
        private Dictionary<int, HierarchyItem> _hierarchyItems =
            new Dictionary<int, HierarchyItem>();

        private void DestroyUnusedTextures()
        {
            var textures = UnityEngine.Object.FindObjectsOfType<Texture2D>();
            var textureSet = new HashSet<Texture2D>(textures);
            textureSet.Remove(_resultTexture);
            foreach (var item in _hierarchyItems.Values)
            {
                if (!(item is LayerItem))
                    continue;
                var layerItem = item as LayerItem;
                textureSet.Remove(layerItem.TextureNoBorder);
                textureSet.Remove(layerItem.TextureWithBorder);
            }
            foreach (var item in _layerTextures)
            {
                textureSet.Remove(item.Value.Item1);
            }
            DestroyTextures(textureSet.ToArray());
            textureSet.Clear();
            textureSet = null;
            textures = null;
        }

        public PsdFile _psdFile = null;
        public Dictionary<int, Tuple<Texture2D, Rect>> _layerTextures =
            new Dictionary<int, Tuple<Texture2D, Rect>>();

        // private PsLayerCreator _layerCreator = null;

        // [DebuggerDisplay("Name = {Name}")]
        public class PreviewLayer
        {
            public readonly string Name = null;
            public readonly Texture2D Texture = null;
            public readonly PsdBlendModeType BlendMode = PsdBlendModeType.NORMAL;
            public bool Visible { get; set; }
            public readonly float Alpha = 1f;

            public PreviewLayer(
                string name,
                Texture2D texture,
                PsdBlendModeType blendMode,
                bool visible,
                float alpha
            ) =>
                (Name, Texture, BlendMode, Visible, Alpha) = (
                    name,
                    texture,
                    blendMode,
                    visible,
                    alpha
                );
        }

        public Dictionary<int, PreviewLayer> _previewLayers = new Dictionary<int, PreviewLayer>();

        private void CreateItemDictionary(Layer[] layers)
        {
            foreach (var layer in layers)
            {
                if (layer is LayerGroup)
                {
                    _hierarchyItems.Add(
                        layer.Id,
                        new GroupItem(layer.Id, ((LayerGroup)layer).IsOpen)
                    );
                    CreateItemDictionary(((LayerGroup)layer).Children);
                    continue;
                }
                _textureLoadingPendingLayers.Enqueue(layer);
                ++_textureCount;
            }
        }

        public void Dispose()
        {
            if (_textureLoader != null || _psdFile != null)
            {
                EditorUtility.ClearProgressBar();
            }

            if (_textureLoader != null)
            {
                _textureLoader.Cancel();
                _textureLoader = null;
                _loadThumbnailThread.Abort();
                _pixelsPending.pending = false;
            }
            DestroyAllTextures();

            if (_psdFile != null)
            {
                _psdFile.Cancel();
                _psdFile = null;
                _psdFileThread.Abort();
            }
            Status = PSDStatus.Disposed;
        }
    }
}
