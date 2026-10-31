using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Rolls pre-existing road blockages once on enable; never drops a tree onto a driving car.</summary>
[DisallowMultipleComponent]
public sealed class ForestRoadBlockages : MonoBehaviour
{
    [Serializable]
    public struct Site
    {
        public Vector3 centre;
        public Vector3 forward;
    }

    public GameObject[] treePrefabs;
    public Site[] sites;
    public Transform vehicle;
    [Range(0, 1)] public float chancePerSite = .45f;
    [Min(0)] public int maximumBlockages = 2;
    [Min(1)] public float minimumSpacing = 25;
    [Min(1)] public float spawnClearance = 20;
    public bool randomSeed = true;
    public int fixedSeed = 31;
    public int SessionSeed { get; private set; }
    public int BlockageCount { get; private set; }
    private GameObject generated;

    private void OnEnable()
    {
        if (Application.isPlaying) Generate(randomSeed ? Guid.NewGuid().GetHashCode() : fixedSeed);
    }

    public void Generate(int seed)
    {
        Clear();
        SessionSeed = seed;
        if (treePrefabs == null || treePrefabs.Length == 0 || sites == null) return;
        var random = new System.Random(seed);
        var order = new List<int>();
        for (int i = 0; i < sites.Length; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int swap = order[i]; order[i] = order[j]; order[j] = swap;
        }
        var selected = new List<Vector3>();
        generated = new GameObject("Session fallen trees");
        generated.transform.SetParent(transform, false);
        foreach (int index in order)
        {
            if (BlockageCount >= maximumBlockages) break;
            var site = sites[index];
            if (vehicle != null && Vector3.Distance(vehicle.position, site.centre) < spawnClearance) continue;
            if (selected.Exists(p => Vector3.Distance(p, site.centre) < minimumSpacing)) continue;
            if (random.NextDouble() >= chancePerSite) continue;
            var prefab = treePrefabs[random.Next(treePrefabs.Length)];
            if (prefab == null) continue;
            Vector3 forward = Vector3.ProjectOnPlane(site.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) continue;
            Vector3 across = Vector3.Cross(Vector3.up, forward);
            across = Quaternion.AngleAxis((float)random.NextDouble() * 24 - 12, Vector3.up) * across;
            if (random.Next(2) == 0) across = -across;
            var instance = Instantiate(prefab, generated.transform);
            instance.name = "Fallen tree at site " + index;
            instance.AddComponent<FallenTreeObstacle>().Place(site.centre, across, 12 + (float)random.NextDouble() * 3);
            selected.Add(site.centre);
            BlockageCount++;
        }
        Physics.SyncTransforms();
    }

    public void Clear()
    {
        if (generated != null)
        {
            // Disable immediately so a same-frame regeneration cannot retain ghost colliders.
            generated.SetActive(false);
            if (Application.isPlaying) Destroy(generated); else DestroyImmediate(generated);
        }
        generated = null;
        BlockageCount = 0;
    }

    private void OnDisable() { Clear(); }
}
