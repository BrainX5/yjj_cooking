using UnityEngine;
using System.Collections;

public class SCUT_WeightlessFloat : MonoBehaviour
{
    [Header("🔗 请在场景里按从左到右的顺序给蘑菇手动编号 (0, 1, 2, 3...)")]
    public int mushroomIndex = 0; 
    public int totalMushroomCount = 5;

    [Header("📐 矩阵间距参数")]
    public float mushroomSpacing = 1.25f; 
    public float distanceInFrontOfImp = 5.2f;
    public float doubleRowDepth = 0.85f;
    public float heightOffsetFromImp = 2.4f;
    public float heightWaveAmplitude = 0.45f;
    public float flyTime = 1.8f;

    [Header("空中无规则微幅浮动动效")]
    public float floatAmplitude = 0.05f;
    public float floatSpeed = 1.5f;

    [Header("✨ 瞄准视觉反馈配置")]
    public GameObject selectionRing;
    public float targetScaleMultiplier = 1.35f;

    private Vector3 startPos; // 地面原位
    private Vector3 targetFloatPos; // 空中稳定悬浮点
    private Vector3 originalScale;
    private bool isFloatingActive = false;
    private float floatSeed;

    private bool isTargeted = false;
    private bool isBeingCollected = false;
    private bool isDeadCollected = false; 
    private Coroutine snapBackCoroutine;

    void Awake()
    {
        originalScale = transform.localScale;
        startPos = transform.position; // 游戏刚加载时死死咬住地面原位
        if (selectionRing != null) selectionRing.SetActive(false);
        
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }

    public void StartFloating()
    {
        isFloatingActive = false;
        isBeingCollected = false;
        isDeadCollected = false;
        floatSeed = Random.Range(0f, 100f); 
        
        SCUT_FlowerDryadController imp = FindObjectOfType<SCUT_FlowerDryadController>();
        if (imp != null)
        {
            Vector3 impForward = imp.transform.forward;
            impForward.y = 0;
            impForward.Normalize();
            Vector3 impRight = Vector3.Cross(Vector3.up, impForward).normalized;

            bool isEvenRow = (mushroomIndex % 2 == 0);
            int rowGroupIndex = mushroomIndex / 2;
            int totalInThisRow = isEvenRow ? Mathf.CeilToInt(totalMushroomCount / 2.0f) : Mathf.FloorToInt(totalMushroomCount / 2.0f);
            
            float centerOffset = (totalInThisRow - 1) * 0.5f;
            float localX = (rowGroupIndex - centerOffset) * mushroomSpacing;

            float localZ = distanceInFrontOfImp;
            float localY = heightOffsetFromImp;

            if (!isEvenRow)
            {
                localZ += doubleRowDepth;       
                localX += mushroomSpacing * 0.5f; 
                localY += heightWaveAmplitude;  
            }

            targetFloatPos = imp.transform.position + impForward * localZ + impRight * localX + Vector3.up * localY;
        }
        else
        {
            targetFloatPos = startPos + Vector3.up * 5f;
        }

        StopAllCoroutines();
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FlyRoutine());
        }
    }

    IEnumerator FlyRoutine()
    {
        float timer = 0f;
        Vector3 curvePoint = (startPos + targetFloatPos) / 2f + Vector3.up * 3.5f;

        while (timer < flyTime)
        {
            timer += Time.deltaTime;
            float t = timer / flyTime;
            Vector3 m1 = Vector3.Lerp(startPos, curvePoint, t);
            Vector3 m2 = Vector3.Lerp(curvePoint, targetFloatPos, t);
            transform.position = Vector3.Lerp(m1, m2, t);
            yield return null;
        }

        transform.position = targetFloatPos;
        isFloatingActive = true;
    }

    void Update()
    {
        if (isFloatingActive && !isBeingCollected && !isDeadCollected)
        {
            float yOffset = Mathf.Sin(Time.time * floatSpeed + floatSeed) * floatAmplitude;
            transform.position = targetFloatPos + new Vector3(0, yOffset, 0);
        }
    }

    public void SetTargeted(bool targeted)
    {
        if (isDeadCollected) return;
        isTargeted = targeted;
        if (!isBeingCollected)
        {
            if (selectionRing != null) selectionRing.SetActive(targeted);
            transform.localScale = targeted ? originalScale * targetScaleMultiplier : originalScale;
        }
    }

    public void UpdateCollectProgress(float progress, Vector3 cameraDestination)
    {
        if (isDeadCollected) return;
        if (snapBackCoroutine != null)
        {
            StopCoroutine(snapBackCoroutine);
            snapBackCoroutine = null;
        }

        isBeingCollected = true;
        progress = Mathf.Clamp01(progress);
        transform.localScale = originalScale;
        if (selectionRing != null) selectionRing.SetActive(false); 

        transform.position = Vector3.Lerp(targetFloatPos, cameraDestination, progress);
    }

    public void ResetCollectProgress()
    {
        if (isDeadCollected || !isBeingCollected) return;
        if (snapBackCoroutine != null) StopCoroutine(snapBackCoroutine);
        snapBackCoroutine = StartCoroutine(SnapBackRoutine());
    }

    IEnumerator SnapBackRoutine()
    {
        float snapTimer = 0f;
        Vector3 currentPos = transform.position;
        while (snapTimer < 0.4f)
        {
            snapTimer += Time.deltaTime;
            transform.position = Vector3.Lerp(currentPos, targetFloatPos, snapTimer / 0.4f);
            yield return null;
        }
        transform.position = targetFloatPos;
        isBeingCollected = false;
    }

    // 🌟 落地还原机制
    public void LandBackToGround()
    {
        if (isDeadCollected) return; 
        isFloatingActive = false;
        isBeingCollected = false;
        isTargeted = false;
        transform.localScale = originalScale;
        if (selectionRing != null) selectionRing.SetActive(false);

        StopAllCoroutines();
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(ResetToGroundRoutine());
        }
    }

    IEnumerator ResetToGroundRoutine()
    {
        float timer = 0f;
        Vector3 currentPos = transform.position;
        while (timer < 1.5f)
        {
            timer += Time.deltaTime;
            transform.position = Vector3.Lerp(currentPos, startPos, timer / 1.5f);
            yield return null;
        }
        transform.position = startPos;
    }

    public bool IsMushroomCollected()
    {
        return isDeadCollected;
    }

    public void CollectSuccess()
    {
        isDeadCollected = true;
        isBeingCollected = false;
        isFloatingActive = false;
        gameObject.SetActive(false); // 彻底隐藏
    }
}