using UnityEngine;

namespace Solarium.ThreeD
{
    public static class Planar3D
    {
        public static Vector3 ToWorld(Vector2 point, float height = 0f) =>
            new(point.x, height, point.y);

        public static Vector2 ToPlanar(Vector3 point) => new(point.x, point.z);

        public static Vector3 ToWorldDirection(Vector2 direction) =>
            new(direction.x, 0f, direction.y);

        public static Vector2 ToPlanarDirection(Vector3 direction) =>
            new(direction.x, direction.z);
    }
}
