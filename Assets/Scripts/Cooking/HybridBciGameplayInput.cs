using UnityEngine;

[DefaultExecutionOrder(-100)]
public class HybridBciGameplayInput : MonoBehaviour
{
    [Header("Attention")]
    [SerializeField] private int attentionActivateThreshold = 60;
    [SerializeField] private int attentionDeactivateThreshold = 55;
    [SerializeField] private float attentionSmoothingTime = 0.4f;

    [Header("Head Look")]
    [SerializeField] private float lookDeadZone = 5f;
    [SerializeField] private float lookFullScaleSignal = 20f;
    [SerializeField] private float lookSmoothingTime = 0.16f;
    [SerializeField] private float lookNeutralFollowThreshold = 3f;
    [SerializeField] private float lookNeutralFollowSpeed = 8f;
    [SerializeField] private bool invertYaw = true;
    [SerializeField] private bool invertPitch;

    [Header("Gyroscope Gestures")]
    [SerializeField] private float gesturePeakThreshold = 12f;
    [SerializeField] private float gestureCenterThreshold = 4.5f;
    [SerializeField] private float gestureSequenceWindow = 0.8f;
    [SerializeField] private float gestureCooldown = 0.6f;

    public static HybridBciGameplayInput Instance { get; private set; }

    public bool HasLiveConnection => bridge != null && bridge.IsConnected;
    public bool IsAttentionActive => HasLiveConnection && attentionActive;
    public float SmoothedAttention => smoothedAttention;
    public float LookYawInput => HasLiveConnection ? smoothedLookYaw : 0f;
    public float LookPitchInput => HasLiveConnection ? smoothedLookPitch : 0f;

    private HybridBciPlatformBridge bridge;
    private float smoothedAttention;
    private float attentionVelocity;
    private bool attentionActive;
    private float smoothedLookYaw;
    private float smoothedLookPitch;
    private float lookYawVelocity;
    private float lookPitchVelocity;
    private float lookYawBaseline;
    private float lookPitchBaseline;
    private bool lookBaselineInitialized;
    private bool wasHeadLookConnectedLastFrame;
    private int pendingHorizontalGestures;
    private int pendingVerticalGestures;
    private readonly GestureTracker horizontalGesture = new GestureTracker();
    private readonly GestureTracker verticalGesture = new GestureTracker();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        ResolveBridge();
        UpdateAttentionState();
        UpdateHeadLookState();
        UpdateGestureState();
    }

    public bool ConsumeHorizontalGesture()
    {
        if (pendingHorizontalGestures <= 0)
        {
            return false;
        }

        pendingHorizontalGestures--;
        return true;
    }

    public bool ConsumeVerticalGesture()
    {
        if (pendingVerticalGestures <= 0)
        {
            return false;
        }

        pendingVerticalGestures--;
        return true;
    }

    private void ResolveBridge()
    {
        if (bridge != null)
        {
            return;
        }

        bridge = HybridBciPlatformBridge.Instance;
        if (bridge == null)
        {
            bridge = FindObjectOfType<HybridBciPlatformBridge>();
        }
    }

    private void UpdateAttentionState()
    {
        var targetAttention = 0f;
        if (HasLiveConnection && bridge.AttentionValue >= 0)
        {
            targetAttention = bridge.AttentionValue;
        }

        smoothedAttention = Mathf.SmoothDamp(
            smoothedAttention,
            targetAttention,
            ref attentionVelocity,
            attentionSmoothingTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        if (!HasLiveConnection)
        {
            attentionActive = false;
            return;
        }

        if (!attentionActive && smoothedAttention >= attentionActivateThreshold)
        {
            attentionActive = true;
        }
        else if (attentionActive && smoothedAttention <= attentionDeactivateThreshold)
        {
            attentionActive = false;
        }
    }

    private void UpdateHeadLookState()
    {
        var targetYaw = 0f;
        var targetPitch = 0f;

        if (HasLiveConnection)
        {
            var gyro = bridge.LastGyroscope;
            if (!wasHeadLookConnectedLastFrame || !lookBaselineInitialized)
            {
                CaptureLookBaseline(gyro);
            }

            UpdateLookBaseline(ref lookYawBaseline, gyro.gyroscopeX);
            UpdateLookBaseline(ref lookPitchBaseline, gyro.gyroscopeY);

            targetYaw = NormalizeLookAxis(gyro.gyroscopeX - lookYawBaseline);
            targetPitch = NormalizeLookAxis(gyro.gyroscopeY - lookPitchBaseline);

            if (invertYaw)
            {
                targetYaw *= -1f;
            }

            if (invertPitch)
            {
                targetPitch *= -1f;
            }
        }
        else
        {
            wasHeadLookConnectedLastFrame = false;
            lookBaselineInitialized = false;
        }

        smoothedLookYaw = Mathf.SmoothDamp(
            smoothedLookYaw,
            targetYaw,
            ref lookYawVelocity,
            lookSmoothingTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        smoothedLookPitch = Mathf.SmoothDamp(
            smoothedLookPitch,
            targetPitch,
            ref lookPitchVelocity,
            lookSmoothingTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    private void CaptureLookBaseline(HybridBciPlatformBridge.GyroscopeState gyro)
    {
        lookYawBaseline = gyro.gyroscopeX;
        lookPitchBaseline = gyro.gyroscopeY;
        lookBaselineInitialized = true;
        wasHeadLookConnectedLastFrame = true;
        smoothedLookYaw = 0f;
        smoothedLookPitch = 0f;
        lookYawVelocity = 0f;
        lookPitchVelocity = 0f;
    }

    private void UpdateLookBaseline(ref float baseline, float rawValue)
    {
        var offsetFromBaseline = rawValue - baseline;
        if (Mathf.Abs(offsetFromBaseline) > lookNeutralFollowThreshold)
        {
            return;
        }

        baseline = Mathf.MoveTowards(
            baseline,
            rawValue,
            lookNeutralFollowSpeed * Time.unscaledDeltaTime);
    }

    private void UpdateGestureState()
    {
        if (!HasLiveConnection)
        {
            pendingHorizontalGestures = 0;
            pendingVerticalGestures = 0;
            horizontalGesture.Reset();
            verticalGesture.Reset();
            return;
        }

        var gyro = bridge.LastGyroscope;
        if (UpdateGestureAxis(gyro.gyroscopeX, horizontalGesture))
        {
            pendingHorizontalGestures++;
        }

        if (UpdateGestureAxis(gyro.gyroscopeY, verticalGesture))
        {
            pendingVerticalGestures++;
        }
    }

    private bool UpdateGestureAxis(float axisValue, GestureTracker tracker)
    {
        var now = Time.unscaledTime;
        var magnitude = Mathf.Abs(axisValue);

        if (magnitude <= gestureCenterThreshold)
        {
            if (!tracker.tracking && now - tracker.lastGestureTime >= gestureCooldown)
            {
                tracker.armed = true;
            }

            return false;
        }

        if (!tracker.tracking)
        {
            if (!tracker.armed || now - tracker.lastGestureTime < gestureCooldown || magnitude < gesturePeakThreshold)
            {
                return false;
            }

            tracker.tracking = true;
            tracker.startDirection = axisValue > 0f ? 1 : -1;
            tracker.startTime = now;
            tracker.armed = false;
            return false;
        }

        if (now - tracker.startTime > gestureSequenceWindow)
        {
            tracker.tracking = false;
            return false;
        }

        var currentDirection = axisValue > 0f ? 1 : -1;
        if (currentDirection != tracker.startDirection && magnitude >= gesturePeakThreshold)
        {
            tracker.tracking = false;
            tracker.lastGestureTime = now;
            return true;
        }

        return false;
    }

    private float NormalizeLookAxis(float axisValue)
    {
        var magnitude = Mathf.Abs(axisValue);
        if (magnitude <= lookDeadZone)
        {
            return 0f;
        }

        var safeFullScale = Mathf.Max(lookDeadZone + 0.01f, lookFullScaleSignal);
        var normalized = Mathf.InverseLerp(lookDeadZone, safeFullScale, magnitude);
        return Mathf.Sign(axisValue) * normalized;
    }

    private sealed class GestureTracker
    {
        public bool armed = true;
        public bool tracking;
        public int startDirection;
        public float startTime;
        public float lastGestureTime = -999f;

        public void Reset()
        {
            armed = true;
            tracking = false;
            startDirection = 0;
            startTime = 0f;
            lastGestureTime = -999f;
        }
    }
}
