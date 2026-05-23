using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MushroomPickupSystem : MonoBehaviour
{
    public static bool HasFocusedHarvestable { get; private set; }

    [Header("Scene Mushrooms")]
    [SerializeField] private string mushroomHouseName = "Mushroom House";
    [SerializeField] private string mushroomHouseDoorName = "Door";
    [SerializeField] private string bridgeName = "Bridge";
    [SerializeField] private string fencesName = "Fences";
    [SerializeField] private float harvestPatchRadius = 18f;

    [Header("Interaction")]
    [SerializeField] private float highlightDistance = 4.5f;
    [SerializeField] private float pickupDistance = 3.6f;

    [Header("UI")]
    [SerializeField] private string titleLabel = "\u8611\u83c7\u91c7\u96c6";
    [SerializeField] private string pickupPrompt = "\u63d0\u793a\uff1a\u73b0\u5728\u53ef\u4ee5\u91c7\u8611\u83c7\uff0c\u8bf7\u6309\u7a7a\u683c\u952e\u62fe\u53d6\u3002";
    [SerializeField] private int titleFontSize = 38;
    [SerializeField] private int promptFontSize = 38;
    [SerializeField] private Color promptColor = new Color(1f, 0.35f, 0.2f, 1f);

    private readonly List<HarvestableMushroom> harvestables = new List<HarvestableMushroom>();

    private Transform playerRoot;
    private HarvestableMushroom currentTarget;
    private Canvas promptCanvas;
    private Text titleText;
    private Text promptText;
    private Font uiFont;

    public void Configure(
        string houseName,
        string doorName,
        string bridgeObjectName,
        string fencesObjectName,
        int targetCount,
        GameObject[] prefabOptions)
    {
        mushroomHouseName = string.IsNullOrWhiteSpace(houseName) ? mushroomHouseName : houseName;
        mushroomHouseDoorName = string.IsNullOrWhiteSpace(doorName) ? mushroomHouseDoorName : doorName;
        bridgeName = string.IsNullOrWhiteSpace(bridgeObjectName) ? bridgeName : bridgeObjectName;
        fencesName = string.IsNullOrWhiteSpace(fencesObjectName) ? fencesName : fencesObjectName;
    }

    private void Start()
    {
        EnsurePromptUi();
        CacheSceneMushrooms();
        RefreshPrompt();
    }

    private void Update()
    {
        if (NeedsRescan())
        {
            CacheSceneMushrooms();
        }

        ResolvePlayer();
        UpdateNearestTarget();
        RefreshPrompt();

        if (currentTarget != null && Input.GetKeyDown(KeyCode.Space))
        {
            currentTarget.TryPick();
            SetCurrentTarget(null);
        }
    }

    private void OnDisable()
    {
        HasFocusedHarvestable = false;
        SetCurrentTarget(null);
        RefreshPrompt();
    }

    private bool NeedsRescan()
    {
        if (harvestables.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < harvestables.Count; i++)
        {
            if (harvestables[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheSceneMushrooms()
    {
        harvestables.Clear();

        var anchor = FindHouseOrDoorAnchor();
        if (anchor == null)
        {
            return;
        }

        var bridge = FindTransformByName(bridgeName);
        var fences = FindTransformByName(fencesName);
        var transforms = FindObjectsOfType<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            var candidate = transforms[i];
            if (!IsSceneMushroomCandidate(candidate, anchor.position, bridge, fences))
            {
                continue;
            }

            var harvestable = candidate.GetComponent<HarvestableMushroom>();
            if (harvestable == null)
            {
                harvestable = candidate.gameObject.AddComponent<HarvestableMushroom>();
            }

            EnsureCollider(candidate.gameObject);
            harvestables.Add(harvestable);
        }
    }

    private bool IsSceneMushroomCandidate(Transform candidate, Vector3 anchorPosition, Transform bridge, Transform fences)
    {
        if (candidate == null || !candidate.gameObject.scene.IsValid())
        {
            return false;
        }

        if (!candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!MatchesMushroomName(candidate.name) && !HasMushroomNamedChild(candidate))
        {
            return false;
        }

        var lowerName = candidate.name.ToLowerInvariant();
        if (lowerName.Contains("template") || lowerName.Contains("indicator") || lowerName.Contains("house"))
        {
            return false;
        }

        if (candidate.GetComponentInChildren<Renderer>(true) == null)
        {
            return false;
        }

        if (HasIgnoredAncestor(candidate))
        {
            return false;
        }

        var anchorFlat = anchorPosition;
        anchorFlat.y = 0f;
        var candidateFlat = candidate.position;
        candidateFlat.y = 0f;
        if (Vector3.Distance(anchorFlat, candidateFlat) > harvestPatchRadius)
        {
            return false;
        }

        if (!IsInsideBoundary(anchorPosition, bridge, candidate.position) ||
            !IsInsideBoundary(anchorPosition, fences, candidate.position))
        {
            return false;
        }

        return true;
    }

    private bool HasMushroomNamedChild(Transform candidate)
    {
        var children = candidate.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] != null && MatchesMushroomName(children[i].name))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesMushroomName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        var lowerName = objectName.ToLowerInvariant();
        return lowerName.Contains("mashroom") ||
               lowerName.StartsWith("mushroom ") ||
               lowerName.StartsWith("mushroom_");
    }

    private bool HasIgnoredAncestor(Transform candidate)
    {
        var current = candidate.parent;
        while (current != null)
        {
            if (current.name == "HarvestMushrooms" ||
                current.name == "HarvestDecor" ||
                current.name == "Campfire Cooking Spot")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ResolvePlayer()
    {
        if (playerRoot != null)
        {
            return;
        }

        try
        {
            var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                playerRoot = taggedPlayer.transform;
            }
        }
        catch (UnityException)
        {
        }

        if (playerRoot == null)
        {
            var controller = FindObjectOfType<CharacterController>();
            if (controller != null)
            {
                playerRoot = controller.transform;
            }
        }
    }

    private void UpdateNearestTarget()
    {
        HasFocusedHarvestable = false;

        if (playerRoot == null)
        {
            SetCurrentTarget(null);
            return;
        }

        HarvestableMushroom nearest = null;
        var nearestDistance = float.MaxValue;
        var playerPosition = playerRoot.position;
        playerPosition.y = 0f;

        for (var i = 0; i < harvestables.Count; i++)
        {
            var harvestable = harvestables[i];
            if (harvestable == null || harvestable.IsPicked || !harvestable.gameObject.activeInHierarchy)
            {
                continue;
            }

            var mushroomPosition = harvestable.transform.position;
            mushroomPosition.y = 0f;
            var distance = Vector3.Distance(playerPosition, mushroomPosition);
            if (distance <= highlightDistance && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = harvestable;
            }
        }

        if (nearest == null || nearestDistance > pickupDistance)
        {
            SetCurrentTarget(null);
            return;
        }

        SetCurrentTarget(nearest);
        HasFocusedHarvestable = true;
    }

    private void SetCurrentTarget(HarvestableMushroom target)
    {
        if (currentTarget == target)
        {
            return;
        }

        if (currentTarget != null)
        {
            currentTarget.SetSelected(false);
        }

        currentTarget = target;

        if (currentTarget != null)
        {
            currentTarget.SetSelected(true);
        }
    }

    private Transform FindHouseOrDoorAnchor()
    {
        var house = FindTransformByName(mushroomHouseName);
        if (house == null)
        {
            return null;
        }

        var children = house.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == mushroomHouseDoorName)
            {
                return children[i];
            }
        }

        return house;
    }

    private Transform FindTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        var transforms = FindObjectsOfType<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == targetName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private bool IsInsideBoundary(Vector3 origin, Transform boundary, Vector3 candidate)
    {
        if (boundary == null)
        {
            return true;
        }

        var toBoundary = boundary.position - origin;
        toBoundary.y = 0f;
        if (toBoundary.sqrMagnitude < 0.01f)
        {
            return true;
        }

        var direction = toBoundary.normalized;
        var maxDistance = Mathf.Max(0.5f, toBoundary.magnitude - 1.4f);
        var toCandidate = candidate - origin;
        toCandidate.y = 0f;
        return Vector3.Dot(toCandidate, direction) <= maxDistance;
    }

    private void EnsureCollider(GameObject target)
    {
        var colliders = target.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = true;
                colliders[i].isTrigger = false;
            }

            return;
        }

        var bounds = CalculateRendererBounds(target);
        var collider = target.AddComponent<SphereCollider>();
        collider.center = target.transform.InverseTransformPoint(bounds.center);
        collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 0.9f;
    }

    private Bounds CalculateRendererBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.one * 0.4f);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void EnsurePromptUi()
    {
        if (promptCanvas != null)
        {
            return;
        }

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "SimSun", "Arial Unicode MS" },
            promptFontSize);

        var canvasObject = new GameObject("Mushroom Harvest UI");
        promptCanvas = canvasObject.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 25;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        titleText = CreateText(
            canvasObject.transform,
            "HarvestTitle",
            new Vector2(40f, -40f),
            new Vector2(460f, 70f),
            titleFontSize,
            FontStyle.Bold,
            Color.white);
        titleText.text = titleLabel;

        promptText = CreateText(
            canvasObject.transform,
            "HarvestPrompt",
            new Vector2(40f, -100f),
            new Vector2(1280f, 180f),
            promptFontSize,
            FontStyle.Bold,
            promptColor);
        promptText.text = pickupPrompt;
    }

    private void RefreshPrompt()
    {
        if (promptCanvas == null)
        {
            return;
        }

        var visible = currentTarget != null;
        promptCanvas.enabled = visible;
        if (!visible)
        {
            return;
        }

        titleText.text = titleLabel;
        promptText.text = pickupPrompt;
    }

    private Text CreateText(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        Color color)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent);

        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        var text = textObject.AddComponent<Text>();
        text.font = uiFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
