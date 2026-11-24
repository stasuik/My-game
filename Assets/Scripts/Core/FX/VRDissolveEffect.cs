using System;
using System.Collections;
using UnityEngine;

namespace RoadBuilder.Core.FX
{
    /// <summary>
    /// Управляет визуальным эффектом растворения (Dissolve) и материализации при операциях Undo / Redo.
    /// В VR исключает резкие рывки появления/исчезновения объектов перед глазами игрока.
    /// </summary>
    public class VRDissolveEffect : MonoBehaviour
    {
        private static readonly int DissolveAmountProp = Shader.PropertyToID("_DissolveAmount");
        private static readonly int EdgeColorProp = Shader.PropertyToID("_EdgeColor");

        [Header("Параметры анимации (VR)")]
        [Tooltip("Длительность растворения/восстановления (в секундах, рекомендуемо 0.25с)")]
        [SerializeField] private float _duration = 0.25f;

        [Tooltip("Кривая анимации растворения")]
        [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Свечение края")]
        [SerializeField] private Color _undoEdgeColor = new Color(1.0f, 0.35f, 0.1f, 1f); // Тёплый янтарный при отмене
        [SerializeField] private Color _redoEdgeColor = new Color(0.15f, 0.75f, 1.0f, 1f); // Голубой голографический при повторе

        private Renderer[] _renderers;
        private MaterialPropertyBlock _propBlock;
        private Coroutine _activeRoutine;

        public bool IsAnimating => _activeRoutine != null;
        public float Duration => _duration;

        private void Awake()
        {
            CacheRenderers();
        }

        public void CacheRenderers()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }
            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }
        }

        /// <summary>
        /// Запустить плавное исчезновение объекта (Undo)
        /// </summary>
        public void PlayDissolveOut(Action onComplete = null)
        {
            CacheRenderers();
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
            }
            _activeRoutine = StartCoroutine(AnimateDissolve(0f, 1f, _undoEdgeColor, () =>
            {
                _activeRoutine = null;
                gameObject.SetActive(false);
                SetDissolveAmount(0f, _undoEdgeColor); // Сброс состояния для последующего появления
                onComplete?.Invoke();
            }));
        }

        /// <summary>
        /// Запустить плавное материализованное появление объекта (Redo)
        /// </summary>
        public void PlayDissolveIn(Action onComplete = null)
        {
            CacheRenderers();
            gameObject.SetActive(true);
            if (_activeRoutine != null)
            {
                StopCoroutine(_activeRoutine);
            }
            SetDissolveAmount(1f, _redoEdgeColor);
            _activeRoutine = StartCoroutine(AnimateDissolve(1f, 0f, _redoEdgeColor, () =>
            {
                _activeRoutine = null;
                SetDissolveAmount(0f, _redoEdgeColor);
                onComplete?.Invoke();
            }));
        }

        /// <summary>
        /// Мгновенная установка значения Dissolve без анимации (для тестов и сброса)
        /// </summary>
        public void SetDissolveAmount(float amount, Color edgeColor)
        {
            CacheRenderers();
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(DissolveAmountProp, Mathf.Clamp01(amount));
                _propBlock.SetColor(EdgeColorProp, edgeColor);
                r.SetPropertyBlock(_propBlock);
            }
        }

        private IEnumerator AnimateDissolve(float from, float to, Color edgeColor, Action onEnd)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, _duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveValue = _fadeCurve.Evaluate(t);
                float currentAmount = Mathf.Lerp(from, to, curveValue);

                SetDissolveAmount(currentAmount, edgeColor);
                yield return null;
            }

            SetDissolveAmount(to, edgeColor);
            onEnd?.Invoke();
        }
    }
}
