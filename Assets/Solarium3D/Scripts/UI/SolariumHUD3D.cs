using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Solarium.ThreeD
{
    public sealed class SolariumHUD3D : MonoBehaviour
    {
        [SerializeField] private SolariumEnvironment3D environment;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image energyFill;
        [SerializeField] private Text statusText;
        [SerializeField] private Text metricsText;
        [SerializeField] private Text deathText;
        [SerializeField] private Text pauseButtonText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speed1Button;
        [SerializeField] private Button speed2Button;
        [SerializeField] private Button speed5Button;
        [SerializeField] private CanvasGroup transition;
        private bool paused;
        private float selectedSpeed = 1f;
        private Coroutine fadeRoutine;

        public void Initialize(
            SolariumEnvironment3D owner,
            Image health,
            Image energy,
            Text status,
            Text metrics,
            Text death,
            Text pauseLabel,
            Button pause,
            Button speed1,
            Button speed2,
            Button speed5,
            CanvasGroup fade)
        {
            environment = owner;
            healthFill = health;
            energyFill = energy;
            statusText = status;
            metricsText = metrics;
            deathText = death;
            pauseButtonText = pauseLabel;
            pauseButton = pause;
            speed1Button = speed1;
            speed2Button = speed2;
            speed5Button = speed5;
            transition = fade;
        }

        private void Awake()
        {
            pauseButton?.onClick.AddListener(TogglePause);
            speed1Button?.onClick.AddListener(() => SetSpeed(1f));
            speed2Button?.onClick.AddListener(() => SetSpeed(2f));
            speed5Button?.onClick.AddListener(() => SetSpeed(5f));
            if (transition != null)
                transition.alpha = 0f;
        }

        private void OnEnable()
        {
            if (environment != null)
                environment.EpisodeCompleted += OnEpisodeCompleted;
        }

        private void OnDisable()
        {
            if (environment != null)
                environment.EpisodeCompleted -= OnEpisodeCompleted;
        }

        private void Update()
        {
            if (environment == null || environment.Agent == null)
                return;
            SolAgent3D agent = environment.Agent;
            if (healthFill != null) healthFill.fillAmount = agent.Vitals.NormalizedHealth;
            if (energyFill != null) energyFill.fillAmount = agent.Vitals.NormalizedEnergy;
            if (statusText != null)
            {
                statusText.text = $"SOL · {agent.DebugState.ToUpperInvariant()}\n"
                    + $"Episodio {environment.EpisodeNumber}  ·  Seed {environment.CurrentSeed}";
            }
            if (metricsText != null)
            {
                metricsText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Supervivencia  {0:0.0}s     Récord  {1:0.0}s\nComida  {2}     Reserva  {3}     Dificultad  {4:0.00}",
                    environment.SurvivalTime,
                    environment.Statistics.BestSurvival,
                    environment.FoodCollected,
                    environment.SuppliesDeposited,
                    environment.CurrentConfig.difficulty);
            }
        }

        public void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : selectedSpeed;
            if (pauseButtonText != null)
                pauseButtonText.text = paused ? "REANUDAR" : "PAUSA";
        }

        public void SetSpeed(float value)
        {
            selectedSpeed = Mathf.Clamp(value, 1f, 5f);
            paused = false;
            Time.timeScale = selectedSpeed;
            if (pauseButtonText != null)
                pauseButtonText.text = "PAUSA";
        }

        private void OnEpisodeCompleted(EpisodeRecord record)
        {
            if (deathText != null)
                deathText.text = ReasonLabel(record.terminationReason);
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeEpisode());
        }

        private IEnumerator FadeEpisode()
        {
            if (transition == null)
                yield break;
            transition.alpha = 1f;
            float elapsed = 0f;
            while (elapsed < 0.8f)
            {
                elapsed += Time.unscaledDeltaTime;
                transition.alpha = 1f - Mathf.Clamp01(elapsed / 0.8f);
                yield return null;
            }
            transition.alpha = 0f;
        }

        private static string ReasonLabel(string reason) => reason switch
        {
            "energy_depleted" => "SOL SE QUEDÓ SIN ENERGÍA",
            "health_depleted" => "SOL CAYÓ ANTE EL PELIGRO",
            "lava" => "SOL CAYÓ EN LA LAVA",
            "manual_restart" => "NUEVO MUNDO",
            _ => "NUEVO EPISODIO"
        };
    }
}
