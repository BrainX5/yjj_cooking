using UnityEngine;

[DefaultExecutionOrder(-100)]
public class HybridBciGameplayInput : MonoBehaviour
{
    [Header("Attention")]
    [SerializeField] private int attentionActivateThreshold = 60;
    [SerializeField] private int attentionDeactivateThreshold = 55;
    [SerializeField] private float attentionSmoothingTime = 0.4f;

    [Header("Gyroscope Gestures")]
    [SerializeField] private float gesturePeakThreshold = 12f;
    [SerializeField] private float gestureCenterThreshold = 4.5f;
    [SerializeField] private float gestureSequenceWindow = 0.8f;
    [SerializeField] private float gestureCooldown = 0.6f;

    public static HybridBciGameplayInput Instance { get; private set; }

    public bool HasLiveConnection => bridge != null && bridge.IsConnected;
    public bool IsAttentionActive => HasLiveConnection && attentionActive;
    public float SmoothedAttention => smoothedAttention;

    private HybridBciPlatformBridge bridge;
    private float smoothedAttention;
    private float attentionVelocity;
    private bool attentionActive;
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
