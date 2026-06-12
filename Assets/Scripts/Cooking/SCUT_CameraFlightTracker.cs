using UnityEngine;
using System.Collections;

public class SCUT_CameraFlightTracker : MonoBehaviour
{
    [Header("🎬 相机相对于风精灵的拉退距离")]
    public float backwardDistance = 5.5f;

    [Header("🎬 相机相对于风精灵的抬高高度")]
    public float upwardHeight = 2.4f;

    [Header("🎬 镜头的俯视低头角度")]
    public float pitchAngle = 14f;

    [Header("运镜平滑过渡的时间 (秒)")]
    public float transitionDuration = 1.5f;

    // 刚性存储玩家触发事件瞬间最原始的视角数据
    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;
    private bool hasSavedInitialTransform = false;

    // 🌟 开放给控制器的绝对归位锁定方法：在事件爆发第一帧无条件存死数据
    public void SavePlayerInitialTransform()
    {
        initialCameraPosition = transform.position;
        initialCameraRotation = transform.rotation;
        hasSavedInitialTransform = true;
        Debug.Log($"<color=yellow>【相机脚本】成功强行锁死玩家的原始机位！坐标：{initialCameraPosition}，朝向：{initialCameraRotation.eulerAngles}</color>");
    }

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

        Vector3 stageForward = transform.forward;
        stageForward.y = 0;
        stageForward.Normalize();

        impTransform.rotation = Quaternion.LookRotation(stageForward);

        Vector3 targetPosition = impTransform.position - stageForward * backwardDistance + Vector3.up * upwardHeight;

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
    }

    // 🌟 退出机制核心调用：平滑将视线拉回触发前
    public void ResetCameraTrack()
    {
        if (!hasSavedInitialTransform)
        {
            Debug.LogError("【相机脚本错误】未检测到已存储的原始机位，相机无法还原！");
            return;
        }
        StopAllCoroutines();
        StartCoroutine(ReturnToInitialRoutine());
    }

    IEnumerator ReturnToInitialRoutine()
    {
        float timer = 0f;
        Vector3 currentPos = transform.position;
        Quaternion currentRot = transform.rotation;

        while (timer < transitionDuration)
        {
            timer += Time.deltaTime;
            float t = timer / transitionDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(currentPos, initialCameraPosition, smoothT);
            transform.rotation = Quaternion.Slerp(currentRot, initialCameraRotation, smoothT);
            yield return null;
        }

        transform.position = initialCameraPosition;
        transform.rotation = initialCameraRotation;
        hasSavedInitialTransform = false; // 重置开关，等待下一次触发
        Debug.Log("<color=yellow>【相机脚本】视角已完美平滑回弹至初始普通玩家视角！</color>");
    }
}