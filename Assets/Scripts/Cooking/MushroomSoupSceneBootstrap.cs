using UnityEngine;
using UnityEngine.UI;

public class MushroomSoupSceneBootstrap : MonoBehaviour
{
    [Header("Scene Placement")]
    [SerializeField] private bool useExistingSceneEnvironment = true;
    [SerializeField] private bool anchorToExistingFire = true;
    [SerializeField] private Vector3 fallbackCookingPosition = new Vector3(-3.08f, 17.76f, -36.1f);
    [SerializeField] private Vector3 instructionOffset = new Vector3(-3.5f, 1.7f, 0f);
    [SerializeField] private Vector3 existingScenePotOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private float existingScenePotScale = 2.2f;

    private Font uiFont;

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

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "SimSun", "Arial Unicode MS" },
            30);

        if (!useExistingSceneEnvironment)
        {
            var camera = EnsureCamera();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.8f, 0.9f, 0.96f, 1f);
            EnsureDirectionalLight();
            CreateGround();
        }

        var cookingRoot = new GameObject("Campfire Cooking Spot").transform;
        cookingRoot.position = ResolveCookingPosition();

        if (!useExistingSceneEnvironment)
        {
            CreateStoneRing(cookingRoot);
            CreateLogs(cookingRoot);
        }

        var fire = ResolveFireEffect(cookingRoot);
        var pot = CreatePot(
            cookingRoot,
            out var soupSurface,
            out var soupRenderer,
            out var stirStick,
            out var mushroomSpawnPoint,
            out var mushroomTargetPoint);

        if (!useExistingSceneEnvironment)
        {
            CreateDecor(cookingRoot);
        }

        CreateUiCanvas(
            out var titleText,
            out var promptText,
            out var fireValueText,
            out var progressText,
            out var mushroomCountText,
            out var fireSliderLabelText,
            out var progressSliderLabelText,
            out var progressSlider,
            out var fireSlider);

        var gameObject = new GameObject("MushroomSoupGame");
        var game = gameObject.AddComponent<MushroomSoupGame>();
        game.Initialize(
            fire,
            pot,
            soupSurface,
            soupRenderer,
            stirStick,
            mushroomSpawnPoint,
            mushroomTargetPoint,
            titleText,
            promptText,
            fireValueText,
            progressText,
            mushroomCountText,
            fireSliderLabelText,
            progressSliderLabelText,
            progressSlider,
            fireSlider);
    }

    private Camera EnsureCamera()
    {
        var camera = Camera.main;
        if (camera == null)
        {
            camera = FindObjectOfType<Camera>();
        }

        if (camera != null)
        {
            if (!useExistingSceneEnvironment)
            {
                camera.transform.position = new Vector3(0f, 4.4f, -7.2f);
                camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            }

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

    private Vector3 ResolveCookingPosition()
    {
        if (!anchorToExistingFire)
        {
            return fallbackCookingPosition;
        }

        var existingFire = FindExistingFireParticle();
        if (existingFire != null)
        {
            return existingFire.transform.position;
        }

        return fallbackCookingPosition;
    }

    private ParticleSystem ResolveFireEffect(Transform parent)
    {
        if (useExistingSceneEnvironment && anchorToExistingFire)
        {
            var existingFire = FindExistingFireParticle();
            if (existingFire != null)
            {
                return existingFire;
            }
        }

        return CreateFire(parent);
    }

    private ParticleSystem FindExistingFireParticle()
    {
        var particles = FindObjectsOfType<ParticleSystem>();
        foreach (var particle in particles)
        {
            if (particle == null)
            {
                continue;
            }

            var objectName = particle.gameObject.name;
            if (objectName.Contains("Fire_PSys") || objectName.Contains("Camp fire") || objectName.Contains("Fire"))
            {
                return particle;
            }
        }

        return null;
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
        main.startLifetime = 0.65f;
        main.startSpeed = 0.4f;
        main.startSize = 0.24f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.45f, 0.05f, 0.95f),
            new Color(1f, 0.82f, 0.18f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = -0.06f;

        var emission = particleSystem.emission;
        emission.rateOverTime = 8f;

        var shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 16f;
        shape.radius = 0.09f;

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        return particleSystem;
    }

    private Transform CreatePot(
        Transform parent,
        out Transform soupSurface,
        out Renderer soupRenderer,
        out Transform stirStick,
        out Transform mushroomSpawnPoint,
        out Transform mushroomTargetPoint)
    {
        var potRoot = new GameObject("Cooking Pot").transform;
        potRoot.SetParent(parent);

        if (useExistingSceneEnvironment)
        {
            potRoot.localPosition = existingScenePotOffset;
            potRoot.localScale = Vector3.one * existingScenePotScale;
        }
        else
        {
            potRoot.localPosition = new Vector3(0f, 1.02f, 0f);
            potRoot.localScale = Vector3.one;
        }

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "PotBody";
        body.transform.SetParent(potRoot);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.92f, 0.44f, 0.92f);
        body.GetComponent<Renderer>().material.color = new Color(0.2f, 0.22f, 0.26f, 1f);
        DestroyCollider(body);

        var innerWall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        innerWall.name = "PotInnerWall";
        innerWall.transform.SetParent(potRoot);
        innerWall.transform.localPosition = new Vector3(0f, 0.16f, 0f);
        innerWall.transform.localScale = new Vector3(0.72f, 0.12f, 0.72f);
        innerWall.GetComponent<Renderer>().material.color = new Color(0.09f, 0.09f, 0.1f, 1f);
        DestroyCollider(innerWall);

        var openHole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        openHole.name = "PotOpenHole";
        openHole.transform.SetParent(potRoot);
        openHole.transform.localPosition = new Vector3(0f, 0.235f, 0f);
        openHole.transform.localScale = new Vector3(0.44f, 0.01f, 0.44f);
        openHole.GetComponent<Renderer>().material.color = new Color(0.03f, 0.03f, 0.03f, 1f);
        DestroyCollider(openHole);

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "PotRim";
        rim.transform.SetParent(potRoot);
        rim.transform.localPosition = new Vector3(0f, 0.29f, 0f);
        rim.transform.localScale = new Vector3(1.02f, 0.03f, 1.02f);
        rim.GetComponent<Renderer>().material.color = new Color(0.34f, 0.35f, 0.38f, 1f);
        DestroyCollider(rim);

        var soup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        soup.name = "SoupSurface";
        soup.transform.SetParent(potRoot);
        soup.transform.localPosition = new Vector3(0f, 0.215f, 0f);
        soup.transform.localScale = new Vector3(0.43f, 0.018f, 0.43f);
        soupRenderer = soup.GetComponent<Renderer>();
        soupRenderer.material.color = new Color(0.91f, 0.84f, 0.56f, 1f);
        soupSurface = soup.transform;
        DestroyCollider(soup);

        CreateTripodLeg(potRoot, new Vector3(-0.45f, -0.65f, -0.38f), -16f);
        CreateTripodLeg(potRoot, new Vector3(0.45f, -0.65f, -0.38f), 16f);
        CreateTripodLeg(potRoot, new Vector3(0f, -0.65f, 0.48f), 0f);
        CreateHandle(potRoot);

        stirStick = CreateStirStick(potRoot);
        mushroomSpawnPoint = CreateMarker(potRoot, "MushroomSpawnPoint", new Vector3(-1.1f, 1.55f, 0f));
        mushroomTargetPoint = CreateMarker(potRoot, "MushroomTargetPoint", new Vector3(0f, 0.28f, 0f));

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
        DestroyCollider(leg);
    }

    private void CreateHandle(Transform parent)
    {
        var leftHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftHandle.name = "HandleLeft";
        leftHandle.transform.SetParent(parent);
        leftHandle.transform.localPosition = new Vector3(-0.76f, 0.22f, 0f);
        leftHandle.transform.localScale = new Vector3(0.22f, 0.05f, 0.05f);
        leftHandle.GetComponent<Renderer>().material.color = new Color(0.24f, 0.24f, 0.26f, 1f);
        DestroyCollider(leftHandle);

        var rightHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightHandle.name = "HandleRight";
        rightHandle.transform.SetParent(parent);
        rightHandle.transform.localPosition = new Vector3(0.76f, 0.22f, 0f);
        rightHandle.transform.localScale = new Vector3(0.22f, 0.05f, 0.05f);
        rightHandle.GetComponent<Renderer>().material.color = new Color(0.24f, 0.24f, 0.26f, 1f);
        DestroyCollider(rightHandle);
    }

    private Transform CreateStirStick(Transform parent)
    {
        var stirRoot = new GameObject("StirStick").transform;
        stirRoot.SetParent(parent);
        stirRoot.localPosition = new Vector3(0.48f, 0.9f, 0f);
        stirRoot.localRotation = Quaternion.Euler(0f, 0f, -28f);

        var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stick.name = "Stick";
        stick.transform.SetParent(stirRoot);
        stick.transform.localPosition = Vector3.zero;
        stick.transform.localScale = new Vector3(0.05f, 0.62f, 0.05f);
        stick.GetComponent<Renderer>().material.color = new Color(0.48f, 0.31f, 0.14f, 1f);
        DestroyCollider(stick);

        var spoon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spoon.name = "SpoonHead";
        spoon.transform.SetParent(stirRoot);
        spoon.transform.localPosition = new Vector3(0f, -0.62f, 0f);
        spoon.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
        spoon.GetComponent<Renderer>().material.color = new Color(0.72f, 0.72f, 0.76f, 1f);
        DestroyCollider(spoon);

        return stirRoot;
    }

    private Transform CreateMarker(Transform parent, string name, Vector3 localPosition)
    {
        var marker = new GameObject(name).transform;
        marker.SetParent(parent);
        marker.localPosition = localPosition;
        marker.localRotation = Quaternion.identity;
        marker.localScale = Vector3.one;
        return marker;
    }

    private void DestroyCollider(GameObject target)
    {
        var collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
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
            DestroyCollider(mushroom);
        }
    }

    private void CreateUiCanvas(
        out Text titleText,
        out Text promptText,
        out Text fireValueText,
        out Text progressText,
        out Text mushroomCountText,
        out Text fireSliderLabelText,
        out Text progressSliderLabelText,
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

        titleText = CreateText(canvas.transform, "Title", new Vector2(40f, -40f), new Vector2(420f, 60f), 34, FontStyle.Bold);
        promptText = CreateText(canvas.transform, "Prompt", new Vector2(40f, -100f), new Vector2(980f, 120f), 28, FontStyle.Bold);
        fireValueText = CreateText(canvas.transform, "FireText", new Vector2(40f, -225f), new Vector2(420f, 42f), 22, FontStyle.Normal);
        progressText = CreateText(canvas.transform, "ProgressText", new Vector2(40f, -270f), new Vector2(420f, 42f), 22, FontStyle.Normal);
        mushroomCountText = CreateText(canvas.transform, "MushroomText", new Vector2(40f, -315f), new Vector2(420f, 42f), 22, FontStyle.Normal);

        fireSliderLabelText = CreateText(canvas.transform, "FireSliderLabel", new Vector2(40f, -370f), new Vector2(180f, 34f), 22, FontStyle.Bold);
        fireSlider = CreateSlider(canvas.transform, "FireSlider", new Vector2(220f, -364f), new Color(0.95f, 0.45f, 0.08f, 1f));

        progressSliderLabelText = CreateText(canvas.transform, "ProgressSliderLabel", new Vector2(40f, -430f), new Vector2(180f, 34f), 22, FontStyle.Bold);
        progressSlider = CreateSlider(canvas.transform, "ProgressSlider", new Vector2(220f, -424f), new Color(0.62f, 0.76f, 0.32f, 1f));
    }

    private Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyle fontStyle)
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
        text.color = Color.white;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
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
