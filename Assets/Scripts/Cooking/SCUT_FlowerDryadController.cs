using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    [Header("🎯 手动把场景里的 3D 圆柱体 RealLaserBeam 拖到这里！")]
    public GameObject realLaserObject;

    private SCUT_WeightlessFloat currentTargetMushroom;
    private float focusTimer = 0f;
    private Animator impAnimator;
    private Renderer[] impRenderers;
    private Transform mainCameraTransform;

    void Awake()
    {
        impAnimator = GetComponent<Animator>();
        impRenderers = GetComponentsInChildren<Renderer>();
    }

    void Start()
    {
        mainCameraTransform = Camera.main != null ? Camera.main.transform : GameObject.FindObjectOfType<Camera>()?.transform;
        SetImpVisibility(false);
        if (windParticle != null) windParticle.Stop();
        if (windAudio != null) windAudio.Stop();

        if (realLaserObject != null) realLaserObject.SetActive(false);
    }

    void Update()
    {
        if (currentPhase == ImpPhase.Weightless && mainCameraTransform != null)
        {
            Vector3 lookPos = mainCameraTransform.position - transform.position;
            lookPos.y = 0;
            if (lookPos != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookPos);
        }

        switch (currentPhase)
        {
            case ImpPhase.Appears:
                HandleImpAppearance();
                break;
            case ImpPhase.Weightless:
                HandleFocusTraining();
                break;
        }
    }

    void SetImpVisibility(bool isVisible)
    {
        if (flowerDryadObject != null) flowerDryadObject.SetActive(isVisible);
        else
        {
            foreach (Renderer r in impRenderers)
            {
                if (r != null) r.enabled = isVisible;
            }
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

        if (realLaserObject != null) realLaserObject.SetActive(false);
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
            focusTimer = 0f;

            if (impAnimator != null) impAnimator.SetTrigger("Cast");
            if (windParticle != null) windParticle.Play();
            if (windAudio != null && !windAudio.isPlaying) windAudio.Play();

            StartCoroutine(BlowWindAndLiftMoshrooms());
        }
    }

    void HandleFocusTraining()
    {
        if (Input.GetKey(KeyCode.T)) focusScore = 85f;
        else focusScore = 50f;

        if (currentTargetMushroom != null)
        {
            // 🎯【圆柱体核心：无条件强制正向准星算法】
            if (realLaserObject != null && mainCameraTransform != null)
            {
                if (!realLaserObject.activeSelf) realLaserObject.SetActive(true);

                // 1. 物理测距：计算眼睛到当前目标蘑菇的绝对直线距离
                float distanceToMushroom = Vector3.Distance(mainCameraTransform.position, currentTargetMushroom.transform.position);

                // 2. 轴向对齐：让圆柱体的长轴（旋转）完全复制相机的视角朝向
                realLaserObject.transform.rotation = mainCameraTransform.rotation;
                realLaserObject.transform.Rotate(90, 0, 0); // 修正圆柱体网格自带的本地 Y 轴偏转

                // 3. 正向推演中心点：把圆柱体的几何中心，笔直地朝着相机的正前方（Forward）推出去一半的距离
                realLaserObject.transform.position = mainCameraTransform.position + mainCameraTransform.forward * (distanceToMushroom / 2f);

                // 4. 尺寸压缩与拉伸：横截面变成极细光线形态，长度完美吻合
                realLaserObject.transform.localScale = new Vector3(0.012f, distanceToMushroom / 2f, 0.012f);
            }

            if (!currentTargetMushroom.gameObject.activeInHierarchy)
            {
                if (realLaserObject != null) realLaserObject.SetActive(false);
                AcquireNextMushroomTarget();
            }
            else
            {
                if (focusScore >= targetFocus)
                {
                    focusTimer += Time.deltaTime;
                    if (focusTimer >= requiredDuration)
                    {
                        focusTimer = 0f;
                        if (realLaserObject != null) realLaserObject.SetActive(false);
                        currentTargetMushroom.ExecuteDrop();
                    }
                }
                else
                {
                    focusTimer = Mathf.Max(0f, focusTimer - Time.deltaTime);
                }
            }
        }
        else
        {
            if (realLaserObject != null && realLaserObject.activeSelf) realLaserObject.SetActive(false);
        }
    }

    IEnumerator BlowWindAndLiftMoshrooms()
    {
        yield return new WaitForSeconds(windBlowDuration);

        if (floatingObjects != null)
        {
            floatingObjects.SetActive(true);

            CameraFlightTracker tracker = GameObject.FindObjectOfType<CameraFlightTracker>();
            if (tracker != null) tracker.StartCameraTrack(this.transform);

            floatingObjects.BroadcastMessage("StartFloating", SendMessageOptions.DontRequireReceiver);

            yield return new WaitForSeconds(0.6f);
            AcquireNextMushroomTarget();

            StartCoroutine(ActivateBCIGameplay());
        }
    }

    IEnumerator ActivateBCIGameplay()
    {
        yield return new WaitForSeconds(2.0f);
        SCUT_BCIFocusTrainingManager manager = GameObject.FindObjectOfType<SCUT_BCIFocusTrainingManager>();
        if (manager != null) manager.StartFocusTrainingPhase();
    }

    void AcquireNextMushroomTarget()
    {
        SCUT_WeightlessFloat[] allMushrooms = GameObject.FindObjectsOfType<SCUT_WeightlessFloat>();
        foreach (var msh in allMushrooms)
        {
            if (msh.gameObject != null && msh.gameObject.activeInHierarchy)
            {
                currentTargetMushroom = msh;
                focusTimer = 0f;
                return;
            }
        }

        currentTargetMushroom = null;
        currentPhase = ImpPhase.Recovered;
        StartCoroutine(WinSequence());
    }

    IEnumerator WinSequence()
    {
        if (impAnimator != null) impAnimator.SetTrigger("Idle");
        if (windParticle != null) windParticle.Stop();
        SetImpVisibility(false);
        if (windAudio != null) windAudio.Stop();
        if (realLaserObject != null) realLaserObject.SetActive(false);

        yield return new WaitForSeconds(1.0f);
        currentPhase = ImpPhase.Waiting;
    }

    void OnGUI() { }
}