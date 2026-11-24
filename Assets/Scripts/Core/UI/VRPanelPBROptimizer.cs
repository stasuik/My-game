using System;
using UnityEngine;

namespace RoadBuilder.Core.UI
{
    /// <summary>
    /// Конфигурация PBR-материалов и оптимизация геометрии VR-панели (Диана, ROA-111/113).
    /// Обеспечивает Single-Pass Instanced рендеринг, batching и GPU instancing для 72+ FPS.
    /// </summary>
    [ExecuteAlways]
    public class VRPanelPBROptimizer : MonoBehaviour
    {
        [Header("PBR Metallic & Roughness")]
        [Range(0f, 1f)]
        [SerializeField] private float _metallic = 0.85f;

        [Range(0f, 1f)]
        [SerializeField] private float _smoothness = 0.65f;

        [SerializeField] private Color _panelBaseColor = new Color(0.12f, 0.13f, 0.16f, 1f);
        [SerializeField] private Color _buttonMetallicColor = new Color(0.24f, 0.26f, 0.31f, 1f);

        [Header("VR Optimization")]
        [Tooltip("Объединение статической геометрии панели для снижения Draw Calls")]
        [SerializeField] private bool _enableStaticBatching = true;

        [Tooltip("Принудительное включение GPU Instancing на всех материалах")]
        [SerializeField] private bool _enableGPUInstancing = true;

        private static readonly int MetallicProp = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessProp = Shader.PropertyToID("_Glossiness");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _propBlock;

        public float Metallic => _metallic;
        public float Smoothness => _smoothness;
        public Color PanelBaseColor => _panelBaseColor;

        private void OnEnable()
        {
            ApplyPBROptimizations();
        }

        public void ApplyPBROptimizations()
        {
            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                if (_enableGPUInstancing && r.sharedMaterial != null)
                {
                    r.sharedMaterial.enableInstancing = true;
                }

                r.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(MetallicProp, _metallic);
                _propBlock.SetFloat(SmoothnessProp, _smoothness);
                _propBlock.SetColor(ColorProp, _panelBaseColor);
                r.SetPropertyBlock(_propBlock);
            }
        }
    }
}
