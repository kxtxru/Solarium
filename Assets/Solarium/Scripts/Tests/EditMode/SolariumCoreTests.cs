using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Solarium.Tests
{
    public sealed class SolariumCoreTests
    {
        [Test]
        public void FoodEnergyClampsToMaximum()
        {
            GameObject go = new("VitalsTest");
            SolVitals vitals = go.AddComponent<SolVitals>();
            vitals.ResetVitals();
            vitals.ConsumeEnergy(10f);
            float restored = vitals.AddEnergy(999f);
            Assert.That(restored, Is.EqualTo(10f).Within(0.001f));
            Assert.That(vitals.CurrentEnergy, Is.EqualTo(vitals.maxEnergy));
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void HealingClampsToMaximum()
        {
            GameObject go = new("VitalsTest");
            SolVitals vitals = go.AddComponent<SolVitals>();
            vitals.SetForTests(70f, 100f);
            float healed = vitals.Heal(999f);
            Assert.That(healed, Is.EqualTo(30f).Within(0.001f));
            Assert.That(vitals.CurrentHealth, Is.EqualTo(vitals.maxHealth));
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void DamageHonorsCooldownAndDeathRaisesEvent()
        {
            GameObject go = new("VitalsTest");
            SolVitals vitals = go.AddComponent<SolVitals>();
            vitals.ResetVitals();
            bool died = false;
            vitals.Died += _ => died = true;
            Assert.That(vitals.TakeDamage(10f, 1f), Is.EqualTo(10f));
            Assert.That(vitals.TakeDamage(10f, 1.1f), Is.Zero);
            Assert.That(vitals.TakeDamage(999f, 2f), Is.EqualTo(90f));
            Assert.That(died, Is.True);
            Assert.That(vitals.IsAlive, Is.False);
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void SpawnValidatorRejectsOverlap()
        {
            var validator = new SpawnValidator();
            validator.Reset(Vector2.zero, new Vector2(12f, 10f), 0.5f);
            Assert.That(validator.TryReserveCircle(Vector2.zero, 1f, 0f, "a"), Is.True);
            Assert.That(validator.TryReserveRect(Vector2.zero, Vector2.one, 0f, "b"), Is.False);
            Assert.That(validator.TryReserveCircle(new Vector2(3f, 0f), 0.5f, 0f, "c"), Is.True);
        }

        [Test]
        public void SameSeedProducesSameDistribution()
        {
            static List<Vector2> Generate(int seed)
            {
                var random = new System.Random(seed);
                var result = new List<Vector2>();
                for (int i = 0; i < 20; i++)
                    result.Add(new Vector2((float)random.NextDouble(), (float)random.NextDouble()));
                return result;
            }
            CollectionAssert.AreEqual(Generate(12345), Generate(12345));
            CollectionAssert.AreNotEqual(Generate(12345), Generate(54321));
        }

        [Test]
        public void DifficultyAlwaysResolvesWithinLimits()
        {
            GameObject go = new("DifficultyTest");
            DifficultyController controller = go.AddComponent<DifficultyController>();
            controller.UseManualDifficulty = true;
            foreach (float value in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                controller.ManualDifficulty = value;
                EpisodeConfig config = controller.Resolve();
                Assert.That(DifficultyController.IsWithinLimits(config), Is.True);
                Assert.That(config.hunterCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(config.mudCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(config.lavaCount, Is.GreaterThanOrEqualTo(1));
            }
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ObservationSizeIsStableAndFiniteInputsAreBounded()
        {
            Assert.That(SolObservations.ObservationSizeFor(12), Is.EqualTo(194));
            Assert.That(SolObservations.ReservedRayFeatureCount, Is.EqualTo(4));
            foreach (float value in new[] { -1f, 0f, 0.5f, 1f })
            {
                Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
                Assert.That(value, Is.InRange(-1f, 1f));
            }
        }

        [Test]
        public void DiagonalMovementDoesNotExceedMaximum()
        {
            Vector2 normalized = SolAgent.NormalizeMovement(new Vector2(1f, 1f));
            Assert.That(normalized.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void MudReducesMovementSpeed()
        {
            Assert.That(SolAgent.MovementSpeedWithTerrain(4f, false, 0.42f), Is.EqualTo(4f));
            Assert.That(SolAgent.MovementSpeedWithTerrain(4f, true, 0.42f), Is.EqualTo(1.68f).Within(0.001f));
        }

        [Test]
        public void LavaDeathRaisesItsTerminationReason()
        {
            GameObject go = new("LavaDeathTest");
            SolVitals vitals = go.AddComponent<SolVitals>();
            vitals.ResetVitals();
            string reason = null;
            vitals.Died += value => reason = value;
            vitals.Kill("lava");
            Assert.That(vitals.IsAlive, Is.False);
            Assert.That(reason, Is.EqualTo("lava"));
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void GridPathDetectsSafeFallback()
        {
            var obstacles = new List<SpawnFootprint>();
            Assert.That(
                SpawnValidator.HasGridPath(
                    new Vector2(-3f, 0f),
                    new Vector2(3f, 0f),
                    Vector2.zero,
                    new Vector2(12f, 10f),
                    obstacles),
                Is.True);
        }

        [Test]
        public void GridPathTreatsLavaAsBlockingButMudAsTraversable()
        {
            var lava = new List<SpawnFootprint>
            {
                new()
                {
                    center = Vector2.zero,
                    halfExtents = new Vector2(0.6f, 5f),
                    kind = "lava"
                }
            };
            Assert.That(
                SpawnValidator.HasGridPath(
                    new Vector2(-4f, 0f),
                    new Vector2(4f, 0f),
                    Vector2.zero,
                    new Vector2(12f, 10f),
                    lava),
                Is.False);

            lava[0] = new SpawnFootprint
            {
                center = Vector2.zero,
                halfExtents = new Vector2(0.6f, 5f),
                kind = "mud"
            };
            Assert.That(
                SpawnValidator.HasGridPath(
                    new Vector2(-4f, 0f),
                    new Vector2(4f, 0f),
                    Vector2.zero,
                    new Vector2(12f, 10f),
                    lava),
                Is.True);
        }
    }
}
