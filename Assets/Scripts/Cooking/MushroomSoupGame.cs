using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MushroomSoupGame : MonoBehaviour
{
    public static MushroomSoupGame Instance { get; private set; }

    private enum DishPhase
    {
        MushroomSoup,
        FishCatch,
        FriedFish,
        Completed
    }

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

    private enum FriedFishStage
    {
        Inactive,
        ReturnToFire,
        HeatingToHalf,
        NeedFlip,
        HeatingToThreeQuarters,
        NeedSeasoning,
        HeatingToDone,
        Completed
    }

    [Header("Scene References")]
    [SerializeField] private ParticleSystem fireEffect;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform waterRoot;
    [SerializeField] private Transform soupPotVisual;
    [SerializeField] private Transform fryPotVisual;
    [SerializeField] private Transform activePotVisual;
    [SerializeField] private Transform soupSurface;
    [SerializeField] private Renderer soupRenderer;
    [SerializeField] private Transform stirStick;
    [SerializeField] private Transform mushroomSpawnPoint;
    [SerializeField] private Transform mushroomTargetPoint;
    [SerializeField] private GameObject mushroomVisualPrefab;
    [SerializeField] private Transform fryFishVisual;
    [SerializeField] private Renderer fryFishRenderer;

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
    [SerializeField] private Text inventoryTitleText;
    [SerializeField] private Text inventoryMushroomText;
    [SerializeField] private Text inventoryFishText;
    [SerializeField] private Vector2 soupPromptPanelPosition = new Vector2(32f, -24f);
    [SerializeField] private Vector2 soupPromptPanelSize = new Vector2(760f, 150f);
    [SerializeField] private string soupPromptSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/UI_SpriteSheet.png";
    [SerializeField] private string soupPromptSpriteName = "UI_SpriteSheet_6";
    [SerializeField] private string soupPromptIconSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/UI_SpriteSheet.png";
    [SerializeField] private string soupPromptIconSpriteName = "UI_SpriteSheet_17";
    [SerializeField] private Color soupPromptFallbackColor = new Color(0.47f, 0.6f, 0.88f, 0.96f);
    [SerializeField] private Color soupPromptTextColor = new Color(0.97f, 0.99f, 1f, 1f);
    [SerializeField] private Vector2 soupCompletePanelPosition = new Vector2(-360f, -120f);
    [SerializeField] private Vector2 soupCompletePanelSize = new Vector2(720f, 220f);
    [SerializeField] private string soupCompleteSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/UI_SpriteSheet.png";
    [SerializeField] private string soupCompleteSpriteName = "UI_SpriteSheet_2";
    [SerializeField] private string soupCompleteIconSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/UI_SpriteSheet.png";
    [SerializeField] private string soupCompleteIconSpriteName = "UI_SpriteSheet_1";
    [SerializeField] private Color soupCompleteFallbackColor = new Color(0.88f, 0.28f, 0.23f, 0.98f);
    [SerializeField] private Color soupCompleteTextColor = new Color(1f, 0.98f, 0.95f, 1f);

    [Header("Cooking Settings")]
    [SerializeField] private float interactionDistance = 4.5f;
    [SerializeField] private float riverInteractionDistance = 3.8f;
    [SerializeField] private float fishingUnlockDistanceFromPot = 7.5f;
    [SerializeField] private float fireDecayPerSecond = 0.9f;
    [SerializeField] private float fireGainPerSpace = 0.18f;
    [SerializeField] private float maxFirePower = 1f;
    [SerializeField] private float maxCookRatePerSecond = 0.1f;
    [SerializeField] private int mushroomsNeeded = 3;
    [SerializeField] private float baseFireRate = 4f;
    [SerializeField] private float fireRateRange = 18f;
    [SerializeField] private float baseFireSpeed = 0.35f;
    [SerializeField] private float fireSpeedRange = 0.85f;
    [SerializeField] private float baseFireSize = 0.22f;
    [SerializeField] private float fireSizeRange = 0.35f;
    [SerializeField] private float fishGripGainPerPress = 0.18f;
    [SerializeField] private float fishGripDecayPerSecond = 0.23f;
    [SerializeField] private float fishGripThreshold = 0.72f;
    [SerializeField] private float fishCatchHoldDuration = 4f;
    [SerializeField] private float fishEscapePressGap = 0.45f;
    [SerializeField] private float fishCatchRetryDelay = 1f;
    [SerializeField] private float attentionFireGainPerSecond = 0.55f;
    [SerializeField] private float attentionFishGripGainPerSecond = 0.45f;

    private DishPhase dishPhase = DishPhase.MushroomSoup;
    private SoupStage soupStage = SoupStage.NeedMushroom;
    private FishCatchState fishCatchState = FishCatchState.Locked;
    private FriedFishStage friedFishStage = FriedFishStage.Inactive;
    private float firePower;
    private float cookProgress;
    private int mushroomsAdded;
    private int harvestedMushroomCount;
    private int caughtFishCount;
    private bool leftStirQueued;
    private bool rightStirQueued;
    private bool flipLeftQueued;
    private bool flipRightQueued;
    private bool seasoningUpQueued;
    private bool seasoningDownQueued;
    private Vector3 soupBaseScale = Vector3.one;
    private Vector3 activePotBaseScale = Vector3.one;
    private Vector3 stirStickBaseLocalPosition = Vector3.zero;
    private Quaternion stirStickBaseLocalRotation = Quaternion.identity;
    private Vector3 fryFishBaseLocalPosition = Vector3.zero;
    private Quaternion fryFishBaseLocalRotation = Quaternion.identity;
    private Vector3 fryFishBaseLocalScale = Vector3.one;
    private Color fryFishBaseColor = new Color(0.78f, 0.86f, 0.96f, 1f);
    private Vector2 cookingTitleDefaultPosition;
    private Vector2 cookingTitleDefaultSize;
    private float stirAnimationTimer;
    private float mushroomAnimationTimer;
    private float fishGrip;
    private float fishHoldTimer;
    private float fishLastPressTime = -999f;
    private float nextFishCatchAllowedTime;
    private float fishStatusMessageTimer;
    private string fishStatusMessage = string.Empty;
    private Transform activeMushroomVisual;
    private bool playerInCookingRange;
    private bool playerNearRiver;
    private bool playerFarEnoughFromPotForFishing;
    private float fishFlipAnimationTimer;
    private float seasoningAnimationTimer;
    private bool pendingFrySetup;
    private bool fishHasBeenFlipped;
    private bool fishPlacedInPan;
    private HybridBciGameplayInput gameplayInput;
    private Canvas soupPromptCanvas;
    private Image soupPromptBackgroundImage;
    private Text soupPromptOverlayText;
    private Sprite soupPromptSprite;
    private Sprite soupPromptIconSprite;
    private Canvas soupCompleteCanvas;
    private Text soupCompleteText;
    private Sprite soupCompleteSprite;
    private Sprite soupCompleteIconSprite;
    private bool isShowingSoupCompletePrompt;
    private bool pendingSoupCompleteTransition;
    private float soupCompletePromptUnlockTime;

    public void Initialize(
        ParticleSystem sceneFireEffect,
        Transform scenePlayerTransform,
        Transform sceneWaterRoot,
        Transform sceneSoupPotVisual,
        Transform sceneFryPotVisual,
        Transform sceneSoupSurface,
        Renderer sceneSoupRenderer,
        Transform sceneStirStick,
        Transform sceneMushroomSpawnPoint,
        Transform sceneMushroomTargetPoint,
        GameObject sceneMushroomVisualPrefab,
        Transform sceneFryFishVisual,
        Renderer sceneFryFishRenderer,
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
        Slider sceneFishCatchSlider,
        Text sceneInventoryTitleText,
        Text sceneInventoryMushroomText,
        Text sceneInventoryFishText)
    {
        Instance = this;
        harvestedMushroomCount = 0;
        caughtFishCount = 0;
        mushroomsAdded = 0;
        fishPlacedInPan = false;
        nextFishCatchAllowedTime = 0f;
        fireEffect = sceneFireEffect;
        playerTransform = scenePlayerTransform;
        waterRoot = sceneWaterRoot;
        soupPotVisual = sceneSoupPotVisual;
        fryPotVisual = sceneFryPotVisual;
        soupSurface = sceneSoupSurface;
        soupRenderer = sceneSoupRenderer;
        stirStick = sceneStirStick;
        mushroomSpawnPoint = sceneMushroomSpawnPoint;
        mushroomTargetPoint = sceneMushroomTargetPoint;
        mushroomVisualPrefab = sceneMushroomVisualPrefab;
        fryFishVisual = sceneFryFishVisual;
        fryFishRenderer = sceneFryFishRenderer;
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
        inventoryTitleText = sceneInventoryTitleText;
        inventoryMushroomText = sceneInventoryMushroomText;
        inventoryFishText = sceneInventoryFishText;

        SetActivePot(soupPotVisual);
        SetSoupModeVisible(true);
        SetFryModeVisible(false);
        CacheVisualState();
        UpdateInteractionState();
        RefreshUI();
        UpdateFireVisuals();
    }

    private void Start()
    {
        Instance = this;
        harvestedMushroomCount = 0;
        caughtFishCount = 0;
        mushroomsAdded = 0;
        fishPlacedInPan = false;
        nextFishCatchAllowedTime = 0f;
        if (activePotVisual == null)
        {
            SetActivePot(soupPotVisual != null ? soupPotVisual : fryPotVisual);
        }

        SetSoupModeVisible(dishPhase == DishPhase.MushroomSoup);
        SetFryModeVisible(dishPhase == DishPhase.FriedFish || pendingFrySetup);
        CacheVisualState();
        UpdateInteractionState();
        RefreshUI();
        UpdateFireVisuals();
        ResolveGameplayInput();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Time.frameCount <= 3)
        {
            harvestedMushroomCount = 0;
            RefreshInventoryUI();
        }

        if (ForestStoryIntroOverlay.IsBlockingInput)
        {
            if (cookingPanel != null)
            {
                cookingPanel.SetActive(false);
            }

            if (fishingPanel != null)
            {
                fishingPanel.SetActive(false);
            }

            return;
        }

        if (isShowingSoupCompletePrompt)
        {
            if (Time.time >= soupCompletePromptUnlockTime &&
                (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                CloseSoupCompletePrompt();
            }

            RefreshSoupCompletePrompt();
            return;
        }

        UpdateInteractionState();
        HandlePhaseTransitions();
        ResolveGameplayInput();

        if (playerNearRiver && dishPhase == DishPhase.FishCatch && IsFishCatchUnlocked())
        {
            ReadFishInput();
        }
        else if (playerInCookingRange && (dishPhase == DishPhase.MushroomSoup || dishPhase == DishPhase.FriedFish))
        {
            ReadCookingInput();
        }

        UpdateFirePower();
        UpdateCookingProgress();
        UpdateFishCatch();
        UpdateAnimations();
        UpdateSoupSurface();
        UpdateFireVisuals();
        RefreshSoupPromptOverlay();
        UpdateMushroomGatherPrompt();
        RefreshUI();
    }

    private void HandlePhaseTransitions()
    {
        if (dishPhase == DishPhase.FishCatch && !pendingFrySetup && caughtFishCount > 0 && playerInCookingRange)
        {
            pendingFrySetup = true;
            friedFishStage = FriedFishStage.ReturnToFire;
            firePower = 0f;
            SetSoupModeVisible(false);
            SetFryModeVisible(true);
            SetActivePot(fryPotVisual != null ? fryPotVisual : soupPotVisual);
        }

        if (pendingFrySetup && playerInCookingRange)
        {
            BeginFriedFish();
            return;
        }
    }

    private void CacheVisualState()
    {
        if (titleText != null)
        {
            var titleRect = titleText.rectTransform;
            cookingTitleDefaultPosition = titleRect.anchoredPosition;
            cookingTitleDefaultSize = titleRect.sizeDelta;
        }

        if (soupSurface != null)
        {
            soupBaseScale = soupSurface.localScale;
        }

        if (activePotVisual != null)
        {
            activePotBaseScale = activePotVisual.localScale;
        }

        if (stirStick != null)
        {
            stirStickBaseLocalPosition = stirStick.localPosition;
            stirStickBaseLocalRotation = stirStick.localRotation;
        }

        if (fryFishVisual != null)
        {
            fryFishBaseLocalPosition = fryFishVisual.localPosition;
            fryFishBaseLocalRotation = fryFishVisual.localRotation;
            fryFishBaseLocalScale = fryFishVisual.localScale;
        }

        if (fryFishRenderer != null)
        {
            fryFishBaseColor = fryFishRenderer.material.color;
        }
    }

    private void ReadCookingInput()
    {
        var keyboardFirePressed = Input.GetKeyDown(KeyCode.Space) && !MushroomPickupSystem.HasFocusedHarvestable;
        var platformFireActive = CanUsePlatformInput() && gameplayInput.IsAttentionActive && !MushroomPickupSystem.HasFocusedHarvestable;

        if (keyboardFirePressed)
        {
            firePower = Mathf.Clamp(firePower + fireGainPerSpace, 0f, maxFirePower);
            CookingAudioController.Instance?.PlayFireTap();
        }

        if (platformFireActive)
        {
            firePower = Mathf.Clamp(firePower + attentionFireGainPerSecond * Time.deltaTime, 0f, maxFirePower);
        }

        if (dishPhase == DishPhase.MushroomSoup)
        {
            ReadSoupInput();
            return;
        }

        if (dishPhase == DishPhase.FriedFish)
        {
            ReadFriedFishInput();
        }
    }

    private void ReadSoupInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || ConsumePlatformVerticalGesture())
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

        if (ConsumePlatformHorizontalGesture())
        {
            leftStirQueued = true;
            rightStirQueued = true;
            TryStir();
        }
    }

    private void ReadFriedFishInput()
    {
        if (friedFishStage == FriedFishStage.ReturnToFire)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || ConsumePlatformVerticalGesture())
            {
                TryPlaceFishInPan();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            flipLeftQueued = true;
            TryFlipFish();
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            flipRightQueued = true;
            TryFlipFish();
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            seasoningUpQueued = true;
            TrySeasonFish();
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            seasoningDownQueued = true;
            TrySeasonFish();
        }

        if (ConsumePlatformHorizontalGesture())
        {
            flipLeftQueued = true;
            flipRightQueued = true;
            TryFlipFish();
        }

        if (ConsumePlatformVerticalGesture())
        {
            seasoningUpQueued = true;
            seasoningDownQueued = true;
            TrySeasonFish();
        }
    }

    private void ReadFishInput()
    {
        if (Time.time < nextFishCatchAllowedTime)
        {
            return;
        }

        var keyboardCatchPressed = Input.GetKeyDown(KeyCode.Space);
        var platformCatchPressed = CanUsePlatformInput() && gameplayInput.IsAttentionActive;

        if ((!keyboardCatchPressed && !platformCatchPressed) || fishCatchState == FishCatchState.Caught)
        {
            return;
        }

        if (fishCatchState == FishCatchState.Escaped)
        {
            fishStatusMessage = "重新准备，集中注意力或猛按空格把鱼抓稳。";
            fishStatusMessageTimer = 1.5f;
            fishCatchState = FishCatchState.NeedToCatch;
        }

        if (fishCatchState == FishCatchState.NeedToCatch)
        {
            fishCatchState = FishCatchState.Catching;
            fishGrip = 0.22f;
            fishHoldTimer = 0f;
            fishStatusMessage = "抓到了，保持专注或快速连按空格，别让它逃走。";
            fishStatusMessageTimer = 1.2f;
        }
        else if (fishCatchState == FishCatchState.Catching && keyboardCatchPressed)
        {
            fishGrip = Mathf.Clamp01(fishGrip + fishGripGainPerPress);
        }

        CookingAudioController.Instance?.PlayFishPress();
        fishLastPressTime = Time.time;
    }

    private void UpdateFirePower()
    {
        if (!playerInCookingRange)
        {
            return;
        }

        if (dishPhase != DishPhase.MushroomSoup && dishPhase != DishPhase.FriedFish)
        {
            return;
        }

        if (dishPhase == DishPhase.MushroomSoup && soupStage == SoupStage.NeedMushroom)
        {
            return;
        }

        if (dishPhase == DishPhase.FriedFish && friedFishStage == FriedFishStage.ReturnToFire)
        {
            return;
        }

        if (CanUsePlatformInput() &&
            gameplayInput != null &&
            gameplayInput.IsAttentionActive &&
            !MushroomPickupSystem.HasFocusedHarvestable)
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

        if (dishPhase == DishPhase.MushroomSoup)
        {
            UpdateSoupProgress();
            return;
        }

        if (dishPhase == DishPhase.FriedFish)
        {
            UpdateFriedFishProgress();
        }
    }

    private void UpdateSoupProgress()
    {
        if (soupStage == SoupStage.NeedMushroom ||
            soupStage == SoupStage.NeedFirstStir ||
            soupStage == SoupStage.NeedSecondStir ||
            soupStage == SoupStage.Completed)
        {
            return;
        }

        cookProgress = Mathf.Clamp01(cookProgress + firePower * maxCookRatePerSecond * Time.deltaTime);

        if (soupStage == SoupStage.HeatingToQuarter && cookProgress >= 0.25f)
        {
            cookProgress = 0.25f;
            soupStage = SoupStage.NeedMushroom;
            return;
        }

        if (soupStage == SoupStage.HeatingToHalf && cookProgress >= 0.5f)
        {
            cookProgress = 0.5f;
            soupStage = SoupStage.NeedFirstStir;
            return;
        }

        if (soupStage == SoupStage.HeatingToThreeQuarters && cookProgress >= 0.75f)
        {
            cookProgress = 0.75f;
            soupStage = SoupStage.NeedSecondStir;
            return;
        }

        if (soupStage == SoupStage.HeatingToDone && cookProgress >= 1f)
        {
            cookProgress = 1f;
            soupStage = SoupStage.Completed;
            TintSoup(new Color(0.84f, 0.77f, 0.56f, 1f));
            pendingSoupCompleteTransition = true;
            ShowSoupCompletePrompt();
            CookingAudioController.Instance?.PlayDishComplete();
        }
    }

    private void UpdateFriedFishProgress()
    {
        if (friedFishStage == FriedFishStage.ReturnToFire ||
            friedFishStage == FriedFishStage.NeedFlip ||
            friedFishStage == FriedFishStage.NeedSeasoning ||
            friedFishStage == FriedFishStage.Completed)
        {
            return;
        }

        cookProgress = Mathf.Clamp01(cookProgress + firePower * maxCookRatePerSecond * Time.deltaTime);
        UpdateFriedFishAppearance();

        if (friedFishStage == FriedFishStage.HeatingToHalf && cookProgress >= 0.5f)
        {
            cookProgress = 0.5f;
            friedFishStage = FriedFishStage.NeedFlip;
            return;
        }

        if (friedFishStage == FriedFishStage.HeatingToThreeQuarters && cookProgress >= 0.75f)
        {
            cookProgress = 0.75f;
            friedFishStage = FriedFishStage.NeedSeasoning;
            return;
        }

        if (friedFishStage == FriedFishStage.HeatingToDone && cookProgress >= 1f)
        {
            cookProgress = 1f;
            friedFishStage = FriedFishStage.Completed;
            dishPhase = DishPhase.Completed;
            fishStatusMessage = "煎鱼完成了，今天的晚餐都准备好了。";
            fishStatusMessageTimer = 4f;
            UpdateFriedFishAppearance();
            CookingAudioController.Instance?.PlayDishComplete();
        }
    }

    private void TryAddMushroom()
    {
        if (soupStage != SoupStage.NeedMushroom)
        {
            return;
        }

        if (harvestedMushroomCount <= 0)
        {
            fishStatusMessage = mushroomsAdded >= mushroomsNeeded
                ? "蘑菇已经放够了，可以开始下一步。"
                : "蘑菇用完了，请再去采一些，至少要放三个。";
            fishStatusMessageTimer = 2f;
            return;
        }

        harvestedMushroomCount--;
        mushroomsAdded++;
        CookingAudioController.Instance?.PlayMushroomDrop();
        TintSoup(new Color(0.69f, 0.62f, 0.37f, 1f));

        if (mushroomsAdded >= mushroomsNeeded)
        {
            soupStage = SoupStage.HeatingToHalf;
            fishStatusMessage = "蘑菇已经放够了，开始继续煮汤。";
            fishStatusMessageTimer = 2f;
        }
    }

    private void TryPlaceFishInPan()
    {
        if (friedFishStage != FriedFishStage.ReturnToFire)
        {
            return;
        }

        if (caughtFishCount <= 0)
        {
            fishStatusMessage = "背包里没有鱼，请先去河边抓鱼。";
            fishStatusMessageTimer = 2f;
            return;
        }

        caughtFishCount--;
        fishPlacedInPan = true;
        friedFishStage = FriedFishStage.HeatingToHalf;
        SetFryFishVisible(true);
        UpdateFriedFishAppearance();
        fishStatusMessage = "已放入 1 条鱼，现在开始煎鱼。";
        fishStatusMessageTimer = 2f;
        CookingAudioController.Instance?.PlayUiClose();
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
        CookingAudioController.Instance?.PlayStir();

        if (soupStage == SoupStage.NeedFirstStir)
        {
            soupStage = SoupStage.HeatingToThreeQuarters;
            NudgeSoupSurface(1.18f);
            TintSoup(new Color(0.76f, 0.68f, 0.43f, 1f));
            return;
        }

        if (soupStage == SoupStage.NeedSecondStir)
        {
            soupStage = SoupStage.HeatingToDone;
            NudgeSoupSurface(1.22f);
            TintSoup(new Color(0.8f, 0.73f, 0.5f, 1f));
        }
    }

    private void TryFlipFish()
    {
        if (friedFishStage != FriedFishStage.NeedFlip || !flipLeftQueued || !flipRightQueued)
        {
            return;
        }

        flipLeftQueued = false;
        flipRightQueued = false;
        fishHasBeenFlipped = true;
        PlayFishFlipAnimation();
        CookingAudioController.Instance?.PlayFishFlip();
        friedFishStage = FriedFishStage.HeatingToThreeQuarters;
    }

    private void TrySeasonFish()
    {
        if (friedFishStage != FriedFishStage.NeedSeasoning || !seasoningUpQueued || !seasoningDownQueued)
        {
            return;
        }

        seasoningUpQueued = false;
        seasoningDownQueued = false;
        PlaySeasoningAnimation();
        CookingAudioController.Instance?.PlaySeasoning();
        friedFishStage = FriedFishStage.HeatingToDone;
    }

    private void UpdateAnimations()
    {
        UpdateStirAnimation();
        UpdateMushroomAnimation();
        UpdateFishFlipAnimation();
        UpdateSeasoningAnimation();
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
        else if (stirStick != null)
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

    private void UpdateFishFlipAnimation()
    {
        if (fryFishVisual == null)
        {
            return;
        }

        if (fishFlipAnimationTimer > 0f)
        {
            fishFlipAnimationTimer -= Time.deltaTime;
            var normalized = 1f - Mathf.Clamp01(fishFlipAnimationTimer / 0.7f);
            var arc = Mathf.Sin(normalized * Mathf.PI) * 0.28f;
            fryFishVisual.localPosition = fryFishBaseLocalPosition + new Vector3(0f, arc, 0f);
            fryFishVisual.localRotation = fryFishBaseLocalRotation * Quaternion.Euler(normalized * 180f, 0f, Mathf.Sin(normalized * Mathf.PI) * 18f);
        }
        else
        {
            fryFishVisual.localPosition = Vector3.Lerp(fryFishVisual.localPosition, fryFishBaseLocalPosition, Time.deltaTime * 8f);
            var targetRotation = fishHasBeenFlipped
                ? fryFishBaseLocalRotation * Quaternion.Euler(180f, 0f, 0f)
                : fryFishBaseLocalRotation;
            fryFishVisual.localRotation = Quaternion.Slerp(fryFishVisual.localRotation, targetRotation, Time.deltaTime * 8f);
        }
    }

    private void UpdateSeasoningAnimation()
    {
        if (fryFishVisual == null)
        {
            return;
        }

        if (seasoningAnimationTimer > 0f)
        {
            seasoningAnimationTimer -= Time.deltaTime;
            var normalized = 1f - Mathf.Clamp01(seasoningAnimationTimer / 0.8f);
            var pulse = 1f + Mathf.Sin(normalized * Mathf.PI * 4f) * 0.08f;
            fryFishVisual.localScale = fryFishBaseLocalScale * pulse;
        }
        else
        {
            fryFishVisual.localScale = Vector3.Lerp(fryFishVisual.localScale, fryFishBaseLocalScale, Time.deltaTime * 7f);
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

    private void PlayFishFlipAnimation()
    {
        fishFlipAnimationTimer = 0.7f;
    }

    private void PlaySeasoningAnimation()
    {
        seasoningAnimationTimer = 0.8f;
    }

    private void UpdateSoupSurface()
    {
        if (soupSurface == null)
        {
            return;
        }

        soupSurface.localScale = Vector3.Lerp(soupSurface.localScale, soupBaseScale, Time.deltaTime * 5f);
    }

    private void UpdateFriedFishAppearance()
    {
        if (fryFishRenderer == null)
        {
            return;
        }

        var cookedColor = new Color(0.75f, 0.48f, 0.22f, 1f);
        fryFishRenderer.material.color = Color.Lerp(fryFishBaseColor, cookedColor, cookProgress);
    }

    private void UpdateFireVisuals()
    {
        CookingAudioController.Instance?.UpdateFireLoop(playerInCookingRange ? firePower : 0f);
        CookingAudioController.Instance?.UpdateRiverLoop(playerNearRiver ? 1f : 0f);

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

        if (activePotVisual != null)
        {
            activePotVisual.localScale = activePotBaseScale;
        }
    }

    private void RefreshUI()
    {
        RefreshInventoryUI();

        var showCookingPanel = playerInCookingRange && (dishPhase == DishPhase.MushroomSoup || dishPhase == DishPhase.FishCatch || dishPhase == DishPhase.FriedFish || dishPhase == DishPhase.Completed);
        var showFishingPanel = playerNearRiver && playerFarEnoughFromPotForFishing && dishPhase == DishPhase.FishCatch && IsFishCatchUnlocked();

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

        ApplyReadableUiOverrides();
    }

    private void ApplyReadableUiOverrides()
    {
        if (inventoryTitleText != null)
        {
            inventoryTitleText.text = "\u6211\u7684\u80cc\u5305";
        }

        if (inventoryMushroomText != null)
        {
            inventoryMushroomText.text = $"\u8611\u83c7: {harvestedMushroomCount}";
        }

        if (inventoryFishText != null)
        {
            inventoryFishText.text = $"\u9c7c: {caughtFishCount}";
        }

        if (dishPhase == DishPhase.FishCatch && mushroomCountText != null && caughtFishCount > 0)
        {
            mushroomCountText.text = $"\u5df2\u6293\u5230 {caughtFishCount} \u6761\u9c7c\uff0c\u56de\u5230\u9505\u8fb9\u4f1a\u81ea\u52a8\u5f00\u59cb\u714e\u9c7c\u3002";
        }
    }

    private void RefreshInventoryUI()
    {
        if (inventoryTitleText != null)
        {
            inventoryTitleText.text = "我的背包";
        }

        if (inventoryMushroomText != null)
        {
            inventoryMushroomText.text = $"蘑菇: {harvestedMushroomCount}";
        }

        if (inventoryFishText != null)
        {
            inventoryFishText.text = $"鱼: {caughtFishCount}";
        }
    }

    private void RefreshCookingUI()
    {
        EnsureSoupPromptOverlay();
        RefreshCookingTitleLayout();

        if (titleText != null)
        {
            if (dishPhase == DishPhase.MushroomSoup)
            {
                titleText.text = "蘑菇汤";
            }
            else
            {
                titleText.text = "煎鱼";
            }
        }

        if (promptText != null)
        {
            promptText.text = ShouldHideDefaultCookingPrompt() ? string.Empty : GetPrompt();
            promptText.color = NeedsAttention() ? new Color(1f, 0.35f, 0.2f, 1f) : Color.white;
        }

        if (fireValueText != null)
        {
            fireValueText.text = $"当前火力: {firePower:0.00}";
        }

        if (progressText != null)
        {
            progressText.text = $"当前进度: {(cookProgress * 100f):0}%";
        }

        if (mushroomCountText != null)
        {
            if (dishPhase == DishPhase.MushroomSoup)
            {
                mushroomCountText.text = $"已加蘑菇: {mushroomsAdded}/{mushroomsNeeded}";
            }
            else if (dishPhase == DishPhase.FishCatch)
            {
                mushroomCountText.text = caughtFishCount > 0
                    ? $"已抓到 {caughtFishCount} 条鱼，回到锅边会自动开始煎鱼。"
                    : "先在河边抓到鱼，才能开始煎鱼。";
            }
            else if (dishPhase == DishPhase.FriedFish)
            {
                mushroomCountText.text = GetFriedFishStatusLine();
            }
            else
            {
                mushroomCountText.text = "今晚的料理都完成了。";
            }
        }

        if (fireSliderLabelText != null)
        {
            fireSliderLabelText.text = "火力值";
        }

        if (progressSliderLabelText != null)
        {
            progressSliderLabelText.text = dishPhase == DishPhase.MushroomSoup ? "煮饭进度" : "煎制进度";
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
            fishCatchButtonText.text = CanUsePlatformInput()
                ? (fishCatchState == FishCatchState.Catching
                    ? "[ 专注 ] 保持注意力，稳稳抓住鱼"
                    : "[ 专注 ] 集中注意力，开始捉鱼")
                : (fishCatchState == FishCatchState.Catching
                    ? "[ 空格 ] 持续猛按，把鱼抓紧"
                    : "[ 空格 ] 开始捉鱼");
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
        var usingPlatform = CanUsePlatformInput();
        if (dishPhase == DishPhase.MushroomSoup)
        {
            switch (soupStage)
            {
                case SoupStage.NeedMushroom:
                    if (mushroomsAdded < mushroomsNeeded)
                    {
                        return string.Empty;
                    }

                    return usingPlatform ? "蘑菇已经放够了，继续保持专注，把蘑菇汤煮到下一阶段。" : "蘑菇已经放够了，按空格继续加热蘑菇汤。";
                case SoupStage.HeatingToQuarter:
                    return usingPlatform ? "保持专注提升火力，把蘑菇汤煮到 1/4 进度。" : "快速按空格键升高火力，把蘑菇汤煮到 1/4 进度。";
                case SoupStage.HeatingToHalf:
                    return usingPlatform ? "蘑菇已经下锅，继续保持专注，把进度推到 2/4。" : "蘑菇已经下锅，继续加热，把进度推到 2/4。";
                case SoupStage.NeedFirstStir:
                    return usingPlatform ? "提示：现在需要第一次搅拌，请左右摇头一次完成搅拌。" : "提示：现在需要第一次搅拌，请左右键各按一次。";
                case SoupStage.HeatingToThreeQuarters:
                    return usingPlatform ? "第一次搅拌完成，继续保持专注，把进度推进到 3/4。" : "第一次搅拌完成，继续加热，把进度推进到 3/4。";
                case SoupStage.NeedSecondStir:
                    return usingPlatform ? "提示：现在需要第二次搅拌，请再左右摇头一次。" : "提示：现在需要第二次搅拌，请左右键各按一次。";
                case SoupStage.HeatingToDone:
                    return usingPlatform ? "最后保持专注收汁加热，马上就完成了。" : "最后收汁加热，马上就完成了。";
                case SoupStage.Completed:
                    return "蘑菇汤制作完成。下一道菜是煎鱼，请先去河边捉鱼。";
            }
        }

        if (dishPhase == DishPhase.FishCatch)
        {
            if (caughtFishCount > 0)
            {
                return "你已经抓到鱼了，可以继续在河边抓，回锅边也能开始煎鱼。";
            }

            return "蘑菇汤已经完成，下一道菜是煎鱼。请先去河边抓鱼。";
        }

        if (dishPhase == DishPhase.FriedFish)
        {
            switch (friedFishStage)
            {
                case FriedFishStage.ReturnToFire:
                    return caughtFishCount > 0
                        ? "回到锅边后，请先按上下键放入 1 条鱼，再开始煎鱼。"
                        : "想煎鱼先要去抓鱼，抓到后回锅边按上下键放入 1 条鱼。";
                case FriedFishStage.HeatingToHalf:
                    return usingPlatform ? "先保持专注控制火力，把煎鱼进度推进到 1/2。" : "先按空格键控制火力，把煎鱼进度推进到 1/2。";
                case FriedFishStage.NeedFlip:
                    return usingPlatform ? "提示：鱼煎到一半了，请左右摇头一次给它翻面。" : "提示：鱼煎到一半了，请按左右键翻面。";
                case FriedFishStage.HeatingToThreeQuarters:
                    return usingPlatform ? "翻面完成，继续保持专注加热，把进度推进到 3/4。" : "翻面完成，继续按空格加火，把进度推进到 3/4。";
                case FriedFishStage.NeedSeasoning:
                    return usingPlatform ? "提示：现在请点头一次，为煎鱼加入调味。" : "提示：现在请按上键和下键加入调味料。";
                case FriedFishStage.HeatingToDone:
                    return usingPlatform ? "调味已经加入，继续保持专注把鱼煎熟。" : "调味已经加入，继续加热把鱼煎熟。";
                case FriedFishStage.Completed:
                    return "煎鱼完成，晚餐准备好了。";
            }
        }

        if (dishPhase == DishPhase.Completed)
        {
            return "煎鱼完成，晚餐准备好了。";
        }

        return string.Empty;
    }

    private void UpdateMushroomGatherPrompt()
    {
        var pickupSystem = MushroomPickupSystem.Instance;
        if (pickupSystem == null)
        {
            return;
        }

        var shouldShow = ShouldShowMushroomGatherDialog();
        if (shouldShow)
        {
            pickupSystem.ShowGatherIntroPrompt(harvestedMushroomCount >= mushroomsNeeded);
        }
        else
        {
            pickupSystem.HideGatherIntroPrompt();
        }
    }

    private bool ShouldShowMushroomGatherDialog()
    {
        return dishPhase == DishPhase.MushroomSoup &&
               soupStage == SoupStage.NeedMushroom &&
               mushroomsAdded < mushroomsNeeded;
    }

    private bool ShouldShowSoupPromptOverlay()
    {
        if (dishPhase != DishPhase.MushroomSoup)
        {
            return false;
        }

        if (ShouldShowMushroomGatherDialog())
        {
            return false;
        }

        return soupStage == SoupStage.NeedMushroom ||
               soupStage == SoupStage.HeatingToQuarter ||
               soupStage == SoupStage.HeatingToHalf ||
               soupStage == SoupStage.NeedFirstStir ||
               soupStage == SoupStage.HeatingToThreeQuarters ||
               soupStage == SoupStage.NeedSecondStir ||
               soupStage == SoupStage.HeatingToDone;
    }

    private bool ShouldHideDefaultCookingPrompt()
    {
        return ShouldShowMushroomGatherDialog() || ShouldShowSoupPromptOverlay();
    }

    private void EnsureSoupPromptOverlay()
    {
        if (soupPromptCanvas != null)
        {
            return;
        }

        var canvasObject = new GameObject("Soup Prompt UI");
        soupPromptCanvas = canvasObject.AddComponent<Canvas>();
        soupPromptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        soupPromptCanvas.sortingOrder = 24;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        var panelObject = new GameObject("SoupPromptPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        var panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = soupPromptPanelPosition;
        panelRect.sizeDelta = soupPromptPanelSize;

        soupPromptBackgroundImage = panelObject.AddComponent<Image>();
        LoadSoupPromptSprite();
        if (soupPromptSprite != null)
        {
            soupPromptBackgroundImage.sprite = soupPromptSprite;
            soupPromptBackgroundImage.color = Color.white;
        }
        else
        {
            soupPromptBackgroundImage.color = soupPromptFallbackColor;
        }

        LoadSoupPromptIconSprite();
        if (soupPromptIconSprite != null)
        {
            var iconObject = new GameObject("SoupPromptIcon");
            iconObject.transform.SetParent(panelObject.transform, false);

            var iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(14f, -10f);
            iconRect.sizeDelta = new Vector2(88f, 88f);

            var iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = soupPromptIconSprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }

        var textObject = new GameObject("SoupPromptText");
        textObject.transform.SetParent(panelObject.transform, false);

        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = new Vector2(118f, -22f);
        textRect.sizeDelta = new Vector2(soupPromptPanelSize.x - 236f, soupPromptPanelSize.y - 54f);

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.16f, 0.23f, 0.45f, 0.75f);
        outline.effectDistance = new Vector2(1f, -1f);

        soupPromptOverlayText = textObject.AddComponent<Text>();
        soupPromptOverlayText.font = titleText != null && titleText.font != null
            ? titleText.font
            : Resources.GetBuiltinResource<Font>("Arial.ttf");
        soupPromptOverlayText.fontSize = 32;
        soupPromptOverlayText.fontStyle = FontStyle.Bold;
        soupPromptOverlayText.color = soupPromptTextColor;
        soupPromptOverlayText.alignment = TextAnchor.MiddleCenter;
        soupPromptOverlayText.horizontalOverflow = HorizontalWrapMode.Wrap;
        soupPromptOverlayText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void RefreshSoupPromptOverlay()
    {
        if (soupPromptCanvas == null)
        {
            return;
        }

        var visible = !ForestStoryIntroOverlay.IsBlockingInput && ShouldShowSoupPromptOverlay();
        soupPromptCanvas.enabled = visible;
        if (!visible)
        {
            return;
        }

        if (soupPromptOverlayText != null)
        {
            soupPromptOverlayText.text = GetPrompt();
        }
    }

    private void EnsureSoupCompletePrompt()
    {
        if (soupCompleteCanvas != null)
        {
            return;
        }

        var canvasObject = new GameObject("Soup Complete UI");
        soupCompleteCanvas = canvasObject.AddComponent<Canvas>();
        soupCompleteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        soupCompleteCanvas.sortingOrder = 40;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        var panelObject = new GameObject("SoupCompletePanel");
        panelObject.transform.SetParent(canvasObject.transform, false);

        var panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = soupCompletePanelPosition;
        panelRect.sizeDelta = soupCompletePanelSize;

        var panelImage = panelObject.AddComponent<Image>();
        LoadSoupCompleteSprite();
        if (soupCompleteSprite != null)
        {
            panelImage.sprite = soupCompleteSprite;
            panelImage.color = Color.white;
        }
        else
        {
            panelImage.color = soupCompleteFallbackColor;
        }

        LoadSoupCompleteIconSprite();
        if (soupCompleteIconSprite != null)
        {
            var iconObject = new GameObject("SoupCompleteIcon");
            iconObject.transform.SetParent(panelObject.transform, false);

            var iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(16f, -12f);
            iconRect.sizeDelta = new Vector2(92f, 92f);

            var iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = soupCompleteIconSprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }

        var textObject = new GameObject("SoupCompleteText");
        textObject.transform.SetParent(panelObject.transform, false);

        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = new Vector2(118f, -24f);
        textRect.sizeDelta = new Vector2(soupCompletePanelSize.x - 170f, soupCompletePanelSize.y - 52f);

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.08f, 0.08f, 0.72f);
        outline.effectDistance = new Vector2(1f, -1f);

        soupCompleteText = textObject.AddComponent<Text>();
        soupCompleteText.font = titleText != null && titleText.font != null
            ? titleText.font
            : Resources.GetBuiltinResource<Font>("Arial.ttf");
        soupCompleteText.fontSize = 30;
        soupCompleteText.fontStyle = FontStyle.Bold;
        soupCompleteText.color = soupCompleteTextColor;
        soupCompleteText.alignment = TextAnchor.MiddleCenter;
        soupCompleteText.horizontalOverflow = HorizontalWrapMode.Wrap;
        soupCompleteText.verticalOverflow = VerticalWrapMode.Overflow;
        soupCompleteText.text = "恭喜你！美味的蘑菇汤煮好了！\n\n按任意键继续";

        soupCompleteCanvas.enabled = false;
    }

    private void ShowSoupCompletePrompt()
    {
        EnsureSoupCompletePrompt();
        isShowingSoupCompletePrompt = true;
        soupCompletePromptUnlockTime = Time.time + 2f;
        RefreshSoupCompletePrompt();
    }

    private void RefreshSoupCompletePrompt()
    {
        if (soupCompleteCanvas == null)
        {
            return;
        }

        soupCompleteCanvas.enabled = isShowingSoupCompletePrompt;
    }

    private void CloseSoupCompletePrompt()
    {
        isShowingSoupCompletePrompt = false;
        if (soupCompleteCanvas != null)
        {
            soupCompleteCanvas.enabled = false;
        }

        if (pendingSoupCompleteTransition)
        {
            pendingSoupCompleteTransition = false;
            dishPhase = DishPhase.FishCatch;
            fishCatchState = FishCatchState.NeedToCatch;
        }
    }

    private void RefreshCookingTitleLayout()
    {
        if (titleText == null)
        {
            return;
        }

        var titleRect = titleText.rectTransform;
        if (ShouldShowSoupPromptOverlay() && dishPhase == DishPhase.MushroomSoup)
        {
            titleRect.anchoredPosition = new Vector2(
                soupPromptPanelPosition.x + 18f,
                soupPromptPanelPosition.y - soupPromptPanelSize.y - 12f);
            titleRect.sizeDelta = new Vector2(320f, 46f);
            titleText.alignment = TextAnchor.UpperLeft;
            return;
        }

        titleRect.anchoredPosition = cookingTitleDefaultPosition;
        titleRect.sizeDelta = cookingTitleDefaultSize;
        titleText.alignment = TextAnchor.UpperLeft;
    }

    private void LoadSoupPromptSprite()
    {
        if (soupPromptSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(soupPromptSpriteSheetPath);
        for (var i = 0; i < assets.Length; i++)
        {
            var sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == soupPromptSpriteName)
            {
                soupPromptSprite = sprite;
                return;
            }
        }
#endif
    }

    private void LoadSoupPromptIconSprite()
    {
        if (soupPromptIconSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(soupPromptIconSpriteSheetPath);
        for (var i = 0; i < assets.Length; i++)
        {
            var sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == soupPromptIconSpriteName)
            {
                soupPromptIconSprite = sprite;
                return;
            }
        }
#endif
    }

    private void LoadSoupCompleteSprite()
    {
        if (soupCompleteSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(soupCompleteSpriteSheetPath);
        for (var i = 0; i < assets.Length; i++)
        {
            var sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == soupCompleteSpriteName)
            {
                soupCompleteSprite = sprite;
                return;
            }
        }
#endif
    }

    private void LoadSoupCompleteIconSprite()
    {
        if (soupCompleteIconSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(soupCompleteIconSpriteSheetPath);
        for (var i = 0; i < assets.Length; i++)
        {
            var sprite = assets[i] as Sprite;
            if (sprite != null && sprite.name == soupCompleteIconSpriteName)
            {
                soupCompleteIconSprite = sprite;
                return;
            }
        }
#endif
    }

    private bool NeedsAttention()
    {
        if (dishPhase == DishPhase.MushroomSoup)
        {
            return soupStage == SoupStage.NeedMushroom ||
                   soupStage == SoupStage.NeedFirstStir ||
                   soupStage == SoupStage.NeedSecondStir;
        }

        if (dishPhase == DishPhase.FriedFish)
        {
            return friedFishStage == FriedFishStage.ReturnToFire ||
                   friedFishStage == FriedFishStage.NeedFlip ||
                   friedFishStage == FriedFishStage.NeedSeasoning;
        }

        return false;
    }

    private string GetFriedFishStatusLine()
    {
        var usingPlatform = CanUsePlatformInput();
        switch (friedFishStage)
        {
            case FriedFishStage.ReturnToFire:
                return "动作要求：按上键或下键放入 1 条鱼。";
            case FriedFishStage.NeedFlip:
                return usingPlatform ? "动作要求：左右摇头一次，完成翻面。" : "动作要求：左右键各按一次，完成翻面。";
            case FriedFishStage.NeedSeasoning:
                return usingPlatform ? "动作要求：点头一次，加入调味料。" : "动作要求：上键和下键各按一次，加入调味料。";
            case FriedFishStage.Completed:
                return "煎鱼已经出锅。";
            default:
                return "主菜：煎鱼";
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

        if (CanUsePlatformInput() && gameplayInput.IsAttentionActive)
        {
            fishGrip = Mathf.Clamp01(fishGrip + attentionFishGripGainPerSecond * Time.deltaTime);
            fishLastPressTime = Time.time;
        }

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
            caughtFishCount++;
            fishCatchState = FishCatchState.NeedToCatch;
            fishGrip = 0f;
            fishHoldTimer = 0f;
            fishLastPressTime = Time.time;
            nextFishCatchAllowedTime = Time.time + fishCatchRetryDelay;
            fishStatusMessage = "成功抓到一条鱼。";
            fishStatusMessageTimer = 4f;
            CookingAudioController.Instance?.PlayFishCaught();
            return;
        }

        if (Time.time - fishLastPressTime > fishEscapePressGap || fishGrip <= 0.02f)
        {
            fishCatchState = FishCatchState.Escaped;
            fishGrip = 0f;
            fishHoldTimer = 0f;
            fishStatusMessage = "你按得太慢了，鱼逃走了！";
            fishStatusMessageTimer = 2.4f;
            CookingAudioController.Instance?.PlayFishEscape();
        }
    }

    private string GetFishPrompt()
    {
        var usingPlatform = CanUsePlatformInput();
        switch (fishCatchState)
        {
            case FishCatchState.NeedToCatch:
                return usingPlatform ? "煎鱼的第一步是去河边捉鱼。集中注意力开始，并持续保持专注。" : "煎鱼的第一步是去河边捉鱼。按空格开始，然后快速连续按 6 秒。";
            case FishCatchState.Catching:
                return usingPlatform ? "注意力越稳定，鱼抓得越紧。一旦走神，鱼就会挣脱。" : "按得越快，鱼抓得越紧。一旦慢下来，鱼就会挣脱。";
            case FishCatchState.Escaped:
                return "鱼溜走了，准备好后可以再试一次。";
            case FishCatchState.Caught:
                return "你已经捉到了鱼，回火堆边开始煎鱼。";
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
            return CanUsePlatformInput()
                ? $"鱼还在挣扎，稳定专注已坚持: {fishHoldTimer:0.0}/{fishCatchHoldDuration:0.0} 秒"
                : $"鱼还在挣扎，高速连按已坚持: {fishHoldTimer:0.0}/{fishCatchHoldDuration:0.0} 秒";
        }

        if (fishCatchState == FishCatchState.Caught)
        {
            return "鱼已经抓住了，回火堆边继续。";
        }

        return "走到河边后，就会出现捉鱼按钮和进度条。";
    }

    private bool IsFishCatchUnlocked()
    {
        return fishCatchState != FishCatchState.Locked;
    }

    private void ResolveGameplayInput()
    {
        if (gameplayInput != null)
        {
            return;
        }

        gameplayInput = HybridBciGameplayInput.Instance;
        if (gameplayInput == null)
        {
            gameplayInput = FindObjectOfType<HybridBciGameplayInput>();
        }
    }

    private bool CanUsePlatformInput()
    {
        return gameplayInput != null && gameplayInput.HasLiveConnection;
    }

    private bool ConsumePlatformHorizontalGesture()
    {
        return CanUsePlatformInput() && gameplayInput.ConsumeHorizontalGesture();
    }

    private bool ConsumePlatformVerticalGesture()
    {
        return CanUsePlatformInput() && gameplayInput.ConsumeVerticalGesture();
    }

    private void BeginFriedFish()
    {
        if (!pendingFrySetup || caughtFishCount <= 0)
        {
            return;
        }

        pendingFrySetup = false;
        dishPhase = DishPhase.FriedFish;
        friedFishStage = FriedFishStage.ReturnToFire;
        firePower = 0f;
        cookProgress = 0f;
        fishGrip = 0f;
        fishHoldTimer = 0f;
        fishHasBeenFlipped = false;
        fishPlacedInPan = false;
        SetSoupModeVisible(false);
        SetFryModeVisible(true);
        SetActivePot(fryPotVisual != null ? fryPotVisual : soupPotVisual);
        CacheVisualState();
        UpdateFriedFishAppearance();
        CookingAudioController.Instance?.PlayUiClose();
    }

    public void RegisterHarvestedMushroom()
    {
        harvestedMushroomCount++;
        RefreshInventoryUI();
    }

    private void SetActivePot(Transform targetPot)
    {
        if (activePotVisual == targetPot)
        {
            return;
        }

        activePotVisual = targetPot;
        if (activePotVisual != null)
        {
            activePotBaseScale = activePotVisual.localScale;
        }
    }

    private void SetSoupModeVisible(bool visible)
    {
        if (soupPotVisual != null)
        {
            soupPotVisual.gameObject.SetActive(visible);
        }

        if (soupSurface != null)
        {
            soupSurface.gameObject.SetActive(visible);
        }

        if (stirStick != null)
        {
            stirStick.gameObject.SetActive(visible);
        }
    }

    private void SetFryModeVisible(bool visible)
    {
        if (fryPotVisual != null)
        {
            fryPotVisual.gameObject.SetActive(visible);
        }

        SetFryFishVisible(visible && fishPlacedInPan);
    }

    private void SetFryFishVisible(bool visible)
    {
        if (fryFishVisual != null)
        {
            fryFishVisual.gameObject.SetActive(visible);
        }
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

        if (activePotVisual != null)
        {
            var playerPosition = playerTransform.position;
            var potPosition = activePotVisual.position;
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

