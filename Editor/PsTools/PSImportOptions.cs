using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UNIArt.Editor
{
    public class PSImportOptions : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        // [MenuItem("Tools/PS Import Options")]
        // public static void ShowPSOptions()
        // {
        //     Resources.FindObjectsOfTypeAll<PSImportOptions>().ToList().ForEach(_ => _.Close());
        // }

        public static void ShowPSOptions(string psdFilePath)
        {
            // 获取所有实例
            var windows = Resources.FindObjectsOfTypeAll<PSImportOptions>().ToList();
            var wnd = windows.FirstOrDefault(w => w.OriginPSDPath == psdFilePath);
            if (wnd == null)
            {
                wnd = CreateInstance<PSImportOptions>();
            }

            wnd.importArgs = UNIArtSettings.GetPSDImportArgs(psdFilePath);
            wnd.titleContent = new GUIContent("PS 文件导入选项");
            wnd.maxSize = new Vector2(400, 300);
            wnd.minSize = new Vector2(400, 300);
            wnd.ShowUtility();
        }

        UNIArtSettings.PSDImportArgs importArgs;

        public string OriginPSDPath => importArgs.psdPath;

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);
            if (importArgs == null)
            {
                this.Close();
                return;
            }

            var _originPSFile = root.Q<ObjectField>("originPSFile");

            _originPSFile.value = importArgs.OriginPSFile;
            _originPSFile.SetEnabled(false);
            _originPSFile.style.opacity = 1;

            var _inputScale = root.Q<FloatField>("input_scale");
            var _b_onlyVisibleLayer = root.Q<Toggle>("b_onlyVisibleLayer");
            var _b_creatAtlas = root.Q<Toggle>("b_createAtlas");
            var _input_atlasMaxSize = root.Q<IntegerField>("input_atlasMaxSize");
            var _b_addPSComponent = root.Q<Toggle>("b_addPSComponent");
            var _b_restoreEntity = root.Q<Toggle>("b_restoreEntity");
            var _object_PSDInstane = root.Q<ObjectField>("object_psdInstance");

            var _btn_reimport = root.Q<Button>("btn_reimport");
            _btn_reimport.RegisterCallback<MouseUpEvent>(evt =>
            {
                Utils.ReimportAsset(importArgs.psdPath);
            });

            _inputScale.value = importArgs.Scale;
            _b_onlyVisibleLayer.value = importArgs.ImportOnlyVisibleLayers;
            _b_creatAtlas.value = importArgs.CreateAtlas;
            _input_atlasMaxSize.value = importArgs.MaxAtlasSize;
            _b_addPSComponent.value = importArgs.AddPSLayer;

            _b_restoreEntity.value = importArgs.RestoreEntity;
            _object_PSDInstane.value = UNIArtSettings.GetPSDEntityInstance(
                importArgs.PSDEntityPath
            );

            _input_atlasMaxSize.style.display = _b_creatAtlas.value
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            _object_PSDInstane.style.display = _b_restoreEntity.value
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            _inputScale.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                _inputScale.value = Mathf.Max(0.1f, _inputScale.value);
                importArgs.Scale = _inputScale.value;
            });

            _b_onlyVisibleLayer.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                importArgs.ImportOnlyVisibleLayers = _b_onlyVisibleLayer.value;
            });

            _b_creatAtlas.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                importArgs.CreateAtlas = _b_creatAtlas.value;
                _input_atlasMaxSize.style.display = _b_creatAtlas.value
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            });

            _input_atlasMaxSize.RegisterCallback<ChangeEvent<int>>(evt =>
            {
                importArgs.MaxAtlasSize = _input_atlasMaxSize.value;
            });

            _b_addPSComponent.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                importArgs.AddPSLayer = _b_addPSComponent.value;
            });

            _b_restoreEntity.RegisterCallback<ChangeEvent<bool>>(evt =>
            {
                importArgs.RestoreEntity = _b_restoreEntity.value;
                _object_PSDInstane.style.display = _b_restoreEntity.value
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            });

            _object_PSDInstane.RegisterCallback<ChangeEvent<Object>>(evt => { });
        }
    }
}
