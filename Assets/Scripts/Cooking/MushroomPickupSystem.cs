using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MushroomPickupSystem : MonoBehaviour
{
    public static bool HasFocusedHarvestable { get; private set; }
    public static MushroomPickupSystem Instance { get; private set; }

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
    [SerializeField] private string platformPickupPrompt = "\u63d0\u793a\uff1a\u9760\u8fd1\u540e\u70b9\u5934\u4e00\u6b21\uff0c\u6216\u8005\u6309\u7a7a\u683c\u952e\u62fe\u53d6\u8611\u83c7\u3002";
    [SerializeField] private string gatherIntroPrompt = "\u6e38\u620f\u5f00\u59cb\u5148\u53bb\u91c7\u8611\u83c7\uff0c\u81f3\u5c11\u91c7\u4e09\u4e2a\uff0c\u518d\u56de\u9505\u8fb9\u6309\u4e0a\u4e0b\u952e\u628a\u8611\u83c7\u653e\u8fdb\u9505\u91cc\u3002";
    [SerializeField] private string gatherReadyPrompt = "\u8611\u83c7\u5df2\u7ecf\u591f\u4e86\uff0c\u56de\u9505\u8fb9\u6309\u4e0a\u4e0b\u952e\u8fde\u7eed\u653e\u8611\u83c7\uff0c\u81f3\u5c11\u653e\u4e09\u4e2a\u3002";
    [SerializeField] private int titleFontSize = 38;
    [SerializeField] private int promptFontSize = 32;
    [SerializeField] private Color promptColor = new Color(1f, 0.98f, 0.93f, 1f);
    [SerializeField] private Vector2 dialogPosition = new Vector2(-360f, -48f);
    [SerializeField] private Vector2 dialogSize = new Vector2(720f, 260f);
    [SerializeField] private Color dialogFallbackColor = new Color(0.79f, 0.35f, 0.2f, 0.96f);
    [SerializeField] private string dialogSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/Panels_SpriteSheet.png";
    [SerializeField] private string dialogSpriteName = "Panels_SpriteSheet_1";
    [SerializeField] private string iconSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/UI_SpriteSheet.png";
    [SerializeField] private string iconSpriteName = "UI_SpriteSheet_17";

    private readonly List<HarvestableMushroom> harvestables = new List<HarvestableMushroom>();

    private Transform playerRoot;
    private HarvestableMushroom currentTarget;
    private Canvas promptCanvas;
    private Image promptBackgroundImage;
    private Text titleText;
    private Text promptText;
    private Font uiFont;
    private Sprite dialogSprite;
    private Sprite iconSprite;
    private bool externalPromptVisible;
    private string externalPromptText = string.Empty;

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
        Instance = this;
        EnsurePromptUi();
        CacheSceneMushrooms();
        RefreshPrompt();
    }

    private void Update()
    {
        if (ForestStoryIntroOverlay.IsBlockingInput)
        {
            SetCurrentTarget(null);
            RefreshPrompt();
            return;
        }

        if (NeedsRescan())
        {
            CacheSceneMushrooms();
        }

        ResolvePlayer();
        UpdateNearestTarget();
        RefreshPrompt();

        if (currentTarget == null)
        {
            return;
        }

        if (TryHandlePlatformPickup() || Input.GetKeyDown(KeyCode.Space))
        {
            PickCurrentTarget();
        }
    }

    private void OnDisable()
    {
        HasFocusedHarvestable = false;
        SetCurrentTarget(null);
        RefreshPrompt();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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

    private bool TryHandlePlatformPickup()
    {
        var gameplayInput = HybridBciGameplayInput.Instance;
        if (gameplayInput == null || !gameplayInput.HasLiveConnection)
        {
            return false;
        }

        return gameplayInput.ConsumeVerticalGesture();
    }

    private void PickCurrentTarget()
    {
        if (currentTarget == null)
        {
            return;
        }

        if (!currentTarget.TryPick())
        {
            return;
        }

        MushroomSoupGame.Instance?.RegisterHarvestedMushroom();
        MiniProgramGameDataManager.Instance?.RecordHarvestedMushroom();
        SetCurrentTarget(null);
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

        var dialogObject = new GameObject("HarvestDialog");
        dialogObject.transform.SetParent(canvasObject.transform, false);

        var dialogRect = dialogObject.AddComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 1f);
        dialogRect.anchorMax = new Vector2(0.5f, 1f);
        dialogRect.pivot = new Vector2(0f, 1f);
        dialogRect.anchoredPosition = dialogPosition;
        dialogRect.sizeDelta = dialogSize;

        promptBackgroundImage = dialogObject.AddComponent<Image>();
        promptBackgroundImage.type = Image.Type.Simple;
        promptBackgroundImage.preserveAspect = false;
        LoadDialogSprite();
        if (dialogSprite != null)
        {
            promptBackgroundImage.sprite = dialogSprite;
            promptBackgroundImage.color = Color.white;
        }
        else
        {
            promptBackgroundImage.color = dialogFallbackColor;
        }

        LoadIconSprite();
        if (iconSprite != null)
        {
            var iconObject = new GameObject("HarvestDialogIcon");
            iconObject.transform.SetParent(dialogObject.transform, false);

            var iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(14f, -10f);
            iconRect.sizeDelta = new Vector2(88f, 88f);

            var iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }

        titleText = CreateText(
            dialogObject.transform,
            "HarvestTitle",
            new Vector2(96f, -24f),
            new Vector2(dialogSize.x - 148f, 44f),
            titleFontSize,
            FontStyle.Bold,
            new Color(1f, 0.98f, 0.93f, 1f),
            TextAnchor.MiddleCenter);
        titleText.text = titleLabel;

        promptText = CreateText(
            dialogObject.transform,
            "HarvestPrompt",
            new Vector2(56f, -86f),
            new Vector2(dialogSize.x - 112f, dialogSize.y - 116f),
            promptFontSize,
            FontStyle.Bold,
            promptColor,
            TextAnchor.UpperLeft);
        promptText.text = pickupPrompt;
    }

    private void RefreshPrompt()
    {
        if (promptCanvas == null)
        {
            return;
        }

        var visible = !ForestStoryIntroOverlay.IsBlockingInput && (externalPromptVisible || currentTarget != null);
        promptCanvas.enabled = visible;
        if (!visible)
        {
            return;
        }

        titleText.text = titleLabel;
        if (externalPromptVisible)
        {
            promptText.text = externalPromptText;
            return;
        }

        var gameplayInput = HybridBciGameplayInput.Instance;
        promptText.text = gameplayInput != null && gameplayInput.HasLiveConnection
            ? platformPickupPrompt
            : pickupPrompt;
    }

    public void ShowGatherIntroPrompt(bool hasEnoughMushrooms)
    {
        externalPromptVisible = true;
        externalPromptText = hasEnoughMushrooms ? gatherReadyPrompt : gatherIntroPrompt;
        RefreshPrompt();
    }

    public void HideGatherIntroPrompt()
    {
        if (!externalPromptVisible)
        {
            return;
        }

        externalPromptVisible = false;
        externalPromptText = string.Empty;
        RefreshPrompt();
    }

    public bool IsShowingGatherIntroPrompt()
    {
        return externalPromptVisible;
    }

    private void LoadDialogSprite()
    {
        if (dialogSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(dialogSpriteSheetPath);
        for (var i = 0; i < assets.Length; i++)
        {
            var sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == dialogSpriteName)
            {
                dialogSprite = sprite;
                return;
            }
        }
#endif
    }

    private void LoadIconSprite()
    {
        if (iconSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(iconSpriteSheetPath);
        for (var i = 0; i < assets.Length; i++)
        {
            var sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == iconSpriteName)
            {
                iconSprite = sprite;
                return;
            }
        }
#endif
    }

    private Text CreateText(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

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
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
