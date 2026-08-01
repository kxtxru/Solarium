using UnityEngine;

namespace Solarium
{
    [DisallowMultipleComponent]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float followSharpness = 6f;
        [SerializeField] private Vector3 offset = new(0f, 0f, -10f);

        public void Initialize(Transform followTarget) => target = followTarget;

        private void LateUpdate()
        {
            if (target == null)
                return;

            float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target.position + offset, blend);
        }
    }
}
