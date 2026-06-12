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
    [Tooltip("被选中时产生的光环特效物体（选填）")]
    public GameObject selectionRing;
    [Tooltip("被瞄准时光栅/模型放大的倍数")]
    public float targetScaleMultiplier = 1.35f;

    private Vector3 startPos;
    private Vector3 targetFloatPos; // 飞到空中悬浮的固定稳定坐标点
    private Vector3 originalScale;
    private bool isFloatingActive = false;
    private float floatSeed;

    // 严密的状态机隔离
    private bool isTargeted = false;
    private bool isBeingCollected = false;
    private Coroutine snapBackCoroutine;

    void Awake()
    {
        originalScale = transform.localScale;
        if (selectionRing != null) selectionRing.SetActive(false);
        
        // 确保身上有碰撞盒，不然射线瞄不准
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }

    public void StartFloating()
    {
        startPos = transform.position;
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
            else
            {
                if (rowGroupIndex % 2 == 0) localY -= heightWaveAmplitude * 0.25f; 
            }

            float edgeDistFactor = Mathf.Abs(localX) / 3.0f; 
            localZ -= edgeDistFactor * 0.4f; 

            targetFloatPos = imp.transform.position + impForward * localZ + impRight * localX + Vector3.up * localY;
        }
        else
        {
            targetFloatPos = startPos + Vector3.up * 5f;
        }

        StartCoroutine(FlyRoutine());
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
        // 关键防抖：只有在非吸取状态下，才允许执行Sin微幅浮动。一旦开始吸取，完全接管控制权，不准抖动！
        if (isFloatingActive && !isBeingCollected)
        {
            float yOffset = Mathf.Sin(Time.time * floatSpeed + floatSeed) * floatAmplitude;
            transform.position = targetFloatPos + new Vector3(0, yOffset, 0);
        }
    }

    /// <summary>
    /// 被物理射线对准/移开时触发
    /// </summary>
    public void SetTargeted(bool targeted)
    {
        isTargeted = targeted;

        // 仅在非吸取状态下，允许通过缩放和光环提示玩家“我被选中了”
        if (!isBeingCollected)
        {
            if (selectionRing != null) selectionRing.SetActive(targeted);
            transform.localScale = targeted ? originalScale * targetScaleMultiplier : originalScale;
        }
    }

    /// <summary>
    /// 核心吸取接口：按住 T 时平滑往摄像头（面前）移动，并在吸取时将大小恢复原样
    /// </summary>
    public void UpdateCollectProgress(float progress, Vector3 cameraDestination)
    {
        if (snapBackCoroutine != null)
        {
            StopCoroutine(snapBackCoroutine);
            snapBackCoroutine = null;
        }

        isBeingCollected = true;
        progress = Mathf.Clamp01(progress);

        // 需求：吸取开始移动时，蘑菇本身不要变大，还原成原始正常尺寸
        transform.localScale = originalScale;
        if (selectionRing != null) selectionRing.SetActive(false); // 正在吸的时候可以关掉提示圈

        // 使用平滑的物理插值移向摄像头面前
        transform.position = Vector3.Lerp(targetFloatPos, cameraDestination, progress);
    }

    /// <summary>
    /// 中断吸取：中途松开 T 键时触发，平滑弹回原悬浮位
    /// </summary>
    public void ResetCollectProgress()
    {
        if (!isBeingCollected) return;
        
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

        // 弹回后根据射线当前是否还在身上，决定是否恢复选中大体积
        if (isTargeted)
        {
            transform.localScale = originalScale * targetScaleMultiplier;
            if (selectionRing != null) selectionRing.SetActive(true);
        }
    }

    public void CollectSuccess()
    {
        Debug.Log($"【获取成功】普通蘑菇 #{mushroomIndex} 已收入背包。");
        gameObject.SetActive(false); 
    }
}