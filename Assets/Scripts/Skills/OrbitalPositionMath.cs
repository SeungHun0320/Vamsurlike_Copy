using UnityEngine;

namespace Vamsurlike.Skills
{
    internal static class OrbitalPositionMath
    {
        public static Vector3 Calculate(
            Vector3 origin,
            float radius,
            float rotationSpeed,
            int index,
            int count)
        {
            int safeCount = Mathf.Max(1, count);
            float angle = Time.time * rotationSpeed + 360f / safeCount * index;
            return origin
                + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
        }
    }
}
