using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class SCUT_FlowerDryadController : MonoBehaviour
{
    public enum ImpPhase { Waiting, Appears, Weightless, Recovered }
    [Header("[Imp State Matrix]")]
    public ImpPhase currentPhase = ImpPhase.Waiting;

    [Header("[BCI Data Core]")]
    [Range(0, 100)] public float focusScore = 50f;
    public float targetFocus = 75f;
    public float requiredDuration = 2.5f;

    [Header("[VFX Core Box]")]
    public ParticleSystem windParticle;

    [Header("[Fly Distance Config]")]
    public float stopDistanceToCamera = 3f;
    public float flySpeed = 15f;

    public GameObject floatingObjects;
    public GameObject flowerDryadObject;
    public AudioSource windAudio;

    [Header("失重演出等待时间")]
    public float windBlowDuration = 2.0f;

    [Header("🎯 瞄准与吸取配置")]
    [Tooltip("手动把场景里的 3D 圆柱体 RealLaserBeam 拖到这里")]
    public GameObject realLaserObject;
    [Tooltip("屏幕中心的蓝色准星 UI")]
    public Image crosshair;
    [Tooltip("允许瞄准的现实世界最大距离（米）")]
    public float maxAimDistance = 40f;
    [Tooltip("吸取蘑菇需要按住 T 的时间")]
    public float collectRequiredTime = 2.5f;

    [Header("🎯 刚性退出机制配置")]
    public int targetCollectCount = 5; 
    private int currentlyCollectedCount = 0; 

    private float focusTimer = 0f;
    private Animator impAnimator;
    private Renderer[] impRenderers;
    private Transform mainCameraTransform;

    private SCUT_WeightlessFloat currentTargetMushroom = null;
    private float tKeyPressTimer = 0f;
    private bool isLockingTarget = false; 

    void Awake()
    {
        impAnimator = GetComponent<Animator>();
        impRenderers = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        mainCameraTransform = Camera.main.transform;
        SetImpVisibility(false);
        if (windParticle != null) windParticle.Stop();
        if (windAudio != null) windAudio.Stop();
        if (realLaserObject != null) realLaserObject.SetActive(false);
    }

    void Update()
    {
        switch (currentPhase)
        {
            case ImpPhase.Appears:
                HandleImpAppearance();
                break;
            case ImpPhase.Weightless:
                if (!isLockingTarget)
                {
                    HandleLaserAiming(false); // 正常每帧进行带阈值的过滤筛选
                }
                HandleMushroomCollection();
                break;
        }
    }

    // 🌟 核心改良：支持重载机制，引入粘性保护区以及无缝强制吸附
    void HandleLaserAiming(bool ignoreRadius)
    {
        if (mainCameraTransform == null || Camera.main == null) return;

        // 1. 定位蓝色光标的屏幕中心点
        Vector2 cursorScreenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        
        if (crosshair != null)
        {
            cursorScreenPos = RectTransformUtility.WorldToScreenPoint(null, crosshair.rectTransform.position);
        }

        // 2. 遍历查找距离该光标最近的有效蘑菇
        SCUT_WeightlessFloat[] allMushrooms = FindObjectsOfType<SCUT_WeightlessFloat>();
        SCUT_WeightlessFloat bestMushroom = null;
        
        float minScreenDistance = float.MaxValue;
        float maxSelectRadiusPixels = 180f; 

        foreach (var msh in allMushrooms)
        {
            if (msh == null || msh.IsMushroomCollected() || !msh.gameObject.activeInHierarchy) 
                continue;

            // 检查 3D 真实世界距离
            float worldDist = Vector3.Distance(mainCameraTransform.position, msh.transform.position);
            if (worldDist > maxAimDistance) continue;

            // 将蘑菇的世界 3D 坐标，计算投影为 2D 屏幕像素坐标
            Vector3 screenPoint = Camera.main.WorldToScreenPoint(msh.transform.position);

            // 确保蘑菇在相机镜头的前方
            if (screenPoint.z > 0)
            {
                // 计算当前蘑菇在屏幕上与蓝色光标的纯视觉像素距离
                float pixelDist = Vector2.Distance(cursorScreenPos, new Vector2(screenPoint.x, screenPoint.y));
                
                // 💡 智能改进逻辑：
                // 情况A：如果这个蘑菇是上一帧就已经锁定的目标，将其判定范围放大2.5倍（粘性区域），防止视角微调导致激光闪烁抖动
                // 情况B：如果 ignoreRadius 为 true（刚收完一个），则彻底解开半径限制，强制捕获屏幕内仅存的最佳下一目标
                float allowedRadius = maxSelectRadiusPixels;
                if (msh == currentTargetMushroom) allowedRadius = maxSelectRadiusPixels * 2.5f;
                if (ignoreRadius) allowedRadius = float.MaxValue;

                if (pixelDist < minScreenDistance && pixelDist < allowedRadius)
                {
                    minScreenDistance = pixelDist;
                    bestMushroom = msh;
                }
            }
        }

        // 3. 产生状态替换
        if (bestMushroom != currentTargetMushroom)
        {
            if (currentTargetMushroom != null) currentTargetMushroom.SetTargeted(false);
            currentTargetMushroom = bestMushroom;
            if (currentTargetMushroom != null) currentTargetMushroom.SetTargeted(true); 
        }

        UpdateLaserBeamTransform();
    }

    void UpdateLaserBeamTransform()
    {
        if (realLaserObject == null) return;

        if (currentTargetMushroom != null)
        {
            realLaserObject.SetActive(true);
            Vector3 startPoint = transform.position; 
            Vector3 endPoint = currentTargetMushroom.transform.position; 

            float distance = Vector3.Distance(startPoint, endPoint);
            if (distance < 0.1f || float.IsNaN(distance))
            {
                realLaserObject.SetActive(false);
                return;
            }

            realLaserObject.transform.position = (startPoint + endPoint) / 2f;
            Vector3 direction = endPoint - startPoint;
            if (direction != Vector3.zero) realLaserObject.transform.LookAt(endPoint);

            Vector3 localScale = realLaserObject.transform.localScale;
            localScale.z = distance * 0.5f; 
            realLaserObject.transform.localScale = localScale;
        }
        else
        {
            realLaserObject.SetActive(false);
        }
    }

    void HandleMushroomCollection()
    {
        if (currentTargetMushroom == null)
        {
            tKeyPressTimer = 0f;
            isLockingTarget = false;
            return;
        }

        if (Input.GetKey(KeyCode.T))
        {
            isLockingTarget = true;
            tKeyPressTimer += Time.deltaTime;
            float progress = tKeyPressTimer / collectRequiredTime;

            Vector3 cameraTargetPos = mainCameraTransform.position + mainCameraTransform.forward * 1.2f;
            currentTargetMushroom.UpdateCollectProgress(progress, cameraTargetPos);

            UpdateLaserBeamTransform();

            if (tKeyPressTimer >= collectRequiredTime)
            {
                // 成功捕获目标
                SCUT_WeightlessFloat completedMushroom = currentTargetMushroom;
                completedMushroom.CollectSuccess();
                
                // 清空并解除原目标绑定
                currentTargetMushroom = null;
                tKeyPressTimer = 0f;
                isLockingTarget = false;

                // 💡 修复关键：取消了强行SetActive(false)这一步，让瞄准接力算法去决定生死

                currentlyCollectedCount++;
                Debug.Log($"<color=green>【进度播报】成功吸取一个蘑菇！当前背包内总数: {currentlyCollectedCount} / {targetCollectCount}</color>");

                if (currentlyCollectedCount >= targetCollectCount)
                {
                    Debug.Log("<color=red>【核心触发】5个蘑菇已全部集齐！无条件切断状态机，触发 WinSequence 退出流程！</color>");
                    currentPhase = ImpPhase.Recovered;
                    StartCoroutine(WinSequence());
                }
                else
                {
                    // 💡 修复关键：在当前帧内立即、强行无视半径限制寻找下一个可用蘑菇，使激光直接“弹跳”连连看，告别黑屏消失感
                    HandleLaserAiming(true);
                }
            }
        }
        else
        {
            if (isLockingTarget)
            {
                tKeyPressTimer = 0f;
                isLockingTarget = false;
                if (currentTargetMushroom != null)
                {
                    currentTargetMushroom.ResetCollectProgress();
                }
            }
        }
    }

    public void TriggerSpecialMushroomEvent(GameObject specialMushroom)
    {
        if (currentPhase != ImpPhase.Waiting) return;

        SCUT_CameraFlightTracker tracker = Camera.main.gameObject.GetComponent<SCUT_CameraFlightTracker>();
        if (tracker == null) tracker = Camera.main.gameObject.AddComponent<SCUT_CameraFlightTracker>();
        tracker.SavePlayerInitialTransform(); 

        currentlyCollectedCount = 0; 

        SetImpVisibility(true);
        if (specialMushroom != null)
        {
            transform.position = specialMushroom.transform.position + Vector3.up * 5f;
        }
        currentPhase = ImpPhase.Appears;
        if (impAnimator != null) impAnimator.SetTrigger("Fly");
    }

    void HandleImpAppearance()
    {
        if (mainCameraTransform == null) return;
        Vector3 targetStopPoint = mainCameraTransform.position + mainCameraTransform.forward * stopDistanceToCamera;
        Vector3 lookPos = mainCameraTransform.position - transform.position;
        lookPos.y = 0; 
        if (lookPos != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookPos);

        transform.position = Vector3.MoveTowards(transform.position, targetStopPoint, flySpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetStopPoint) < 0.1f)
        {
            transform.position = targetStopPoint;
            currentPhase = ImpPhase.Weightless;
            if (impAnimator != null) impAnimator.SetTrigger("Cast");
            if (windParticle != null) windParticle.Play();
            if (windAudio != null && !windAudio.isPlaying) windAudio.Play();
            StartCoroutine(BlowWindAndLiftMoshrooms());
        }
    }

    IEnumerator BlowWindAndLiftMoshrooms()
    {
        yield return new WaitForSeconds(windBlowDuration);
        if (floatingObjects != null)
        {
            floatingObjects.SetActive(true);
            
            SCUT_CameraFlightTracker tracker = Camera.main.gameObject.GetComponent<SCUT_CameraFlightTracker>();
            if (tracker == null) tracker = Camera.main.gameObject.AddComponent<SCUT_CameraFlightTracker>();
            
            tracker.StartCameraTrack(this.transform); 
            floatingObjects.BroadcastMessage("StartFloating", SendMessageOptions.DontRequireReceiver);
            
            yield return new WaitForSeconds(0.6f);
            StartCoroutine(ActivateBCIGameplay());
        }
    }

    IEnumerator ActivateBCIGameplay()
    {
        yield return new WaitForSeconds(2.0f); 
        SCUT_BCIFocusTrainingManager manager = FindObjectOfType<SCUT_BCIFocusTrainingManager>();
        if (manager != null) manager.StartFocusTrainingPhase(); 
    }

    void SetImpVisibility(bool isVisible)
    {
        if (flowerDryadObject != null) flowerDryadObject.SetActive(isVisible);
        else
        {
            foreach (Renderer r in impRenderers) if (r != null) r.enabled = isVisible;
        }
    }

    IEnumerator WinSequence()
    {
        if (impAnimator != null) impAnimator.SetTrigger("Idle");
        if (windParticle != null) windParticle.Stop();
        if (realLaserObject != null) realLaserObject.SetActive(false);
        if (windAudio != null) windAudio.Stop();

        if (floatingObjects != null)
        {
            floatingObjects.BroadcastMessage("LandBackToGround", SendMessageOptions.DontRequireReceiver);
        }

        SCUT_CameraFlightTracker tracker = Camera.main.gameObject.GetComponent<SCUT_CameraFlightTracker>();
        if (tracker != null)
        {
            tracker.ResetCameraTrack(); 
        }

        yield return new WaitForSeconds(1.5f);
        SetImpVisibility(false);
        currentPhase = ImpPhase.Waiting;
    }
}