using UnityEngine;
using UnityEngine.InputSystem;

namespace Solarium.ThreeD
{
    [RequireComponent(typeof(Camera))]
    public sealed class IsometricCamera3D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private SolariumEnvironment3D environment;
        [SerializeField, Range(25f, 70f)] private float pitch = 48f;
        [SerializeField] private float yaw = 45f;
        [SerializeField, Min(4f)] private float distance = 16f;
        [SerializeField, Min(4f)] private float minDistance = 8f;
        [SerializeField, Min(5f)] private float maxDistance = 22f;
        [SerializeField, Min(0.1f)] private float followSharpness = 6f;
        [SerializeField, Min(1f)] private float orbitSpeed = 75f;
        [SerializeField, Min(0.1f)] private float zoomSpeed = 1.3f;
        private Vector3 smoothedTarget;

        public void Initialize(SolariumEnvironment3D owner, Transform followTarget)
        {
            environment = owner;
            target = followTarget;
            Snap();
        }

        private void OnEnable()
        {
            if (environment != null)
                environment.EpisodeStarted += Snap;
        }

        private void OnDisable()
        {
            if (environment != null)
                environment.EpisodeStarted -= Snap;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            Keyboard keyboard = Keyboard.current;
            float orbit = 0f;
            if (keyboard != null)
            {
                if (keyboard.qKey.isPressed) orbit -= 1f;
                if (keyboard.eKey.isPressed) orbit += 1f;
            }
            yaw += orbit * orbitSpeed * Time.unscaledDeltaTime;
            Mouse mouse = Mouse.current;
            if (mouse != null)
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.01f * zoomSpeed, minDistance, maxDistance);

            Vector3 desiredTarget = ClampTarget(target.position);
            float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            smoothedTarget = Vector3.Lerp(smoothedTarget, desiredTarget, blend);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(smoothedTarget - rotation * Vector3.forward * distance, rotation);
        }

        public void Snap()
        {
            if (target == null)
                return;
            smoothedTarget = ClampTarget(target.position);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(smoothedTarget - rotation * Vector3.forward * distance, rotation);
        }

        private Vector3 ClampTarget(Vector3 value)
        {
            if (environment == null)
                return value;
            Vector2 center = Planar3D.ToPlanar(environment.transform.position);
            Vector2 half = environment.CurrentConfig.arenaSize.sqrMagnitude > 0f
                ? environment.CurrentConfig.arenaSize * 0.5f
                : new Vector2(7f, 5f);
            Vector2 planar = Planar3D.ToPlanar(value);
            planar.x = Mathf.Clamp(planar.x, center.x - half.x + 1f, center.x + half.x - 1f);
            planar.y = Mathf.Clamp(planar.y, center.y - half.y + 1f, center.y + half.y - 1f);
            return Planar3D.ToWorld(planar, 0.55f);
        }
    }
}
