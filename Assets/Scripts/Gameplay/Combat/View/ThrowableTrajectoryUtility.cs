using UnityEngine;

namespace MemorialArchive.Gameplay.Combat.View
{
    /// <summary>投掷预览和真实刚体共用的二维弹道计算。</summary>
    public static class ThrowableTrajectoryUtility
    {
        public static bool TrySolve(
            Vector2 origin,
            Vector2 requestedTarget,
            float maximumDistance,
            float gravityScale,
            float arcHeight,
            out Vector2 velocity,
            out float flightSeconds,
            out Vector2 target)
        {
            var offset = requestedTarget - origin;
            if (maximumDistance > 0f && offset.magnitude > maximumDistance)
                offset = offset.normalized * maximumDistance;
            target = origin + offset;

            var gravity = Mathf.Abs(Physics2D.gravity.y * Mathf.Max(0.01f, gravityScale));
            if (gravity <= 0.0001f)
            {
                velocity = Vector2.zero;
                flightSeconds = 0f;
                return false;
            }

            var apexY = Mathf.Max(origin.y, target.y) + Mathf.Max(0.1f, arcHeight);
            var rise = Mathf.Max(0.01f, apexY - origin.y);
            var fall = Mathf.Max(0.01f, apexY - target.y);
            var verticalSpeed = Mathf.Sqrt(2f * gravity * rise);
            var upSeconds = verticalSpeed / gravity;
            var downSeconds = Mathf.Sqrt(2f * fall / gravity);
            flightSeconds = Mathf.Max(0.05f, upSeconds + downSeconds);
            velocity = new Vector2((target.x - origin.x) / flightSeconds, verticalSpeed);
            return true;
        }

        public static Vector2 Evaluate(Vector2 origin, Vector2 velocity, float gravityScale, float seconds)
        {
            return origin + velocity * seconds + 0.5f * Physics2D.gravity * Mathf.Max(0.01f, gravityScale) * seconds * seconds;
        }
    }
}
