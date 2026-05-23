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

    [Header("Scene References")]
    [SerializeField] private ParticleSystem fireEffect;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform potVisual;
    [SerializeField] private Transform soupSurface;
    [SerializeField] private Renderer soupRenderer;
    [SerializeField] private Transform stirStick;
    [SerializeField] private Transform mushroomSpawnPoint;
    [SerializeField] private Transform mushroomTargetPoint;
    [SerializeField] private GameObject mushroomVisualPrefab;

    [Header("UI")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text promptText;
    [SerializeField] private Text fireValueText;
    [SerializeField] private Text progressText;
    [SerializeField] private Text mushroomCountText;
    [SerializeField] private Text fireSliderLabelText;
    [SerializeField] private Text progressSliderLabelText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Slider fireSlider;

    [Header("Cooking Settings")]
    [SerializeField] private float interactionDistance = 4.5f;
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

    private SoupStage stage = SoupStage.HeatingToQuarter;
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
    private Transform activeMushroomVisual;
    private Canvas cookingCanvas;
    private bool playerInRange;

    public void Initialize(
        ParticleSystem sceneFireEffect,
        Transform scenePlayerTransform,
        Transform scenePotVisual,
        Transform sceneSoupSurface,
        Renderer sceneSoupRenderer,
        Transform sceneStirStick,
        Transform sceneMushroomSpawnPoint,
        Transform sceneMushroomTargetPoint,
        GameObject sceneMushroomVisualPrefab,
        Text sceneTitleText,
        Text scenePromptText,
        Text sceneFireValueText,
        Text sceneProgressText,
        Text sceneMushroomCountText,
        Text sceneFireSliderLabelText,
        Text sceneProgressSliderLabelText,
        Slider sceneProgressSlider,
        Slider sceneFireSlider)
    {
        fireEffect = sceneFireEffect;
        playerTransform = scenePlayerTransform;
        potVisual = scenePotVisual;
        soupSurface = sceneSoupSurface;
        soupRenderer = sceneSoupRenderer;
        stirStick = sceneStirStick;
        mushroomSpawnPoint = sceneMushroomSpawnPoint;
        mushroomTargetPoint = sceneMushroomTargetPoint;
        mushroomVisualPrefab = sceneMushroomVisualPrefab;
        titleText = sceneTitleText;
        promptText = scenePromptText;
        fireValueText = sceneFireValueText;
        progressText = sceneProgressText;
        mushroomCountText = sceneMushroomCountText;
        fireSliderLabelText = sceneFireSliderLabelText;
        progressSliderLabelText = sceneProgressSliderLabelText;
        progressSlider = sceneProgressSlider;
        fireSlider = sceneFireSlider;
        cookingCanvas = titleText != null ? titleText.GetComponentInParent<Canvas>() : null;

        CacheVisualState();
        UpdateInteractionState();
        RefreshUI();
        UpdateFireVisuals();
    }

    private void Start()
    {
        cookingCanvas = titleText != null ? titleText.GetComponentInParent<Canvas>() : cookingCanvas;
        CacheVisualState();
        UpdateInteractionState();
        RefreshUI();
        UpdateFireVisuals();
    }

    private void Update()
    {
        UpdateInteractionState();
        if (playerInRange)
        {
            ReadInput();
        }

        UpdateFirePower();
        UpdateCookingProgress();
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

    private void ReadInput()
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

    private void UpdateFirePower()
    {
        if (!playerInRange)
        {
            return;
        }

        firePower = Mathf.MoveTowards(firePower, 0f, fireDecayPerSecond * Time.deltaTime);
    }

    private void UpdateCookingProgress()
    {
        if (!playerInRange)
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
        if (cookingCanvas != null)
        {
            cookingCanvas.enabled = playerInRange;
        }

        if (!playerInRange)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text = "\u8611\u83c7\u6c64";
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
            fireValueText.text = $"\u5f53\u524d\u706b\u529b: {firePower:0.00}";
        }

        if (progressText != null)
        {
            progressText.text = $"\u5f53\u524d\u716e\u996d\u8fdb\u5ea6: {(cookProgress * 100f):0}%";
        }

        if (mushroomCountText != null)
        {
            mushroomCountText.text = $"\u5df2\u52a0\u8611\u83c7: {mushroomsAdded}/{mushroomsNeeded}";
        }

        if (fireSliderLabelText != null)
        {
            fireSliderLabelText.text = "\u706b\u529b\u503c";
        }

        if (progressSliderLabelText != null)
        {
            progressSliderLabelText.text = "\u716e\u996d\u8fdb\u5ea6";
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

    private string GetPrompt()
    {
        switch (stage)
        {
            case SoupStage.HeatingToQuarter:
                return "\u5feb\u901f\u6309\u7a7a\u683c\u952e\u5347\u9ad8\u706b\u529b\uff0c\u628a\u8611\u83c7\u6c64\u716e\u5230 1/4 \u8fdb\u5ea6\u3002";
            case SoupStage.NeedMushroom:
                return "\u63d0\u793a\uff1a\u73b0\u5728\u9700\u8981\u52a0\u8611\u83c7\uff0c\u8bf7\u6309\u4e0a\u952e\u6216\u4e0b\u952e\u628a\u8611\u83c7\u4e22\u8fdb\u9505\u91cc\u3002";
            case SoupStage.HeatingToHalf:
                return "\u8611\u83c7\u5df2\u7ecf\u4e0b\u9505\uff0c\u7ee7\u7eed\u52a0\u70ed\uff0c\u628a\u8fdb\u5ea6\u63a8\u8fdb\u5230 2/4\u3002";
            case SoupStage.NeedFirstStir:
                return "\u63d0\u793a\uff1a\u73b0\u5728\u9700\u8981\u7b2c\u4e00\u6b21\u6405\u62cc\uff0c\u8bf7\u5de6\u53f3\u952e\u5404\u6309\u4e00\u6b21\u3002";
            case SoupStage.HeatingToThreeQuarters:
                return "\u7b2c\u4e00\u6b21\u6405\u62cc\u5b8c\u6210\uff0c\u7ee7\u7eed\u52a0\u70ed\uff0c\u628a\u8fdb\u5ea6\u63a8\u8fdb\u5230 3/4\u3002";
            case SoupStage.NeedSecondStir:
                return "\u63d0\u793a\uff1a\u73b0\u5728\u9700\u8981\u7b2c\u4e8c\u6b21\u6405\u62cc\uff0c\u8bf7\u5de6\u53f3\u952e\u5404\u6309\u4e00\u6b21\u3002";
            case SoupStage.HeatingToDone:
                return "\u6700\u540e\u6536\u6c41\u52a0\u70ed\uff0c\u9a6c\u4e0a\u5c31\u5b8c\u6210\u4e86\u3002";
            case SoupStage.Completed:
                return "\u8611\u83c7\u6c64\u5236\u4f5c\u5b8c\u6210\u3002";
            default:
                return string.Empty;
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

        if (playerTransform == null || potVisual == null)
        {
            playerInRange = false;
            return;
        }

        var playerPosition = playerTransform.position;
        var potPosition = potVisual.position;
        playerPosition.y = 0f;
        potPosition.y = 0f;
        playerInRange = Vector3.Distance(playerPosition, potPosition) <= interactionDistance;
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
}
