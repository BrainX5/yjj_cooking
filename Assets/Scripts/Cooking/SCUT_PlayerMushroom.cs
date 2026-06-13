using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SCUT_PlayerMushroom : MonoBehaviour
{
    public int count = 0;
    public KeyCode pickupKey = KeyCode.Space;

    [Header("兼容旧版场景面板")]
    public GameObject weightlessUIPanel;

    [Header("提示框文案")]
    [SerializeField] private string promptTitle = "失重室邀请函";
    [SerializeField] private string keyboardPrompt =
        "诶呀，风精灵把你的蘑菇吹跑啦，还混进了好多石块和杂草。\n按空格进去失重室，把真正的蘑菇一朵朵找回来吧。";
    [SerializeField] private string platformPrompt =
        "诶呀，风精灵把你的蘑菇吹跑啦，还混进了好多石块和杂草。\n点头进入失重室，再用持续专注把真正的蘑菇稳稳吸回来吧。";
    [SerializeField] private int titleFontSize = 34;
    [SerializeField] private int promptFontSize = 28;
    [SerializeField] private Vector2 dialogPosition = new Vector2(-360f, -52f);
    [SerializeField] private Vector2 dialogSize = new Vector2(760f, 248f);
    [SerializeField] private Color dialogFallbackColor = new Color(0.79f, 0.35f, 0.2f, 0.96f);
    [SerializeField] private Color titleColor = new Color(1f, 0.98f, 0.93f, 1f);
    [SerializeField] private Color promptColor = new Color(1f, 0.95f, 0.86f, 1f);
    [SerializeField] private string dialogSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/Panels_SpriteSheet.png";
    [SerializeField] private string dialogSpriteName = "Panels_SpriteSheet_1";
    [SerializeField] private string iconSpriteSheetPath = "Assets/ONDAD/Alert Panels/Graphics/UI_SpriteSheet.png";
    [SerializeField] private string iconSpriteName = "UI_SpriteSheet_17";

    private bool isGameStarted;
    private bool isNearSpecialMushroom;
    private GameObject currentSpecialMushroom;
    private Canvas promptCanvas;
    private Image promptBackgroundImage;
    private Text titleText;
    private Text promptText;
    private Font uiFont;
    private Sprite dialogSprite;
    private Sprite iconSprite;
    private HybridBciGameplayInput gameplayInput;

    public void StartGame()
    {
        isGameStarted = true;
        count = 0;
        HideLegacyPanel();
        HidePrompt();
        Debug.Log("【游戏正式开始】采蘑菇脚本激活。");
    }

    private void Start()
    {
        EnsurePromptUi();
        StartGame();
    }

    private void Update()
    {
        if (!isGameStarted)
        {
            return;
        }

        ResolveGameplayInput();

        if (isNearSpecialMushroom && currentSpecialMushroom != null)
        {
            ShowPrompt();
            if (Input.GetKeyDown(pickupKey) || ConsumePlatformEnter())
            {
                HidePrompt();
                isNearSpecialMushroom = false;

                var dryad = FindObjectOfType<SCUT_FlowerDryadController>();
                if (dryad != null)
                {
                    dryad.TriggerSpecialMushroomEvent(currentSpecialMushroom);
                }
                else
                {
                    Debug.LogError("场景中未找到 SCUT_FlowerDryadController 脚本。");
                }
            }

            return;
        }

        HidePrompt();

        if (Input.GetKeyDown(pickupKey))
        {
            count++;
            Debug.Log("普通Mushroom Picked! Count: " + count);
        }
    }

    private void OnDisable()
    {
        HidePrompt();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name != "SpecialMushroom")
        {
            return;
        }

        isNearSpecialMushroom = true;
        currentSpecialMushroom = other.gameObject;
        ShowPrompt();
        Debug.Log("【检测成功】靠近了特殊蘑菇，已弹出失重室提示框。");
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name != "SpecialMushroom")
        {
            return;
        }

        isNearSpecialMushroom = false;
        currentSpecialMushroom = null;
        HidePrompt();
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

    private bool ConsumePlatformEnter()
    {
        return gameplayInput != null && gameplayInput.HasLiveConnection && gameplayInput.ConsumeVerticalGesture();
    }

    private void HideLegacyPanel()
    {
        if (weightlessUIPanel != null)
        {
            weightlessUIPanel.SetActive(false);
        }
    }

    private void EnsurePromptUi()
    {
        if (promptCanvas != null)
        {
            return;
        }

        HideLegacyPanel();

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "SimSun", "Arial Unicode MS" },
            promptFontSize);

        var canvasObject = new GameObject("Weightless Entry Prompt");
        promptCanvas = canvasObject.AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 35;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        var dialogObject = new GameObject("WeightlessDialog");
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
            var iconObject = new GameObject("WeightlessDialogIcon");
            iconObject.transform.SetParent(dialogObject.transform, false);

            var iconRect = iconObject.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(18f, -12f);
            iconRect.sizeDelta = new Vector2(84f, 84f);

            var iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }

        titleText = CreateText(
            dialogObject.transform,
            "WeightlessTitle",
            new Vector2(102f, -26f),
            new Vector2(dialogSize.x - 152f, 42f),
            titleFontSize,
            FontStyle.Bold,
            titleColor,
            TextAnchor.MiddleCenter);
        titleText.text = promptTitle;

        promptText = CreateText(
            dialogObject.transform,
            "WeightlessPrompt",
            new Vector2(58f, -84f),
            new Vector2(dialogSize.x - 116f, dialogSize.y - 108f),
            promptFontSize,
            FontStyle.Bold,
            promptColor,
            TextAnchor.UpperLeft);

        promptCanvas.enabled = false;
    }

    private void ShowPrompt()
    {
        if (promptCanvas == null)
        {
            EnsurePromptUi();
        }

        HideLegacyPanel();

        if (titleText != null)
        {
            titleText.text = promptTitle;
        }

        if (promptText != null)
        {
            var usePlatformPrompt = gameplayInput != null && gameplayInput.HasLiveConnection;
            promptText.text = usePlatformPrompt ? platformPrompt : keyboardPrompt;
        }

        if (promptCanvas != null)
        {
            promptCanvas.enabled = true;
        }
    }

    private void HidePrompt()
    {
        HideLegacyPanel();
        if (promptCanvas != null)
        {
            promptCanvas.enabled = false;
        }
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
