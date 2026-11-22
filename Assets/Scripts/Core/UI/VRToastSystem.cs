using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RoadBuilder.Core.UI
{
    public enum ToastType
    {
        Info,
        Success,
        Warning
    }

    /// <summary>
    /// Toast-система для VR. Показывает неблокирующие текстовые всплывающие уведомления
    /// с защитой от спама и автоматическим скрытием.
    /// </summary>
    public class VRToastSystem : MonoBehaviour
    {
        private static VRToastSystem _instance;
        public static VRToastSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<VRToastSystem>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[VRToastSystem]", typeof(RectTransform));
                        _instance = go.AddComponent<VRToastSystem>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Настройки отображения")]
        [SerializeField] private float defaultDuration = 2.0f;
        [SerializeField] private float fadeDuration = 0.2f;

        public bool IsActive { get; private set; } = false;
        public string CurrentMessage { get; private set; } = string.Empty;
        public ToastType CurrentType { get; private set; } = ToastType.Info;
        public int NotificationCount { get; private set; } = 0;

        public Text toastText;
        public Image toastBg;
        public CanvasGroup canvasGroup;
        public RectTransform containerRect;

        private Coroutine activeRoutine;
        private float remainingDuration = 0f;

        private static readonly Color CreamBg = new Color(1f, 0.97f, 0.92f, 0.95f);
        private static readonly Color InkText = new Color(0.23f, 0.17f, 0.10f, 1f);
        private static readonly Color OrangeAccent = new Color(1f, 0.62f, 0.11f, 1f);
        private static readonly Color WarningAccent = new Color(0.92f, 0.35f, 0.20f, 1f);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            EnsureUIStructure();
        }

        public void EnsureUIStructure()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            containerRect = GetComponent<RectTransform>();
            if (containerRect == null) containerRect = gameObject.AddComponent<RectTransform>();

            if (toastBg == null)
            {
                toastBg = GetComponent<Image>();
                if (toastBg == null) toastBg = gameObject.AddComponent<Image>();
                toastBg.color = CreamBg;
            }

            if (toastText == null)
            {
                GameObject textGo = new GameObject("ToastText", typeof(RectTransform));
                textGo.transform.SetParent(transform, false);
                toastText = textGo.AddComponent<Text>();
                toastText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                toastText.fontSize = 18;
                toastText.alignment = TextAnchor.MiddleCenter;
                toastText.color = InkText;

                RectTransform trt = textGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(16, 8);
                trt.offsetMax = new Vector2(-16, -8);
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void ShowUndoSuccess(string actionName = "")
        {
            string msg = string.IsNullOrEmpty(actionName) ? "Отмена выполнена" : $"Отмена: {actionName}";
            Show(msg, defaultDuration, ToastType.Success);
        }

        public void ShowRedoSuccess(string actionName = "")
        {
            string msg = string.IsNullOrEmpty(actionName) ? "Повтор выполнен" : $"Повтор: {actionName}";
            Show(msg, defaultDuration, ToastType.Success);
        }

        public void ShowUndoEmpty()
        {
            Show("Нечего отменять", defaultDuration, ToastType.Warning);
        }

        public void ShowRedoEmpty()
        {
            Show("Нечего повторять", defaultDuration, ToastType.Warning);
        }

        /// <summary>
        /// Показ тоста с защитой от спама: если тост уже показан, текст обновляется
        /// и таймер сбрасывается без создания накладывающихся плашек.
        /// </summary>
        public void Show(string message, float duration = 2.0f, ToastType type = ToastType.Info)
        {
            if (string.IsNullOrEmpty(message)) return;

            CurrentMessage = message;
            CurrentType = type;
            NotificationCount++;
            remainingDuration = duration > 0f ? duration : defaultDuration;

            if (toastText != null) toastText.text = message;
            if (toastBg != null)
            {
                toastBg.color = type == ToastType.Warning ? new Color(1f, 0.94f, 0.92f, 0.98f) : CreamBg;
            }

            IsActive = true;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            if (gameObject.activeInHierarchy)
            {
                activeRoutine = StartCoroutine(ToastLifecycleRoutine());
            }
        }

        private IEnumerator ToastLifecycleRoutine()
        {
            while (remainingDuration > 0f)
            {
                remainingDuration -= Time.unscaledDeltaTime;
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                }
                yield return null;
            }

            HideImmediately();
        }

        public void HideImmediately()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }
            IsActive = false;
            remainingDuration = 0f;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
    }
}