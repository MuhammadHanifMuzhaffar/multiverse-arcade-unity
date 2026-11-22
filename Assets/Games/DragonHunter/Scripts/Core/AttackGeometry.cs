using UnityEngine;

namespace Multiverse.Games.DragonHunter
{
    public enum AttackShape
    {
        Cone,   // busur depan (gigitan, semburan api)
        Circle, // lingkaran penuh di sekitar naga (sapuan ekor, nova)
    }

    /// <summary>Uji kena datar (bidang XZ) yang dipakai AI, telegraf, dan test.</summary>
    public static class AttackGeometry
    {
        public static bool IsInCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float fullAngleDegrees)
        {
            Vector3 toTarget = Flatten(target - origin);
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance > range * range) return false;
            if (sqrDistance < 0.0001f) return true; // berdiri tepat di titik asal selalu dihitung kena

            Vector3 flatForward = Flatten(forward);
            if (flatForward.sqrMagnitude < 0.0001f) return false;

            return Vector3.Angle(flatForward, toTarget) <= fullAngleDegrees * 0.5f;
        }

        public static bool IsInCircle(Vector3 origin, Vector3 target, float radius)
        {
            return Flatten(target - origin).sqrMagnitude <= radius * radius;
        }

        public static bool IsInShape(AttackShape shape, Vector3 origin, Vector3 forward, Vector3 target,
                                     float range, float fullAngleDegrees, float extraRadius = 0f)
        {
            float r = range + Mathf.Max(0f, extraRadius);
            return shape == AttackShape.Circle
                ? IsInCircle(origin, target, r)
                : IsInCone(origin, forward, target, r, fullAngleDegrees);
        }

        public static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
