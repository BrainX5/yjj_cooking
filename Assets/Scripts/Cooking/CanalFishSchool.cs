using System.Collections.Generic;
using FishAlive;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class CanalFishSchool : MonoBehaviour
{
    [SerializeField] private Transform waterRoot;
    [SerializeField] private int fishCount = 8;
    [SerializeField] private Vector3 boundsPadding = new Vector3(1.2f, 0.4f, 1.2f);
    [SerializeField] private GameObject fishPrefab;
    [SerializeField] private string fishPrefabPath = "Assets/DenysAlmaral/FishAlive/Prefabs/FishFreshwater/freshWater_guppy.prefab";
    [SerializeField] private Vector2 swimDepthBelowSurface = new Vector2(0.04f, 0.18f);
    [SerializeField] private Vector2 retargetDelayRange = new Vector2(1.6f, 4.2f);
    [SerializeField] private Vector2 schoolScaleRange = new Vector2(5.2f, 7.2f);
    [SerializeField] private bool keepFishInVisibleWater = true;
    [SerializeField] private Vector2 viewportPadding = new Vector2(0.18f, 0.2f);
    [SerializeField] private float visibleBoundsRefreshInterval = 0.8f;

    private bool spawned;
    private float nextVisibleBoundsRefreshTime;
    private Vector3 currentBoundsMin;
    private Vector3 currentBoundsMax;
    private readonly List<CanalFishWanderTarget> wanderTargets = new List<CanalFishWanderTarget>();
    private readonly List<FishMotion> fishMotions = new List<FishMotion>();
    private readonly List<Transform> fishTransforms = new List<Transform>();

    public void Configure(Transform waterTransform, int count, GameObject prefabAsset, string prefabAssetPath, Vector3 padding)
    {
        waterRoot = waterTransform;
        fishCount = Mathf.Max(1, count);
        fishPrefab = prefabAsset;
        fishPrefabPath = prefabAssetPath;
        boundsPadding = padding;
        TrySpawnFish();
    }

    private void Start()
    {
        TrySpawnFish();
    }

    private void Update()
    {
        if (!spawned || !keepFishInVisibleWater || Time.time < nextVisibleBoundsRefreshTime)
        {
            return;
        }

        nextVisibleBoundsRefreshTime = Time.time + visibleBoundsRefreshInterval;
        RefreshSwimBounds();
    }

    private void TrySpawnFish()
    {
        if (spawned || waterRoot == null)
        {
            return;
        }

        spawned = true;
        SpawnFish();
    }

    private void SpawnFish()
    {
        if (!TryCalculateSwimBounds(out currentBoundsMin, out currentBoundsMax))
        {
            return;
        }

        var spawnedCount = 0;

        for (var i = 0; i < fishCount; i++)
        {
            var fish = InstantiateFishPrefab();
            if (fish == null)
            {
                continue;
            }

            fish.name = $"CanalFish_{i + 1}";
            fish.transform.SetParent(transform, false);
            fish.transform.position = RandomPoint(currentBoundsMin, currentBoundsMax);
            fish.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            fish.transform.localScale *= Random.Range(schoolScaleRange.x, schoolScaleRange.y);
            EnsureVisibleRenderers(fish);

            var wanderTarget = new GameObject($"{fish.name}_Target").transform;
            wanderTarget.SetParent(transform, false);
            wanderTarget.position = RandomPoint(currentBoundsMin, currentBoundsMax);

            var fishMotion = fish.GetComponent<FishMotion>();
            if (fishMotion != null)
            {
                fishMotion.target = wanderTarget.gameObject;
                fishMotion.SetReachMode(ReachMode.Wander);
                fishMotion.SetAutoMotion(true);
                fishMotion.SetAvoidanceEnabled(false);
                fishMotion.EnableHardLimits(currentBoundsMin, currentBoundsMax);
            }

            var wander = fish.AddComponent<CanalFishWanderTarget>();
            wander.Configure(wanderTarget, currentBoundsMin, currentBoundsMax, retargetDelayRange);
            wanderTargets.Add(wander);
            fishTransforms.Add(fish.transform);
            fishMotions.Add(fishMotion);
            spawnedCount++;
        }

        if (spawnedCount == 0)
        {
            Debug.LogWarning("CanalFishSchool did not spawn any fish. Check prefab reference or script compilation.", this);
        }
        else
        {
            Debug.Log($"CanalFishSchool spawned {spawnedCount} fish under {name}.", this);
        }
    }

    private void RefreshSwimBounds()
    {
        if (!TryCalculateSwimBounds(out var min, out var max))
        {
            return;
        }

        currentBoundsMin = min;
        currentBoundsMax = max;

        for (var i = 0; i < wanderTargets.Count; i++)
        {
            if (wanderTargets[i] != null)
            {
                wanderTargets[i].SetBounds(currentBoundsMin, currentBoundsMax);
            }
        }

        for (var i = 0; i < fishMotions.Count; i++)
        {
            if (fishMotions[i] != null)
            {
                fishMotions[i].EnableHardLimits(currentBoundsMin, currentBoundsMax);
            }

            if (fishTransforms[i] != null)
            {
                fishTransforms[i].position = ClampToBounds(fishTransforms[i].position, currentBoundsMin, currentBoundsMax);
            }
        }
    }

    private bool TryCalculateSwimBounds(out Vector3 min, out Vector3 max)
    {
        var rendererBounds = CalculateWaterBounds();
        if (rendererBounds.size.sqrMagnitude <= 0.01f)
        {
            min = Vector3.zero;
            max = Vector3.zero;
            return false;
        }

        min = rendererBounds.min + boundsPadding;
        max = rendererBounds.max - boundsPadding;
        if (max.x <= min.x || max.y <= min.y || max.z <= min.z)
        {
            min = rendererBounds.min;
            max = rendererBounds.max;
        }

        var waterSurfaceY = rendererBounds.center.y;
        min.y = waterSurfaceY - swimDepthBelowSurface.y;
        max.y = waterSurfaceY - swimDepthBelowSurface.x;

        if (keepFishInVisibleWater &&
            TryCalculateVisibleWaterBounds(waterSurfaceY, min, max, out var visibleMin, out var visibleMax))
        {
            min = visibleMin;
            max = visibleMax;
        }

        return true;
    }

    private bool TryCalculateVisibleWaterBounds(float waterSurfaceY, Vector3 baseMin, Vector3 baseMax, out Vector3 visibleMin, out Vector3 visibleMax)
    {
        visibleMin = baseMin;
        visibleMax = baseMax;

        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return false;
        }

        var waterPlane = new Plane(Vector3.up, new Vector3(0f, waterSurfaceY, 0f));
        var samplePoints = new[]
        {
            new Vector2(viewportPadding.x, viewportPadding.y),
            new Vector2(0.5f, viewportPadding.y),
            new Vector2(1f - viewportPadding.x, viewportPadding.y),
            new Vector2(viewportPadding.x, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(1f - viewportPadding.x, 0.5f),
            new Vector2(viewportPadding.x, 1f - viewportPadding.y),
            new Vector2(0.5f, 1f - viewportPadding.y),
            new Vector2(1f - viewportPadding.x, 1f - viewportPadding.y)
        };

        var points = new List<Vector3>(samplePoints.Length);
        for (var i = 0; i < samplePoints.Length; i++)
        {
            var ray = activeCamera.ViewportPointToRay(new Vector3(samplePoints[i].x, samplePoints[i].y, 0f));
            if (waterPlane.Raycast(ray, out var enter))
            {
                points.Add(ray.GetPoint(enter));
            }
        }

        if (points.Count < 4)
        {
            return false;
        }

        var min = points[0];
        var max = points[0];
        for (var i = 1; i < points.Count; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        min.x = Mathf.Clamp(min.x, baseMin.x, baseMax.x);
        max.x = Mathf.Clamp(max.x, baseMin.x, baseMax.x);
        min.z = Mathf.Clamp(min.z, baseMin.z, baseMax.z);
        max.z = Mathf.Clamp(max.z, baseMin.z, baseMax.z);

        min.x = Mathf.Max(min.x, baseMin.x + 0.5f);
        max.x = Mathf.Min(max.x, baseMax.x - 0.5f);
        min.z = Mathf.Max(min.z, baseMin.z + 0.5f);
        max.z = Mathf.Min(max.z, baseMax.z - 0.5f);
        min.y = baseMin.y;
        max.y = baseMax.y;

        if (max.x - min.x < 1.5f || max.z - min.z < 1.5f)
        {
            return false;
        }

        visibleMin = min;
        visibleMax = max;
        return true;
    }

    private Bounds CalculateWaterBounds()
    {
        var renderers = waterRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(waterRoot.position, Vector3.one * 10f);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private Vector3 RandomPoint(Vector3 min, Vector3 max)
    {
        return new Vector3(
            Random.Range(min.x, max.x),
            Random.Range(min.y, max.y),
            Random.Range(min.z, max.z));
    }

    private GameObject InstantiateFishPrefab()
    {
        if (fishPrefab != null)
        {
            return Instantiate(fishPrefab);
        }

#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fishPrefabPath);
        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
            {
                return instance;
            }
        }
#endif
        return null;
    }

    private void EnsureVisibleRenderers(GameObject fish)
    {
        var renderers = fish.GetComponentsInChildren<Renderer>(true);
        foreach (var item in renderers)
        {
            item.enabled = true;
        }
    }

    private Vector3 ClampToBounds(Vector3 position, Vector3 min, Vector3 max)
    {
        return new Vector3(
            Mathf.Clamp(position.x, min.x, max.x),
            Mathf.Clamp(position.y, min.y, max.y),
            Mathf.Clamp(position.z, min.z, max.z));
    }
}
