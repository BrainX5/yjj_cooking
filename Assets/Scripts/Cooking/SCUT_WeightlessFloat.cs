using UnityEngine;
using System.Collections;

public class WeightlessFloat : MonoBehaviour
{
    [Header("漂浮高度")]
    public float riseHeight = 3f;

    [Header("飞行时间")]
    public float flyTime = 2f;

    [Header("随机范围")]
    public float randomX = 2f;
    public float randomZ = 2f;

    [Header("轻微浮动")]
    public float floatAmplitude = 0.05f;

    public float floatSpeed = 1.5f;

    private Vector3 startPos;
    private Vector3 targetPos;

    private bool floating = false;

    // 不自动执行
    public void StartFloating()
    {
        startPos = transform.position;

        GenerateTarget();

        StartCoroutine(FlyRoutine());
    }

    void GenerateTarget()
    {
        Camera cam = Camera.main;

        Vector3 forwardOffset =
            cam.transform.forward * Random.Range(3f, 5f);

        Vector3 randomOffset = new Vector3(
            Random.Range(-randomX, randomX),
            Random.Range(1f, riseHeight),
            Random.Range(-randomZ, randomZ)
        );

        targetPos =
            cam.transform.position +
            forwardOffset +
            randomOffset;
    }

    IEnumerator FlyRoutine()
    {
        float timer = 0f;

        Vector3 curvePoint =
            (startPos + targetPos) / 2f +
            new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(1f, 2f),
                Random.Range(-1f, 1f)
            );

        while (timer < flyTime)
        {
            timer += Time.deltaTime;

            float t = timer / flyTime;

            // 贝塞尔曲线
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
            FloatMotion();
        }
    }

    void FloatMotion()
    {
        Vector3 pos = transform.position;

        // 微小上下浮动
        pos.y += Mathf.Sin(Time.time * floatSpeed)
                 * floatAmplitude
                 * Time.deltaTime;

        transform.position = pos;
    }
}