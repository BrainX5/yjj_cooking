using UnityEngine;
using System.Collections;

public class SCUT_WeightlessFloat : MonoBehaviour
{
    [Header("🔗 请在场景里按从左到右的顺序给蘑菇手动编号 (0, 1, 2, 3...)")]
    public int mushroomIndex = 0; 

    [Header("🔗 场景里一共会有多少个会飞的普通蘑菇")]
    public int totalMushroomCount = 5;

    [Header("📐 单排蘑菇在视野里的总跨度宽度（值越小越紧凑）")]
    public float totalWidth = 6.0f; 

    [Header("🌟 蘑菇到风精灵的绝对前方距离（确保完全穿过精灵不被遮挡）")]
    public float distanceInFrontOfImp = 6.5f;

    [Header("双排前后的交错纵深距离（防止重叠）")]
    public float doubleRowDepth = 1.2f;

    [Header("相对于风精灵头顶往上抬高的高度（保证在视野上方）")]
    public float heightOffsetFromImp = 2.5f;

    [Header("飞行总时间")]
    public float flyTime = 2f;

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
        
        // 寻找场景中的风精灵
        SCUT_FlowerDryadController imp = FindObjectOfType<SCUT_FlowerDryadController>();
        if (imp != null)
        {
            // 1. 获取精灵此时此刻面向前方的水平向量（此时精灵已被相机运镜脚本强行归一化了朝向）
            Vector3 impForward = imp.transform.forward;
            impForward.y = 0;
            impForward.Normalize();

            Vector3 impRight = Vector3.Cross(Vector3.up, impForward).normalized;

            // 2. 【高级奇偶交错双排算法】
            // 将蘑菇分为两排：偶数在前排，奇数在后排
            bool isEvenRow = (mushroomIndex % 2 == 0);
            
            // 计算当前行在自身队伍里的标准比例 t 
            // 采用统一映射，防止一端过偏
            float perStep = totalWidth / Mathf.Max(1, (totalMushroomCount - 1));
            float halfWidth = totalWidth * 0.5f;
            float localX = (mushroomIndex * perStep) - halfWidth;

            // 3. 纵深（Z轴）交错：奇数排往后退一点，并在横向上增加微调以实现错位交错
            float localZ = distanceInFrontOfImp;
            if (!isEvenRow)
            {
                localZ += doubleRowDepth; // 往后排推
                localX += perStep * 0.5f; // 横向错开半个身位，完美填补前排空隙
            }

            // 4. 合成最终完美目标点（基于精灵坐标作为绝对锚点推导）
            targetPos = imp.transform.position 
                        + impForward * localZ 
                        + impRight * localX 
                        + Vector3.up * heightOffsetFromImp;
        }
        else
        {
            // 兜底策略
            targetPos = startPos + Vector3.up * 5f;
        }

        StartCoroutine(FlyRoutine());
    }

    IEnumerator FlyRoutine()
    {
        float timer = 0f;
        // 抛物线弧度平滑飞入
        Vector3 curvePoint = (startPos + targetPos) / 2f + Vector3.up * 4f;

        while (timer < flyTime)
        {
            timer += Time.deltaTime;
            float t = timer / flyTime;
            // 贝塞尔曲线平滑插值
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
            // 上下呼吸感微动
            float yOffset = Mathf.Sin(Time.time * floatSpeed + floatSeed) * floatAmplitude;
            transform.position = targetPos + new Vector3(0, yOffset, 0);
        }
    }
}