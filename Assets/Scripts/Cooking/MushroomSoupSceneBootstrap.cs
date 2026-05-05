using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MushroomSoupSceneBootstrap : MonoBehaviour
{
    private void Start()
    {
        BuildScene();
    }

    private void BuildScene()
    {
        var existingGame = FindObjectOfType<MushroomSoupGame>();
        if (existingGame != null)
        {
            return;
        }

        var camera = EnsureCamera();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.8f, 0.9f, 0.96f, 1f);

        EnsureDirectionalLight();
        CreateGround();

        var cookingRoot = new GameObject("Campfire Cooking Spot").transform;
        cookingRoot.position = Vector3.zero;

        CreateStoneRing(cookingRoot);
        CreateLogs(cookingRoot);
        var fire = CreateFire(cookingRoot);
        var pot = CreatePot(cookingRoot, out var soupSurface, out var soupRenderer);
        CreateDecor(cookingRoot);
        CreateInstructionSigns();

        CreateUiCanvas(
            out var titleText,
            out var promptText,
            out var fireValueText,
            out var progressText,
            out var mushroomCountText,
            out var progressSlider,
            out var fireSlider);

        var gameObject = new GameObject("MushroomSoupGame");
        var game = gameObject.AddComponent<MushroomSoupGame>();
        game.Initialize(
            fire,
            pot,
            soupSurface,
            soupRenderer,
            titleText,
            promptText,
            fireValueText,
            progressText,
            mushroomCountText,
            progressSlider,
            fireSlider);
    }

    private Camera EnsureCamera()
    {
        var camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 4.4f, -7.2f);
            camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            return camera;
        }

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.transform.position = new Vector3(0f, 4.4f, -7.2f);
        camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
        return camera;
    }

    private void EnsureDirectionalLight()
    {
        var lightObject = FindObjectOfType<Light>();
        if (lightObject != null)
        {
            lightObject.type = LightType.Directional;
            lightObject.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return;
        }

        var directionalLight = new GameObject("Directional Light").AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.intensity = 1.15f;
        directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(2.2f, 1f, 2.2f);
        ground.GetComponent<Renderer>().material.color = new Color(0.33f, 0.55f, 0.32f, 1f);
    }

    private void CreateStoneRing(Transform parent)
    {
        for (var i = 0; i < 10; i++)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rock.name = $"Stone_{i + 1}";
            rock.transform.SetParent(parent);

            var angle = i * Mathf.PI * 2f / 10f;
            rock.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.82f, 0.14f, Mathf.Sin(angle) * 0.82f);
            rock.transform.localScale = new Vector3(0.2f, 0.14f, 0.2f);
            rock.transform.localRotation = Quaternion.Euler(0f, i * 36f, 10f);
            rock.GetComponent<Renderer>().material.color = new Color(0.43f, 0.43f, 0.45f, 1f);
        }
    }

    private void CreateLogs(Transform parent)
    {
        for (var i = 0; i < 3; i++)
        {
            var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = $"Log_{i + 1}";
            log.transform.SetParent(parent);
            log.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            log.transform.localScale = new Vector3(0.11f, 0.45f, 0.11f);
            log.transform.localRotation = Quaternion.Euler(90f, i * 60f, 18f);
            log.GetComponent<Renderer>().material.color = new Color(0.33f, 0.2f, 0.1f, 1f);
        }
    }

    private ParticleSystem CreateFire(Transform parent)
    {
        var fireObject = new GameObject("Fire");
        fireObject.transform.SetParent(parent);
        fireObject.transform.localPosition = new Vector3(0f, 0.16f, 0f);

        var particleSystem = fireObject.AddComponent<ParticleSystem>();
        var main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 0.75f;
        main.startSpeed = 0.85f;
        main.startSize = 0.6f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.45f, 0.05f, 0.95f),
            new Color(1f, 0.82f, 0.18f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = -0.06f;

        var emission = particleSystem.emission;
        emission.rateOverTime = 20f;

        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.18f;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.08f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.2f, 0.2f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.8f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

        return particleSystem;
    }

    private Transform CreatePot(Transform parent, out Transform soupSurface, out Renderer soupRenderer)
    {
        var potRoot = new GameObject("Cooking Pot").transform;
        potRoot.SetParent(parent);
        potRoot.localPosition = new Vector3(0f, 1.02f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "PotBody";
        body.transform.SetParent(potRoot);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.88f, 0.38f, 0.88f);
        body.GetComponent<Renderer>().material.color = new Color(0.14f, 0.15f, 0.18f, 1f);

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "PotRim";
        rim.transform.SetParent(potRoot);
        rim.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        rim.transform.localScale = new Vector3(0.95f, 0.03f, 0.95f);
        rim.GetComponent<Renderer>().material.color = new Color(0.22f, 0.22f, 0.24f, 1f);

        var soup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        soup.name = "SoupSurface";
        soup.transform.SetParent(potRoot);
        soup.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        soup.transform.localScale = new Vector3(0.72f, 0.02f, 0.72f);
        soupRenderer = soup.GetComponent<Renderer>();
        soupRenderer.material.color = new Color(0.55f, 0.46f, 0.24f, 1f);
        soupSurface = soup.transform;

        CreateTripodLeg(potRoot, new Vector3(-0.45f, -0.65f, -0.38f), -16f);
        CreateTripodLeg(potRoot, new Vector3(0.45f, -0.65f, -0.38f), 16f);
        CreateTripodLeg(potRoot, new Vector3(0f, -0.65f, 0.48f), 0f);
        CreateHandle(potRoot);

        return potRoot;
    }

    private void CreateTripodLeg(Transform parent, Vector3 localPosition, float zAngle)
    {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.name = "PotLeg";
        leg.transform.SetParent(parent);
        leg.transform.localPosition = localPosition;
        leg.transform.localScale = new Vector3(0.06f, 0.75f, 0.06f);
        leg.transform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
        leg.GetComponent<Renderer>().material.color = new Color(0.22f, 0.16f, 0.1f, 1f);
    }

    private void CreateHandle(Transform parent)
    {
        var leftHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftHandle.name = "HandleLeft";
        leftHandle.transform.SetParent(parent);
        leftHandle.transform.localPosition = new Vector3(-0.72f, 0.16f, 0f);
        leftHandle.transform.localScale = new Vector3(0.18f, 0.04f, 0.04f);
        leftHandle.GetComponent<Renderer>().material.color = new Color(0.24f, 0.24f, 0.26f, 1f);

        var rightHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightHandle.name = "HandleRight";
        rightHandle.transform.SetParent(parent);
        rightHandle.transform.localPosition = new Vector3(0.72f, 0.16f, 0f);
        rightHandle.transform.localScale = new Vector3(0.18f, 0.04f, 0.04f);
        rightHandle.GetComponent<Renderer>().material.color = new Color(0.24f, 0.24f, 0.26f, 1f);
    }

    private void CreateDecor(Transform parent)
    {
        for (var i = 0; i < 4; i++)
        {
            var mushroom = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mushroom.name = $"MushroomDecor_{i + 1}";
            mushroom.transform.SetParent(parent);
            mushroom.transform.localPosition = new Vector3(-1.7f + i * 0.28f, 0.18f, 1.1f + i * 0.08f);
            mushroom.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
            mushroom.GetComponent<Renderer>().material.color = i % 2 == 0
                ? new Color(0.88f, 0.26f, 0.18f, 1f)
                : new Color(0.91f, 0.82f, 0.56f, 1f);
        }
    }

    private void CreateInstructionSigns()
    {
        CreateWorldLabel(new Vector3(-3.5f, 1.7f, 0f), "\u7a7a\u683c: \u5347\u9ad8\u706b\u529b");
        CreateWorldLabel(new Vector3(-3.5f, 1.3f, 0f), "\u4e0a / \u4e0b: \u52a0 1 \u9897\u8611\u83c7");
        CreateWorldLabel(new Vector3(-3.5f, 0.9f, 0f), "\u5de6 + \u53f3: \u6405\u62cc\u4e00\u6b21");
    }

    private void CreateWorldLabel(Vector3 position, string content)
    {
        var textObject = new GameObject(content);
        textObject.transform.position = position;
        var text = textObject.AddComponent<TextMeshPro>();
        text.text = content;
        text.fontSize = 4f;
        text.color = new Color(0.12f, 0.1f, 0.08f, 1f);
        text.alignment = TextAlignmentOptions.Left;
    }

    private void CreateUiCanvas(
        out TextMeshProUGUI titleText,
        out TextMeshProUGUI promptText,
        out TextMeshProUGUI fireValueText,
        out TextMeshProUGUI progressText,
        out TextMeshProUGUI mushroomCountText,
        out Slider progressSlider,
        out Slider fireSlider)
    {
        var canvasObject = new GameObject("Cooking UI");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        titleText = CreateText(canvas.transform, "Title", new Vector2(30f, -30f), new Vector2(420f, 60f), 36, FontStyles.Bold);
        promptText = CreateText(canvas.transform, "Prompt", new Vector2(30f, -90f), new Vector2(900f, 90f), 28, FontStyles.Normal);
        fireValueText = CreateText(canvas.transform, "FireText", new Vector2(30f, -190f), new Vector2(320f, 45f), 24, FontStyles.Normal);
        progressText = CreateText(canvas.transform, "ProgressText", new Vector2(30f, -240f), new Vector2(320f, 45f), 24, FontStyles.Normal);
        mushroomCountText = CreateText(canvas.transform, "MushroomText", new Vector2(30f, -290f), new Vector2(320f, 45f), 24, FontStyles.Normal);

        fireSlider = CreateSlider(canvas.transform, "FireSlider", new Vector2(30f, -350f), new Color(0.95f, 0.45f, 0.08f, 1f));
        progressSlider = CreateSlider(canvas.transform, "ProgressSlider", new Vector2(30f, -410f), new Color(0.62f, 0.76f, 0.32f, 1f));
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent);

        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        return text;
    }

    private Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, Color fillColor)
    {
        var sliderObject = new GameObject(name);
        sliderObject.transform.SetParent(parent);

        var rect = sliderObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(360f, 36f);

        var background = new GameObject("Background");
        background.transform.SetParent(sliderObject.transform);
        var bgRect = background.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.25f);

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObject.transform);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(6f, 6f);
        fillAreaRect.offsetMax = new Vector2(-6f, -6f);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = fillColor;

        var slider = sliderObject.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.targetGraphic = fillImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        return slider;
    }
}
