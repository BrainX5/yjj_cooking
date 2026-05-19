using UnityEngine;

public class CanalFishWanderTarget : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 boundsMin;
    [SerializeField] private Vector3 boundsMax;
    [SerializeField] private Vector2 retargetDelayRange = new Vector2(1.6f, 4.2f);
    [SerializeField] private float verticalBobAmplitude = 0.15f;
    [SerializeField] private float verticalBobSpeed = 1.2f;

    private float nextRetargetTime;
    private Vector3 anchorPoint;
    private float bobOffset;

    public void Configure(Transform targetTransform, Vector3 min, Vector3 max, Vector2 delayRange)
    {
        target = targetTransform;
        boundsMin = min;
        boundsMax = max;
        retargetDelayRange = delayRange;
        anchorPoint = target != null ? target.position : Vector3.zero;
        bobOffset = Random.Range(0f, Mathf.PI * 2f);
        ScheduleRetarget(0f);
    }

    public void SetBounds(Vector3 min, Vector3 max)
    {
        boundsMin = min;
        boundsMax = max;
        anchorPoint = ClampToBounds(anchorPoint);

        if (target != null)
        {
            target.position = ClampToBounds(target.position);
        }
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        if (Time.time >= nextRetargetTime)
        {
            anchorPoint = RandomPoint(boundsMin, boundsMax);
            ScheduleRetarget(Random.Range(retargetDelayRange.x, retargetDelayRange.y));
        }

        var bob = Mathf.Sin(Time.time * verticalBobSpeed + bobOffset) * verticalBobAmplitude;
        var position = anchorPoint;
        position.y = Mathf.Clamp(position.y + bob, boundsMin.y, boundsMax.y);
        target.position = position;
    }

    private void ScheduleRetarget(float delay)
    {
        nextRetargetTime = Time.time + Mathf.Max(0.1f, delay);
    }

    private Vector3 RandomPoint(Vector3 min, Vector3 max)
    {
        return new Vector3(
            Random.Range(min.x, max.x),
            Random.Range(min.y, max.y),
            Random.Range(min.z, max.z));
    }

    private Vector3 ClampToBounds(Vector3 point)
    {
        return new Vector3(
            Mathf.Clamp(point.x, boundsMin.x, boundsMax.x),
            Mathf.Clamp(point.y, boundsMin.y, boundsMax.y),
            Mathf.Clamp(point.z, boundsMin.z, boundsMax.z));
    }
}
