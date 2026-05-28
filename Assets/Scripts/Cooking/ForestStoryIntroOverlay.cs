using UnityEngine;
using UnityEngine.UI;

public class ForestStoryIntroOverlay : MonoBehaviour
{
    [SerializeField] private string titleText = "\u68ee\u53a8\u5c0f\u5f53\u5bb6";
    [SerializeField] private string bodyText =
        "\u6b22\u8fce\u6765\u5230\u6668\u96fe\u68ee\u6797\u3002\n\n" +
        "\u4eca\u5929\u4f60\u8981\u5316\u8eab\u68ee\u6797\u5c0f\u53a8\u5e08\uff0c" +
        "\u7528\u4e13\u6ce8\u4e0e\u5fc3\u610f\uff0c\u4e3a\u68ee\u6797\u4f19\u4f34\u51c6\u5907\u4e00\u987f\u6696\u5fc3\u5143\u6c14\u9910\u3002\n\n" +
        "\u4f60\u53ef\u4ee5\u5148\u5728\u8611\u83c7\u623f\u9644\u8fd1\u91c7\u6458\u65b0\u9c9c\u8611\u83c7\uff0c" +
        "\u518d\u5230\u6797\u95f4\u5c0f\u6eaa\u8fb9\u89c2\u5bdf\u5c0f\u9c7c\uff0c\u6700\u540e\u56de\u5230\u6797\u95f4\u5c0f\u53a8\u623f\u719f\u716e\u4e00\u9505\u9999\u55b7\u55b7\u7684\u8611\u83c7\u6c64\u3002\n\n" +
        "\u4e0d\u7740\u6025\uff0c\u6162\u6162\u6765\u3002\n" +
        "\u770b\u6e05\u695a\u3001\u8d70\u8fd1\u5b83\u3001\u5b8c\u6210\u4f60\u7684\u6bcf\u4e00\u6b65\u5c0f\u4efb\u52a1\u3002";
    [SerializeField] private string footerText = "\u70b9\u5934\u4e00\u6b21\uff0c\u6216\u6309\u7a7a\u683c\u952e\u3001\u70b9\u51fb\u5377\u8f74\uff0c\u5f00\u59cb\u4f60\u7684\u68ee\u6797\u5c0f\u53a8\u623f\u5192\u9669";

    private Canvas overlayCanvas;
    private Font uiFont;
    private bool isVisible;

    public static bool IsBlockingInput { get; private set; }

    private void Start()
    {
        BuildOverlay();
        Show();
    }

    private void Update()
    {
        if (!isVisible)
        {
            return;
        }

        var gameplayInput = HybridBciGameplayInput.Instance;
        if (gameplayInput != null &&
            gameplayInput.HasLiveConnection &&
            gameplayInput.ConsumeVerticalGesture())
        {
            Hide();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetMouseButtonDown(0))
        {
            Hide();
        }
    }

    private void OnDestroy()
    {
        if (isVisible)
        {
            IsBlockingInput = false;
        }
    }

    private void BuildOverlay()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "SimSun", "Arial Unicode MS" },
            32);

        var canvasObject = new GameObject("ForestStoryIntroCanvas");
        canvasObject.transform.SetParent(transform, false);
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 120;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        var dimmer = CreateImage(
            canvasObject.transform,
            "Dimmer",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Color(0.17f, 0.12f, 0.08f, 0.58f));
        dimmer.rectTransform.sizeDelta = Vector2.zero;
        dimmer.rectTransform.anchorMin = Vector2.zero;
        dimmer.rectTransform.anchorMax = Vector2.one;

        var scroll = CreateImage(
            canvasObject.transform,
            "StoryScroll",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f),
            new Vector2(1120f, 760f),
            new Color(0.93f, 0.84f, 0.65f, 0.98f));
        scroll.rectTransform.SetAsLastSibling();

        AddOutline(scroll.gameObject, new Color(0.33f, 0.2f, 0.09f, 0.9f), new Vector2(4f, -4f));
        AddShadow(scroll.gameObject, new Color(0.2f, 0.11f, 0.04f, 0.45f), new Vector2(14f, -16f));

        CreateRoll(scroll.transform, "LeftRoll", new Vector2(-532f, 0f));
        CreateRoll(scroll.transform, "RightRoll", new Vector2(532f, 0f));
        CreateEdgeBand(scroll.transform, "TopBand", new Vector2(0f, 314f), new Vector2(1000f, 54f));
        CreateEdgeBand(scroll.transform, "BottomBand", new Vector2(0f, -314f), new Vector2(1000f, 54f));

        var title = CreateText(
            scroll.transform,
            "Title",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -78f),
            new Vector2(840f, 80f),
            42,
            FontStyle.Bold,
            new Color(0.37f, 0.19f, 0.06f, 1f),
            TextAnchor.MiddleCenter);
        title.text = titleText;

        var body = CreateText(
            scroll.transform,
            "Body",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -8f),
            new Vector2(820f, 430f),
            28,
            FontStyle.Normal,
            new Color(0.3f, 0.19f, 0.1f, 1f),
            TextAnchor.UpperLeft);
        body.text = bodyText;
        body.lineSpacing = 1.2f;

        var footer = CreateText(
            scroll.transform,
            "Footer",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 62f),
            new Vector2(860f, 72f),
            26,
            FontStyle.Bold,
            new Color(0.63f, 0.24f, 0.08f, 1f),
            TextAnchor.MiddleCenter);
        footer.text = footerText;
    }

    private void Show()
    {
        isVisible = true;
        IsBlockingInput = true;
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = true;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Hide()
    {
        isVisible = false;
        IsBlockingInput = false;
        if (overlayCanvas != null)
        {
            overlayCanvas.enabled = false;
        }

        Time.timeScale = 1f;
    }

    private Image CreateImage(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        var target = new GameObject(name);
        target.transform.SetParent(parent, false);

        var rect = target.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = target.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        FontStyle fontStyle,
        Color color,
        TextAnchor alignment)
    {
        var target = new GameObject(name);
        target.transform.SetParent(parent, false);

        var rect = target.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var outline = target.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.18f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var text = target.AddComponent<Text>();
        text.font = uiFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void CreateRoll(Transform parent, string name, Vector2 anchoredPosition)
    {
        var roll = CreateImage(
            parent,
            name,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            new Vector2(90f, 690f),
            new Color(0.73f, 0.48f, 0.24f, 1f));
        AddOutline(roll.gameObject, new Color(0.34f, 0.18f, 0.06f, 0.8f), new Vector2(3f, -3f));
    }

    private void CreateEdgeBand(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        var band = CreateImage(
            parent,
            name,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            anchoredPosition,
            size,
            new Color(0.75f, 0.54f, 0.29f, 0.95f));
        AddOutline(band.gameObject, new Color(0.39f, 0.22f, 0.08f, 0.55f), new Vector2(2f, -2f));
    }

    private void AddOutline(GameObject target, Color color, Vector2 offset)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = offset;
    }

    private void AddShadow(GameObject target, Color color, Vector2 offset)
    {
        var shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = offset;
    }
}
