using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Solarium.ThreeD
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(SolVitals))]
    [RequireComponent(typeof(SolObservations3D))]
    public sealed class SolAgent3D : Agent
    {
        private SolariumEnvironment3D environment;
        private SolVitals vitals;
        private SolObservations3D observations;
        private Rigidbody body;
        private readonly HashSet<MudZone3D> mudZones = new();
        private readonly HashSet<ShelterZone3D> shelterZones = new();
        private Vector2 requestedMovement;
        private Vector2 lastPositionSample;
        private Vector2 knockbackVelocity;
        private float stillTime;
        private float knockbackUntil;
        private float nextWallPenaltyTime;
        private bool requestedSprint;
        private bool carryingSupply;
        private float nextInteractionTime;

        public SolVitals Vitals => vitals;
        public Vector2 LastMovement => requestedMovement;
        public bool IsSprinting => requestedSprint && vitals != null && vitals.CurrentEnergy > 0f;
        public bool IsInMud => mudZones.Count > 0;
        public bool IsCarryingSupply => carryingSupply;
        public bool IsSheltered => shelterZones.Count > 0;
        public string DebugState { get; private set; } = "Explorando";

        protected override void Awake()
        {
            base.Awake();
            body = GetComponent<Rigidbody>();
            vitals = GetComponent<SolVitals>();
            observations = GetComponent<SolObservations3D>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            vitals.Damaged += HandleDamage;
            vitals.Died += HandleDeath;
        }

        public void Bind(SolariumEnvironment3D owner, int rayCount)
        {
            environment = owner;
            observations.Initialize(owner, vitals, body, rayCount);
        }

        public override void OnEpisodeBegin()
        {
            requestedMovement = Vector2.zero;
            requestedSprint = false;
            stillTime = 0f;
            if (body != null)
                body.linearVelocity = Vector3.zero;
            environment?.BeginEpisode();
        }

        public override void CollectObservations(VectorSensor sensor) =>
            observations.Write(sensor, IsSprinting);

        public override void OnActionReceived(ActionBuffers actions)
        {
            Vector2 raw = new(
                Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f),
                Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f));
            requestedMovement = NormalizeMovement(raw);
            requestedSprint = actions.DiscreteActions.Length > 0 && actions.DiscreteActions[0] > 0;
            if (actions.DiscreteActions.Length > 1
                && actions.DiscreteActions[1] > 0
                && environment != null
                && Time.time >= nextInteractionTime)
            {
                nextInteractionTime = Time.time + environment.Settings.interactionCooldown;
                environment.TryInteract(this);
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            Vector2 movement = Vector2.zero;
            bool sprint = false;
            bool interact = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
                sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                interact = keyboard.eKey.wasPressedThisFrame;
            }

            movement = NormalizeMovement(movement);
            ActionSegment<float> continuous = actionsOut.ContinuousActions;
            ActionSegment<int> discrete = actionsOut.DiscreteActions;
            continuous[0] = movement.x;
            continuous[1] = movement.y;
            discrete[0] = sprint ? 1 : 0;
            discrete[1] = interact ? 1 : 0;
        }

        public static Vector2 NormalizeMovement(Vector2 movement) =>
            Vector2.ClampMagnitude(movement, 1f);

        public static float MovementSpeedWithTerrain(float baseSpeed, bool inMud, float mudMultiplier) =>
            Mathf.Max(0f, baseSpeed) * (inMud ? Mathf.Clamp01(mudMultiplier) : 1f);

        private void FixedUpdate()
        {
            if (environment == null || vitals == null || !vitals.IsAlive)
                return;

            int episodeBeforeTick = environment.EpisodeNumber;
            float baseSpeed = vitals.movementSpeed * (IsSprinting ? vitals.sprintMultiplier : 1f);
            float speed = MovementSpeedWithTerrain(baseSpeed, IsInMud, environment.Settings.mudSpeedMultiplier);
            Vector2 planarVelocity = Time.time < knockbackUntil
                ? knockbackVelocity
                : requestedMovement * speed;
            body.linearVelocity = Planar3D.ToWorldDirection(planarVelocity);

            float energySpent = vitals.TickEnergy(
                Time.fixedDeltaTime,
                requestedMovement.magnitude,
                IsSprinting,
                environment.CurrentConfig.energyDrainMultiplier);
            if (episodeBeforeTick != environment.EpisodeNumber)
                return;

            UpdateStuckState();
            environment.OnAgentFixedStep(energySpent, IsSprinting, stillTime);
            UpdateDebugState();
        }

        public void AddTrackedReward(float value, RewardComponent component)
        {
            AddReward(value);
            environment?.RecordReward(component, value);
        }

        public void PrepareForEpisode(Vector2 position)
        {
            Vector3 world = Planar3D.ToWorld(position, 0.55f);
            transform.position = world;
            body.position = world;
            body.linearVelocity = Vector3.zero;
            requestedMovement = Vector2.zero;
            requestedSprint = false;
            mudZones.Clear();
            shelterZones.Clear();
            carryingSupply = false;
            knockbackVelocity = Vector2.zero;
            knockbackUntil = 0f;
            nextWallPenaltyTime = 0f;
            nextInteractionTime = 0f;
            vitals.ResetVitals();
            lastPositionSample = position;
            stillTime = 0f;
        }

        public void SetVisionRange(float value) => observations.SetVisionRange(value);
        public void EnterMud(MudZone3D zone) { if (zone != null) mudZones.Add(zone); }
        public void ExitMud(MudZone3D zone) { if (zone != null) mudZones.Remove(zone); }
        public void EnterShelter(ShelterZone3D zone) { if (zone != null) shelterZones.Add(zone); }
        public void ExitShelter(ShelterZone3D zone) { if (zone != null) shelterZones.Remove(zone); }

        public bool PickUpSupply()
        {
            if (carryingSupply)
                return false;
            carryingSupply = true;
            return true;
        }

        public bool DepositSupply()
        {
            if (!carryingSupply)
                return false;
            carryingSupply = false;
            return true;
        }

        public void ApplyKnockback(Vector2 direction, float speed, float duration)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return;
            knockbackVelocity = direction.normalized * Mathf.Max(0f, speed);
            knockbackUntil = Time.time + Mathf.Max(0f, duration);
        }

        private void UpdateStuckState()
        {
            Vector2 position = Planar3D.ToPlanar(transform.position);
            float displacement = Vector2.Distance(position, lastPositionSample);
            if (displacement >= environment.Settings.stuckMinimumDisplacement)
            {
                stillTime = 0f;
                lastPositionSample = position;
            }
            else
            {
                stillTime += Time.fixedDeltaTime;
            }
        }

        private void UpdateDebugState()
        {
            if (vitals.NormalizedHealth < 0.35f) DebugState = "Buscando curación";
            else if (environment.GetNearestEnemyDistance(Planar3D.ToPlanar(transform.position)) < 3f) DebugState = "Huyendo";
            else if (IsInMud) DebugState = "Cruzando barro";
            else if (stillTime >= environment.Settings.stuckWindowSeconds) DebugState = "Atrapado";
            else if (vitals.NormalizedEnergy < 0.45f) DebugState = "Buscando comida";
            else DebugState = "Explorando";
        }

        private void OnCollisionStay(Collision collision)
        {
            if (environment == null
                || collision.collider.GetComponentInParent<ArenaSolid3D>() == null
                || Time.time < nextWallPenaltyTime)
                return;
            nextWallPenaltyTime = Time.time + environment.Settings.wallPenaltyInterval;
            AddTrackedReward(environment.Settings.wallCollisionPenalty, RewardComponent.WallCollision);
        }

        private void HandleDamage(float amount)
        {
            if (environment == null || vitals.maxHealth <= 0f)
                return;
            environment.RegisterDamage(amount);
            AddTrackedReward(environment.Settings.damagePenalty * (amount / vitals.maxHealth), RewardComponent.Damage);
        }

        private void HandleDeath(string reason)
        {
            if (environment == null)
                return;
            AddTrackedReward(environment.Settings.deathPenalty, RewardComponent.Death);
            environment.EndEpisode(reason);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (body != null)
                body.linearVelocity = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (vitals == null)
                return;
            vitals.Damaged -= HandleDamage;
            vitals.Died -= HandleDeath;
        }
    }
}
