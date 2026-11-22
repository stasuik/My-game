using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoadBuilder.Core.UI
{
    /// <summary>
    /// Всплывающая подсказка в VR при наведении лазера или фокусе стиком.
    /// </summary>
    public class VRTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Контент подсказки")]
        [TextArea] public string tooltipText;
        public float hoverDelay = 0.35f;

        private GameObject tooltipObj;
        private Text label;
        private CanvasGroup cg;
        private bool isHovered = false;
        private float hoverTimer = 0f;

        public static VRTooltipTrigger Attach(GameObject target, string text)
        {
            if (target == null) return null;
            var trigger = target.GetComponent<VRTooltipTrigger>();
            if (trigger == null) trigger = target.AddComponent<VRTooltipTrigger>();
            trigger.tooltipText = text;
            return trigger;
        }

        private void Update()
        {
            if (isHovered)
            {
                hoverTimer += Time.unscaledDeltaTime;
                if (hoverTimer >= hoverDelay && (tooltipObj == null || !tooltipObj.activeSelf))
                {
                    ShowTooltip();
                }
            }
            else
            {
                hoverTimer = 0f;
                if (tooltipObj != null && tooltipObj.activeSelf)
                {
                    tooltipObj.SetActive(false);
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            isHovered = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            isHovered = false;
        }

        private void ShowTooltip()
        {
            if (string.IsNullOrEmpty(tooltipText)) return;

            if (tooltipObj == null)
            {
                tooltipObj = new GameObject("VRTooltipPopup", typeof(RectTransform));
                tooltipObj.transform.SetParent(transform, false);

                RectTransform rt = tooltipObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(240, 40);
                rt.anchoredPosition = new Vector2(0, 48); // над кнопкой

                Image bg = tooltipObj.AddComponent<Image>();
                bg.color = new Color(0.12f, 0.13f, 0.16f, 0.92f);

                GameObject tgo = new GameObject("Text", typeof(RectTransform));
                tgo.transform.SetParent(tooltipObj.transform, false);
                label = tgo.AddComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.fontSize = 13;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;

                RectTransform trt = tgo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(8, 4);
                trt.offsetMax = new Vector2(-8, -4);
            }

            if (label != null) label.text = tooltipText;
            tooltipObj.SetActive(true);
        }

        private void OnDisable()
        {
            isHovered = false;
            if (tooltipObj != null) tooltipObj.SetActive(false);
        }
    }
}