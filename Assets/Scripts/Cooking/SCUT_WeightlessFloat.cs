using UnityEngine;
using System.Collections;

public class SCUT_WeightlessFloat : MonoBehaviour
{
    [Header("🔗 请在场景里按从左到右的顺序给蘑菇手动编号 (0, 1, 2, 3...)")]
    public int mushroomIndex = 0; 

    [Header("🔗 场景里一共会有多少个会飞的普通蘑菇")]
    public int totalMushroomCount = 5;

    [Header("📐 双排横向间距（已完美向两边散开，让视口极具呼吸感）")]
    public float mushroomSpacing = 1.25f; 

    [Header("🌟 蘑菇到风精灵的绝对前方距离")]
    public float distanceInFrontOfImp = 5.2f;

    [Header("双排前后的交错纵深距离")]
    public float doubleRowDepth = 0.85f;

    [Header("相对于风精灵头顶的基础抬高高度")]
    public float heightOffsetFromImp = 2.4f;

    [Header("⛰️ 奇偶高低错落落差（前后排的上下高度落差）")]
    public float heightWaveAmplitude = 0.45f;

    [Header("飞行总时间")]
    public float flyTime = 1.8f;

    [Header("空中无规则微幅浮动动效")]
    public float floatAmplitude = 0.05f;
    public float floatSpeed = 1.5f;

    private Vector3 startPos;
    private Vector3 targetPos;
    private bool floating = false;
    private float floatSeed;

    public void StartFloating()
    {
        startPos = transform.position;
        floatSeed = Random.Range(0f, 100f); 
        
        SCUT_FlowerDryadController imp = FindObjectOfType<SCUT_FlowerDryadController>();
        if (imp != null)
        {
            // 1. 获取精灵此时此刻面向前方的水平向量
            Vector3 impForward = imp.transform.forward;
            impForward.y = 0;
            impForward.Normalize();

            Vector3 impRight = Vector3.Cross(Vector3.up, impForward).normalized;

            // 2. 【3D 弧形错落矩阵算法】
            // 将蘑菇分为两排：偶数在前排（靠近相机），奇数在后排（远离相机）
            bool isEvenRow = (mushroomIndex % 2 == 0);
            
            // 分别计算前排和后排各自的内部计数索引
            int rowGroupIndex = mushroomIndex / 2;
            
            // 计算当前排总共有几个蘑菇
            int totalInThisRow = isEvenRow ? Mathf.CeilToInt(totalMushroomCount / 2.0f) : Mathf.FloorToInt(totalMushroomCount / 2.0f);
            
            // 让每一排都以绝对中央为 0 点，向左右两侧舒适拉开
            float centerOffset = (totalInThisRow - 1) * 0.5f;
            float localX = (rowGroupIndex - centerOffset) * mushroomSpacing;

            // 3. 纵深（Z轴）与高度（Y轴）的三维交错
            float localZ = distanceInFrontOfImp;
            float localY = heightOffsetFromImp;

            if (!isEvenRow)
            {
                // 后排逻辑
                localZ += doubleRowDepth;       // 后排往深处推
                localX += mushroomSpacing * 0.5f; // 横向交错半个身位，完美填补前排空隙
                localY += heightWaveAmplitude;  // 后排整体抬高，错落有致
            }
            else
            {
                // 前排逻辑：内部做细微高度起伏，打破呆板
                if (rowGroupIndex % 2 == 0)
                {
                    localY -= heightWaveAmplitude * 0.25f; 
                }
            }

            // ✨【新增向心弧形算法】：两边的蘑菇在纵深上稍微往前收拢一点点，形成舞台环抱感
            // 这样既能在大气拉开的同时，收住两翼，防止最边缘的蘑菇戳进两侧树里
            float edgeDistFactor = Mathf.Abs(localX) / 3.0f; 
            localZ -= edgeDistFactor * 0.4f; 

            // 4. 合成最终完美目标点
            targetPos = imp.transform.position 
                        + impForward * localZ 
                        + impRight * localX 
                        + Vector3.up * localY;
        }
        else
        {
            targetPos = startPos + Vector3.up * 5f;
        }

        StartCoroutine(FlyRoutine());
    }

    IEnumerator FlyRoutine()
    {
        float timer = 0f;
        Vector3 curvePoint = (startPos + targetPos) / 2f + Vector3.up * 3.5f;

        while (timer < flyTime)
        {
            timer += Time.deltaTime;
            float t = timer / flyTime;
            Vector3 m1 = Vector3.Lerp(startPos, curvePoint, t);
            Vector3 m2 = Vector3.Lerp(curvePoint, targetPos, t);
            transform.position = Vector3.Lerp(m1, m2, t);
            yield return null;
        }

        transform.position = targetPos;
        floating = true;
    }

    void Update()
    {
        if (floating)
        {
            float yOffset = Mathf.Sin(Time.time * floatSpeed + floatSeed) * floatAmplitude;
            transform.position = targetPos + new Vector3(0, yOffset, 0);
        }
    }
}