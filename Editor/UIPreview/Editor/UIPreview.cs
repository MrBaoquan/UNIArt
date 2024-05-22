#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Kamgam.UIPreview
{
    [CustomPreview(typeof(GameObject))]
    public class UIPreview : ObjectPreview
    {
        private GUIContent m_Title;

        static RenderTexture renderTexture;

        static Texture2D ErrorTexture = new Texture2D(2, 2);

        /// <summary>
        /// Cache with one texture per prefab.
        /// </summary>
        static Dictionary<Object, Texture2D> previews = new Dictionary<Object, Texture2D>();

        public override Object target
        {
            get
            {
                // There is a weird behaviour if the preview window is NOT docked (floating window).
                // If it is floating then the returned "target/targets" are not updated properly, thus
                // we return the currently selected object instead.
                //
                // TODO #1: We should only do that if the window is NOT docked but there seems to be no API
                // for that. Sadly ObjectPreview does not derive from EditorWindow.
                //
                // TODO #2: This does not fix the preview for multiple objects. Additional caveat: There seems
                // to be no view dropdown if the window is floating. Maybe Unity devs never intended the
                // preview window to be a floating window? -> This requires further investigation.

                if (m_Targets == null || (m_Targets.Length == 1 && base.target != Selection.activeObject))
                {
                    return Selection.activeObject;
                }
                else
                {
                    return base.target;
                }
            }
        }

#if UNITY_2021_1_OR_NEWER
        public void OnDisable()
        {
            Cleanup();
        }

        public override void Cleanup()
        {
            // Unity:
            // Make sure that base.Cleanup is called if overriding the Cleanup method.
            // If you are implementing this in an Editor or EditorWindow, don't forget
            // to call ObjectPreview.Cleanup in OnDisable.
            base.Cleanup();
        }
#endif

        public bool IsUIPrefabAsset()
        {
            if (target == null)
                return false;

            if (PrefabUtility.GetPrefabAssetType(target) == PrefabAssetType.NotAPrefab)
                return false;

            if (!AssetDatabase.Contains(target))
                return false;

            var targetGameObject = target as GameObject;
            if (targetGameObject == null)
                return false;

            var rectTransform = targetGameObject.GetComponent<RectTransform>();
            if (rectTransform == null)
                return false;

            return true;
        }

        public override GUIContent GetPreviewTitle()
        {
            if (!shouldBeShown())
            {
                return base.GetPreviewTitle();
            }

            if (m_Title == null)
            {
                m_Title = EditorGUIUtility.TrTextContent("UI Preview");
            }
            return m_Title;
        }

        protected Dictionary<int, bool> _shouldBeShownLookupTabe = new Dictionary<int, bool>();

        protected bool shouldBeShown()
        {
            if (target == null)
                return false;

            int id = target.GetInstanceID();
            if (_shouldBeShownLookupTabe.ContainsKey(id))
            {
                return _shouldBeShownLookupTabe[id];
            }

            // Abort if in play mode
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var settings = UIPreviewSettings.GetOrCreateSettings();
                if (!settings.ExecuteInPlayMode)
                {
                    _shouldBeShownLookupTabe[id] = false;
                    return _shouldBeShownLookupTabe[id];
                }
            }

            if (!IsUIPrefabAsset())
            {
                _shouldBeShownLookupTabe[id] = false;
                return _shouldBeShownLookupTabe[id];
            }

            _shouldBeShownLookupTabe[id] = true;
            return _shouldBeShownLookupTabe[id];
        }

        public override bool HasPreviewGUI()
        {
            if (shouldBeShown() && IsUIPrefabAsset())
            {
                PreviewsListHelper<UIPreview>.ScheduleReorderPreviews();
                return true;
            }

            return base.HasPreviewGUI();
        }

        public override void Initialize(Object[] targets)
        {
            if (!shouldBeShown())
            {
                base.Initialize(targets);
                return;
            }

            if (targets == null || targets.Length == 0)
                return;

            // create preview textures
            foreach (var p in previews)
            {
                Texture2D.DestroyImmediate(p.Value);
            }
            previews.Clear();
            for (int i = 0; i < targets.Length; i++)
            {
                createPreviewTextureForTarget(targets[i]);
            }

            base.Initialize(targets);

            PreviewsListHelper<UIPreview>.ScheduleReorderPreviews();
        }

        public override void OnPreviewSettings()
        {
            base.OnPreviewSettings();

            if (!shouldBeShown())
            {
                return;
            }

            if (GUILayout.Button("Update"))
            {
                createPreviewTextureForTarget(target);
            }
        }

        protected void createPreviewTextureForTarget(Object target)
        {
            var settings = UIPreviewSettings.GetOrCreateSettings();
            bool logEnabled = Debug.unityLogger.logEnabled;
            try
            {
                if (settings.IgnoreErrors)
                    Debug.unityLogger.logEnabled = false;

                // create preview texture for target
                if (previews.ContainsKey(target))
                    previews[target] = CreateAssetPreview(target);
                else
                    previews.Add(target, CreateAssetPreview(target));
            }
            catch (System.Exception e)
            {
                if (!settings.IgnoreErrors)
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }
            }
            finally
            {
                Debug.unityLogger.logEnabled = logEnabled;
            }
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (target == null)
                return;

            if (!shouldBeShown())
            {
                return;
            }

            var targetGameObject = target as GameObject;
            if (targetGameObject == null)
                return;

            // draw background
            base.OnPreviewGUI(r, background);

            drawPreview(r);
        }

        // Disabled because it somehow renders the text twice.
        /*
        public override string GetInfoString()
        {
            return target.name;
        }
        */

        protected void drawPreview(Rect r)
        {
            if (previews.ContainsKey(target) && previews[target] != null)
            {
                GUI.DrawTexture(r, previews[target], ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(r, target.name + " preview not available.");
            }
        }

        static bool reflectionCacheBuilt = false;
        static System.Reflection.FieldInfo currentDrawingSceneViewField;

        static void buildReflectionCache()
        {
            if (!reflectionCacheBuilt)
            {
                reflectionCacheBuilt = true;

                var type = typeof(SceneView);
                currentDrawingSceneViewField = type.GetField("s_CurrentDrawingSceneView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
        }

        static bool setCurrentDrawingSceneWithReflections(SceneView sceneView)
        {
            buildReflectionCache();

            if (currentDrawingSceneViewField == null)
                return false;

            currentDrawingSceneViewField.SetValue(null, sceneView);
            return true;
        }

        public static Texture2D CreateAssetPreview(Object target)
        {
            // Stop if Prefab contains particle systems.
            var checkForParticlesGO = target as GameObject;
            var checkParticleSystems = checkForParticlesGO.GetComponentsInChildren<ParticleSystem>();
            if (checkParticleSystems != null && checkParticleSystems.Length > 0)
            {
                Debug.LogWarning("UIPreview: Preview of Prefabs with ParticleSystems is not supported due to a Unity Bug (Isse#:1399450 and similar).");
                return ErrorTexture;
            }

            var previewTex = AssetPreview.GetAssetPreview(target);
            if (previewTex != null)
                return previewTex;

            var previewScene = EditorSceneManager.NewPreviewScene();

            // cam
            GameObject cameraObj = EditorUtility.CreateGameObjectWithHideFlags("camera", HideFlags.DontSave);
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
            GameObject canvasObj = EditorUtility.CreateGameObjectWithHideFlags("canvas", HideFlags.DontSave, typeof(Canvas));
            EditorSceneManager.MoveGameObjectToScene(canvasObj, previewScene);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.transform.localScale = Vector3.one;
            canvas.transform.localPosition = Vector3.zero;
            canvas.transform.localRotation = Quaternion.identity;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;

            // canvas scaler
            var settings = UIPreviewSettings.GetOrCreateSettings();
            if (settings.UseCanvasScaler)
            {
                CanvasScaler canvasScaler = null;
                canvasScaler = canvasObj.AddComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = settings.UIScaleMode;
                canvasScaler.scaleFactor = settings.ScaleFactor;
                canvasScaler.matchWidthOrHeight = settings.MatchWidthOrHeight;
                canvasScaler.screenMatchMode = settings.ScreenMatchMode;
                canvasScaler.referencePixelsPerUnit = settings.ReferencePixelsPerUnit;
                canvasScaler.referenceResolution = settings.ReferenceResolution;
                canvasScaler.physicalUnit = settings.PhysicalUnit;
                canvasScaler.fallbackScreenDPI = settings.FallbackScreenDPI;
                canvasScaler.defaultSpriteDPI = settings.DefaultSpriteDPI;
            }

            // prefab

            // make sure it is instantiated in an inactive state
            var targetGO = target as GameObject;
            bool prefabActiveState = targetGO.activeSelf;
            targetGO.SetActive(false);
            var obj = GameObject.Instantiate(targetGO);

            // remove any CUSTOM scripts BEFORE activating the object to avoid side effects
            if (settings.DisableCustomScriptsInPreview)
            {
                try
                {
                    var scripts = obj.GetComponentsInChildren<MonoBehaviour>();
                    foreach (var script in scripts)
                    {
                        var nsp = script.GetType().Namespace;
                        var scriptName = script.GetType().Name;

                        // Allow TextMeshPro
                        if (nsp != null && nsp.StartsWith("TMPro"))
                            continue;

                        // Allow excluded namespaces
                        if (nsp != null && settings.DisableCustomScriptsExclusions.Contains(nsp))
                            continue;

                        // Allow excluded scripts
                        if (scriptName != null && settings.DisableCustomScriptsExclusions.Contains(scriptName))
                            continue;

                        // If not in UnityEngine or UnityEditor namespace, then assume custom script and remove
                        if (nsp == null || !nsp.StartsWith("UnityE"))
                        {
                            // Destroy script (this check automatically excludes circular references)
                            if (!IsRequiredByOtherComponent(script))
                            {
                                DestroyComponentTree(script);
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    if ((int)settings.LogLevel <= (int)LogLevel.Warning)
                        Debug.LogWarning("Could not remove scripts from preview: " + e.Message);
                }
            }

            // restore prefabs active state
            targetGO.SetActive(prefabActiveState);

            // activate
            obj.SetActive(true);
            EditorSceneManager.MoveGameObjectToScene(obj, previewScene);
            obj.hideFlags = HideFlags.DontSave;
            obj.transform.SetParent(canvasObj.transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localScale = Vector3.one;
            obj.transform.localRotation = Quaternion.identity;

            // Fix/Update layout elements
            var rectTransforms = canvas.GetComponentsInChildren<RectTransform>();
            for (int i = rectTransforms.Length - 1; i >= 0; i--)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransforms[i]);
            }

            // bounds for camera based on prefab size
            Bounds bounds = GetBounds(obj);
            // We do clamp the bounds to steps of constant sizes so we get similarly sized render textures.
            // This is done to avoid creating a new render texture for every preview. Previews of similar aspect
            // ratios should reuse the render textures.
            if (canvas.renderMode == RenderMode.WorldSpace && (bounds.size.x < 50 || bounds.size.y < 50))
            {
                bounds = ClampBounds(bounds, segmentSize: 1);
            }
            else
            {
                bounds = ClampBounds(bounds, segmentSize: 50);
            }
            Vector3 Min = bounds.min;
            Vector3 Max = bounds.max;
            float width = Max.x - Min.x;
            float height = Max.y - Min.y;
            float maxSize = width > height ? width : height;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, camera.transform.position.z);
            float aspect = bounds.size.x / bounds.size.y;
            if (bounds.size.x > bounds.size.y)
                camera.orthographicSize = maxSize / (2 * aspect);
            else
                camera.orthographicSize = maxSize / 2;



            // Calc render texture size
            int texWidth = settings.PreviewTextureResolution;
            int texHeight = settings.PreviewTextureResolution;
            if (bounds.size.x > 0 && bounds.size.y > 0)
            {
                if (bounds.size.x > bounds.size.y)
                    texHeight = Mathf.RoundToInt(texHeight * bounds.size.y / bounds.size.x);
                else if (bounds.size.x < bounds.size.y)
                    texWidth = Mathf.RoundToInt(texWidth * bounds.size.x / bounds.size.y);
            }

            // create render texture
            if (renderTexture == null || renderTexture.width != texWidth || renderTexture.height != texHeight)
                renderTexture = RenderTexturePool.Get(texWidth, texHeight, depth: 0, RenderTextureFormat.Default);

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
            if (currentDrawingSceneView == null && renderPipeline != null && renderPipeline.GetType().ToString().Contains("Universal"))
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

        public static void DestroyComponentTree(Component component)
        {
            if (component == null)
                return;

            var requiredComponents = GetRequiredComponents(component);

            // Do not destroy any transforms
            if (!(component is RectTransform) && !(component is Transform))
            {
                GameObject.DestroyImmediate(component);
            }

            // Destroy script dependencies
            if (requiredComponents.Count > 0)
            {
                foreach (var comp in requiredComponents)
                {
                    // Destroy (but only if it is not a circular reference).
                    DestroyComponentTree(comp);
                }
            }
        }

        /// <summary>
        /// Finds out whether or not the component is required by any other component on this game object.
        /// Based on: https://answers.unity.com/questions/1237666/check-if-component-is-requred-by-another.html
        /// </summary>
        /// <returns></returns>
        public static bool IsRequiredByOtherComponent(Component component, Component otherComponent = null)
        {
            if (component == null)
                return false;

            var componentType = component.GetType();
            var go = component.gameObject;
            var comps = go.GetComponents<Component>();
            foreach (var comp in comps)
            {
                // Iterate all component's attributes, look for
                // the RequireComponent attributes
                foreach (var attr in comp.GetType().GetCustomAttributes(true))
                {
                    if (attr is RequireComponent)
                    {
                        var rcAttr = (RequireComponent)attr;

                        // Check all three of the required types to see if
                        // other componentType is required (for some reason, you
                        // can require up to 3 component types per attribute).
                        if ((rcAttr.m_Type0?.IsAssignableFrom(componentType) ?? false) ||
                            (rcAttr.m_Type1?.IsAssignableFrom(componentType) ?? false) ||
                            (rcAttr.m_Type2?.IsAssignableFrom(componentType) ?? false))
                        {
                            if (otherComponent == null || comp == otherComponent)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a list of components which the given component depends on.
        /// Based on: https://answers.unity.com/questions/1237666/check-if-component-is-requred-by-another.html
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        public static List<Component> GetRequiredComponents(Component component)
        {
            var requiredComponents = new List<Component>();

            if (component == null)
                return requiredComponents;

            var go = component.gameObject;

            // Iterate all component's attributes, look for
            // the RequireComponent attributes
            foreach (var attr in component.GetType().GetCustomAttributes(true))
            {
                if (attr is RequireComponent)
                {
                    var rcAttr = (RequireComponent)attr;

                    // For some reason, you can require up to 3 component types per attribute.
                    if (rcAttr.m_Type0 != null)
                    {
                        var requiredComp0 = go.GetComponent(rcAttr.m_Type0);
                        if (requiredComp0 != null)
                            requiredComponents.Add(requiredComp0);
                    }
                    if (rcAttr.m_Type1 != null)
                    {
                        var requiredComp1 = go.GetComponent(rcAttr.m_Type1);
                        if (requiredComp1 != null)
                            requiredComponents.Add(requiredComp1);
                    }
                    if (rcAttr.m_Type2 != null)
                    {
                        var requiredComp2 = go.GetComponent(rcAttr.m_Type2);
                        if (requiredComp2 != null)
                            requiredComponents.Add(requiredComp2);
                    }
                }
            }

            return requiredComponents;
        }

        public static Bounds ClampBounds(Bounds bounds, int segmentSize)
        {
            var size = bounds.size;
            size.x = Mathf.CeilToInt(size.x / segmentSize) * segmentSize;
            size.y = Mathf.CeilToInt(size.y / segmentSize) * segmentSize;
            bounds.size = size;
            return bounds;
        }

        public static int Clamp(int value, int segmentSize)
        {
            return Mathf.CeilToInt(value / ((float)segmentSize)) * segmentSize;
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

                if (corner[0].x < min.x) min.x = corner[0].x;
                if (corner[0].y < min.y) min.y = corner[0].y;
                if (corner[0].z < min.z) min.z = corner[0].z;

                if (corner[2].x > max.x) max.x = corner[2].x;
                if (corner[2].y > max.y) max.y = corner[2].y;
                if (corner[2].z > max.z) max.z = corner[2].z;
            }

            Vector3 center = (min + max) / 2f;
            Vector3 size = new Vector3(max.x - min.x, max.y - min.y, max.z - min.z);
            return new Bounds(center, size);
        }

    }
}
#endif
