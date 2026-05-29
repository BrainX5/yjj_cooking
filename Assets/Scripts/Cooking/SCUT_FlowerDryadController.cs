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
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        SetImpVisibility(false);
        if (windParticle != null) windParticle.Stop();
    }

    void Update()
    {
        if (currentPhase == ImpPhase.Weightless)
        {
            HandleFocusTraining();
        }
    }

    // 📢 Trigger Interface Called By Broadcast
    public void TriggerImpAttack()
    {
        if (currentPhase != ImpPhase.Waiting) return;
        currentPhase = ImpPhase.Appears;
        StartCoroutine(ImpEntranceAndFlySequence());
    }

    // 🎯 Core Fly Path Logic
    IEnumerator ImpEntranceAndFlySequence()
    {
        if (windParticle != null) windParticle.Play();
        Debug.Log("Signal received: Storm started on the other side.");
        yield return new WaitForSeconds(0.6f);

        SetImpVisibility(true);
        if (impAnimator != null)
        {
            impAnimator.SetTrigger("Attack");
        }

        if (mainCameraTransform != null)
        {
            Vector3 targetPosition = mainCameraTransform.position + (mainCameraTransform.forward * stopDistanceToCamera);
            targetPosition.y = mainCameraTransform.position.y - 0.5f;

            Debug.Log("Path locked: Sprite is flying to Main Camera POV.");

            while (Vector3.Distance(transform.position, targetPosition) > 0.2f)
            {
                Vector3 lookPos = mainCameraTransform.position - transform.position;
                lookPos.y = 0;
                if (lookPos != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(lookPos);
                }

                transform.position = Vector3.MoveTowards(transform.position, targetPosition, flySpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = targetPosition;
        }

        Debug.Log("Intercept success: Arrived at Main Camera POV. Focus check start.");
        currentPhase = ImpPhase.Weightless;
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