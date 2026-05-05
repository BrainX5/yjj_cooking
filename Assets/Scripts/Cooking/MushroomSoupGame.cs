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
    [SerializeField] private Transform potVisual;
    [SerializeField] private Transform soupSurface;
    [SerializeField] private Renderer soupRenderer;
    [SerializeField] private Transform stirStick;
    [SerializeField] private Transform mushroomSpawnPoint;
    [SerializeField] private Transform mushroomTargetPoint;

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

    public void Initialize(
        ParticleSystem sceneFireEffect,
        Transform scenePotVisual,
        Transform sceneSoupSurface,
        Renderer sceneSoupRenderer,
        Transform sceneStirStick,
        Transform sceneMushroomSpawnPoint,
        Transform sceneMushroomTargetPoint,
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
        potVisual = scenePotVisual;
        soupSurface = sceneSoupSurface;
        soupRenderer = sceneSoupRenderer;
        stirStick = sceneStirStick;
        mushroomSpawnPoint = sceneMushroomSpawnPoint;
        mushroomTargetPoint = sceneMushroomTargetPoint;
        titleText = sceneTitleText;
        promptText = scenePromptText;
        fireValueText = sceneFireValueText;
        progressText = sceneProgressText;
        mushroomCountText = sceneMushroomCountText;
        fireSliderLabelText = sceneFireSliderLabelText;
        progressSliderLabelText = sceneProgressSliderLabelText;
        progressSlider = sceneProgressSlider;
        fireSlider = sceneFireSlider;

        CacheVisualState();
        RefreshUI();
        UpdateFireVisuals();
    }

    private void Start()
    {
        CacheVisualState();
        RefreshUI();
        UpdateFireVisuals();
    }

    private void Update()
    {
        ReadInput();
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
        firePower = Mathf.MoveTowards(firePower, 0f, fireDecayPerSecond * Time.deltaTime);
    }

    private void UpdateCookingProgress()
    {
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

        if (stage == SoupStage.NeedFirstStir)
        {
            PlayStirAnimation();
            stage = SoupStage.HeatingToThreeQuarters;
            NudgeSoupSurface(1.18f);
            TintSoup(new Color(0.76f, 0.68f, 0.43f, 1f));
            return;
        }

        if (stage == SoupStage.NeedSecondStir)
        {
            PlayStirAnimation();
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
            var angle = normalized * Mathf.PI * 6f;
            var swirlX = Mathf.Sin(angle) * 0.22f;
            var swirlZ = Mathf.Cos(angle) * 0.22f;

            stirStick.localPosition = stirStickBaseLocalPosition + new Vector3(swirlX, -0.15f, swirlZ);
            stirStick.localRotation = stirStickBaseLocalRotation * Quaternion.Euler(0f, normalized * 1080f, -40f + Mathf.Sin(angle * 0.5f) * 16f);

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
        var duration = 0.8f;
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

        var mushroom = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
        mushroom.name = "DroppingMushroom";
        mushroom.position = mushroomSpawnPoint.position;
        mushroom.localScale = Vector3.one * 0.34f;

        var renderer = mushroom.GetComponent<Renderer>();
        renderer.material.color = new Color(0.9f, 0.82f, 0.62f, 1f);

        var collider = mushroom.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        activeMushroomVisual = mushroom;
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
                return "蘑菇汤制作完成。";
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
}
