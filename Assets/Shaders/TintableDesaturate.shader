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
        #pragma multi_compile_instancing
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;

        // _Color/_Saturation은 몬스터마다(재사용 프리팹 팔레트 스왑) 값이 달라야 하는 MaterialPropertyBlock
        // 오버라이드 대상이다 — GPU Instancing이 켜진 상태(Enemy 재질 4종 모두 Enable Instancing)에서는
        // 이렇게 UNITY_INSTANCING_BUFFER로 선언하지 않으면 배치된 인스턴스끼리 값이 뒤섞여
        // 서로 다른 색으로 스폰된 적들의 색이 겹쳐 보이는 문제가 생긴다.
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
            UNITY_DEFINE_INSTANCED_PROP(half, _Saturation)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 color      = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            half   saturation = UNITY_ACCESS_INSTANCED_PROP(Props, _Saturation);

            // 채도 조절을 먼저 원본 텍스처에 적용한 뒤 틴트를 곱해야 한다 — 틴트부터 곱하고 나중에
            // 채도를 낮추면 saturation=0일 때 색상 정보(휘도만 남고 색조는 사라짐)가 통째로 사라져서
            // _Color가 뭐든 그냥 무채색이 되어버린다(예: 오렌지 틴트를 줘도 회색으로만 보임).
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            half grey = dot(tex.rgb, half3(0.299, 0.587, 0.114));
            fixed3 desaturated = lerp(grey.xxx, tex.rgb, saturation);

            o.Albedo = desaturated * color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = tex.a * color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
