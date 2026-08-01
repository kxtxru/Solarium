using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium.ThreeD
{
    public enum WorldMode3D
    {
        BoundedArena,
        InfinitePersistent,
        InfiniteTraining
    }

    public enum Biome3D
    {
        Meadow,
        Forest,
        Wetland,
        Volcanic
    }

    [Serializable]
    public struct ChunkCoord3D : IEquatable<ChunkCoord3D>
    {
        public int x;
        public int y;

        public ChunkCoord3D(int xValue, int yValue)
        {
            x = xValue;
            y = yValue;
        }

        public bool Equals(ChunkCoord3D other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is ChunkCoord3D other && Equals(other);
        public override int GetHashCode() => unchecked((x * 397) ^ y);
        public override string ToString() => $"{x},{y}";

        public static bool operator ==(ChunkCoord3D left, ChunkCoord3D right) => left.Equals(right);
        public static bool operator !=(ChunkCoord3D left, ChunkCoord3D right) => !left.Equals(right);
        public static ChunkCoord3D operator +(ChunkCoord3D left, ChunkCoord3D right) =>
            new(left.x + right.x, left.y + right.y);
        public static ChunkCoord3D operator -(ChunkCoord3D left, ChunkCoord3D right) =>
            new(left.x - right.x, left.y - right.y);
    }

    [Serializable]
    public sealed class ChunkEntityState3D
    {
        public string id;
        public bool available = true;
        public float readyAtWorldTime;
        public int storedRations;
        public bool visited;
    }

    [Serializable]
    public sealed class ChunkState3D
    {
        public int x;
        public int y;
        public Biome3D biome;
        public bool visited;
        public List<ChunkEntityState3D> entities = new();

        public ChunkCoord3D Coord => new(x, y);

        public ChunkEntityState3D GetOrCreateEntity(string id, bool initiallyAvailable = true)
        {
            entities ??= new List<ChunkEntityState3D>();
            for (int i = 0; i < entities.Count; i++)
            {
                ChunkEntityState3D existing = entities[i];
                if (existing != null && existing.id == id)
                    return existing;
            }

            var created = new ChunkEntityState3D
            {
                id = id,
                available = initiallyAvailable
            };
            entities.Add(created);
            return created;
        }
    }

    [Serializable]
    public sealed class WorldSaveData3D
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int worldSeed;
        public float elapsedWorldTime;
        public bool hasLastShelter;
        public int lastShelterChunkX;
        public int lastShelterChunkY;
        public string lastShelterEntityId;
        public float lastShelterLocalX;
        public float lastShelterLocalY;
        public List<ChunkState3D> chunks = new();
    }

    public static class InfiniteWorldMath3D
    {
        public const float DefaultChunkSize = 24f;

        public static ChunkCoord3D LogicalToChunk(Vector2 logicalPosition, float chunkSize)
        {
            float size = Mathf.Max(1f, chunkSize);
            float half = size * 0.5f;
            return new ChunkCoord3D(
                Mathf.FloorToInt((logicalPosition.x + half) / size),
                Mathf.FloorToInt((logicalPosition.y + half) / size));
        }

        public static Vector2 ChunkCenter(ChunkCoord3D coord, float chunkSize) =>
            new(coord.x * chunkSize, coord.y * chunkSize);

        public static int ChunkSeed(int worldSeed, ChunkCoord3D coord)
        {
            unchecked
            {
                uint value = (uint)worldSeed;
                value ^= (uint)coord.x * 0x9E3779B9u;
                value = (value << 13) | (value >> 19);
                value ^= (uint)coord.y * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                return (int)(value & 0x7FFFFFFF);
            }
        }

        public static Biome3D BiomeFor(int worldSeed, ChunkCoord3D coord)
        {
            int regionX = FloorDiv(coord.x, 3);
            int regionY = FloorDiv(coord.y, 3);
            int hash = ChunkSeed(worldSeed ^ 0x4B1D5A77, new ChunkCoord3D(regionX, regionY));
            return (Biome3D)(hash % 4);
        }

        public static bool IsSanctuaryChunk(int worldSeed, ChunkCoord3D coord)
        {
            int regionX = FloorDiv(coord.x, 3);
            int regionY = FloorDiv(coord.y, 3);
            int hash = ChunkSeed(worldSeed ^ 0x2194A6D3, new ChunkCoord3D(regionX, regionY));
            int localX = PositiveMod(hash, 3);
            int localY = PositiveMod(hash / 3, 3);
            return PositiveMod(coord.x, 3) == localX && PositiveMod(coord.y, 3) == localY;
        }

        public static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        public static int PositiveMod(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
