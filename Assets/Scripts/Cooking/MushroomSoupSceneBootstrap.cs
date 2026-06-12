using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MushroomSoupSceneBootstrap : MonoBehaviour
{
    [Header("Scene Placement")]
    [SerializeField] private bool useExistingSceneEnvironment = true;
    [SerializeField] private bool anchorToExistingFire = true;
    [SerializeField] private Vector3 fallbackCookingPosition = new Vector3(-3.08f, 17.76f, -36.1f);
    [SerializeField] private Vector3 instructionOffset = new Vector3(-3.5f, 1.7f, 0f);
    [SerializeField] private Vector3 existingScenePotOffset = new Vector3(-3.18f, 3.04f, 6.02f);
    [SerializeField] private Vector3 existingScenePotRotation = new Vector3(0f, -75.376f, 0f);
    [SerializeField] private float existingScenePotScale = 1f;
    [SerializeField] private string soupPotPrefabPath = "Assets/3D Game Kit Clay Pot/Prefabs/pot3.prefab";
    [SerializeField] private string fryPotPrefabPath = "Assets/3D Game Kit Clay Pot/Prefabs/pot7 .prefab";
    [SerializeField] private string mushroomPrefabPath = "Assets/Oode studios/Lowpoly nature/Prefabs/Mashrooms/Mashroom 001.prefab";

    [Header("Player Spawn")]
    [SerializeField] private bool placePlayerAtMushroomHouse = true;
    [SerializeField] private string mushroomHouseName = "Mushroom House";
    [SerializeField] private string mushroomHouseDoorName = "Door";
    [SerializeField] private float spawnDistanceFromDoor = 1.65f;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.05f, 0f);
    [SerializeField] private float actorSearchDuration = 5f;
    [SerializeField] private float actorSearchInterval = 0.25f;
    [SerializeField] private bool autoCreatePlayer = true;
    [SerializeField] private Vector3 fallbackPlayerPosition = new Vector3(-3.5f, 18.1f, -42f);
    [SerializeField] private Vector3 fallbackPlayerRotation = new Vector3(10f, 0f, 0f);

    [Header("Canal Fish")]
    [SerializeField] private bool populateCanalWithFish = true;
    [SerializeField] private string waterRootName = "Water";
    [SerializeField] private int canalFishCount = 8;
    [SerializeField] private Vector3 canalFishPadding = new Vector3(1.2f, 0.4f, 1.2f);
    [SerializeField] private GameObject canalFishPrefab;
    [SerializeField] private string fishPrefabPath = "Assets/DenysAlmaral/FishAlive/Prefabs/FishFreshwater/freshWater_guppy.prefab";
    [SerializeField] private float fryPotScaleMultiplier = 0.3333f;
    [SerializeField] private Vector3 fryFishLocalOffset = new Vector3(0f, 0.18f, 0f);
    [SerializeField] private Vector3 fryFishLocalRotation = new Vector3(0f, 90f, 0f);
    [SerializeField] private float fryFishPanFill = 0.39f;

    [Header("Harvest Mushrooms")]
    [SerializeField] private bool enableMushroomHarvesting = true;
    [SerializeField] private string bridgeName = "Bridge";
    [SerializeField] private string fencesName = "Fences";
    [SerializeField] private int harvestMushroomCount = 0;
    [SerializeField] private GameObject[] harvestMushroomPrefabs;

    [Header("Opening Story")]
    [SerializeField] private bool showOpeningStory = true;

    [Header("HybridBCI Platform")]
    [SerializeField] private bool enableHybridBciPlatformBridge = true;

    [Header("Cloud Logging")]
    [SerializeField] private bool enableCloudLogging = true;
    [SerializeField] private string cloudApiUrl = "https://cloud1-d9gz2tmfub107d0ff.service.tcloudbase.com/submitGameLog";
    [SerializeField] private string testUserOpenid = "test_user";

    private Font uiFont;

    private void Start()
    {
        ApplyMorningLighting();
        BuildScene();
        var player = EnsurePlayerRig();
        if (placePlayerAtMushroomHouse)
        {
            StartCoroutine(PositionPrimaryActorAtMushroomHouse(player));
        }
        else
        {
            PositionActorAtCookingSpot(player);
        }

        if (populateCanalWithFish)
        {
            EnsureCanalFishSchool();
        }

        if (enableMushroomHarvesting)
        {
            EnsureMushroomPickupSystem();
        }

        if (showOpeningStory)
        {
            EnsureOpeningStoryOverlay();
        }

        EnsureCookingAudioController();
        if (enableHybridBciPlatformBridge)
        {
            EnsureHybridBciPlatformBridge();
        }

        if (enableCloudLogging)
        {
            EnsureCloudLogging();
        }
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
        if (useExistingSceneEnvironment)
        {
            cookingRoot.position = Vector3.zero;
        }
        else
        {
            cookingRoot.position = ResolveCookingPosition();
        }

        if (!useExistingSceneEnvironment)
        {
            CreateStoneRing(cookingRoot);
            CreateLogs(cookingRoot);
        }

        var fire = ResolveFireEffect(cookingRoot);
        var soupPot = CreateSoupPot(
            cookingRoot,
            out var soupSurface,
            out var soupRenderer,
            out var stirStick,
            out var mushroomSpawnPoint,
            out var mushroomTargetPoint);
        var fryPot = CreateFryPot(cookingRoot, soupPot, out var fryFishVisual, out var fryFishRenderer);
        var mushroomPrefab = CreateMushroomVisualTemplate(cookingRoot);

        if (!useExistingSceneEnvironment)
        {
            CreateDecor(cookingRoot);
        }

        CreateUiCanvas(
            out var cookingPanel,
            out var fishingPanel,
            out var titleText,
            out var promptText,
            out var fireValueText,
            out var progressText,
            out var mushroomCountText,
            out var fireSliderLabelText,
            out var progressSliderLabelText,
            out var progressSlider,
            out var fireSlider,
            out var fishTitleText,
            out var fishPromptText,
            out var fishStatusText,
            out var fishCatchButtonText,
            out var fishSliderLabelText,
            out var fishCatchSlider);

        var gameObject = new GameObject("MushroomSoupGame");
        var game = gameObject.AddComponent<MushroomSoupGame>();
        game.Initialize(
            fire,
            FindPrimaryActor(false),
            FindNamedTransform(waterRootName, waterRootName),
            soupPot,
            fryPot,
            soupSurface,
            soupRenderer,
            stirStick,
            mushroomSpawnPoint,
            mushroomTargetPoint,
            mushroomPrefab,
            fryFishVisual,
            fryFishRenderer,
            cookingPanel,
            fishingPanel,
            titleText,
            promptText,
            fireValueText,
            progressText,
            mushroomCountText,
            fireSliderLabelText,
            progressSliderLabelText,
            progressSlider,
            fireSlider,
            fishTitleText,
            fishPromptText,
            fishStatusText,
            fishCatchButtonText,
            fishSliderLabelText,
            fishCatchSlider);
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

    private Transform EnsurePlayerRig()
    {
        var existingPlayer = FindPrimaryActor(false);
        if (existingPlayer != null)
        {
            EnsurePlayerComponents(existingPlayer);
            return existingPlayer;
        }

        if (!autoCreatePlayer)
        {
            return Camera.main != null ? Camera.main.transform : null;
        }

        var playerObject = new GameObject("Player");
        TryAssignTag(playerObject, "Player");
        playerObject.transform.position = fallbackPlayerPosition;
        playerObject.transform.rotation = Quaternion.Euler(0f, fallbackPlayerRotation.y, 0f);

        var controller = playerObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.32f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.3f;

        var movement = playerObject.AddComponent<SimpleFirstPersonController>();
        var camera = AttachOrCreatePlayerCamera(playerObject.transform);
        movement.SetCamera(camera);

        return playerObject.transform;
    }

    private void EnsurePlayerComponents(Transform actor)
    {
        if (actor == null)
        {
            return;
        }

        var controller = actor.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = actor.gameObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
        }

        var movement = actor.GetComponent<SimpleFirstPersonController>();
        if (movement == null)
        {
            movement = actor.gameObject.AddComponent<SimpleFirstPersonController>();
        }

        var camera = AttachOrCreatePlayerCamera(actor);
        movement.SetCamera(camera);
    }

    private Camera AttachOrCreatePlayerCamera(Transform playerRoot)
    {
        var existingCamera = playerRoot.GetComponentInChildren<Camera>(true);
        if (existingCamera == null)
        {
            existingCamera = Camera.main;
        }

        if (existingCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            TryAssignTag(cameraObject, "MainCamera");
            existingCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        var cameraTransform = existingCamera.transform;
        cameraTransform.SetParent(playerRoot);
        cameraTransform.localPosition = new Vector3(0f, 3.2f, 0f);
        cameraTransform.localRotation = Quaternion.Euler(12f, 0f, 0f);

        if (existingCamera.GetComponent<AudioListener>() == null)
        {
            existingCamera.gameObject.AddComponent<AudioListener>();
        }

        TryAssignTag(existingCamera.gameObject, "MainCamera");
        existingCamera.enabled = true;
        return existingCamera;
    }

    private void TryAssignTag(GameObject target, string tagName)
    {
        if (target == null)
        {
            return;
        }

        try
        {
            target.tag = tagName;
        }
        catch (UnityException)
        {
        }
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
            lightObject.color = new Color(1f, 0.76f, 0.58f, 1f);
            lightObject.intensity = 1.18f;
            lightObject.shadows = LightShadows.Soft;
            lightObject.shadowStrength = 0.82f;
            lightObject.transform.rotation = Quaternion.Euler(18f, -28f, 0f);
            RenderSettings.sun = lightObject;
            return;
        }

        var directionalLight = new GameObject("Directional Light").AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.color = new Color(1f, 0.76f, 0.58f, 1f);
        directionalLight.intensity = 1.18f;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.82f;
        directionalLight.transform.rotation = Quaternion.Euler(18f, -28f, 0f);
        RenderSettings.sun = directionalLight;
    }

    private void ApplyMorningLighting()
    {
        var camera = EnsureCamera();
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.99f, 0.82f, 0.63f, 1f);
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.98f, 0.79f, 0.66f, 1f);
        RenderSettings.fogStartDistance = 0f;
        RenderSettings.fogEndDistance = 150f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.69f, 0.81f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.96f, 0.76f, 0.58f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.28f, 0.22f, 1f);
        RenderSettings.ambientIntensity = 1.18f;
        RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.38f, 0.36f, 1f);
        RenderSettings.reflectionIntensity = 0.82f;

        EnsureDirectionalLight();
    }

    private IEnumerator PositionPrimaryActorAtMushroomHouse(Transform actor)
    {
        var deadline = Time.time + actorSearchDuration;

        while (Time.time <= deadline)
        {
            var door = FindNamedTransform(mushroomHouseDoorName, mushroomHouseName);
            if (door != null && actor != null)
            {
                PlaceActorAtDoor(actor, door);
                yield break;
            }

            yield return new WaitForSeconds(actorSearchInterval);
        }

        PositionActorAtCookingSpot(actor);
    }

    private void PlaceActorAtDoor(Transform actor, Transform door)
    {
        var facing = door.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.01f && door.parent != null)
        {
            facing = door.parent.forward;
            facing.y = 0f;
        }

        if (facing.sqrMagnitude < 0.01f)
        {
            facing = Vector3.forward;
        }

        facing.Normalize();

        var spawnPosition = door.position + facing * spawnDistanceFromDoor + spawnOffset;
        PlaceActorAtPositionAndLook(actor, spawnPosition, facing);
    }

    private void PositionActorAtCookingSpot(Transform actor)
    {
        if (actor == null)
        {
            return;
        }

        var pot = FindExistingPotVisual("pot3");
        if (pot == null)
        {
            PlaceActorAtPositionAndLook(actor, fallbackPlayerPosition, Quaternion.Euler(0f, fallbackPlayerRotation.y, 0f) * Vector3.forward);
            return;
        }

        var bounds = CalculateRendererBounds(pot.gameObject);
        var target = bounds.center;
        var facing = -pot.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.01f)
        {
            facing = Vector3.forward;
        }

        facing.Normalize();
        var spawnPosition = target + facing * 3.5f + Vector3.up * 0.05f;
        PlaceActorAtPositionAndLook(actor, spawnPosition, -facing);
    }

    private void PlaceActorAtPositionAndLook(Transform actor, Vector3 position, Vector3 lookDirection)
    {
        if (actor == null)
        {
            return;
        }

        if (lookDirection.sqrMagnitude < 0.01f)
        {
            lookDirection = Vector3.forward;
        }

        var characterController = actor.GetComponent<CharacterController>();
        if (Physics.Raycast(position + Vector3.up * 4f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y;
            if (characterController != null)
            {
                position.y += characterController.height + 0.2f;
            }
        }

        lookDirection.y = 0f;
        lookDirection.Normalize();

        if (characterController != null)
        {
            var wasEnabled = characterController.enabled;
            characterController.enabled = false;
            actor.position = position;
            actor.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            characterController.enabled = wasEnabled;
        }
        else
        {
            actor.position = position;
            actor.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }

        var movement = actor.GetComponent<SimpleFirstPersonController>();
        if (movement != null)
        {
            movement.SnapToHeight(position.y);
        }

        var rigidbody = actor.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private Transform FindPrimaryActor(bool allowCameraFallback = true)
    {
        GameObject taggedPlayer = null;
        try
        {
            taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            taggedPlayer = null;
        }

        if (taggedPlayer != null)
        {
            return taggedPlayer.transform;
        }

        var characterControllers = FindObjectsOfType<CharacterController>(true);
        foreach (var controller in characterControllers)
        {
            if (controller != null && controller.gameObject.activeInHierarchy)
            {
                return controller.transform;
            }
        }

        var animators = FindObjectsOfType<Animator>(true);
        foreach (var animator in animators)
        {
            if (animator == null || animator.GetComponent<Camera>() != null)
            {
                continue;
            }

            var lowerName = animator.name.ToLowerInvariant();
            if (lowerName.Contains("player") || lowerName.Contains("character") || lowerName.Contains("hero"))
            {
                return animator.transform;
            }
        }

        return allowCameraFallback && Camera.main != null ? Camera.main.transform : null;
    }

    private Transform FindNamedTransform(string childName, string parentName)
    {
        var transforms = FindObjectsOfType<Transform>(true);
        Transform parent = null;

        foreach (var item in transforms)
        {
            if (item != null && item.name == parentName)
            {
                parent = item;
                break;
            }
        }

        if (parent == null)
        {
            return null;
        }

        foreach (var item in parent.GetComponentsInChildren<Transform>(true))
        {
            if (item != null && item.name == childName)
            {
                return item;
            }
        }

        return parent;
    }

    private void EnsureCanalFishSchool()
    {
        if (FindObjectOfType<CanalFishSchool>() != null)
        {
            return;
        }

        var waterRoot = FindNamedTransform(waterRootName, waterRootName);
        if (waterRoot == null)
        {
            return;
        }

        var fishSchoolObject = new GameObject("CanalFishSchool");
        var fishSchool = fishSchoolObject.AddComponent<CanalFishSchool>();
        fishSchool.Configure(waterRoot, canalFishCount, canalFishPrefab, fishPrefabPath, canalFishPadding);
        Debug.Log($"Created CanalFishSchool on {waterRoot.name} with {canalFishCount} fish.", fishSchoolObject);
    }

    private void EnsureMushroomPickupSystem()
    {
        var pickupSystem = FindObjectOfType<MushroomPickupSystem>();
        if (pickupSystem == null)
        {
            pickupSystem = new GameObject("MushroomPickupSystem").AddComponent<MushroomPickupSystem>();
        }

        pickupSystem.Configure(
            mushroomHouseName,
            mushroomHouseDoorName,
            bridgeName,
            fencesName,
            harvestMushroomCount,
            harvestMushroomPrefabs);
    }

    private void EnsureOpeningStoryOverlay()
    {
        if (FindObjectOfType<ForestStoryIntroOverlay>() != null)
        {
            return;
        }

        new GameObject("ForestStoryIntroOverlay").AddComponent<ForestStoryIntroOverlay>();
    }

    private void EnsureHybridBciPlatformBridge()
    {
        if (FindObjectOfType<HybridBciPlatformBridge>() != null)
        {
            EnsureHybridBciGameplayInput();
            return;
        }

        new GameObject("HybridBciPlatformBridge").AddComponent<HybridBciPlatformBridge>();
        EnsureHybridBciGameplayInput();
    }

    private void EnsureHybridBciGameplayInput()
    {
        if (FindObjectOfType<HybridBciGameplayInput>() != null)
        {
            return;
        }

        new GameObject("HybridBciGameplayInput").AddComponent<HybridBciGameplayInput>();
    }

    private void EnsureCloudLogging()
    {
        // GameLogSender: HTTP 发送脚本
        GameLogSender logSender = FindObjectOfType<GameLogSender>();
        if (logSender == null)
        {
            var senderGo = new GameObject("GameLogSender");
            logSender = senderGo.AddComponent<GameLogSender>();
        }
        logSender.apiUrl = cloudApiUrl;
        logSender.userOpenid = testUserOpenid;

        // GameplayMetricsTracker: 自动追踪指标 + 提交
        if (FindObjectOfType<GameplayMetricsTracker>() == null)
        {
            var trackerGo = new GameObject("GameplayMetricsTracker");
            var tracker = trackerGo.AddComponent<GameplayMetricsTracker>();
            tracker.logSender = logSender;
        }
    }

    private void EnsureCookingAudioController()
    {
        if (FindObjectOfType<CookingAudioController>() != null)
        {
            return;
        }

        var audioObject = new GameObject("CookingAudioController");
        audioObject.AddComponent<AudioSource>();
        audioObject.AddComponent<CookingAudioController>();
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

    private Transform CreateSoupPot(
        Transform parent,
        out Transform soupSurface,
        out Renderer soupRenderer,
        out Transform stirStick,
        out Transform mushroomSpawnPoint,
        out Transform mushroomTargetPoint)
    {
        var existingPot = useExistingSceneEnvironment ? FindExistingPotVisual("pot3") : null;
        if (existingPot != null)
        {
            existingPot.name = "SoupPotVisual";
            return AttachGameplayToExistingPot(
                existingPot,
                out soupSurface,
                out soupRenderer,
                out stirStick,
                out mushroomSpawnPoint,
                out mushroomTargetPoint);
        }

        var potRoot = new GameObject("Cooking Pot").transform;
        potRoot.SetParent(parent);

        if (useExistingSceneEnvironment)
        {
            potRoot.localPosition = existingScenePotOffset;
            potRoot.localRotation = Quaternion.Euler(existingScenePotRotation);
            potRoot.localScale = Vector3.one;
        }
        else
        {
            potRoot.localPosition = new Vector3(0f, 1.02f, 0f);
            potRoot.localRotation = Quaternion.identity;
            potRoot.localScale = Vector3.one;
        }

        potRoot.name = "SoupPotVisual";
        var potModel = CreatePotModel(potRoot, soupPotPrefabPath, "SoupPotMesh");
        var bounds = CalculateRendererBounds(potModel != null ? potModel : potRoot.gameObject);
        var openingY = bounds.center.y + bounds.extents.y * 0.52f;
        var soupRadius = Mathf.Max(0.18f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.62f);

        var soup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        soup.name = "SoupSurface";
        soup.transform.SetParent(potRoot);
        soup.transform.position = new Vector3(bounds.center.x, openingY, bounds.center.z);
        soup.transform.localScale = new Vector3(soupRadius, 0.03f, soupRadius);
        soupRenderer = soup.GetComponent<Renderer>();
        soupRenderer.material.color = new Color(0.91f, 0.84f, 0.56f, 1f);
        soupSurface = soup.transform;
        DestroyCollider(soup);

        var soupCenter = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        soupCenter.name = "SoupBody";
        soupCenter.transform.SetParent(potRoot);
        soupCenter.transform.position = new Vector3(bounds.center.x, openingY - 0.03f, bounds.center.z);
        soupCenter.transform.localScale = new Vector3(soupRadius * 0.9f, 0.12f, soupRadius * 0.9f);
        var soupCenterRenderer = soupCenter.GetComponent<Renderer>();
        soupCenterRenderer.material.color = new Color(0.9f, 0.82f, 0.54f, 1f);
        DestroyCollider(soupCenter);

        CreateTripodLeg(potRoot, new Vector3(-0.45f, -0.65f, -0.38f), -16f);
        CreateTripodLeg(potRoot, new Vector3(0.45f, -0.65f, -0.38f), 16f);
        CreateTripodLeg(potRoot, new Vector3(0f, -0.65f, 0.48f), 0f);

        stirStick = CreateStirStick(potRoot);
        stirStick.position = new Vector3(bounds.center.x + bounds.extents.x * 0.16f, openingY + 0.72f, bounds.center.z);
        stirStick.rotation = Quaternion.Euler(10f, 0f, -18f);

        mushroomSpawnPoint = CreateMarker(potRoot, "MushroomSpawnPoint", Vector3.zero);
        mushroomSpawnPoint.position = new Vector3(bounds.center.x - bounds.extents.x * 1.35f, openingY + 0.95f, bounds.center.z);

        mushroomTargetPoint = CreateMarker(potRoot, "MushroomTargetPoint", Vector3.zero);
        mushroomTargetPoint.position = new Vector3(bounds.center.x, openingY + 0.03f, bounds.center.z);

        return potRoot;
    }

    private Transform CreateFryPot(Transform parent, Transform soupPot, out Transform fryFishVisual, out Renderer fryFishRenderer)
    {
        var fryPotRoot = new GameObject("FryPotVisual").transform;
        fryPotRoot.SetParent(parent);

        if (soupPot != null)
        {
            fryPotRoot.position = soupPot.position;
            fryPotRoot.rotation = soupPot.rotation;
            fryPotRoot.localScale = soupPot.localScale * fryPotScaleMultiplier;
        }
        else if (useExistingSceneEnvironment)
        {
            fryPotRoot.localPosition = existingScenePotOffset;
            fryPotRoot.localRotation = Quaternion.Euler(existingScenePotRotation);
            fryPotRoot.localScale = Vector3.one * fryPotScaleMultiplier;
        }
        else
        {
            fryPotRoot.localPosition = new Vector3(0f, 1.02f, 0f);
            fryPotRoot.localRotation = Quaternion.identity;
            fryPotRoot.localScale = Vector3.one * fryPotScaleMultiplier;
        }

        var fryPotModel = CreatePotModel(fryPotRoot, fryPotPrefabPath, "FryPotMesh");
        var bounds = CalculateRendererBounds(fryPotModel != null ? fryPotModel : fryPotRoot.gameObject);
        fryFishVisual = CreateFishPresentation(fryPotRoot, bounds, out fryFishRenderer);
        fryPotRoot.gameObject.SetActive(false);
        return fryPotRoot;
    }

    private Transform AttachGameplayToExistingPot(
        Transform existingPot,
        out Transform soupSurface,
        out Renderer soupRenderer,
        out Transform stirStick,
        out Transform mushroomSpawnPoint,
        out Transform mushroomTargetPoint)
    {
        RemoveAllColliders(existingPot.gameObject);

        var bounds = CalculateRendererBounds(existingPot.gameObject);
        var openingY = bounds.center.y + bounds.extents.y * 0.52f;
        var soupRadius = Mathf.Max(0.18f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.62f);

        var soup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        soup.name = "SoupSurface";
        soup.transform.SetParent(existingPot);
        soup.transform.position = new Vector3(bounds.center.x, openingY, bounds.center.z);
        soup.transform.localScale = new Vector3(soupRadius, 0.03f, soupRadius);
        soupRenderer = soup.GetComponent<Renderer>();
        soupRenderer.material.color = new Color(0.91f, 0.84f, 0.56f, 1f);
        soupSurface = soup.transform;
        DestroyCollider(soup);

        var soupCenter = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        soupCenter.name = "SoupBody";
        soupCenter.transform.SetParent(existingPot);
        soupCenter.transform.position = new Vector3(bounds.center.x, openingY - 0.03f, bounds.center.z);
        soupCenter.transform.localScale = new Vector3(soupRadius * 0.9f, 0.12f, soupRadius * 0.9f);
        var soupCenterRenderer = soupCenter.GetComponent<Renderer>();
        soupCenterRenderer.material.color = new Color(0.9f, 0.82f, 0.54f, 1f);
        DestroyCollider(soupCenter);

        stirStick = CreateStirStick(existingPot);
        stirStick.position = new Vector3(bounds.center.x + bounds.extents.x * 0.16f, openingY + 0.72f, bounds.center.z);
        stirStick.rotation = Quaternion.Euler(10f, 0f, -18f);

        mushroomSpawnPoint = CreateMarker(existingPot, "MushroomSpawnPoint", Vector3.zero);
        mushroomSpawnPoint.position = new Vector3(bounds.center.x - bounds.extents.x * 1.35f, openingY + 0.95f, bounds.center.z);

        mushroomTargetPoint = CreateMarker(existingPot, "MushroomTargetPoint", Vector3.zero);
        mushroomTargetPoint.position = new Vector3(bounds.center.x, openingY + 0.03f, bounds.center.z);

        return existingPot;
    }

    private Transform FindExistingPotVisual(string keyword)
    {
        var transforms = FindObjectsOfType<Transform>(true);
        foreach (var item in transforms)
        {
            if (item == null)
            {
                continue;
            }

            var objectName = item.name.ToLowerInvariant();
            if (objectName.Contains(keyword))
            {
                return item;
            }
        }

        return null;
    }

    private GameObject CreatePotModel(Transform parent, string prefabPath, string instanceName)
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
            {
                instance.name = instanceName;
                instance.transform.SetParent(parent);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                instance.transform.localScale = Vector3.one * existingScenePotScale;
                RemoveAllColliders(instance);
                return instance;
            }
        }
#endif
        return null;
    }

    private Transform CreateFishPresentation(Transform parent, Bounds potBounds, out Renderer fishRenderer)
    {
        var fishRoot = new GameObject("FryFishVisual").transform;
        fishRoot.SetParent(parent);

        var center = parent.InverseTransformPoint(potBounds.center);
        fishRoot.localPosition = center + fryFishLocalOffset;
        fishRoot.localRotation = Quaternion.Euler(fryFishLocalRotation);
        fishRoot.localScale = Vector3.one;

        GameObject fishObject = null;

#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fishPrefabPath);
        if (prefab != null)
        {
            fishObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
#endif

        if (fishObject == null)
        {
            fishObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        }

        fishObject.name = "FishModel";
        fishObject.transform.SetParent(fishRoot, false);
        fishObject.transform.localPosition = Vector3.zero;
        fishObject.transform.localRotation = Quaternion.identity;
        fishObject.transform.localScale = Vector3.one;

        var fishBounds = CalculateRendererBounds(fishObject);
        var fishCenterLocal = fishObject.transform.InverseTransformPoint(fishBounds.center);
        fishObject.transform.localPosition = -fishCenterLocal;

        fishBounds = CalculateRendererBounds(fishObject);
        var fishLongestSide = Mathf.Max(fishBounds.size.x, Mathf.Max(fishBounds.size.y, fishBounds.size.z));
        var targetLength = Mathf.Max(0.2f, Mathf.Min(potBounds.size.x, potBounds.size.z) * fryFishPanFill);
        var scaleFactor = fishLongestSide > 0.0001f ? targetLength / fishLongestSide : 0.8f;
        fishObject.transform.localScale = Vector3.one * scaleFactor;

        fishBounds = CalculateRendererBounds(fishObject);
        fishCenterLocal = fishObject.transform.InverseTransformPoint(fishBounds.center);
        fishObject.transform.localPosition = -fishCenterLocal;

        RemoveAllColliders(fishObject);
        DisableFishMotion(fishObject);

        fishRenderer = fishObject.GetComponentInChildren<Renderer>(true);
        if (fishRenderer != null)
        {
            fishRenderer.material.color = new Color(0.86f, 0.73f, 0.44f, 1f);
        }

        return fishRoot;
    }

    private void DisableFishMotion(GameObject fishObject)
    {
        var behaviours = fishObject.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            if (behaviour is MushroomSoupSceneBootstrap || behaviour is CanalFishSchool)
            {
                continue;
            }

            behaviour.enabled = false;
        }

        var animators = fishObject.GetComponentsInChildren<Animator>(true);
        foreach (var animator in animators)
        {
            animator.enabled = false;
        }

        var animations = fishObject.GetComponentsInChildren<Animation>(true);
        foreach (var animationComponent in animations)
        {
            animationComponent.enabled = false;
        }
    }

    private Bounds CalculateRendererBounds(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.one);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void CreatePotWallRing(Transform parent, int segmentCount, float radius, float width, float height, float depth, Color color)
    {
        for (var i = 0; i < segmentCount; i++)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"PotWall_{i + 1}";
            segment.transform.SetParent(parent);

            var angle = i * Mathf.PI * 2f / segmentCount;
            var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            segment.transform.localPosition = new Vector3(radial.x * radius, 0.02f, radial.z * radius);
            segment.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(radial.z, radial.x) * Mathf.Rad2Deg, 0f);
            segment.transform.localScale = new Vector3(width, height, depth);

            var renderer = segment.GetComponent<Renderer>();
            renderer.material.color = color;
            DestroyCollider(segment);
        }
    }

    private void CreateOpenLid(Transform parent)
    {
        var lidPivot = new GameObject("LidPivot").transform;
        lidPivot.SetParent(parent);
        lidPivot.localPosition = new Vector3(-0.86f, -0.3f, -0.64f);
        lidPivot.localRotation = Quaternion.Euler(0f, 0f, 6f);

        var lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lid.name = "PotLid";
        lid.transform.SetParent(lidPivot);
        lid.transform.localPosition = Vector3.zero;
        lid.transform.localScale = new Vector3(0.38f, 0.02f, 0.38f);
        lid.GetComponent<Renderer>().material.color = new Color(0.28f, 0.29f, 0.32f, 1f);
        DestroyCollider(lid);

        var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        knob.name = "LidKnob";
        knob.transform.SetParent(lidPivot);
        knob.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        knob.transform.localScale = new Vector3(0.09f, 0.08f, 0.09f);
        knob.GetComponent<Renderer>().material.color = new Color(0.18f, 0.18f, 0.2f, 1f);
        DestroyCollider(knob);
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
        leftHandle.transform.localPosition = new Vector3(-0.7f, 0.12f, 0f);
        leftHandle.transform.localScale = new Vector3(0.18f, 0.035f, 0.035f);
        leftHandle.GetComponent<Renderer>().material.color = new Color(0.24f, 0.24f, 0.26f, 1f);
        DestroyCollider(leftHandle);

        var rightHandle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightHandle.name = "HandleRight";
        rightHandle.transform.SetParent(parent);
        rightHandle.transform.localPosition = new Vector3(0.7f, 0.12f, 0f);
        rightHandle.transform.localScale = new Vector3(0.18f, 0.035f, 0.035f);
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

    private GameObject CreateMushroomVisualTemplate(Transform parent)
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mushroomPrefabPath);
        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance != null)
            {
                instance.name = "MushroomVisualTemplate";
                instance.transform.SetParent(parent);
                instance.transform.localPosition = new Vector3(-2f, -10f, 0f);
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one * 0.8f;
                instance.SetActive(false);
                RemoveAllColliders(instance);
                return instance;
            }
        }
#endif

        var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        fallback.name = "MushroomVisualTemplate";
        fallback.transform.SetParent(parent);
        fallback.transform.localPosition = new Vector3(-2f, -10f, 0f);
        fallback.transform.localScale = new Vector3(0.2f, 0.28f, 0.2f);
        fallback.GetComponent<Renderer>().material.color = new Color(0.9f, 0.82f, 0.62f, 1f);
        DestroyCollider(fallback);
        fallback.SetActive(false);
        return fallback;
    }

    private void RemoveAllColliders(GameObject target)
    {
        var colliders = target.GetComponentsInChildren<Collider>(true);
        foreach (var collider in colliders)
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
        out GameObject cookingPanel,
        out GameObject fishingPanel,
        out Text titleText,
        out Text promptText,
        out Text fireValueText,
        out Text progressText,
        out Text mushroomCountText,
        out Text fireSliderLabelText,
        out Text progressSliderLabelText,
        out Slider progressSlider,
        out Slider fireSlider,
        out Text fishTitleText,
        out Text fishPromptText,
        out Text fishStatusText,
        out Text fishCatchButtonText,
        out Text fishSliderLabelText,
        out Slider fishCatchSlider)
    {
        var canvasObject = new GameObject("Cooking UI");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObject.AddComponent<GraphicRaycaster>();

        cookingPanel = new GameObject("CookingPanel");
        cookingPanel.transform.SetParent(canvas.transform);
        ConfigurePanelRect(cookingPanel);
        fishingPanel = new GameObject("FishingPanel");
        fishingPanel.transform.SetParent(canvas.transform);
        ConfigurePanelRect(fishingPanel);

        titleText = CreateText(cookingPanel.transform, "Title", new Vector2(40f, -40f), new Vector2(420f, 60f), 34, FontStyle.Bold);
        promptText = CreateText(cookingPanel.transform, "Prompt", new Vector2(40f, -100f), new Vector2(980f, 120f), 28, FontStyle.Bold);
        fireValueText = CreateText(cookingPanel.transform, "FireText", new Vector2(40f, -225f), new Vector2(420f, 42f), 22, FontStyle.Normal);
        progressText = CreateText(cookingPanel.transform, "ProgressText", new Vector2(40f, -270f), new Vector2(420f, 42f), 22, FontStyle.Normal);
        mushroomCountText = CreateText(cookingPanel.transform, "MushroomText", new Vector2(40f, -315f), new Vector2(420f, 42f), 22, FontStyle.Normal);

        fireSliderLabelText = CreateText(cookingPanel.transform, "FireSliderLabel", new Vector2(40f, -370f), new Vector2(180f, 34f), 22, FontStyle.Bold);
        fireSlider = CreateSlider(cookingPanel.transform, "FireSlider", new Vector2(220f, -364f), new Color(0.95f, 0.45f, 0.08f, 1f));

        progressSliderLabelText = CreateText(cookingPanel.transform, "ProgressSliderLabel", new Vector2(40f, -430f), new Vector2(180f, 34f), 22, FontStyle.Bold);
        progressSlider = CreateSlider(cookingPanel.transform, "ProgressSlider", new Vector2(220f, -424f), new Color(0.62f, 0.76f, 0.32f, 1f));

        fishTitleText = CreateText(fishingPanel.transform, "FishTitle", new Vector2(40f, -40f), new Vector2(420f, 60f), 34, FontStyle.Bold);
        fishPromptText = CreateText(fishingPanel.transform, "FishPrompt", new Vector2(40f, -100f), new Vector2(980f, 120f), 28, FontStyle.Bold);
        fishStatusText = CreateText(fishingPanel.transform, "FishStatus", new Vector2(40f, -235f), new Vector2(760f, 48f), 24, FontStyle.Normal);
        fishCatchButtonText = CreateText(fishingPanel.transform, "FishCatchButton", new Vector2(40f, -300f), new Vector2(540f, 54f), 28, FontStyle.Bold);
        fishSliderLabelText = CreateText(fishingPanel.transform, "FishSliderLabel", new Vector2(40f, -380f), new Vector2(180f, 34f), 22, FontStyle.Bold);
        fishCatchSlider = CreateSlider(fishingPanel.transform, "FishCatchSlider", new Vector2(220f, -374f), new Color(0.2f, 0.8f, 0.95f, 1f));

        fishingPanel.SetActive(false);
    }

    private void ConfigurePanelRect(GameObject panelObject)
    {
        var rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
