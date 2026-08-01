using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium
{
    [Serializable]
    public struct SpawnFootprint
    {
        public Vector2 center;
        public Vector2 halfExtents;
        public string kind;

        public Rect Rect => new(center - halfExtents, halfExtents * 2f);
    }

    public sealed class SpawnValidator
    {
        private readonly List<SpawnFootprint> occupied = new();
        private Rect allowedBounds;

        public IReadOnlyList<SpawnFootprint> Occupied => occupied;

        public void Reset(Vector2 center, Vector2 arenaSize, float boundaryMargin)
        {
            Vector2 innerSize = arenaSize - Vector2.one * Mathf.Max(0f, boundaryMargin) * 2f;
            allowedBounds = new Rect(center - innerSize * 0.5f, innerSize);
            occupied.Clear();
        }

        public bool TryReserveCircle(Vector2 position, float radius, float clearance, string kind)
        {
            Vector2 extent = Vector2.one * Mathf.Max(0.01f, radius + clearance);
            return TryReserve(position, extent, kind);
        }

        public bool TryReserveRect(Vector2 position, Vector2 size, float clearance, string kind)
        {
            Vector2 extent = size * 0.5f + Vector2.one * Mathf.Max(0f, clearance);
            return TryReserve(position, extent, kind);
        }

        public bool IsFree(Vector2 position, Vector2 halfExtents)
        {
            Rect candidate = new(position - halfExtents, halfExtents * 2f);
            if (!allowedBounds.Contains(candidate.min) || !allowedBounds.Contains(candidate.max))
                return false;

            for (int i = 0; i < occupied.Count; i++)
            {
                if (candidate.Overlaps(occupied[i].Rect, true))
                    return false;
            }
            return true;
        }

        private bool TryReserve(Vector2 position, Vector2 halfExtents, string kind)
        {
            if (!IsFree(position, halfExtents))
                return false;
            occupied.Add(new SpawnFootprint
            {
                center = position,
                halfExtents = halfExtents,
                kind = kind
            });
            return true;
        }

        public static bool HasGridPath(
            Vector2 start,
            Vector2 goal,
            Vector2 center,
            Vector2 arenaSize,
            IReadOnlyList<SpawnFootprint> obstacles,
            float agentRadius = 0.5f)
        {
            const float cell = 0.5f;
            int width = Mathf.Max(1, Mathf.FloorToInt((arenaSize.x - 1f) / cell));
            int height = Mathf.Max(1, Mathf.FloorToInt((arenaSize.y - 1f) / cell));
            Vector2 min = center - new Vector2(width, height) * cell * 0.5f;
            int ToIndex(Vector2 point)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((point.x - min.x) / cell), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((point.y - min.y) / cell), 0, height - 1);
                return y * width + x;
            }

            bool[] blocked = new bool[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 point = min + new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);
                for (int i = 0; i < obstacles.Count; i++)
                {
                    if (obstacles[i].kind != "obstacle" && obstacles[i].kind != "lava")
                        continue;
                    Rect expanded = obstacles[i].Rect;
                    expanded.xMin -= agentRadius;
                    expanded.xMax += agentRadius;
                    expanded.yMin -= agentRadius;
                    expanded.yMax += agentRadius;
                    if (expanded.Contains(point))
                    {
                        blocked[y * width + x] = true;
                        break;
                    }
                }
            }

            int startIndex = ToIndex(start);
            int goalIndex = ToIndex(goal);
            if (blocked[startIndex] || blocked[goalIndex])
                return false;

            var queue = new Queue<int>();
            bool[] visited = new bool[blocked.Length];
            visited[startIndex] = true;
            queue.Enqueue(startIndex);
            int[] offsets = { 1, -1, width, -width };

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == goalIndex)
                    return true;
                int cx = current % width;
                int cy = current / width;
                for (int i = 0; i < offsets.Length; i++)
                {
                    int nx = cx + (i == 0 ? 1 : i == 1 ? -1 : 0);
                    int ny = cy + (i == 2 ? 1 : i == 3 ? -1 : 0);
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;
                    int next = current + offsets[i];
                    if (blocked[next] || visited[next])
                        continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}
