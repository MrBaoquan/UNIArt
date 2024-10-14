using UnityEngine;
using UnityEngine.UI;

namespace UNIArt.Runtime
{
    [ExecuteAlways, RequireComponent(typeof(Toggle)), RequireComponent(typeof(Image))]
    public class ToggleImage : MonoBehaviour
    {
        private Toggle toggle => GetComponent<Toggle>();

        private Image targetImage => GetComponent<Image>();

        private void OnEnable()
        {
            toggle.onValueChanged.AddListener(OnToggleValueChanged);
            UpdateImage(toggle?.isOn ?? false);
        }

        private void OnDisable()
        {
            toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        private void OnValidate()
        {
            UpdateImage(toggle.isOn);
        }

        private void OnToggleValueChanged(bool isOn)
        {
            UpdateImage(isOn);
        }

        // 更新图片的逻辑
        private void UpdateImage(bool isOn)
        {
            SpriteState spriteState = toggle.spriteState;
            targetImage.sprite = isOn ? spriteState.pressedSprite : spriteState.highlightedSprite;
        }
    }
}
