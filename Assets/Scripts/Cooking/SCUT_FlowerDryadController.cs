using UnityEngine;
using System.Collections;

public class SCUT_FlowerDryadController : MonoBehaviour
{
    public enum ImpPhase { Waiting, Appears, Weightless, Recovered }
    [Header("[Imp State Matrix]")]
    public ImpPhase currentPhase = ImpPhase.Waiting;

    [Header("[BCI Data Core]")]
    [Range(0, 100)] public float focusScore = 0f;
    public float targetFocus = 75f;
    public float requiredDuration = 2.5f;

    [Header("[VFX Core Box]")]
    public ParticleSystem windParticle;

    [Header("[Fly Distance Config]")]
    public float stopDistanceToCamera = 3f;
    public float flySpeed = 15f;

    public GameObject floatingObjects; // 场景里其他普通蘑菇组的父物体（可以没有）
    public GameObject flowerDryadObject;
    public AudioSource windAudio;

    [Header("失重演出等待时间")]
    public float windBlowDuration = 2.0f; // 精灵停在相机前开始吹风后，等待多少秒蘑菇才起飞

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
        if (Camera.main != null) mainCameraTransform = Camera.main.transform;

        SetImpVisibility(false);
        if (flowerDryadObject != null) flowerDryadObject.SetActive(false);
        if (windParticle != null) windParticle.Stop();
        
        if (windAudio != null)
        {
            windAudio.Stop();
            windAudio.loop = true; 
        } 
    }

    void Update()
    {
        if (currentPhase == ImpPhase.Weightless)
        {
            HandleFocusTraining();
        }
    }

    // 由采蘑菇脚本在玩家按下空格时调用
    public void TriggerSpecialMushroomEvent(GameObject specialMushroom)
    {
        if (currentPhase != ImpPhase.Waiting) return; 
        
        currentPhase = ImpPhase.Appears;
        if (flowerDryadObject != null) flowerDryadObject.SetActive(true);

        // 启动全新的失重动画剧情时间线
        StartCoroutine(ZeroGravityTimelineRoutine(specialMushroom));
    }

    IEnumerator ZeroGravityTimelineRoutine(GameObject specialMushroom)
    {
        Debug.Log("【剧情控制】风精灵现身，开始飞向玩家镜头前方...");
        SetImpVisibility(true);
        if (impAnimator != null) impAnimator.SetTrigger("Attack");

        // 1. 精灵移动至玩家主相机的前方固定距离（飞入视野）
        if (mainCameraTransform != null)
        {
            Vector3 targetPosition = mainCameraTransform.position + (mainCameraTransform.forward * stopDistanceToCamera);
            targetPosition.y = mainCameraTransform.position.y - 0.5f; // 稍微向下微调，防止正中心挡死屏幕

            while (Vector3.Distance(transform.position, targetPosition) > 0.2f)
            {
                Vector3 lookPos = mainCameraTransform.position - transform.position;
                lookPos.y = 0;
                if (lookPos != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookPos);

                transform.position = Vector3.MoveTowards(transform.position, targetPosition, flySpeed * Time.deltaTime);
                yield return null;
            }
            transform.position = targetPosition;
        }

        // 2. 精灵到位，立刻开启吹风特效，播放风声音乐
        Debug.Log("【剧情控制】风精灵已就位！开始吹风并播放风声音效。");
        if (windParticle != null) windParticle.Play();
        if (windAudio != null) windAudio.Play();

        // 3. 让风在玩家眼前吹上 2 秒钟（作为失重前的氛围铺垫）
        yield return new WaitForSeconds(windBlowDuration);

        // 4. 风力彻底生效，让特殊蘑菇瞬间起飞！
        Debug.Log("【剧情控制】吹风完成！命令特殊蘑菇起飞飞入视野！");
        currentPhase = ImpPhase.Weightless;

        if (specialMushroom != null)
        {
            // 通过 SendMessage 远程跨语言通信调用你 WeightlessFloat 脚本里写好的公开函数 "StartFloating"
            // 这种写法极为安全，不会引起任何编译器找不到类型的错误报错
            specialMushroom.SendMessage("StartFloating", SendMessageOptions.DontRequireReceiver);
        }

        // 5. 如果你在 Inspector 里面分配了其他需要一同跟着飞的普通蘑菇组（floatingObjects）
        if (floatingObjects != null)
        {
            floatingObjects.SetActive(true);
            // 广播呼叫该父物体下面所有带有漂浮功能的子蘑菇一并起飞
            floatingObjects.BroadcastMessage("StartFloating", SendMessageOptions.DontRequireReceiver);
        }
    }

    void HandleFocusTraining()
    {
        if (mainCameraTransform != null)
        {
            Vector3 lookPos = mainCameraTransform.position - transform.position;
            lookPos.y = 0;
            if (lookPos != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookPos);
        }

        if (focusScore >= targetFocus)
        {
            focusTimer += Time.deltaTime;
            if (focusTimer >= requiredDuration)
            {
                currentPhase = ImpPhase.Recovered;
                StartCoroutine(WinSequence());
            }
        }
        else
        {
            focusTimer = Mathf.Max(0f, focusTimer - Time.deltaTime);
        }
    }

    IEnumerator WinSequence()
    {
        if (impAnimator != null) impAnimator.SetTrigger("Idle");
        if (windParticle != null) windParticle.Stop();
        SetImpVisibility(false);
        if (windAudio != null) windAudio.Stop();

        yield return new WaitForSeconds(1.0f);
        currentPhase = ImpPhase.Waiting;
    }

    void SetImpVisibility(bool isVisible)
    {
        foreach (Renderer rend in impRenderers)
        {
            if (rend != null) rend.enabled = isVisible;
        }
    }
}