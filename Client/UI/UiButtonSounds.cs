using Comfort.Common;
using EFT.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImprovedCustomizationUI.UI
{
    public class UiButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private Button _button;

        public static void AddTo(GameObject target)
        {
            if (target.GetComponent<UiButtonSounds>() == null)
            {
                target.AddComponent<UiButtonSounds>();
            }
        }

        private bool Usable()
        {
            if (_button == null)
            {
                _button = GetComponent<Button>();
            }
            return _button == null || _button.IsInteractable();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Usable())
            {
                Play(EUISoundType.ButtonOver);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Usable())
            {
                Play(EUISoundType.ButtonClick);
            }
        }

        private static void Play(EUISoundType sound)
        {
            GUISounds sounds = Singleton<GUISounds>.Instance;
            if (sounds != null)
            {
                sounds.PlayUISound(sound);
            }
        }
    }
}
