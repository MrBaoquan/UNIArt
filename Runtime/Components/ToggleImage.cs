using UnityEngine;
using UnityEngine.UI;

namespace UNIArt.Runtime
{
    [ExecuteAlways, RequireComponent(typeof(Toggle)), RequireComponent(typeof(Image))]
    public class ToggleImage : MonoBehaviour
    {
        [SerializeField, Tooltip("在切换状态时自动匹配图片大小")]
        public bool MatchNativeSize = true;

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

        private void UpdateImage(bool isOn)
        {
            SpriteState spriteState = toggle.spriteState;
            targetImage.sprite = isOn ? spriteState.pressedSprite : spriteState.highlightedSprite;
            if (MatchNativeSize)
                targetImage.SetNativeSize();
        }
    }
}
