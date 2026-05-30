using UnityEngine;
using System.Collections;

public class FloatingRise : MonoBehaviour
{
    [Header("上升设置")]
    public float riseHeight = 3f;          // 上升高度
    public float riseTime = 2f;            // 上升时间

    [Header("随机范围")]
    public float randomX = 2f;
    public float randomZ = 2f;

    [Header("漂浮设置")]
    public float floatAmplitude = 0.2f;   // 漂浮幅度
    public float floatSpeed = 1.5f;       // 漂浮速度

    [Header("旋转设置")]
    public float rotateSpeed = 20f;

    private Vector3 startPos;
    private Vector3 targetPos;

    private bool finishedRise = false;

    void Start()
    {
        startPos = transform.position;

        // 随机终点位置
        targetPos = startPos + new Vector3(
            Random.Range(-randomX, randomX),
            riseHeight,
            Random.Range(-randomZ, randomZ)
        );

        StartCoroutine(RiseAnimation());
    }

    IEnumerator RiseAnimation()
    {
        float timer = 0f;

        while (timer < riseTime)
        {
            timer += Time.deltaTime;

            float t = timer / riseTime;

            // 平滑上升
            Vector3 currentPos = Vector3.Lerp(
                startPos,
                targetPos,
                Mathf.SmoothStep(0f, 1f, t)
            );

            // 加一点随机摆动
            currentPos.x += Mathf.Sin(Time.time * 2f + transform.position.x) * 0.15f;
            currentPos.z += Mathf.Cos(Time.time * 2f + transform.position.z) * 0.15f;

            transform.position = currentPos;

            yield return null;
        }

        finishedRise = true;
    }

    void Update()
    {
        if (finishedRise)
        {
            FloatingMotion();
        }
    }

    void FloatingMotion()
    {
        // 上下漂浮
        Vector3 pos = transform.position;

        pos.y += Mathf.Sin(Time.time * floatSpeed) * floatAmplitude * Time.deltaTime;

        transform.position = pos;

        // 缓慢旋转
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }
}