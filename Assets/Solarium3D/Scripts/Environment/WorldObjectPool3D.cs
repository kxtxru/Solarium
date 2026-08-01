using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solarium.ThreeD
{
    public sealed class PooledWorldObject3D : MonoBehaviour
    {
        public string Key { get; private set; }
        public void SetKey(string value) => Key = value;
    }

    [DisallowMultipleComponent]
    public sealed class WorldObjectPool3D : MonoBehaviour
    {
        private readonly Dictionary<string, Stack<GameObject>> available = new();
        private Transform poolRoot;

        public int CreatedCount { get; private set; }
        public int AvailableCount { get; private set; }

        public GameObject Rent(string key, Func<GameObject> factory, Transform parent)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A pool key is required.", nameof(key));

            EnsureRoot();
            if (!available.TryGetValue(key, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                available[key] = stack;
            }

            GameObject instance = null;
            while (stack.Count > 0 && instance == null)
            {
                instance = stack.Pop();
                AvailableCount = Mathf.Max(0, AvailableCount - 1);
            }

            if (instance == null)
            {
                instance = factory();
                PooledWorldObject3D marker = instance.GetComponent<PooledWorldObject3D>()
                    ?? instance.AddComponent<PooledWorldObject3D>();
                marker.SetKey(key);
                CreatedCount++;
            }

            instance.SetActive(false);
            instance.transform.SetParent(parent, false);
            return instance;
        }

        public void ReturnEpisodeChildren(Transform episodeRoot)
        {
            if (episodeRoot == null)
                return;
            EnsureRoot();

            for (int i = episodeRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = episodeRoot.GetChild(i);
                PooledWorldObject3D marker = child.GetComponent<PooledWorldObject3D>();
                if (marker == null || string.IsNullOrWhiteSpace(marker.Key))
                {
                    child.gameObject.SetActive(false);
                    child.SetParent(poolRoot, false);
                    continue;
                }

                child.gameObject.SetActive(false);
                child.SetParent(poolRoot, false);
                if (!available.TryGetValue(marker.Key, out Stack<GameObject> stack))
                {
                    stack = new Stack<GameObject>();
                    available[marker.Key] = stack;
                }
                stack.Push(child.gameObject);
                AvailableCount++;
            }
        }

        private void EnsureRoot()
        {
            if (poolRoot != null)
                return;
            var root = new GameObject("World Object Pool");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            poolRoot = root.transform;
        }
    }
}
