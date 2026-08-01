using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Solarium.ThreeD.Tests
{
    public sealed class Solarium3DPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrainingSceneHasSixIndependentCompatibleAgents()
        {
            yield return SceneManager.LoadSceneAsync("SolariumTraining3D", LoadSceneMode.Single);
            yield return null;
            yield return new WaitForFixedUpdate();
            SolariumEnvironment3D[] environments = Object.FindObjectsByType<SolariumEnvironment3D>();
            for (int i = 0; i < 30 && environments.Any(item => item.EpisodeNumber == 0); i++)
                yield return null;
            foreach (SolariumEnvironment3D item in environments)
            {
                if (item.EpisodeNumber == 0)
                    item.Agent.OnEpisodeBegin();
            }
            Assert.That(environments.Length, Is.EqualTo(6));
            Assert.That(environments.All(item => item.Agent != null), Is.True);
            Assert.That(environments.All(item => item.Agent.GetComponent<BehaviorParameters>().BrainParameters.VectorObservationSize == 194), Is.True);

            SolariumEnvironment3D target = environments[0];
            int targetEpisode = target.EpisodeNumber;
            int[] others = environments.Skip(1).Select(item => item.EpisodeNumber).ToArray();
            target.Agent.Vitals.Kill("health_depleted");
            yield return null;

            Assert.That(target.LastTerminationReason, Is.EqualTo("health_depleted"));
            Assert.That(environments.Skip(1).Select(item => item.EpisodeNumber), Is.EqualTo(others));
            if (target.EpisodeNumber == targetEpisode)
                target.Agent.OnEpisodeBegin();
            Assert.That(target.EpisodeNumber, Is.GreaterThan(targetEpisode));
            Assert.That(environments.Skip(1).Select(item => item.EpisodeNumber), Is.EqualTo(others));
        }

        [UnityTest]
        public IEnumerator EpisodeRunsPastOneHundredTwentySecondsAndEndsAtTrainingHorizon()
        {
            yield return SceneManager.LoadSceneAsync("SolariumTraining3D", LoadSceneMode.Single);
            yield return null;
            SolariumEnvironment3D environment = Object.FindAnyObjectByType<SolariumEnvironment3D>();
            for (int i = 0; i < 30 && environment.EpisodeNumber == 0; i++)
                yield return null;
            Assert.That(environment.EpisodeNumber, Is.GreaterThan(0));
            int episode = environment.EpisodeNumber;
            for (int i = 0; i < 6100; i++)
                environment.OnAgentFixedStep(0f, false, 0f);
            Assert.That(environment.SurvivalTime, Is.GreaterThan(120f));
            Assert.That(environment.EpisodeNumber, Is.EqualTo(episode));

            int horizonTicks = Mathf.CeilToInt(
                environment.Settings.infiniteTrainingEpisodeSeconds / Time.fixedDeltaTime) + 1;
            for (int i = 6100; i < horizonTicks; i++)
                environment.OnAgentFixedStep(0f, false, 0f);
            Assert.That(environment.LastTerminationReason, Is.EqualTo("time_limit"));
            Assert.That(environment.EpisodeNumber, Is.GreaterThan(episode));
        }

        [UnityTest]
        public IEnumerator SurvivalSceneLoadsCompatibleOnnx()
        {
            yield return SceneManager.LoadSceneAsync("SolariumSurvival3D", LoadSceneMode.Single);
            yield return null;
            SolariumEnvironment3D environment = Object.FindAnyObjectByType<SolariumEnvironment3D>();
            for (int i = 0; i < 30 && environment.EpisodeNumber == 0; i++)
                yield return null;
            BehaviorParameters behavior = environment.Agent.GetComponent<BehaviorParameters>();
            Assert.That(behavior.BrainParameters.VectorObservationSize, Is.EqualTo(194));
            Assert.That(behavior.BrainParameters.ActionSpec.NumContinuousActions, Is.EqualTo(2));
            Assert.That(behavior.BrainParameters.ActionSpec.BranchSizes, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(behavior.Model, Is.Not.Null);
            Assert.That(behavior.Model.name, Is.EqualTo("SolariumPersistentMemoryV3"));
            Assert.That(behavior.BehaviorType, Is.EqualTo(BehaviorType.InferenceOnly));

            if (!environment.IsEpisodeActive)
            {
                environment.Agent.OnEpisodeBegin();
                yield return null;
            }
            int episode = environment.EpisodeNumber;
            for (int i = 0; i < 20; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(environment.EpisodeNumber, Is.EqualTo(episode));
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(environment.Agent.isActiveAndEnabled, Is.True);
            Assert.That(environment.Agent.Vitals.IsAlive, Is.True);
            Assert.That(environment.SurvivalTime, Is.GreaterThan(0.1f));

            InfiniteWorld3D world = environment.InfiniteWorld;
            ChunkCoord3D startChunk = world.CurrentChunk;
            ChunkCoord3D startOrigin = world.OriginChunk;
            ChunkCoord3D nextChunk = startChunk + new ChunkCoord3D(1, 0);
            Vector2 crossingPlanar = world.PhysicalCenter(startChunk) + Vector2.right * 13f;
            Rigidbody body = environment.Agent.GetComponent<Rigidbody>();
            Vector3 crossingPosition = Planar3D.ToWorld(crossingPlanar, 0.55f);
            body.position = crossingPosition;
            environment.Agent.transform.position = crossingPosition;
            environment.OnAgentFixedStep(0f, false, 0f);

            Assert.That(world.CurrentChunk, Is.EqualTo(nextChunk));
            Assert.That(world.OriginChunk, Is.EqualTo(startOrigin),
                "Crossing an adjacent survival chunk must not recenter the world.");
            Assert.That(body.position.x, Is.EqualTo(crossingPosition.x).Within(0.01f),
                "The agent must keep a continuous physical position at a chunk seam.");

            GameObject startRoot = GameObject.Find($"Chunk_{startChunk.x}_{startChunk.y}");
            GameObject nextRoot = GameObject.Find($"Chunk_{nextChunk.x}_{nextChunk.y}");
            Assert.That(startRoot, Is.Not.Null);
            Assert.That(nextRoot, Is.Not.Null);
            Collider startGround = startRoot.GetComponentInChildren<ArenaGround3D>().GetComponent<Collider>();
            Collider nextGround = nextRoot.GetComponentInChildren<ArenaGround3D>().GetComponent<Collider>();
            Assert.That(startGround.bounds.max.x, Is.EqualTo(nextGround.bounds.min.x).Within(0.02f),
                "Adjacent chunk grounds must meet without a physical gap.");

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            environment.Agent.Vitals.SetForTests(50f, 25f);
            yield return null;
            Image healthFill = GameObject.Find("Health Fill").GetComponent<Image>();
            Image energyFill = GameObject.Find("Energy Fill").GetComponent<Image>();
            Assert.That(healthFill.rectTransform.localScale.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(energyFill.rectTransform.localScale.x, Is.EqualTo(0.25f).Within(0.01f));
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator InfiniteTrainingStreamsRevisitsAndUsesStoredRations()
        {
            yield return SceneManager.LoadSceneAsync("SolariumTraining3D", LoadSceneMode.Single);
            yield return null;
            SolariumEnvironment3D environment = Object.FindAnyObjectByType<SolariumEnvironment3D>();
            for (int i = 0; i < 30 && environment.EpisodeNumber == 0; i++)
                yield return null;
            if (environment.EpisodeNumber == 0)
            {
                environment.Agent.OnEpisodeBegin();
                yield return null;
            }

            InfiniteWorld3D world = environment.InfiniteWorld;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.Mode, Is.EqualTo(WorldMode3D.InfiniteTraining));
            Assert.That(world.ActiveChunkCount, Is.EqualTo(9));
            int episode = environment.EpisodeNumber;

            Rigidbody body = environment.Agent.GetComponent<Rigidbody>();
            Vector3 anchor = environment.transform.position;
            body.position = anchor + Vector3.right * 13f + Vector3.up * 0.55f;
            environment.Agent.transform.position = body.position;
            environment.OnAgentFixedStep(0f, false, 0f);
            Assert.That(world.CurrentChunk, Is.EqualTo(new ChunkCoord3D(1, 0)));
            Assert.That(environment.EpisodeNumber, Is.EqualTo(episode));

            body.position = anchor + Vector3.left * 13f + Vector3.up * 0.55f;
            environment.Agent.transform.position = body.position;
            environment.OnAgentFixedStep(0f, false, 0f);
            Assert.That(world.CurrentChunk, Is.EqualTo(new ChunkCoord3D(0, 0)));
            Assert.That(world.EpisodeRevisitedChunks, Is.GreaterThanOrEqualTo(1));

            ChunkCoord3D sanctuaryCoord = default;
            bool found = false;
            for (int y = 0; y < 3 && !found; y++)
            for (int x = 0; x < 3 && !found; x++)
            {
                ChunkCoord3D candidate = new(x, y);
                if (!InfiniteWorldMath3D.IsSanctuaryChunk(world.WorldSeed, candidate))
                    continue;
                sanctuaryCoord = candidate;
                found = true;
            }
            Assert.That(found, Is.True);
            body.position = anchor + new Vector3(sanctuaryCoord.x * 24f, 0.55f, sanctuaryCoord.y * 24f);
            environment.Agent.transform.position = body.position;
            environment.OnAgentFixedStep(0f, false, 0f);
            yield return null;

            ShelterZone3D shelter = environment.Shelters.FirstOrDefault();
            Assert.That(shelter, Is.Not.Null);
            body.position = shelter.transform.position;
            environment.Agent.transform.position = shelter.transform.position;
            ReserveIndicator3D indicator = shelter.gameObject.AddComponent<ReserveIndicator3D>();
            indicator.Initialize(environment.Palette);
            shelter.BindIndicator(indicator);
            Assert.That(environment.CanInteract(environment.Agent), Is.False,
                "An empty shelter should not expose a useless interaction.");
            Assert.That(environment.Agent.PickUpSupply(), Is.True);
            Assert.That(environment.CanInteract(environment.Agent), Is.True,
                "Carrying a ration next to a shelter should enable interaction.");
            Assert.That(shelter.Deposit(environment.Agent), Is.True);
            Assert.That(shelter.StoredSupplies, Is.EqualTo(1));
            Assert.That(environment.RationsAvailable, Is.EqualTo(1));
            Assert.That(indicator.DisplayedCount, Is.EqualTo(1));
            Assert.That(indicator.HasActiveGlow, Is.True);

            environment.Agent.Vitals.SetForTests(100f, 50f);
            Assert.That(environment.CanInteract(environment.Agent), Is.True,
                "A stored ration should be usable when energy is low.");
            Assert.That(shelter.TryUseReserve(environment.Agent), Is.True);
            Assert.That(environment.Agent.Vitals.CurrentEnergy, Is.EqualTo(88f).Within(0.001f));
            Assert.That(shelter.StoredSupplies, Is.Zero);
            Assert.That(environment.RationsConsumed, Is.EqualTo(1));
            Assert.That(indicator.HasActiveGlow, Is.False);
        }
    }
}
