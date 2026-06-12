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
    public Image crosshair;
    [Tooltip("射线检测的最大距离")]
    public float maxAimDistance = 40f;
    [Tooltip("吸取蘑菇需要按住 T 的时间")]
    public float collectRequiredTime = 2.5f;

    [Header("🎯 刚性退出机制配置")]
    public int targetCollectCount = 5; // 目标收集5个
    private int currentlyCollectedCount = 0; // 核心刚性计数器

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
                    HandleLaserAiming();
                }
                HandleMushroomCollection();
                break;
        }
    }

    void HandleLaserAiming()
    {
        if (mainCameraTransform == null) return;

        Ray ray = new Ray(mainCameraTransform.position, mainCameraTransform.forward);
        RaycastHit hit;
        SCUT_WeightlessFloat hitMushroom = null;

        if (Physics.Raycast(ray, out hit, maxAimDistance))
        {
            SCUT_WeightlessFloat scr = hit.collider.GetComponent<SCUT_WeightlessFloat>();
            if (scr != null && !scr.IsMushroomCollected())
            {
                hitMushroom = scr;
            }
        }

        if (hitMushroom != currentTargetMushroom)
        {
            if (currentTargetMushroom != null) currentTargetMushroom.SetTargeted(false);
            currentTargetMushroom = hitMushroom;
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
                // 单个蘑菇收集成功
                currentTargetMushroom.CollectSuccess();
                currentTargetMushroom = null;
                tKeyPressTimer = 0f;
                isLockingTarget = false;

                if (realLaserObject != null) realLaserObject.SetActive(false);

                // 🌟 核心修改：无视任何物理检测，纯数字刚性累加！
                currentlyCollectedCount++;
                Debug.Log($"<color=green>【进度播报】成功吸取一个蘑菇！当前背包内总数: {currentlyCollectedCount} / {targetCollectCount}</color>");

                // 刚性判定：一旦等于目标数，立刻强制执行退出机制
                if (currentlyCollectedCount >= targetCollectCount)
                {
                    Debug.Log("<color=red>【核心触发】5个蘑菇已全部集齐！无条件切断状态机，触发 WinSequence 退出流程！</color>");
                    currentPhase = ImpPhase.Recovered;
                    StartCoroutine(WinSequence());
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

        // 🌟 核心修改：在视角发生任何改变前，第一帧立刻抓取并锁死相机此时此刻最原始的机位！
        SCUT_CameraFlightTracker tracker = Camera.main.gameObject.GetComponent<SCUT_CameraFlightTracker>();
        if (tracker == null) tracker = Camera.main.gameObject.AddComponent<SCUT_CameraFlightTracker>();
        tracker.SavePlayerInitialTransform(); 

        currentlyCollectedCount = 0; // 每次启动，计数安全清零

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

        Debug.Log("【退出流程进行中】1. 命令场景中剩余未收集的悬浮装饰物平滑落回地面原位...");
        if (floatingObjects != null)
        {
            floatingObjects.BroadcastMessage("LandBackToGround", SendMessageOptions.DontRequireReceiver);
        }

        Debug.Log("【退出流程进行中】2. 呼叫相机脚本，执行机位平滑还原归位...");
        SCUT_CameraFlightTracker tracker = Camera.main.gameObject.GetComponent<SCUT_CameraFlightTracker>();
        if (tracker != null)
        {
            tracker.ResetCameraTrack(); // 让相机平滑返回到最开始记录的原始视角
        }

        yield return new WaitForSeconds(1.5f);

        SetImpVisibility(false);
        
        Debug.Log("<color=cyan>【退出流程结束】3. 状态全面重置完毕，重回 Waiting 状态，等待下一次特殊蘑菇触发。</color>");
        currentPhase = ImpPhase.Waiting;
    }
}