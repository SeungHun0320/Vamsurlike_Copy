Shader "Vamsurlike/TintableDesaturate"
{
    // Standard 셰이더의 기본 PBR 외형(알베도+메탈릭+스무스니스)은 그대로 유지하면서,
    // 몬스터 프리팹 재사용(팔레트 스왑) 시 _Color 곱하기 틴트만으로는 원본 텍스처의 색상 비율이
    // 그대로 남아 "밝기만 달라지고 실제로 무채색이 되지 않는" 문제를 해결하기 위해
    // _Saturation(0=완전 무채색, 1=원본)을 MaterialPropertyBlock으로 인스턴스별 조절 가능하게 추가한다.
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Range(0,1)] _Saturation ("Saturation (0=Grey, 1=Original)", Range(0,1)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 _Color;
        half _Saturation;
        half _Glossiness;
        half _Metallic;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half grey = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
            o.Albedo = lerp(grey.xxx, albedo.rgb, _Saturation);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = albedo.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
