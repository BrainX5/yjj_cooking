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

    private float focusTimer = 0f;
    private Animator impAnimator;
    private Renderer[] impRenderers;
    private Transform mainCameraTransform;

    // 严密的单目标控制状态机
    private SCUT_WeightlessFloat currentTargetMushroom = null;
    private float tKeyPressTimer = 0f;
    private bool isLockingTarget = false; // 是否在吸取中锁死当前目标，不准切走

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
                // 只有在没按住T吸取时，才可以自由瞄准和切换目标
                if (!isLockingTarget)
                {
                    HandleLaserAiming();
                }
                HandleMushroomCollection();
                break;
        }
    }

    /// <summary>
    /// 射线物理检测：自由瞄准阶段
    /// </summary>
    void HandleLaserAiming()
    {
        if (mainCameraTransform == null) return;

        Ray ray = new Ray(mainCameraTransform.position, mainCameraTransform.forward);
        RaycastHit hit;
        SCUT_WeightlessFloat hitMushroom = null;

        if (Physics.Raycast(ray, out hit, maxAimDistance))
        {
            hitMushroom = hit.collider.GetComponent<SCUT_WeightlessFloat>();
        }

        // 切换瞄准目标
        if (hitMushroom != currentTargetMushroom)
        {
            if (currentTargetMushroom != null)
            {
                currentTargetMushroom.SetTargeted(false);
            }

            currentTargetMushroom = hitMushroom;

            if (currentTargetMushroom != null)
            {
                currentTargetMushroom.SetTargeted(true); // 只有对准的这一个变大、亮圈
            }
        }

        // 动态同步更新激光位置和朝向
        UpdateLaserBeamTransform();
    }

    /// <summary>
    /// 动态改变 3D 激光模型的位置、朝向和拉伸
    /// </summary>
    void UpdateLaserBeamTransform()
    {
        if (realLaserObject == null) return;

        if (currentTargetMushroom != null)
        {
            realLaserObject.SetActive(true);
            Vector3 startPoint = transform.position; // 激光起点：精灵位置
            Vector3 endPoint = currentTargetMushroom.transform.position; // 激光终点：蘑菇中心

            float distance = Vector3.Distance(startPoint, endPoint);
            if (distance < 0.1f || float.IsNaN(distance))
            {
                realLaserObject.SetActive(false);
                return;
            }

            realLaserObject.transform.position = (startPoint + endPoint) / 2f;
            Vector3 direction = endPoint - startPoint;
            if (direction != Vector3.zero)
            {
                realLaserObject.transform.LookAt(endPoint);
            }

            // 动态调节拉伸长度
            Vector3 localScale = realLaserObject.transform.localScale;
            localScale.z = distance * 0.5f; 
            if (!float.IsNaN(localScale.x) && !float.IsNaN(localScale.y) && !float.IsNaN(localScale.z))
            {
                realLaserObject.transform.localScale = localScale;
            }
        }
        else
        {
            realLaserObject.SetActive(false);
        }
    }

    /// <summary>
    /// 处理按住 T 键 2.5 秒的吸取过程（带唯一锁死机制）
    /// </summary>
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
            // 一旦按下 T 键，锁死该目标，玩家转头射线滑走也不会切换，确保一次只获取一个
            isLockingTarget = true;

            tKeyPressTimer += Time.deltaTime;
            float progress = tKeyPressTimer / collectRequiredTime;

            // 每帧将固定好的相机前方位置传给蘑菇，促使其向镜头移动
            Vector3 cameraTargetPos = mainCameraTransform.position + mainCameraTransform.forward * 1.2f;
            currentTargetMushroom.UpdateCollectProgress(progress, cameraTargetPos);

            // 吸取时也要实时把激光连在蘑菇身上
            UpdateLaserBeamTransform();

            if (tKeyPressTimer >= collectRequiredTime)
            {
                // 2.5秒满，成功捕获消失！
                currentTargetMushroom.CollectSuccess();
                currentTargetMushroom = null;
                tKeyPressTimer = 0f;
                isLockingTarget = false;

                if (realLaserObject != null) realLaserObject.SetActive(false);

                // 刷新、检查场景里是否还有残存的飞天普通蘑菇
                CheckAllMushroomsCollected();
            }
        }
        else
        {
            // 松开 T 键，解锁目标，重置并原路退回
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

    void CheckAllMushroomsCollected()
    {
        SCUT_WeightlessFloat[] remaining = FindObjectsOfType<SCUT_WeightlessFloat>();
        bool anyLeft = false;
        foreach (var m in remaining)
        {
            if (m.gameObject.activeInHierarchy)
            {
                anyLeft = true;
                break;
            }
        }

        if (!anyLeft)
        {
            Debug.Log("【通关】所有悬浮蘑菇吸取完毕。");
            currentPhase = ImpPhase.Recovered;
            StartCoroutine(WinSequence());
        }
    }

    public void TriggerSpecialMushroomEvent(GameObject specialMushroom)
    {
        if (currentPhase != ImpPhase.Waiting) return;
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
            CameraFlightTracker tracker = Camera.main.gameObject.GetComponent<CameraFlightTracker>();
            if (tracker == null) tracker = Camera.main.gameObject.AddComponent<CameraFlightTracker>();
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
        SetImpVisibility(false);
        if (windAudio != null) windAudio.Stop();
        yield return new WaitForSeconds(1.0f);
        currentPhase = ImpPhase.Waiting;
    }
}