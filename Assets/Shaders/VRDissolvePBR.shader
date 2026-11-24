Shader "Custom/VRDissolvePBR"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _NoiseTex ("Dissolve Noise (R)", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0.0
        _EdgeWidth ("Edge Width", Range(0,0.2)) = 0.05
        [HDR] _EdgeColor ("Edge Color (Emission)", Color) = (0.2, 0.7, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NoiseTex;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NoiseTex;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        half _DissolveAmount;
        half _EdgeWidth;
        fixed4 _EdgeColor;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half noise = tex2D(_NoiseTex, IN.uv_NoiseTex).r;
            // Если шум не назначен, генерируем базовый шум из мировых координат
            if (noise <= 0.001)
            {
                noise = frac(sin(dot(IN.worldPos.xyz, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            // Отсечение по порогу диссолва
            half dissolveThreshold = _DissolveAmount;
            clip(noise - dissolveThreshold);

            // Свечение горящего/голографического края
            half edge = 1.0 - smoothstep(dissolveThreshold, dissolveThreshold + _EdgeWidth, noise);
            fixed3 emission = _EdgeColor.rgb * edge * (_DissolveAmount > 0.001 ? 2.5 : 0.0);

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = emission;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Standard"
}
