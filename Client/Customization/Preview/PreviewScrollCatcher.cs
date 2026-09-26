using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ImprovedCustomizationUI.Customization.Preview
{
    public class PreviewScrollCatcher : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<PointerEventData> Scrolled;
        public bool Hovered;

        public void OnScroll(PointerEventData eventData)
        {
            if (Scrolled != null)
            {
                Scrolled(eventData);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
        }

        private void OnDisable()
        {
            Hovered = false;
        }
    }
}
