using UnityEngine;
using UnityEngine.UI;

public class MushroomSoupGame : MonoBehaviour
{
    private enum SoupStage
    {
        HeatingToQuarter,
        NeedMushroom,
        HeatingToHalf,
        NeedFirstStir,
        HeatingToThreeQuarters,
        NeedSecondStir,
        HeatingToDone,
        Completed
    }

    private enum FishCatchState
    {
        Locked,
        NeedToCatch,
        Catching,
        Escaped,
        Caught
    }

    [Header("Scene References")]
    [SerializeField] private ParticleSystem fireEffect;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform waterRoot;
    [SerializeField] private Transform potVisual;
    [SerializeField] private Transform soupSurface;
    [SerializeField] private Renderer soupRenderer;
    [SerializeField] private Transform stirStick;
    [SerializeField] private Transform mushroomSpawnPoint;
    [SerializeField] private Transform mushroomTargetPoint;
    [SerializeField] private GameObject mushroomVisualPrefab;

    [Header("UI")]
    [SerializeField] private GameObject cookingPanel;
    [SerializeField] private GameObject fishingPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text promptText;
    [SerializeField] private Text fireValueText;
    [SerializeField] private Text progressText;
    [SerializeField] private Text mushroomCountText;
    [SerializeField] private Text fireSliderLabelText;
    [SerializeField] private Text progressSliderLabelText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Slider fireSlider;
    [SerializeField] private Text fishTitleText;
    [SerializeField] private Text fishPromptText;
    [SerializeField] private Text fishStatusText;
    [SerializeField] private Text fishCatchButtonText;
    [SerializeField] private Text fishSliderLabelText;
    [SerializeField] private Slider fishCatchSlider;

    [Header("Cooking Settings")]
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private float riverInteractionDistance = 3.8f;
    [SerializeField] private float fishingUnlockDistanceFromPot = 7.5f;
    [SerializeField] private float fireDecayPerSecond = 0.9f;
    [SerializeField] private float fireGainPerSpace = 0.18f;
    [SerializeField] private float maxFirePower = 1f;
    [SerializeField] private float maxCookRatePerSecond = 0.22f;
    [SerializeField] private int mushroomsNeeded = 1;
    [SerializeField] private float baseFireRate = 4f;
    [SerializeField] private float fireRateRange = 18f;
    [SerializeField] private float baseFireSpeed = 0.35f;
    [SerializeField] private float fireSpeedRange = 0.85f;
    [SerializeField] private float baseFireSize = 0.22f;
    [SerializeField] private float fireSizeRange = 0.35f;
    [SerializeField] private float fishGripGainPerPress = 0.18f;
    [SerializeField] private float fishGripDecayPerSecond = 0.23f;
    [SerializeField] private float fishGripThreshold = 0.72f;
    [SerializeField] private float fishCatchHoldDuration = 6f;
    [SerializeField] private float fishEscapePressGap = 0.45f;

    private SoupStage stage = SoupStage.HeatingToQuarter;
    private FishCatchState fishCatchState = FishCatchState.Locked;
    private float firePower;
    private float cookProgress;
    private int mushroomsAdded;
    private bool leftStirQueued;
    private bool rightStirQueued;
    private Vector3 soupBaseScale = Vector3.one;
    private Vector3 potBaseScale = Vector3.one;
    private Vector3 stirStickBaseLocalPosition = Vector3.zero;
    private Quaternion stirStickBaseLocalRotation = Quaternion.identity;
    private float stirAnimationTimer;
    private float mushroomAnimationTimer;
    private float fishGrip;
    private float fishHoldTimer;
    private float fishLastPressTime = -999f;
    private float fishStatusMessageTimer;
    private string fishStatusMessage = string.Empty;
    private Transform activeMushroomVisual;
    private bool playerInCookingRange;
    private bool playerNearRiver;
    private bool playerFarEnoughFromPotForFishing;

    public void Initialize(
        ParticleSystem sceneFireEffect,
        Transform scenePlayerTransform,
        Transform sceneWaterRoot,
        Transform scenePotVisual,
        Transform sceneSoupSurface,
        Renderer sceneSoupRenderer,
        Transform sceneStirStick,
        Transform sceneMushroomSpawnPoint,
        Transform sceneMushroomTargetPoint,
        GameObject sceneMushroomVisualPrefab,
        GameObject sceneCookingPanel,
        GameObject sceneFishingPanel,
        Text sceneTitleText,
        Text scenePromptText,
        Text sceneFireValueText,
        Text sceneProgressText,
        Text sceneMushroomCountText,
        Text sceneFireSliderLabelText,
        Text sceneProgressSliderLabelText,
        Slider sceneProgressSlider,
        Slider sceneFireSlider,
        Text sceneFishTitleText,
        Text sceneFishPromptText,
        Text sceneFishStatusText,
        Text sceneFishCatchButtonText,
        Text sceneFishSliderLabelText,
        Slider sceneFishCatchSlider)
    {
        fireEffect = sceneFireEffect;
        playerTransform = scenePlayerTransform;
        waterRoot = sceneWaterRoot;
        potVisual = scenePotVisual;
        soupSurface = sceneSoupSurface;
        soupRenderer = sceneSoupRenderer;
        stirStick = sceneStirStick;
        mushroomSpawnPoint = sceneMushroomSpawnPoint;
        mushroomTargetPoint = sceneMushroomTargetPoint;
        mushroomVisualPrefab = sceneMushroomVisualPrefab;
        cookingPanel = sceneCookingPanel;
        fishingPanel = sceneFishingPanel;
        titleText = sceneTitleText;
        promptText = scenePromptText;
        fireValueText = sceneFireValueText;
        progressText = sceneProgressText;
        mushroomCountText = sceneMushroomCountText;
        fireSliderLabelText = sceneFireSliderLabelText;
        progressSliderLabelText = sceneProgressSliderLabelText;
        progressSlider = sceneProgressSlider;
        fireSlider = sceneFireSlider;
        fishTitleText = sceneFishTitleText;
        fishPromptText = sceneFishPromptText;
        fishStatusText = sceneFishStatusText;
        fishCatchButtonText = sceneFishCatchButtonText;
        fishSliderLabelText = sceneFishSliderLabelText;
        fishCatchSlider = sceneFishCatchSlider;

        CacheVisualState();
        UpdateInteractionState();
        RefreshUI();
        UpdateFireVisuals();
    }

    private void Start()
    {
        CacheVisualState();
        UpdateInteractionState();
        RefreshUI();
        UpdateFireVisuals();
    }

    private void Update()
    {
        UpdateInteractionState();

        if (playerNearRiver && IsFishCatchUnlocked())
        {
            ReadFishInput();
        }
        else if (playerInCookingRange)
        {
            ReadCookingInput();
        }

        UpdateFirePower();
        UpdateCookingProgress();
        UpdateFishCatch();
        UpdateAnimations();
        UpdateSoupSurface();
        UpdateFireVisuals();
        RefreshUI();
    }

    private void CacheVisualState()
    {
        if (soupSurface != null)
        {
            soupBaseScale = soupSurface.localScale;
        }

        if (potVisual != null)
        {
            potBaseScale = potVisual.localScale;
        }

        if (stirStick != null)
        {
            stirStickBaseLocalPosition = stirStick.localPosition;
            stirStickBaseLocalRotation = stirStick.localRotation;
        }
    }

    private void ReadCookingInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            firePower = Mathf.Clamp(firePower + fireGainPerSpace, 0f, maxFirePower);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            PlayMushroomAnimation();
            TryAddMushroom();
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            leftStirQueued = true;
            TryStir();
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            rightStirQueued = true;
            TryStir();
        }
    }

    private void ReadFishInput()
    {
        if (!Input.GetKeyDown(KeyCode.Space) || fishCatchState == FishCatchState.Caught)
        {
            return;
        }

        if (fishCatchState == FishCatchState.Escaped)
        {
            fishStatusMessage = "重新准备，猛按空格键把鱼抓稳。";
            fishStatusMessageTimer = 1.5f;
            fishCatchState = FishCatchState.NeedToCatch;
        }

        if (fishCatchState == FishCatchState.NeedToCatch)
        {
            fishCatchState = FishCatchState.Catching;
            fishGrip = 0.22f;
            fishHoldTimer = 0f;
            fishStatusMessage = "抓到了！快速连续按空格，别让它逃走。";
            fishStatusMessageTimer = 1.2f;
        }
        else if (fishCatchState == FishCatchState.Catching)
        {
            fishGrip = Mathf.Clamp01(fishGrip + fishGripGainPerPress);
        }

        fishLastPressTime = Time.time;
    }

    private void UpdateFirePower()
    {
        if (!playerInCookingRange)
        {
            return;
        }

        firePower = Mathf.MoveTowards(firePower, 0f, fireDecayPerSecond * Time.deltaTime);
    }

    private void UpdateCookingProgress()
    {
        if (!playerInCookingRange)
        {
            return;
        }

        if (stage == SoupStage.NeedMushroom ||
            stage == SoupStage.NeedFirstStir ||
            stage == SoupStage.NeedSecondStir ||
            stage == SoupStage.Completed)
        {
            return;
        }

        cookProgress = Mathf.Clamp01(cookProgress + firePower * maxCookRatePerSecond * Time.deltaTime);

        if (stage == SoupStage.HeatingToQuarter && cookProgress >= 0.25f)
        {
            cookProgress = 0.25f;
            stage = SoupStage.NeedMushroom;
            return;
        }

        if (stage == SoupStage.HeatingToHalf && cookProgress >= 0.5f)
        {
            cookProgress = 0.5f;
            stage = SoupStage.NeedFirstStir;
            return;
        }

        if (stage == SoupStage.HeatingToThreeQuarters && cookProgress >= 0.75f)
        {
            cookProgress = 0.75f;
            stage = SoupStage.NeedSecondStir;
            return;
        }

        if (stage == SoupStage.HeatingToDone && cookProgress >= 1f)
        {
            cookProgress = 1f;
            stage = SoupStage.Completed;
            fishCatchState = FishCatchState.NeedToCatch;
            TintSoup(new Color(0.84f, 0.77f, 0.56f, 1f));
        }
    }

    private void TryAddMushroom()
    {
        if (stage != SoupStage.NeedMushroom || mushroomsAdded >= mushroomsNeeded)
        {
            return;
        }

        mushroomsAdded++;
        TintSoup(new Color(0.69f, 0.62f, 0.37f, 1f));

        if (mushroomsAdded >= mushroomsNeeded)
        {
            stage = SoupStage.HeatingToHalf;
        }
    }

    private void TryStir()
    {
        if (!leftStirQueued || !rightStirQueued)
        {
            return;
        }

        leftStirQueued = false;
        rightStirQueued = false;

        PlayStirAnimation();

        if (stage == SoupStage.NeedFirstStir)
        {
            stage = SoupStage.HeatingToThreeQuarters;
            NudgeSoupSurface(1.18f);
            TintSoup(new Color(0.76f, 0.68f, 0.43f, 1f));
            return;
        }

        if (stage == SoupStage.NeedSecondStir)
        {
            stage = SoupStage.HeatingToDone;
            NudgeSoupSurface(1.22f);
            TintSoup(new Color(0.8f, 0.73f, 0.5f, 1f));
        }
    }

    private void UpdateAnimations()
    {
        UpdateStirAnimation();
        UpdateMushroomAnimation();
    }

    private void UpdateStirAnimation()
    {
        if (stirStick == null)
        {
            return;
        }

        if (stirAnimationTimer > 0f)
        {
            stirAnimationTimer -= Time.deltaTime;
            var normalized = 1f - Mathf.Clamp01(stirAnimationTimer / 1.1f);
            var angle = normalized * Mathf.PI * 3.2f;
            var swirlX = Mathf.Sin(angle) * 0.09f;
            var swirlZ = Mathf.Cos(angle) * 0.09f;

            stirStick.localPosition = stirStickBaseLocalPosition + new Vector3(swirlX, -0.08f, swirlZ);
            stirStick.localRotation = stirStickBaseLocalRotation * Quaternion.Euler(0f, normalized * 420f, -26f + Mathf.Sin(angle * 0.5f) * 8f);

            if (soupSurface != null)
            {
                var wave = 1.05f + Mathf.Abs(Mathf.Sin(angle)) * 0.16f;
                soupSurface.localScale = new Vector3(
                    soupBaseScale.x * wave,
                    soupBaseScale.y,
                    soupBaseScale.z * wave);
            }
        }
        else
        {
            stirStick.localPosition = Vector3.Lerp(stirStick.localPosition, stirStickBaseLocalPosition, Time.deltaTime * 7f);
            stirStick.localRotation = Quaternion.Slerp(stirStick.localRotation, stirStickBaseLocalRotation, Time.deltaTime * 7f);
        }
    }

    private void UpdateMushroomAnimation()
    {
        if (activeMushroomVisual == null)
        {
            return;
        }

        mushroomAnimationTimer += Time.deltaTime;
        const float duration = 0.8f;
        var normalized = Mathf.Clamp01(mushroomAnimationTimer / duration);

        if (mushroomSpawnPoint != null && mushroomTargetPoint != null)
        {
            var start = mushroomSpawnPoint.position;
            var end = mushroomTargetPoint.position;
            var height = Mathf.Sin(normalized * Mathf.PI) * 1.2f;
            activeMushroomVisual.position = Vector3.Lerp(start, end, normalized) + Vector3.up * height;
            activeMushroomVisual.Rotate(new Vector3(180f, 260f, 140f) * Time.deltaTime, Space.Self);
        }

        if (normalized >= 1f)
        {
            Destroy(activeMushroomVisual.gameObject);
            activeMushroomVisual = null;
            mushroomAnimationTimer = 0f;
            NudgeSoupSurface(1.16f);
        }
    }

    private void PlayMushroomAnimation()
    {
        if (mushroomSpawnPoint == null || mushroomTargetPoint == null)
        {
            return;
        }

        if (activeMushroomVisual != null)
        {
            Destroy(activeMushroomVisual.gameObject);
        }

        GameObject mushroomObject;
        if (mushroomVisualPrefab != null)
        {
            mushroomObject = Instantiate(mushroomVisualPrefab);
        }
        else
        {
            mushroomObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var fallbackRenderer = mushroomObject.GetComponent<Renderer>();
            if (fallbackRenderer != null)
            {
                fallbackRenderer.material.color = new Color(0.9f, 0.82f, 0.62f, 1f);
            }
        }

        mushroomObject.name = "DroppingMushroom";
        mushroomObject.SetActive(true);
        mushroomObject.transform.position = mushroomSpawnPoint.position;
        mushroomObject.transform.rotation = Quaternion.Euler(-15f, 0f, 25f);
        mushroomObject.transform.localScale = Vector3.one * 0.34f;
        RemoveAllColliders(mushroomObject);

        activeMushroomVisual = mushroomObject.transform;
        mushroomAnimationTimer = 0f;
    }

    private void PlayStirAnimation()
    {
        stirAnimationTimer = 1.1f;
    }

    private void UpdateSoupSurface()
    {
        if (soupSurface == null)
        {
            return;
        }

        soupSurface.localScale = Vector3.Lerp(soupSurface.localScale, soupBaseScale, Time.deltaTime * 5f);
    }

    private void UpdateFireVisuals()
    {
        if (fireEffect != null)
        {
            var emission = fireEffect.emission;
            emission.rateOverTime = baseFireRate + firePower * fireRateRange;

            var main = fireEffect.main;
            main.startSpeed = baseFireSpeed + firePower * fireSpeedRange;
            main.startSize = baseFireSize + firePower * fireSizeRange;

            if (!fireEffect.isPlaying)
            {
                fireEffect.Play();
            }
        }

        if (potVisual != null)
        {
            var targetScale = potBaseScale * (1f + firePower * 0.05f);
            potVisual.localScale = Vector3.Lerp(potVisual.localScale, targetScale, Time.deltaTime * 4f);
        }
    }

    private void RefreshUI()
    {
        var showCookingPanel = playerInCookingRange;
        var showFishingPanel = playerNearRiver && playerFarEnoughFromPotForFishing && IsFishCatchUnlocked();

        if (cookingPanel != null)
        {
            cookingPanel.SetActive(showCookingPanel);
        }

        if (fishingPanel != null)
        {
            fishingPanel.SetActive(showFishingPanel);
        }

        if (showCookingPanel)
        {
            RefreshCookingUI();
        }

        if (showFishingPanel)
        {
            RefreshFishingUI();
        }
    }

    private void RefreshCookingUI()
    {
        if (titleText != null)
        {
            titleText.text = "蘑菇汤";
        }

        if (promptText != null)
        {
            promptText.text = GetPrompt();
            promptText.color = stage == SoupStage.NeedMushroom || stage == SoupStage.NeedFirstStir || stage == SoupStage.NeedSecondStir
                ? new Color(1f, 0.35f, 0.2f, 1f)
                : Color.white;
        }

        if (fireValueText != null)
        {
            fireValueText.text = $"当前火力: {firePower:0.00}";
        }

        if (progressText != null)
        {
            progressText.text = $"当前煮饭进度: {(cookProgress * 100f):0}%";
        }

        if (mushroomCountText != null)
        {
            mushroomCountText.text = $"已加蘑菇: {mushroomsAdded}/{mushroomsNeeded}";
        }

        if (fireSliderLabelText != null)
        {
            fireSliderLabelText.text = "火力值";
        }

        if (progressSliderLabelText != null)
        {
            progressSliderLabelText.text = "煮饭进度";
        }

        if (progressSlider != null)
        {
            progressSlider.value = cookProgress;
        }

        if (fireSlider != null)
        {
            fireSlider.value = maxFirePower <= 0f ? 0f : firePower / maxFirePower;
        }
    }

    private void RefreshFishingUI()
    {
        if (fishTitleText != null)
        {
            fishTitleText.text = "捉鱼";
        }

        if (fishPromptText != null)
        {
            fishPromptText.text = GetFishPrompt();
        }

        if (fishStatusText != null)
        {
            fishStatusText.text = GetFishStatusText();
        }

        if (fishCatchButtonText != null)
        {
            fishCatchButtonText.text = fishCatchState == FishCatchState.Catching
                ? "[ 空格 ] 持续猛按，把鱼抓稳"
                : "[ 空格 ] 开始捉鱼";
        }

        if (fishSliderLabelText != null)
        {
            fishSliderLabelText.text = "抓紧程度";
        }

        if (fishCatchSlider != null)
        {
            fishCatchSlider.value = fishGrip;
        }
    }

    private string GetPrompt()
    {
        switch (stage)
        {
            case SoupStage.HeatingToQuarter:
                return "快速按空格键升高火力，把蘑菇汤煮到 1/4 进度。";
            case SoupStage.NeedMushroom:
                return "提示：现在需要加蘑菇，请按上键或下键把蘑菇丢进锅里。";
            case SoupStage.HeatingToHalf:
                return "蘑菇已经下锅，继续加热，把进度推进到 2/4。";
            case SoupStage.NeedFirstStir:
                return "提示：现在需要第一次搅拌，请左右键各按一次。";
            case SoupStage.HeatingToThreeQuarters:
                return "第一次搅拌完成，继续加热，把进度推进到 3/4。";
            case SoupStage.NeedSecondStir:
                return "提示：现在需要第二次搅拌，请左右键各按一次。";
            case SoupStage.HeatingToDone:
                return "最后收汁加热，马上就完成了。";
            case SoupStage.Completed:
                return "蘑菇汤制作完成。下一道菜是鱼汤，请先去河边捉鱼。";
            default:
                return string.Empty;
        }
    }

    private void UpdateFishCatch()
    {
        if (!IsFishCatchUnlocked())
        {
            return;
        }

        if (fishStatusMessageTimer > 0f)
        {
            fishStatusMessageTimer -= Time.deltaTime;
            if (fishStatusMessageTimer <= 0f)
            {
                fishStatusMessage = string.Empty;
            }
        }

        if (!playerNearRiver || fishCatchState != FishCatchState.Catching)
        {
            return;
        }

        fishGrip = Mathf.Clamp01(fishGrip - fishGripDecayPerSecond * Time.deltaTime);

        if (fishGrip >= fishGripThreshold)
        {
            fishHoldTimer += Time.deltaTime;
        }
        else
        {
            fishHoldTimer = 0f;
        }

        if (fishHoldTimer >= fishCatchHoldDuration)
        {
            fishCatchState = FishCatchState.Caught;
            fishGrip = 1f;
            fishStatusMessage = "恭喜你捉到一只鱼！接下来可以准备煮鱼汤了。";
            fishStatusMessageTimer = 4f;
            return;
        }

        if (Time.time - fishLastPressTime > fishEscapePressGap || fishGrip <= 0.02f)
        {
            fishCatchState = FishCatchState.Escaped;
            fishGrip = 0f;
            fishHoldTimer = 0f;
            fishStatusMessage = "你按得太慢了，鱼逃走了！";
            fishStatusMessageTimer = 2.4f;
        }
    }

    private string GetFishPrompt()
    {
        switch (fishCatchState)
        {
            case FishCatchState.NeedToCatch:
                return "鱼汤的第一步是去河边捉鱼。按空格开始，然后快速连续按 6 秒。";
            case FishCatchState.Catching:
                return "按得越快，鱼抓得越紧。一旦慢下来，鱼就会挣脱。";
            case FishCatchState.Escaped:
                return "鱼刺溜走了，准备好后可以再试一次。";
            case FishCatchState.Caught:
                return "你已经捉到了鱼，鱼汤的食材到手了。";
            default:
                return string.Empty;
        }
    }

    private string GetFishStatusText()
    {
        if (!string.IsNullOrEmpty(fishStatusMessage))
        {
            return fishStatusMessage;
        }

        if (fishCatchState == FishCatchState.Catching)
        {
            return $"鱼还在挣扎！高速连按已坚持: {fishHoldTimer:0.0}/{fishCatchHoldDuration:0.0} 秒";
        }

        if (fishCatchState == FishCatchState.Caught)
        {
            return "恭喜你捉到一只鱼。";
        }

        return "走到河边后，就会出现捉鱼按钮和进度条。";
    }

    private bool IsFishCatchUnlocked()
    {
        return fishCatchState != FishCatchState.Locked;
    }

    private void TintSoup(Color targetColor)
    {
        if (soupRenderer == null)
        {
            return;
        }

        soupRenderer.material.color = targetColor;
    }

    private void NudgeSoupSurface(float multiplier)
    {
        if (soupSurface == null)
        {
            return;
        }

        soupSurface.localScale = new Vector3(
            soupBaseScale.x * multiplier,
            soupBaseScale.y,
            soupBaseScale.z * multiplier);
    }

    private void RemoveAllColliders(GameObject target)
    {
        var colliders = target.GetComponentsInChildren<Collider>(true);
        foreach (var collider in colliders)
        {
            Destroy(collider);
        }
    }

    private void UpdateInteractionState()
    {
        if (playerTransform == null)
        {
            playerTransform = FindPlayerTransform();
        }

        if (waterRoot == null)
        {
            waterRoot = FindWaterTransform();
        }

        if (playerTransform == null)
        {
            playerInCookingRange = false;
            playerNearRiver = false;
            playerFarEnoughFromPotForFishing = false;
            return;
        }

        playerInCookingRange = false;
        playerNearRiver = false;
        playerFarEnoughFromPotForFishing = true;

        if (potVisual != null)
        {
            var playerPosition = playerTransform.position;
            var potPosition = potVisual.position;
            playerPosition.y = 0f;
            potPosition.y = 0f;
            var distanceToPot = Vector3.Distance(playerPosition, potPosition);
            playerInCookingRange = distanceToPot <= interactionDistance;
            playerFarEnoughFromPotForFishing = distanceToPot >= fishingUnlockDistanceFromPot;
        }

        if (waterRoot != null)
        {
            playerNearRiver = IsNearWater(playerTransform.position);
        }
    }

    private Transform FindPlayerTransform()
    {
        try
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                return playerObject.transform;
            }
        }
        catch (UnityException)
        {
        }

        var controller = FindObjectOfType<CharacterController>();
        return controller != null ? controller.transform : null;
    }

    private Transform FindWaterTransform()
    {
        var direct = GameObject.Find("Water");
        if (direct != null)
        {
            return direct.transform;
        }

        var renderers = FindObjectsOfType<Renderer>(true);
        foreach (var item in renderers)
        {
            if (item != null && item.name.ToLowerInvariant().Contains("water"))
            {
                return item.transform;
            }
        }

        return null;
    }

    private bool IsNearWater(Vector3 playerPosition)
    {
        var bounds = CalculateBounds(waterRoot.gameObject);
        var closestPoint = bounds.ClosestPoint(playerPosition);
        closestPoint.y = playerPosition.y;
        return Vector3.Distance(playerPosition, closestPoint) <= riverInteractionDistance;
    }

    private Bounds CalculateBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.one * 3f);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}
