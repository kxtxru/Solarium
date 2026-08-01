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

        [Test]
        public void InfiniteChunkCoordinatesHandleNegativeEdges()
        {
            Assert.That(InfiniteWorldMath3D.LogicalToChunk(new Vector2(11.99f, 0f), 24f),
                Is.EqualTo(new ChunkCoord3D(0, 0)));
            Assert.That(InfiniteWorldMath3D.LogicalToChunk(new Vector2(12.01f, 0f), 24f),
                Is.EqualTo(new ChunkCoord3D(1, 0)));
            Assert.That(InfiniteWorldMath3D.LogicalToChunk(new Vector2(-12.01f, 0f), 24f),
                Is.EqualTo(new ChunkCoord3D(-1, 0)));
        }

        [Test]
        public void ChunkGenerationKeysAreDeterministicAndRegionsHaveOneSanctuary()
        {
            const int seed = 49371;
            ChunkCoord3D coord = new(-7, 13);
            Assert.That(InfiniteWorldMath3D.ChunkSeed(seed, coord),
                Is.EqualTo(InfiniteWorldMath3D.ChunkSeed(seed, coord)));
            Assert.That(InfiniteWorldMath3D.BiomeFor(seed, coord),
                Is.EqualTo(InfiniteWorldMath3D.BiomeFor(seed, coord)));

            int sanctuaryCount = 0;
            for (int y = -3; y <= -1; y++)
            for (int x = 6; x <= 8; x++)
                if (InfiniteWorldMath3D.IsSanctuaryChunk(seed, new ChunkCoord3D(x, y)))
                    sanctuaryCount++;
            Assert.That(sanctuaryCount, Is.EqualTo(1));
        }

        [Test]
        public void CompactWorldSaveRoundTripsMutableChunkState()
        {
            var save = new WorldSaveData3D
            {
                worldSeed = 8128,
                elapsedWorldTime = 42.5f,
                hasLastShelter = true,
                lastShelterChunkX = 2,
                lastShelterChunkY = -3
            };
            var chunk = new ChunkState3D { x = 2, y = -3, biome = Biome3D.Forest, visited = true };
            ChunkEntityState3D shelter = chunk.GetOrCreateEntity("shelter_0");
            shelter.storedRations = 3;
            save.chunks.Add(chunk);

            string json = JsonUtility.ToJson(save);
            WorldSaveData3D restored = JsonUtility.FromJson<WorldSaveData3D>(json);
            Assert.That(restored.schemaVersion, Is.EqualTo(WorldSaveData3D.CurrentSchemaVersion));
            Assert.That(restored.worldSeed, Is.EqualTo(8128));
            Assert.That(restored.chunks[0].GetOrCreateEntity("shelter_0").storedRations, Is.EqualTo(3));
        }
    }
}
