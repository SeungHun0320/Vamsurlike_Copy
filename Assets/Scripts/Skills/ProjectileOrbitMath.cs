using UnityEngine;

namespace Vamsurlike.Skills
{
    internal static class ProjectileOrbitMath
    {
        public static Vector3 GetRadial(float angleDegrees)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        public static Vector3 GetTangent(Vector3 radial, float rotSpeed)
        {
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            return rotSpeed < 0f ? -tangent : tangent;
        }
    }
}
