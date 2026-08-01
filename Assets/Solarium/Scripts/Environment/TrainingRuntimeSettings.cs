using UnityEngine;

namespace Solarium
{
    public sealed class TrainingRuntimeSettings : MonoBehaviour
    {
        [SerializeField, Range(1f, 100f)] private float simulationTimeScale = 20f;
        private float previousTimeScale;
        private int previousVSync;

        private void Awake()
        {
            previousTimeScale = Time.timeScale;
            previousVSync = QualitySettings.vSyncCount;
            Time.timeScale = simulationTimeScale;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        private void OnDestroy()
        {
            Time.timeScale = previousTimeScale;
            QualitySettings.vSyncCount = previousVSync;
        }
    }
}
