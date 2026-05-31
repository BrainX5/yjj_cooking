using UnityEngine;
using System.Collections;

public class CameraFlightTracker : MonoBehaviour
{
    [Header("🎬 相机相对于风精灵的拉退距离")]
    public float backwardDistance = 5.5f;

    [Header("🎬 相机相对于风精灵的抬高高度")]
    public float upwardHeight = 2.4f;

    [Header("🎬 镜头的俯视低头角度")]
    public float pitchAngle = 14f;

    [Header("运镜平滑过渡的时间 (秒)")]
    public float transitionDuration = 1.5f;

    public void StartCameraTrack(Transform impTransform)
    {
        if (impTransform == null) return;
        
        StopAllCoroutines();
        StartCoroutine(LockStepStageRoutine(impTransform));
    }

    IEnumerator LockStepStageRoutine(Transform impTransform)
    {
        float timer = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // 提取相机的纯水平朝向
        Vector3 stageForward = transform.forward;
        stageForward.y = 0;
        stageForward.Normalize();

        // 让精灵面对相机的正前方站好（确立前方矩阵基准）
        impTransform.rotation = Quaternion.LookRotation(stageForward);

        // 计算相机到精灵的固定机位
        Vector3 targetPosition = impTransform.position - stageForward * backwardDistance + Vector3.up * upwardHeight;

        // 盯着风精灵
        Vector3 lookDir = impTransform.position - targetPosition;
        Quaternion baseLookRot = Quaternion.LookRotation(lookDir);
        Quaternion finalTargetRotation = Quaternion.Euler(pitchAngle, baseLookRot.eulerAngles.y, 0f);

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(startPos, targetPosition, smoothT);
            transform.rotation = Quaternion.Slerp(startRot, finalTargetRotation, smoothT);
            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = finalTargetRotation;
        Debug.Log("【运镜完毕】相机已完美锁定在精灵后方视口。");
    }
}