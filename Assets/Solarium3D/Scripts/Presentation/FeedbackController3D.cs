using UnityEngine;

namespace Solarium.ThreeD
{
    [RequireComponent(typeof(ProceduralAudio3D))]
    public sealed class FeedbackController3D : MonoBehaviour
    {
        [SerializeField] private SolariumEnvironment3D environment;
        [SerializeField] private SolariumPalette3D palette;
        [SerializeField, Min(2)] private int particlePoolSize = 10;
        private ParticleSystem[] particles;
        private int nextParticle;
        private ProceduralAudio3D audioPlayer;
        private bool subscribed;

        public void Initialize(SolariumEnvironment3D owner, SolariumPalette3D visualPalette)
        {
            Unsubscribe();
            environment = owner;
            palette = visualPalette;
            Subscribe();
        }

        private void Awake()
        {
            audioPlayer = GetComponent<ProceduralAudio3D>();
            particles = new ParticleSystem[Mathf.Max(2, particlePoolSize)];
            for (int i = 0; i < particles.Length; i++)
                particles[i] = CreateParticleSystem(i);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Play(Vector3 position, FeedbackKind3D kind)
        {
            ParticleSystem particle = particles[nextParticle++ % particles.Length];
            particle.transform.position = position + Vector3.up * 0.45f;
            var main = particle.main;
            main.startColor = ColorFor(kind);
            main.startSize = kind == FeedbackKind3D.Death ? 0.2f : 0.12f;
            particle.Play(true);
            audioPlayer.Play(kind);
        }

        private void Subscribe()
        {
            if (subscribed || environment == null)
                return;
            environment.FeedbackRequested += Play;
            if (environment.InfiniteWorld != null)
                environment.InfiniteWorld.OriginShifted += HandleOriginShift;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || environment == null)
                return;
            environment.FeedbackRequested -= Play;
            if (environment.InfiniteWorld != null)
                environment.InfiniteWorld.OriginShifted -= HandleOriginShift;
            subscribed = false;
        }

        private void HandleOriginShift(Vector3 shift)
        {
            if (particles == null)
                return;
            foreach (ParticleSystem particle in particles)
                particle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private ParticleSystem CreateParticleSystem(int index)
        {
            var go = new GameObject($"Feedback Particles {index}");
            go.layer = SolariumLayers3D.Effects;
            go.transform.SetParent(transform, false);
            ParticleSystem particle = go.AddComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.35f;
            main.startLifetime = 0.5f;
            main.startSpeed = 2.1f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particle.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            return particle;
        }

        private static Color ColorFor(FeedbackKind3D kind) => kind switch
        {
            FeedbackKind3D.Food => new Color(0.35f, 1f, 0.35f),
            FeedbackKind3D.GoldenFood => new Color(1f, 0.76f, 0.1f),
            FeedbackKind3D.Healing => new Color(0.2f, 0.85f, 1f),
            FeedbackKind3D.SupplyPickup => new Color(1f, 0.35f, 0.95f),
            FeedbackKind3D.SupplyDeposit => new Color(0.2f, 1f, 0.85f),
            FeedbackKind3D.ReserveUse => new Color(0.55f, 1f, 0.28f),
            FeedbackKind3D.Damage => new Color(1f, 0.15f, 0.12f),
            _ => new Color(1f, 0.35f, 0.1f)
        };
    }
}
