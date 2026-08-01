using NUnit.Framework;
using UnityEngine;

namespace Solarium.ThreeD.Tests
{
    public sealed class Solarium3DCoreTests
    {
        [Test]
        public void PlanarConversionRoundTripsExactly()
        {
            Vector2 planar = new(-7.25f, 12.5f);
            Vector3 world = Planar3D.ToWorld(planar, 0.55f);
            Assert.That(world, Is.EqualTo(new Vector3(-7.25f, 0.55f, 12.5f)));
            Assert.That(Planar3D.ToPlanar(world), Is.EqualTo(planar));
        }

        [Test]
        public void ObservationAndActionContractsRemainCompatible()
        {
            Assert.That(SolObservations3D.InternalObservationCount, Is.EqualTo(26));
            Assert.That(SolObservations3D.ValuesPerRay, Is.EqualTo(14));
            Assert.That(SolObservations3D.ReservedRayFeatureCount, Is.EqualTo(4));
            Assert.That(SolObservations3D.ObservationSizeFor(12), Is.EqualTo(194));
        }

        [Test]
        public void DiagonalMovementRemainsBounded()
        {
            Vector2 movement = SolAgent3D.NormalizeMovement(new Vector2(1f, 1f));
            Assert.That(movement.magnitude, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void TerrainSpeedMatchesTheTwoDimensionalPolicySemantics()
        {
            Assert.That(SolAgent3D.MovementSpeedWithTerrain(4f, false, 0.42f), Is.EqualTo(4f));
            Assert.That(SolAgent3D.MovementSpeedWithTerrain(4f, true, 0.42f), Is.EqualTo(1.68f).Within(0.001f));
        }

        [Test]
        public void PoolReusesReturnedObjects()
        {
            var owner = new GameObject("Pool Test");
            WorldObjectPool3D pool = owner.AddComponent<WorldObjectPool3D>();
            var episode = new GameObject("Episode").transform;
            episode.SetParent(owner.transform);
            GameObject first = pool.Rent("food", () => new GameObject("Food"), episode);
            first.SetActive(true);
            pool.ReturnEpisodeChildren(episode);
            GameObject second = pool.Rent("food", () => new GameObject("Unexpected"), episode);
            Assert.That(second, Is.SameAs(first));
            Assert.That(pool.CreatedCount, Is.EqualTo(1));
            Object.DestroyImmediate(owner);
        }
    }
}
