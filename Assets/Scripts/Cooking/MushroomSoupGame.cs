using TMPro;
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

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI fireValueText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI mushroomCountText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Slider fireSlider;

    [Header("Cooking Settings")]
    [SerializeField] private float fireDecayPerSecond = 0.9f;
    [SerializeField] private float fireGainPerSpace = 0.18f;
    [SerializeField] private float maxFirePower = 1f;
    [SerializeField] private float maxCookRatePerSecond = 0.22f;
    [SerializeField] private int mushroomsNeeded = 1;

    private SoupStage stage = SoupStage.HeatingToQuarter;
    private float firePower;
    private float cookProgress;
    private int mushroomsAdded;
    private bool leftStirQueued;
    private bool rightStirQueued;
    private Vector3 soupBaseScale = Vector3.one;

    public void Initialize(
        ParticleSystem sceneFireEffect,
        Transform scenePotVisual,
        Transform sceneSoupSurface,
        Renderer sceneSoupRenderer,
        TextMeshProUGUI sceneTitleText,
        TextMeshProUGUI scenePromptText,
        TextMeshProUGUI sceneFireValueText,
        TextMeshProUGUI sceneProgressText,
        TextMeshProUGUI sceneMushroomCountText,
        Slider sceneProgressSlider,
        Slider sceneFireSlider)
    {
        fireEffect = sceneFireEffect;
        potVisual = scenePotVisual;
        soupSurface = sceneSoupSurface;
        soupRenderer = sceneSoupRenderer;
        titleText = sceneTitleText;
        promptText = scenePromptText;
        fireValueText = sceneFireValueText;
        progressText = sceneProgressText;
        mushroomCountText = sceneMushroomCountText;
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
    }

    private void ReadInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            firePower = Mathf.Clamp(firePower + fireGainPerSpace, 0f, maxFirePower);
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
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
            stage = SoupStage.HeatingToThreeQuarters;
            NudgeSoupSurface();
            TintSoup(new Color(0.76f, 0.68f, 0.43f, 1f));
            return;
        }

        if (stage == SoupStage.NeedSecondStir)
        {
            stage = SoupStage.HeatingToDone;
            NudgeSoupSurface();
            TintSoup(new Color(0.8f, 0.73f, 0.5f, 1f));
        }
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
            emission.rateOverTime = 10f + firePower * 45f;

            var main = fireEffect.main;
            main.startSpeed = 0.6f + firePower * 1.8f;
            main.startSize = 0.45f + firePower * 0.8f;

            if (!fireEffect.isPlaying)
            {
                fireEffect.Play();
            }
        }

        if (potVisual != null)
        {
            var targetScale = new Vector3(1f + firePower * 0.04f, 1f, 1f + firePower * 0.04f);
            potVisual.localScale = Vector3.Lerp(potVisual.localScale, targetScale, Time.deltaTime * 4f);
        }
    }

    private void RefreshUI()
    {
        if (titleText != null)
        {
            titleText.text = "\u8611\u83c7\u6c64";
        }

        if (promptText != null)
        {
            promptText.text = GetPrompt();
        }

        if (fireValueText != null)
        {
            fireValueText.text = $"\u706b\u529b\u503c: {firePower:0.00}";
        }

        if (progressText != null)
        {
            progressText.text = $"\u70f9\u996a\u8fdb\u5ea6: {(cookProgress * 100f):0}%";
        }

        if (mushroomCountText != null)
        {
            mushroomCountText.text = $"\u5df2\u52a0\u8611\u83c7: {mushroomsAdded}/{mushroomsNeeded}";
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
                return "\u5feb\u901f\u6309\u7a7a\u683c\u952e\u5347\u9ad8\u706b\u529b\uff0c\u628a\u6c64\u716e\u5230 1/4 \u8fdb\u5ea6\u3002";
            case SoupStage.NeedMushroom:
                return "\u8fdb\u5ea6\u5230 1/4 \u4e86\uff0c\u8bf7\u6309\u4e0a\u952e\u6216\u4e0b\u952e\u52a0\u5165 1 \u9897\u8611\u83c7\u3002";
            case SoupStage.HeatingToHalf:
                return "\u7ee7\u7eed\u52a0\u70ed\uff0c\u628a\u6c64\u716e\u5230 2/4 \u8fdb\u5ea6\u3002";
            case SoupStage.NeedFirstStir:
                return "\u8fdb\u5ea6\u5230 2/4 \u4e86\uff0c\u8bf7\u5de6\u53f3\u952e\u5404\u6309\u4e00\u6b21\u5b8c\u6210\u7b2c\u4e00\u6b21\u6405\u62cc\u3002";
            case SoupStage.HeatingToThreeQuarters:
                return "\u7ee7\u7eed\u52a0\u70ed\uff0c\u628a\u6c64\u716e\u5230 3/4 \u8fdb\u5ea6\u3002";
            case SoupStage.NeedSecondStir:
                return "\u8fdb\u5ea6\u5230 3/4 \u4e86\uff0c\u8bf7\u5de6\u53f3\u952e\u5404\u6309\u4e00\u6b21\u5b8c\u6210\u7b2c\u4e8c\u6b21\u6405\u62cc\u3002";
            case SoupStage.HeatingToDone:
                return "\u6700\u540e\u52a0\u70ed\u6536\u6c41\uff0c\u9a6c\u4e0a\u5c31\u5b8c\u6210\u4e86\u3002";
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

    private void NudgeSoupSurface()
    {
        if (soupSurface == null)
        {
            return;
        }

        soupSurface.localScale = new Vector3(
            soupBaseScale.x * 1.08f,
            soupBaseScale.y,
            soupBaseScale.z * 1.08f);
    }
}
