using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace PluginMaster
{
    public class PsLayerDemo : MonoBehaviour
    {
        public Dropdown _modeDropdown = null;
        public Slider _opacitySlider = null;
        public PsLayerImage _uiLayer = null;
        public PsGroup _spriteGroup = null;

        private void Awake()
        {
            Assert.IsNotNull(_modeDropdown);
            Assert.IsNotNull(_opacitySlider);
            Assert.IsNotNull(_uiLayer);
            Assert.IsNotNull(_spriteGroup);

            _modeDropdown.ClearOptions();
            var options = new List<Dropdown.OptionData>();
            foreach (var name in PsdBlendModeType.GrabPassBlendModeNames)
            {
                options.Add(new Dropdown.OptionData(name));
            }
            _modeDropdown.AddOptions(options);
        }

        public void OnModeChange()
        {
            _spriteGroup.BlendModeType = _uiLayer.BlendModeType = (PsdBlendModeType.BlendModeType)_modeDropdown.value;
        }

        public void OnOppacityChange()
        {
            _spriteGroup.Opacity = _uiLayer.Opacity = _opacitySlider.value;
        }

    }
}
