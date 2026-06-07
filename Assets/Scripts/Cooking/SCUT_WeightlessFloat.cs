using UnityEngine;
using System.Collections;

public class SCUT_WeightlessFloat : MonoBehaviour
{
    [Header("🔗 请在场景里按从左到右的顺序给蘑菇手动编号 (0, 1, 2, 3...)")]
    public int mushroomIndex = 0;
    public int totalMushroomCount = 5;
    public float mushroomSpacing = 1.25f;
    public float distanceInFrontOfImp = 5.2f;
    public float doubleRowDepth = 0.85f;
    public float heightOffsetFromImp = 2.4f;
    public float heightWaveAmplitude = 0.45f;
    public float flyTime = 1.8f;
    public float floatAmplitude = 0.05f;
    public float floatSpeed = 1.5f;

    private Rigidbody rb;
    private Vector3 startPos;
    private Vector3 targetPos;
    private bool floating = false;
    private float floatSeed;
    private bool isDropped = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    public void StartFloating()
    {
        isDropped = false;
        floating = false;
        rb.isKinematic = true;
        rb.useGravity = false;

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

            targetPos = imp.transform.position + impForward * localZ + impRight * localX + Vector3.up * localY;
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
        if (floating && !isDropped)
        {
            float yOffset = Mathf.Sin(Time.time * floatSpeed + floatSeed) * floatAmplitude;
            transform.position = targetPos + new Vector3(0, yOffset, 0);
        }
    }

    // 由花精灵主控制器在内部逻辑判定通过时直接触发掉落
    public void ExecuteDrop()
    {
        if (isDropped) return;
        floating = false;
        isDropped = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        StartCoroutine(HideDelay());
    }

    IEnumerator HideDelay()
    {
        yield return new WaitForSeconds(2.0f);
        gameObject.SetActive(false);
    }
}