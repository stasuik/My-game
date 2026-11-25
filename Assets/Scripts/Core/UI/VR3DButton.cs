using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RoadBuilder.Core.UI
{
    /// <summary>
    /// 3D-кнопка для физического взаимодействия пальцем или лучом контроллера в VR.
    /// Поддерживает глубину вдавливания (travel distance) и тактильные маркеры.
    /// </summary>
    public class VR3DButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Геометрия и ход кнопки")]
        [SerializeField] private float pressTravelDistance = 0.005f; // 5 мм
        [SerializeField] private float springSpeed = 25f;
        [SerializeField] private Transform buttonCap;
        [SerializeField] private Transform tactileMarker;

        [Header("Состояние")]
        public bool interactable = true;
        public bool isPressed { get; private set; } = false;
        public bool isHovered { get; private set; } = false;
        public float currentDepression { get; private set; } = 0f;

        public event Action OnPressed;
        public event Action OnReleased;

        private Vector3 initialCapPosition;

        private void Awake()
        {
            if (buttonCap == null) buttonCap = transform;
            initialCapPosition = buttonCap.localPosition;
        }

        private void Update()
        {
            float targetDepression = (isPressed && interactable) ? pressTravelDistance : 0f;
            currentDepression = Mathf.MoveTowards(currentDepression, targetDepression, Time.unscaledDeltaTime * springSpeed);

            if (buttonCap != null)
            {
                buttonCap.localPosition = initialCapPosition - new Vector3(0, 0, currentDepression);
            }
        }

        public void Press()
        {
            if (!interactable) return;
            isPressed = true;
            OnPressed?.Invoke();
        }

        public void Release()
        {
            if (!isPressed) return;
            isPressed = false;
            OnReleased?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Press();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            if (isPressed) Release();
        }

        /// <summary>
        /// Стендовый расчет смещения колпачка при нажатии
        /// </summary>
        public float CalculateDepressionOffset(bool pressed)
        {
            return pressed && interactable ? pressTravelDistance : 0f;
        }
    }
}