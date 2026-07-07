using UnityEngine;

namespace Vamsurlike.VFX
{
    // 런타임에 new GameObject()+AddComponent<LineRenderer>()로 즉석 생성되는 텔레그래프/범위
    // 표시(AreaCircleVFX, MeleeArcVFX 등)가 별도 머티리얼을 안 받았을 때 쓰는 공용 폴백.
    // RULES.md: 런타임 new Material() 금지 — Resources에 미리 구워둔 에셋을 로드만 한다.
    public static class LineDefaultMaterial
    {
        private const string ResourcePath = "Materials/M_LineDefault";

        private static Material cached;

        public static Material Get()
        {
            if (cached != null) return cached;

            cached = Resources.Load<Material>(ResourcePath);
            if (cached == null)
                Debug.LogWarning($"[{nameof(LineDefaultMaterial)}] {ResourcePath} 를 찾을 수 없습니다.");

            return cached;
        }
    }
}
