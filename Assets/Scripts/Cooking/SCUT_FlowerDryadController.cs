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

    public GameObject floatingObjects; 
    public GameObject flowerDryadObject;
    public AudioSource windAudio;

    [Header("失重演出等待时间")]
    public float windBlowDuration = 2.0f; 

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
        mainCameraTransform = Camera.main.transform;
        SetImpVisibility(false);
        if (windParticle != null) windParticle.Stop();
        if (windAudio != null) windAudio.Stop();
    }

    void Update()
    {
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
        if (flowerDryadObject != null)
        {
            flowerDryadObject.SetActive(isVisible);
        }
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

        Debug.Log("【风精灵核心事件激活】开始执行连招动作...");
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
            focusTimer = 0f;

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
            
            StartCoroutine(ActivateBCIGameplay());
        }
    }

    IEnumerator ActivateBCIGameplay()
    {
        yield return new WaitForSeconds(2.0f); 
        // 🔥【已修复】：这里已经完美更换为新的类名 SCUT_BCIFocusTrainingManager
        SCUT_BCIFocusTrainingManager manager = FindObjectOfType<SCUT_BCIFocusTrainingManager>();
        if (manager != null)
        {
            manager.StartFocusTrainingPhase(); 
        }
    }

    void HandleFocusTraining()
    {
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
}