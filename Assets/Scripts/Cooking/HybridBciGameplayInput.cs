using UnityEngine;

[DefaultExecutionOrder(-100)]
public class HybridBciGameplayInput : MonoBehaviour
{
    [Header("Attention")]
    [SerializeField] private int attentionActivateThreshold = 60;
    [SerializeField] private int attentionDeactivateThreshold = 55;
    [SerializeField] private float attentionSmoothingTime = 0.4f;

    [Header("Gyroscope Gestures")]
    [SerializeField] private float gesturePeakThreshold = 8f;
    [SerializeField] private float gestureCenterThreshold = 3.5f;
    [SerializeField] private float verticalFocusPeakThreshold = 55f;
    [SerializeField] private float verticalFocusCenterThreshold = 18f;
    [SerializeField] private float verticalFocusPeakAreaRatio = 0.08f;
    [SerializeField] private float verticalFocusCenterAreaRatio = 0.03f;
    [SerializeField] private float minimumFocusAreaHeightForScaling = 200f;
    [SerializeField] private float focusYSmoothingSpeed = 12f;
    [SerializeField] private float gestureSequenceWindow = 1.1f;
    [SerializeField] private float gestureCooldown = 0.6f;
    [SerializeField] private float yawNeutralUpdateRange = 4f;
    [SerializeField] private float focusYNeutralUpdateRange = 14f;
    [SerializeField] private float neutralAdaptSpeed = 6f;
    [SerializeField] private float forcedNeutralRecenterDelay = 0.45f;
    [SerializeField] private float forcedNeutralRecenterThresholdMultiplier = 2.2f;
    [SerializeField] private float forcedNeutralRecenterSpeed = 18f;

    public static HybridBciGameplayInput Instance { get; private set; }

    public bool HasLiveConnection => bridge != null && bridge.IsConnected;
    public bool IsAttentionActive => HasLiveConnection && attentionActive;
    public float SmoothedAttention => smoothedAttention;
    public float CurrentHorizontalGestureDelta => currentHorizontalGestureDelta;
    public float CurrentVerticalGestureDelta => currentVerticalGestureDelta;
    public float CurrentVerticalPeakThreshold => currentVerticalPeakThreshold;
    public float CurrentVerticalCenterThreshold => currentVerticalCenterThreshold;
    public float NeutralYawAngle => neutralYawAngle;
    public float NeutralFocusY => neutralFocusY;

    private HybridBciPlatformBridge bridge;
    private float smoothedAttention;
    private float attentionVelocity;
    private bool attentionActive;
    private int pendingHorizontalGestures;
    private int pendingVerticalGestures;
    private readonly GestureTracker horizontalGesture = new GestureTracker();
    private readonly GestureTracker verticalGesture = new GestureTracker();
    private float neutralYawAngle;
    private float neutralFocusY;
    private bool hasNeutralYawAngle;
    private bool hasNeutralFocusY;
    private float currentHorizontalGestureDelta;
    private float currentVerticalGestureDelta;
    private float currentVerticalPeakThreshold;
    private float currentVerticalCenterThreshold;
    private float horizontalOffCenterTime;
    private float verticalOffCenterTime;
    private float smoothedFocusY;
    private bool hasSmoothedFocusY;

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

    private void UpdateGestureState()
    {
        if (!HasLiveConnection)
        {
            ResetGestureState();
            return;
        }

        var gyro = bridge.LastGyroscope;
        currentVerticalPeakThreshold = ResolveFocusThreshold(
            gyro.focusAreaHeight,
            verticalFocusPeakThreshold,
            verticalFocusPeakAreaRatio);
        currentVerticalCenterThreshold = ResolveFocusThreshold(
            gyro.focusAreaHeight,
            verticalFocusCenterThreshold,
            verticalFocusCenterAreaRatio);

        currentHorizontalGestureDelta = GetAxisOffset(
            gyro.gyroscopeX,
            ref neutralYawAngle,
            ref hasNeutralYawAngle,
            ref horizontalOffCenterTime,
            yawNeutralUpdateRange,
            gesturePeakThreshold * forcedNeutralRecenterThresholdMultiplier,
            horizontalGesture.tracking);

        if (UpdateGestureAxis(
            currentHorizontalGestureDelta,
            horizontalGesture,
            gesturePeakThreshold,
            gestureCenterThreshold))
        {
            pendingHorizontalGestures++;
        }

        var filteredFocusY = GetSmoothedFocusY(gyro.focusY);
        currentVerticalGestureDelta = GetAxisOffset(
            filteredFocusY,
            ref neutralFocusY,
            ref hasNeutralFocusY,
            ref verticalOffCenterTime,
            focusYNeutralUpdateRange,
            currentVerticalPeakThreshold * forcedNeutralRecenterThresholdMultiplier,
            verticalGesture.tracking);

        if (UpdateVerticalGestureAxis(
            currentVerticalGestureDelta,
            verticalGesture,
            currentVerticalPeakThreshold,
            currentVerticalCenterThreshold))
        {
            pendingVerticalGestures++;
        }
    }

    private void ResetGestureState()
    {
        pendingHorizontalGestures = 0;
        pendingVerticalGestures = 0;
        currentHorizontalGestureDelta = 0f;
        currentVerticalGestureDelta = 0f;
        currentVerticalPeakThreshold = 0f;
        currentVerticalCenterThreshold = 0f;
        horizontalOffCenterTime = 0f;
        verticalOffCenterTime = 0f;
        hasNeutralYawAngle = false;
        hasNeutralFocusY = false;
        hasSmoothedFocusY = false;
        smoothedFocusY = 0f;
        horizontalGesture.Reset();
        verticalGesture.Reset();
    }

    private float GetSmoothedFocusY(float rawValue)
    {
        if (!hasSmoothedFocusY)
        {
            smoothedFocusY = rawValue;
            hasSmoothedFocusY = true;
            return smoothedFocusY;
        }

        var blend = 1f - Mathf.Exp(-focusYSmoothingSpeed * Time.unscaledDeltaTime);
        smoothedFocusY = Mathf.Lerp(smoothedFocusY, rawValue, blend);
        return smoothedFocusY;
    }

    private float GetAxisOffset(
        float rawValue,
        ref float neutralValue,
        ref bool hasNeutralValue,
        ref float offCenterTime,
        float neutralUpdateRange,
        float forcedRecenterDistance,
        bool freezeNeutral)
    {
        if (!hasNeutralValue)
        {
            neutralValue = rawValue;
            hasNeutralValue = true;
            offCenterTime = 0f;
            return 0f;
        }

        var offset = rawValue - neutralValue;
        if (freezeNeutral)
        {
            offCenterTime = 0f;
            return offset;
        }

        if (Mathf.Abs(offset) <= neutralUpdateRange)
        {
            var blend = 1f - Mathf.Exp(-neutralAdaptSpeed * Time.unscaledDeltaTime);
            neutralValue = Mathf.Lerp(neutralValue, rawValue, blend);
            offCenterTime = 0f;
            return rawValue - neutralValue;
        }

        offCenterTime += Time.unscaledDeltaTime;
        if (Mathf.Abs(offset) >= forcedRecenterDistance && offCenterTime >= forcedNeutralRecenterDelay)
        {
            var blend = 1f - Mathf.Exp(-forcedNeutralRecenterSpeed * Time.unscaledDeltaTime);
            neutralValue = Mathf.Lerp(neutralValue, rawValue, blend);
            return rawValue - neutralValue;
        }

        return offset;
    }

    private float ResolveFocusThreshold(float focusAreaHeight, float fallbackThreshold, float areaRatio)
    {
        if (focusAreaHeight >= minimumFocusAreaHeightForScaling)
        {
            return Mathf.Max(1f, focusAreaHeight * areaRatio);
        }

        return fallbackThreshold;
    }

    private bool UpdateGestureAxis(float axisValue, GestureTracker tracker, float peakThreshold, float centerThreshold)
    {
        var now = Time.unscaledTime;
        var magnitude = Mathf.Abs(axisValue);

        if (magnitude <= centerThreshold)
        {
            if (!tracker.tracking && now - tracker.lastGestureTime >= gestureCooldown)
            {
                tracker.armed = true;
            }

            return false;
        }

        if (!tracker.tracking)
        {
            if (!tracker.armed || now - tracker.lastGestureTime < gestureCooldown || magnitude < peakThreshold)
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
        if (currentDirection != tracker.startDirection && magnitude >= peakThreshold)
        {
            tracker.tracking = false;
            tracker.lastGestureTime = now;
            return true;
        }

        return false;
    }

    private bool UpdateVerticalGestureAxis(float axisValue, GestureTracker tracker, float peakThreshold, float centerThreshold)
    {
        var now = Time.unscaledTime;
        var magnitude = Mathf.Abs(axisValue);

        if (!tracker.tracking)
        {
            if (magnitude <= centerThreshold)
            {
                if (now - tracker.lastGestureTime >= gestureCooldown)
                {
                    tracker.armed = true;
                }

                return false;
            }

            if (!tracker.armed || now - tracker.lastGestureTime < gestureCooldown || magnitude < peakThreshold)
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

        if (magnitude <= centerThreshold)
        {
            tracker.tracking = false;
            tracker.lastGestureTime = now;
            return true;
        }

        return false;
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
