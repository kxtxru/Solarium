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
        [SerializeField] private Text healthValueText;
        [SerializeField] private Text energyValueText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text metricsText;
        [SerializeField] private Text worldText;
        [SerializeField] private Text deathText;
        [SerializeField] private Text pauseButtonText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button speed1Button;
        [SerializeField] private Button speed2Button;
        [SerializeField] private Button speed5Button;
        [SerializeField] private Button newWorldButton;
        [SerializeField] private Button confirmNewWorldButton;
        [SerializeField] private Button cancelNewWorldButton;
        [SerializeField] private CanvasGroup transition;
        [SerializeField] private CanvasGroup newWorldConfirmation;
        private bool paused;
        private float selectedSpeed = 1f;
        private Coroutine fadeRoutine;

        public void Initialize(
            SolariumEnvironment3D owner,
            Image health,
            Image energy,
            Text healthValue,
            Text energyValue,
            Text status,
            Text metrics,
            Text world,
            Text death,
            Text pauseLabel,
            Button pause,
            Button speed1,
            Button speed2,
            Button speed5,
            Button newWorld,
            Button confirmNewWorld,
            Button cancelNewWorld,
            CanvasGroup fade,
            CanvasGroup confirmation)
        {
            environment = owner;
            healthFill = health;
            energyFill = energy;
            healthValueText = healthValue;
            energyValueText = energyValue;
            statusText = status;
            metricsText = metrics;
            worldText = world;
            deathText = death;
            pauseButtonText = pauseLabel;
            pauseButton = pause;
            speed1Button = speed1;
            speed2Button = speed2;
            speed5Button = speed5;
            newWorldButton = newWorld;
            confirmNewWorldButton = confirmNewWorld;
            cancelNewWorldButton = cancelNewWorld;
            transition = fade;
            newWorldConfirmation = confirmation;
        }

        private void Awake()
        {
            pauseButton?.onClick.AddListener(TogglePause);
            speed1Button?.onClick.AddListener(() => SetSpeed(1f));
            speed2Button?.onClick.AddListener(() => SetSpeed(2f));
            speed5Button?.onClick.AddListener(() => SetSpeed(5f));
            newWorldButton?.onClick.AddListener(ShowNewWorldConfirmation);
            confirmNewWorldButton?.onClick.AddListener(ConfirmNewWorld);
            cancelNewWorldButton?.onClick.AddListener(HideNewWorldConfirmation);
            if (transition != null)
                transition.alpha = 0f;
            SetConfirmationVisible(false);
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
            SetBarVisual(healthFill, agent.Vitals.NormalizedHealth);
            SetBarVisual(energyFill, agent.Vitals.NormalizedEnergy);
            if (healthValueText != null)
                healthValueText.text = $"VIDA  {agent.Vitals.CurrentHealth:0} / {agent.Vitals.maxHealth:0}";
            if (energyValueText != null)
                energyValueText.text = $"ENERGÍA  {agent.Vitals.CurrentEnergy:0} / {agent.Vitals.maxEnergy:0}";
            if (statusText != null)
            {
                statusText.text = $"SOL · {agent.DebugState.ToUpperInvariant()}\n"
                    + $"Episodio {environment.EpisodeNumber}  ·  Seed {environment.CurrentSeed}";
            }
            if (metricsText != null)
            {
                metricsText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Supervivencia  {0:0.0}s     Récord  {1:0.0}s\nComida  {2}     Raciones  {3}     Dificultad  {4:0.00}",
                    environment.SurvivalTime,
                    environment.Statistics.BestSurvival,
                    environment.FoodCollected,
                    environment.RationsAvailable,
                    environment.CurrentConfig.difficulty);
            }
            if (worldText != null)
            {
                worldText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Zonas descubiertas  {0}     Distancia  {1:0} m\nGuardadas  {2}     Consumidas  {3}",
                    environment.ChunksDiscovered,
                    environment.DistanceFromWorldOrigin,
                    environment.SuppliesDeposited,
                    environment.RationsConsumed);
            }
        }

        private static void SetBarVisual(Image fill, float value)
        {
            if (fill == null)
                return;
            float normalized = Mathf.Clamp01(value);
            fill.fillAmount = normalized;
            RectTransform rect = fill.rectTransform;
            Vector3 scale = rect.localScale;
            scale.x = normalized;
            rect.localScale = scale;
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

        public void ShowNewWorldConfirmation() => SetConfirmationVisible(true);

        public void HideNewWorldConfirmation() => SetConfirmationVisible(false);

        public void ConfirmNewWorld()
        {
            SetConfirmationVisible(false);
            SetSpeed(selectedSpeed);
            environment?.CreateNewWorld();
        }

        private void SetConfirmationVisible(bool visible)
        {
            if (newWorldConfirmation == null)
                return;
            newWorldConfirmation.alpha = visible ? 1f : 0f;
            newWorldConfirmation.interactable = visible;
            newWorldConfirmation.blocksRaycasts = visible;
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
