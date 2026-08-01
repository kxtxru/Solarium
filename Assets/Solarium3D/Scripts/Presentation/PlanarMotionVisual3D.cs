using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class PlanarMotionVisual3D : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float bobAmount = 0.05f;
        [SerializeField] private bool faceVelocity = true;
        [SerializeField] private float turnSharpness = 12f;
        private Rigidbody body;
        private SolAgent3D agent;
        private Vector3 basePosition;
        private Vector3 baseScale;
        private float phase;

        public void Configure(Transform root, float bob, bool rotate)
        {
            visualRoot = root;
            bobAmount = bob;
            faceVelocity = rotate;
            CaptureBase();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            agent = GetComponent<SolAgent3D>();
            phase = Mathf.Abs(name.GetHashCode() % 37) * 0.17f;
            CaptureBase();
        }

        private void OnEnable() => CaptureBase();

        private void LateUpdate()
        {
            if (visualRoot == null)
                return;
            Vector2 movement = agent != null
                ? agent.LastMovement
                : body == null ? Vector2.zero : Planar3D.ToPlanarDirection(body.linearVelocity);
            float speed = Mathf.Clamp01(movement.magnitude);
            float bob = Mathf.Sin(Time.time * (2.5f + speed * 6f) + phase) * bobAmount * (0.35f + speed);
            visualRoot.localPosition = basePosition + Vector3.up * bob;
            float squash = speed * Mathf.Sin(Time.time * 10f + phase) * 0.025f;
            visualRoot.localScale = new Vector3(baseScale.x * (1f + squash), baseScale.y * (1f - squash), baseScale.z * (1f + squash));

            if (!faceVelocity || movement.sqrMagnitude < 0.0025f)
                return;
            Quaternion target = Quaternion.LookRotation(Planar3D.ToWorldDirection(movement), Vector3.up);
            float blend = 1f - Mathf.Exp(-turnSharpness * Time.deltaTime);
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, target, blend);
        }

        private void CaptureBase()
        {
            if (visualRoot == null)
                visualRoot = transform.Find("Sol Visual") ?? transform.Find("Hunter Visual") ?? transform.Find("Patroller Visual");
            if (visualRoot == null)
                return;
            basePosition = visualRoot.localPosition;
            baseScale = visualRoot.localScale;
        }
    }
}
