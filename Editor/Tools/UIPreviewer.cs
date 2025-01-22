using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UNIArt.Editor
{
    public class UIPreviewer
    {
        static Texture2D ErrorTexture = new Texture2D(2, 2);
        static RenderTexture renderTexture;

        public static Texture2D CreateAssetPreview(Object target, string savePath)
        {
            var _texture = CreateAssetPreview(target);
            if (_texture is null)
                return null;

            Utils.CreateFolderIfNotExist(Path.GetDirectoryName(savePath));

            var _bytes = _texture.EncodeToPNG();
            File.WriteAllBytes(savePath, _bytes);

            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        }

        public static void CreateAssetPreview(string assetPath, string savePath)
        {
            var _obj = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            CreateAssetPreview(_obj, savePath);
        }

        public static Texture2D CreateAssetPreview(Object target)
        {
            // Stop if Prefab contains particle systems.
            var checkForParticlesGO = target as GameObject;
            var checkParticleSystems =
                checkForParticlesGO.GetComponentsInChildren<ParticleSystem>();
            if (checkParticleSystems != null && checkParticleSystems.Length > 0)
            {
                Debug.LogWarning(
                    "UIPreview: Preview of Prefabs with ParticleSystems is not supported due to a Unity Bug (Isse#:1399450 and similar)."
                );
                return ErrorTexture;
            }

            var previewTex = AssetPreview.GetAssetPreview(target);
            if (previewTex != null)
                return previewTex;

            var previewScene = EditorSceneManager.NewPreviewScene();

            // cam
            GameObject cameraObj = EditorUtility.CreateGameObjectWithHideFlags(
                "camera",
                HideFlags.DontSave
            );
            EditorSceneManager.MoveGameObjectToScene(cameraObj, previewScene);
            cameraObj.transform.localScale = Vector3.one;
            cameraObj.transform.localPosition = new Vector3(0, 0, -10f);
            cameraObj.transform.localRotation = Quaternion.identity;
            Camera camera = cameraObj.AddComponent<Camera>();
            camera.backgroundColor = new Color(0.193f, 0.193f, 0.193f, 1f);
            camera.clearFlags = CameraClearFlags.Color;
            camera.cameraType = CameraType.SceneView;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000;
            camera.scene = previewScene;
            camera.enabled = true;
            camera.useOcclusionCulling = false;
            camera.orthographic = true;

            // canvas
            GameObject canvasObj = EditorUtility.CreateGameObjectWithHideFlags(
                "canvas",
                HideFlags.DontSave,
                typeof(Canvas)
            );
            EditorSceneManager.MoveGameObjectToScene(canvasObj, previewScene);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.transform.localScale = Vector3.one;
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;

            // prefab

            // make sure it is instantiated in an inactive state
            var targetGO = target as GameObject;
            bool prefabActiveState = targetGO.activeSelf;
            targetGO.SetActive(false);
            var obj = GameObject.Instantiate(targetGO);

            // restore prefabs active state
            targetGO.SetActive(prefabActiveState);

            // activate
            obj.SetActive(true);
            EditorSceneManager.MoveGameObjectToScene(obj, previewScene);
            obj.hideFlags = HideFlags.DontSave;
            obj.transform.SetParent(canvasObj.transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localScale = Vector3.one;
            // obj.transform.localRotation = Quaternion.identity;

            // Fix/Update layout elements
            var rectTransforms = canvas.GetComponentsInChildren<RectTransform>();
            for (int i = rectTransforms.Length - 1; i >= 0; i--)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransforms[i]);
            }

            var _objRect = obj.GetComponent<RectTransform>();

            if (_objRect == null)
                return null;

            if (_objRect.anchorMin == Vector2.zero && _objRect.anchorMax == Vector2.one)
            {
                _objRect.anchorMin = Vector2.one * 0.5f;
                _objRect.anchorMax = Vector2.one * 0.5f;
                _objRect.anchoredPosition = Vector2.zero;
                var _screenSize = Utils.GetRenderingResolution();
                _objRect.sizeDelta = _screenSize;
                // LayoutRebuilder.ForceRebuildLayoutImmediate(_objRect);
            }

            // bounds for camera based on prefab size
            // Bounds bounds = GetBounds(obj);
            Bounds bounds = GetRectTransformBounds(_objRect);

            Vector3 Min = bounds.min;
            Vector3 Max = bounds.max;
            float width = Max.x - Min.x;
            float height = Max.y - Min.y;
            float maxSize = width > height ? width : height;
            camera.transform.position = new Vector3(
                bounds.center.x,
                bounds.center.y,
                camera.transform.position.z
            );
            float aspect = bounds.size.x / bounds.size.y;
            if (bounds.size.x > bounds.size.y)
                camera.orthographicSize = maxSize / (2 * aspect);
            else
                camera.orthographicSize = maxSize / 2;

            // Calc render texture size
            int texWidth = 512; //settings.PreviewTextureResolution;
            int texHeight = 512; //settings.PreviewTextureResolution;
            if (bounds.size.x > 0 && bounds.size.y > 0)
            {
                if (bounds.size.x > bounds.size.y)
                    texHeight = Mathf.RoundToInt(texHeight * bounds.size.y / bounds.size.x);
                else if (bounds.size.x < bounds.size.y)
                    texWidth = Mathf.RoundToInt(texWidth * bounds.size.x / bounds.size.y);
            }

            // create render texture
            if (
                renderTexture == null
                || renderTexture.width != texWidth
                || renderTexture.height != texHeight
            )
                renderTexture = RenderTexturePool.Get(
                    texWidth,
                    texHeight,
                    depth: 0,
                    RenderTextureFormat.Default
                );

            camera.targetTexture = renderTexture;
            var currentRT = RenderTexture.active;
            RenderTexture.active = camera.targetTexture;

            // render cam into renderTexture ..
            // camera.Render(); // Disabled, see comment below.

            // There is a problem with URP + 2D lighting where "SceneView.currentDrawingSceneView" is
            // always NULL and causes a NullPointer.
            // If the render pipeline is URP we set the value of "SceneView.currentDrawingSceneView"
            // via reflection to circumvent this problem.
            var renderPipeline = GraphicsSettings.defaultRenderPipeline;
            var currentDrawingSceneView = SceneView.currentDrawingSceneView;
            if (
                currentDrawingSceneView == null
                && renderPipeline != null
                && renderPipeline.GetType().ToString().Contains("Universal")
            )
            {
                if (SceneView.lastActiveSceneView != null)
                {
                    setCurrentDrawingSceneWithReflections(SceneView.lastActiveSceneView);
                    camera.Render();
                    setCurrentDrawingSceneWithReflections(currentDrawingSceneView);
                }
                else
                {
                    // Maybe warn the user that a preview is not possible.
                }
            }
            else
            {
                camera.Render();
            }

            // .. and copy renderTexture into a new Texture2D
            var texture = new Texture2D(renderTexture.width, renderTexture.height);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0); // reads from RenderTexture.active
            texture.Apply();

            // restore render texture
            RenderTexture.active = currentRT;
            camera.targetTexture = null;

            EditorSceneManager.ClosePreviewScene(previewScene);

            return texture;
        }

        static bool reflectionCacheBuilt = false;
        static System.Reflection.FieldInfo currentDrawingSceneViewField;

        public static Bounds GetRectTransformBounds(RectTransform rectTransform)
        {
            // 获取 RectTransform 在世界空间中的四个角
            Vector3[] worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);

            // 通过四个角的坐标计算边界
            Vector3 min = worldCorners[0];
            Vector3 max = worldCorners[2];

            // 计算最小和最大值
            for (int i = 1; i < 4; i++)
            {
                min = Vector3.Min(min, worldCorners[i]);
                max = Vector3.Max(max, worldCorners[i]);
            }

            // 返回计算出的边界
            return new Bounds((min + max) / 2, max - min);
        }

        static bool setCurrentDrawingSceneWithReflections(SceneView sceneView)
        {
            buildReflectionCache();

            if (currentDrawingSceneViewField == null)
                return false;

            currentDrawingSceneViewField.SetValue(null, sceneView);
            return true;
        }

        static void buildReflectionCache()
        {
            if (!reflectionCacheBuilt)
            {
                reflectionCacheBuilt = true;

                var type = typeof(SceneView);
                currentDrawingSceneViewField = type.GetField(
                    "s_CurrentDrawingSceneView",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
                );
            }
        }

        public static Bounds ClampBounds(Bounds bounds, int segmentSize)
        {
            var size = bounds.size;
            size.x = Mathf.CeilToInt(size.x / segmentSize) * segmentSize;
            size.y = Mathf.CeilToInt(size.y / segmentSize) * segmentSize;
            bounds.size = size;
            return bounds;
        }

        public static Bounds GetBounds(GameObject obj)
        {
            Vector3 min = new Vector3(90900f, 90900f, 90900f);
            Vector3 max = new Vector3(-90900f, -90900f, -90900f);

            var transforms = obj.GetComponentsInChildren<RectTransform>();
            var corner = new Vector3[4];
            RectMask2D lastRectMask = null;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (!transforms[i].gameObject.activeInHierarchy)
                    continue;

                var rectMask = transforms[i].gameObject.GetComponent<RectMask2D>();
                if (rectMask != null && rectMask.enabled)
                    lastRectMask = rectMask;

                // Ignore elements without visible graphics
                var graphic = transforms[i].gameObject.GetComponent<Graphic>();
                if (graphic == null && rectMask == null)
                    continue;

                // Ignore masked elements
                if (lastRectMask != null && transforms[i].IsChildOf(lastRectMask.transform))
                    continue;

                transforms[i].GetWorldCorners(corner);

                if (corner[0].x < min.x)
                    min.x = corner[0].x;
                if (corner[0].y < min.y)
                    min.y = corner[0].y;
                if (corner[0].z < min.z)
                    min.z = corner[0].z;

                if (corner[2].x > max.x)
                    max.x = corner[2].x;
                if (corner[2].y > max.y)
                    max.y = corner[2].y;
                if (corner[2].z > max.z)
                    max.z = corner[2].z;
            }

            Vector3 center = (min + max) / 2f;
            Vector3 size = new Vector3(max.x - min.x, max.y - min.y, max.z - min.z);
            return new Bounds(center, size);
        }
    }

    public static class RenderTexturePool
    {
        static int MaxCapacity = 2;
        static Dictionary<int, RenderTexture> renderTextures = new Dictionary<int, RenderTexture>();

        public static RenderTexture Get(
            int width,
            int height,
            int depth,
            RenderTextureFormat renderTextureFormat
        )
        {
            int key = getKey(width, height);

            // Clean up destroyed textures
            if (renderTextures.ContainsKey(key))
            {
                if (renderTextures[key] == null)
                {
                    renderTextures.Remove(key);
                }
            }

            // Find or create texture
            if (renderTextures.ContainsKey(key))
            {
                return renderTextures[key];
            }
            else
            {
                // Remove if above capacity
                if (renderTextures.Count > MaxCapacity)
                {
                    var e = renderTextures.Keys.GetEnumerator();
                    e.MoveNext();
                    renderTextures.Remove(e.Current);
                }

                // add new
                renderTextures.Add(
                    key,
                    new RenderTexture(width, height, depth, renderTextureFormat)
                );
                return renderTextures[key];
            }
        }

        static int getKey(int width, int height)
        {
            return width * 10000 + height;
        }
    }
}
