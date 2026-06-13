using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class HybridBciPlatformBridge : MonoBehaviour
{
    [Header("Auto Connect")]
    [SerializeField] private bool autoConnectOnStart = true;
    [SerializeField] private bool preferCommandLineParameters = true;
    [SerializeField] private ConnectionMode fallbackConnectionMode = ConnectionMode.Tcp;
    [SerializeField] private string fallbackServerName = "HNNKPlatform";
    [SerializeField] private string fallbackHost = "127.0.0.1";
    [SerializeField] private int fallbackPort = 8000;
    [SerializeField] private float reconnectDelay = 3f;

    [Header("Debug HUD")]
    [SerializeField] private bool showStatusOverlay = true;
    [SerializeField] private Vector2 overlayAnchoredPosition = new Vector2(-35f, -30f);
    [SerializeField] private Vector2 overlaySize = new Vector2(260f, 110f);

    public static HybridBciPlatformBridge Instance { get; private set; }

    public bool IsConnected => client != null && client.IsConnected;
    public bool IsVisibleRequestedByPlatform { get; private set; } = true;
    public string LastAlgorithmName { get; private set; } = string.Empty;
    public int AttentionValue { get; private set; } = -1;
    public bool BlinkTriggered { get; private set; }
    public GyroscopeState LastGyroscope { get; private set; }
    public DeviceState LastDeviceState { get; private set; }
    public UserInfoState LastUserInfo { get; private set; }
    public string LastRawMessage { get; private set; } = string.Empty;
    public string ConnectionSummary { get; private set; } = "HybridBCI platform disconnected";

    private PlatformClient client;
    private Coroutine reconnectCoroutine;
    private Canvas overlayCanvas;
    private Text overlayText;
    private Font uiFont;
    private bool appQuitRequested;
    private bool connecting;
    private ConnectionSettings activeSettings;
    private bool blinkConsumed = true;

    public event Action<string, IDictionary<string, object>> MessageReceived;

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

    private void Start()
    {
        if (showStatusOverlay)
        {
            EnsureOverlay();
        }

        if (autoConnectOnStart)
        {
            TryAutoConnect();
        }
        else
        {
            RefreshOverlay();
        }
    }

    private void Update()
    {
        client?.Poll();
        RefreshOverlay();
    }

    private void OnApplicationQuit()
    {
        appQuitRequested = true;
        SendSetVisible(true);
        Disconnect();
    }

    public void TryAutoConnect()
    {
        if (connecting || IsConnected)
        {
            return;
        }

        activeSettings = ResolveConnectionSettings();
        Connect(activeSettings);
    }

    public void Connect(ConnectionSettings settings)
    {
        Disconnect();

        activeSettings = settings;
        client = settings.mode == ConnectionMode.LocalPipe
            ? (PlatformClient)new NamedPipePlatformClient()
            : new TcpPlatformClient();

        client.Connected += HandleConnected;
        client.Disconnected += HandleDisconnected;
        client.MessageReceived += HandleRawMessage;

        connecting = true;
        ConnectionSummary = settings.mode == ConnectionMode.LocalPipe
            ? $"Connecting local pipe {settings.serverName}"
            : $"Connecting {settings.host}:{settings.port}";

        var started = client.Connect(settings);
        if (!started)
        {
            connecting = false;
            ConnectionSummary = "HybridBCI platform start failed";
            ScheduleReconnect();
        }
    }

    public void Disconnect()
    {
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }

        if (client == null)
        {
            connecting = false;
            return;
        }

        client.Connected -= HandleConnected;
        client.Disconnected -= HandleDisconnected;
        client.MessageReceived -= HandleRawMessage;
        client.Dispose();
        client = null;
        connecting = false;
    }

    public bool ConsumeBlink()
    {
        if (blinkConsumed || !BlinkTriggered)
        {
            return false;
        }

        blinkConsumed = true;
        BlinkTriggered = false;
        return true;
    }

    public void SendEvent(int eventCode)
    {
        SendMessage(new Dictionary<string, object>
        {
            { "msg", "ipc_event" },
            { "event", eventCode }
        });
    }

    public void SendStartTest(string algorithmName, IDictionary<string, object> algorithmArgs = null)
    {
        var payload = new Dictionary<string, object>
        {
            { "msg", "ipc_algorithm_start_test" }
        };

        if (!string.IsNullOrWhiteSpace(algorithmName))
        {
            payload["algorithm_name"] = algorithmName;
        }

        payload["algorithm_args"] = algorithmArgs ?? new Dictionary<string, object>();
        SendMessage(payload);
    }

    public void SendStopTest()
    {
        SendMessage(new Dictionary<string, object>
        {
            { "msg", "ipc_algorithm_stop_test" }
        });
    }

    public void SendGyroscopeCalibration()
    {
        SendMessage(new Dictionary<string, object>
        {
            { "msg", "ipc_device_gyro_calibration" }
        });
    }

    public void SendSetVisible(bool visible)
    {
        SendMessage(new Dictionary<string, object>
        {
            { "msg", "ipc_set_visible" },
            { "visible", visible }
        });
    }

    public void SendWindowHandle()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        var hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
        {
            hwnd = GetForegroundWindow();
        }

        if (hwnd != IntPtr.Zero)
        {
            SendMessage(new Dictionary<string, object>
            {
                { "msg", "ipc_user_info" },
                { "window", hwnd.ToInt64() }
            });
        }
#endif
    }

    public void SendMessage(IDictionary<string, object> payload)
    {
        if (payload == null || client == null || !client.IsConnected)
        {
            return;
        }

        client.Send(SimpleJson.Serialize(payload));
    }

    private void HandleConnected()
    {
        connecting = false;
        ConnectionSummary = activeSettings.mode == ConnectionMode.LocalPipe
            ? $"Connected pipe {activeSettings.serverName}"
            : $"Connected {activeSettings.host}:{activeSettings.port}";
        SendSetVisible(true);
    }

    private void HandleDisconnected(string reason)
    {
        connecting = false;
        AttentionValue = -1;
        BlinkTriggered = false;
        blinkConsumed = true;
        ConnectionSummary = string.IsNullOrWhiteSpace(reason)
            ? "HybridBCI platform disconnected"
            : $"HybridBCI platform disconnected: {reason}";

        if (!appQuitRequested)
        {
            ScheduleReconnect();
        }
    }

    private void HandleRawMessage(string message)
    {
        LastRawMessage = message;

        if (!(SimpleJson.Deserialize(message) is IDictionary<string, object> payload))
        {
            Debug.LogWarning($"HybridBCI message parse failed: {message}", this);
            return;
        }

        var msg = GetString(payload, "msg");
        if (string.IsNullOrWhiteSpace(msg))
        {
            return;
        }

        switch (msg)
        {
            case "ipc_user_info":
                HandleUserInfo(payload);
                break;
            case "ipc_set_visible":
                HandleSetVisible(payload);
                break;
            case "ipc_exit":
                HandleExit();
                break;
            case "ipc_device_info":
                HandleDeviceInfo(payload);
                break;
            case "ipc_device_gyroscope":
                HandleStandaloneGyroscope(payload);
                break;
            case "ipc_algorithm_test":
                HandleAlgorithmResult(payload);
                break;
            case "ipc_event":
                HandleEventAck(payload);
                break;
            default:
                break;
        }

        MessageReceived?.Invoke(msg, payload);
    }

    private void HandleUserInfo(IDictionary<string, object> payload)
    {
        LastUserInfo = new UserInfoState
        {
            userName = GetString(payload, "user_name"),
            realName = GetString(payload, "real_name"),
            nickName = GetString(payload, "nick_name"),
            userToken = GetString(payload, "user_token"),
            groupName = GetString(payload, "group_name"),
            groupId = GetString(payload, "group_id"),
            subgroupName = GetString(payload, "subgroup_name"),
            subgroupId = GetString(payload, "subgroup_id"),
            researchNo = GetString(payload, "research_no"),
            layoutType = GetInt(payload, "layout_type", 0)
        };

        ConnectionSummary = $"User attached: {GetPreferredUserDisplayName()}";

        if (LastUserInfo.layoutType == 1)
        {
            SendWindowHandle();
        }
    }

    private void HandleSetVisible(IDictionary<string, object> payload)
    {
        IsVisibleRequestedByPlatform = GetBool(payload, "visible", true);
        if (showStatusOverlay && overlayCanvas != null)
        {
            overlayCanvas.enabled = IsVisibleRequestedByPlatform;
        }
    }

    private void HandleExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandleDeviceInfo(IDictionary<string, object> payload)
    {
        LastDeviceState = new DeviceState
        {
            deviceId = GetString(payload, "device_id"),
            deviceName = GetString(payload, "devie_name"),
            deviceType = GetInt(payload, "device_type", GetInt(payload, "device_type:", 0)),
            battery = GetInt(payload, "battery", -1),
            sampleRate = GetInt(payload, "sample_rate", 0),
            eegChannel = GetInt(payload, "eeg_channel", 0),
            otherChannel = GetInt(payload, "other_channel", 0),
            wear = GetBool(payload, "wear", false),
            connect = GetBool(payload, "connect", false)
        };
    }

    private void HandleStandaloneGyroscope(IDictionary<string, object> payload)
    {
        var gyro = LastGyroscope;
        gyro.gyroscopeX = GetFloat(payload, "gyroscopeX", gyro.gyroscopeX);
        gyro.gyroscopeY = GetFloat(payload, "gyroscopeY", gyro.gyroscopeY);
        gyro.gyroscopeZ = GetFloat(payload, "gyroscopeZ", gyro.gyroscopeZ);
        LastGyroscope = gyro;
    }

    private void HandleAlgorithmResult(IDictionary<string, object> payload)
    {
        LastAlgorithmName = GetString(payload, "algorithm_name");
        if (!(GetObject(payload, "result_args") is IDictionary<string, object> resultArgs))
        {
            return;
        }

        var data = GetObject(resultArgs, "data");
        switch (LastAlgorithmName)
        {
            case "attention":
                AttentionValue = ToInt(data, AttentionValue);
                break;
            case "blink":
                var blinkValue = ToStringSafe(data);
                BlinkTriggered = blinkValue == "1" || string.Equals(blinkValue, "true", StringComparison.OrdinalIgnoreCase);
                if (BlinkTriggered)
                {
                    blinkConsumed = false;
                }
                break;
            case "gyroscope":
                UpdateGyroscopeFromData(data);
                break;
            default:
                if (data is IDictionary<string, object> multiModalData)
                {
                    if (multiModalData.ContainsKey("attention"))
                    {
                        AttentionValue = ToInt(multiModalData["attention"], AttentionValue);
                    }

                    if (multiModalData.ContainsKey("blink"))
                    {
                        var blinkValueText = ToStringSafe(multiModalData["blink"]);
                        BlinkTriggered = blinkValueText == "1" || string.Equals(blinkValueText, "true", StringComparison.OrdinalIgnoreCase);
                        if (BlinkTriggered)
                        {
                            blinkConsumed = false;
                        }
                    }

                    if (multiModalData.ContainsKey("gyroscope") && multiModalData["gyroscope"] is IDictionary<string, object> gyroPayload)
                    {
                        UpdateGyroscopeFromData(gyroPayload);
                    }
                    else
                    {
                        UpdateGyroscopeFromData(multiModalData);
                    }
                }
                break;
        }
    }

    private void HandleEventAck(IDictionary<string, object> payload)
    {
        var eventCode = GetInt(payload, "event", -1);
        if (eventCode >= 0)
        {
            ConnectionSummary = $"Event marker synced: {eventCode}";
        }
    }

    private void UpdateGyroscopeFromData(object data)
    {
        if (!(data is IDictionary<string, object> gyroData))
        {
            return;
        }

        var gyro = LastGyroscope;
        gyro.focusX = GetFloat(gyroData, "focus_x", gyro.focusX);
        gyro.focusY = GetFloat(gyroData, "focus_y", gyro.focusY);
        gyro.focusAreaX = GetFloat(gyroData, "focus_area_x", gyro.focusAreaX);
        gyro.focusAreaY = GetFloat(gyroData, "focus_area_y", gyro.focusAreaY);
        gyro.focusAreaWidth = GetFloat(gyroData, "focus_area_width", gyro.focusAreaWidth);
        gyro.focusAreaHeight = GetFloat(gyroData, "focus_area_height", gyro.focusAreaHeight);
        gyro.gyroscopeX = GetFloat(gyroData, "gyroscope_x", gyro.gyroscopeX);
        gyro.gyroscopeY = GetFloat(gyroData, "gyroscope_y", gyro.gyroscopeY);
        gyro.gyroscopeZ = GetFloat(gyroData, "gyroscope_z", gyro.gyroscopeZ);
        LastGyroscope = gyro;
    }

    private void ScheduleReconnect()
    {
        if (reconnectCoroutine != null || appQuitRequested)
        {
            return;
        }

        reconnectCoroutine = StartCoroutine(ReconnectAfterDelay());
    }

    private IEnumerator ReconnectAfterDelay()
    {
        yield return new WaitForSeconds(reconnectDelay);
        reconnectCoroutine = null;

        if (!IsConnected && !appQuitRequested)
        {
            TryAutoConnect();
        }
    }

    private ConnectionSettings ResolveConnectionSettings()
    {
        var settings = new ConnectionSettings
        {
            mode = fallbackConnectionMode,
            serverName = fallbackServerName,
            host = fallbackHost,
            port = fallbackPort
        };

        if (!preferCommandLineParameters)
        {
            return settings;
        }

        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (arg.StartsWith("-server_name=", StringComparison.OrdinalIgnoreCase))
            {
                settings.mode = ConnectionMode.LocalPipe;
                settings.serverName = arg.Substring("-server_name=".Length).Trim().Trim('"');
            }
            else if (arg.StartsWith("-ip=", StringComparison.OrdinalIgnoreCase))
            {
                settings.mode = ConnectionMode.Tcp;
                settings.host = arg.Substring("-ip=".Length).Trim().Trim('"');
            }
            else if (arg.StartsWith("-host=", StringComparison.OrdinalIgnoreCase))
            {
                settings.mode = ConnectionMode.Tcp;
                settings.host = arg.Substring("-host=".Length).Trim().Trim('"');
            }
            else if (arg.StartsWith("-port=", StringComparison.OrdinalIgnoreCase))
            {
                var portText = arg.Substring("-port=".Length).Trim().Trim('"');
                if (int.TryParse(portText, out var parsedPort))
                {
                    settings.port = parsedPort;
                }
            }
        }

        return settings;
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null)
        {
            return;
        }

        uiFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "SimSun", "Arial Unicode MS" },
            28);

        var canvasObject = new GameObject("HybridBciPlatformCanvas");
        canvasObject.transform.SetParent(transform, false);
        overlayCanvas = canvasObject.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 130;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasObject.AddComponent<GraphicRaycaster>();

        var panelObject = new GameObject("StatusPanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        var rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = overlayAnchoredPosition;
        rect.sizeDelta = overlaySize;

        var image = panelObject.AddComponent<Image>();
        image.color = new Color(0.14f, 0.18f, 0.17f, 0.76f);

        var outline = panelObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        var textObject = new GameObject("StatusText");
        textObject.transform.SetParent(panelObject.transform, false);
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);

        overlayText = textObject.AddComponent<Text>();
        overlayText.font = uiFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        overlayText.fontSize = 34;
        overlayText.alignment = TextAnchor.MiddleCenter;
        overlayText.horizontalOverflow = HorizontalWrapMode.Wrap;
        overlayText.verticalOverflow = VerticalWrapMode.Overflow;
        overlayText.color = new Color(0.96f, 0.95f, 0.88f, 1f);
    }

    private void RefreshOverlay()
    {
        if (!showStatusOverlay || overlayText == null)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("注意力值");
        builder.Append(AttentionValue >= 0 ? AttentionValue.ToString() : "--");
        overlayText.text = builder.ToString();
    }

    private string GetPreferredUserDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(LastUserInfo.realName))
        {
            return LastUserInfo.realName;
        }

        if (!string.IsNullOrWhiteSpace(LastUserInfo.nickName))
        {
            return LastUserInfo.nickName;
        }

        return LastUserInfo.userName;
    }

    private static string GetString(IDictionary<string, object> source, string key)
    {
        return source != null && source.TryGetValue(key, out var value) ? ToStringSafe(value) : string.Empty;
    }

    private static int GetInt(IDictionary<string, object> source, string key, int fallback)
    {
        return source != null && source.TryGetValue(key, out var value) ? ToInt(value, fallback) : fallback;
    }

    private static float GetFloat(IDictionary<string, object> source, string key, float fallback)
    {
        return source != null && source.TryGetValue(key, out var value) ? ToFloat(value, fallback) : fallback;
    }

    private static bool GetBool(IDictionary<string, object> source, string key, bool fallback)
    {
        if (source == null || !source.TryGetValue(key, out var value))
        {
            return fallback;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        var text = ToStringSafe(value);
        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ToInt(value, fallback ? 1 : 0) != 0;
    }

    private static object GetObject(IDictionary<string, object> source, string key)
    {
        return source != null && source.TryGetValue(key, out var value) ? value : null;
    }

    private static string ToStringSafe(object value)
    {
        return value?.ToString() ?? string.Empty;
    }

    private static int ToInt(object value, int fallback)
    {
        if (value == null)
        {
            return fallback;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is long longValue)
        {
            return (int)longValue;
        }

        if (value is double doubleValue)
        {
            return Mathf.RoundToInt((float)doubleValue);
        }

        if (value is float floatValue)
        {
            return Mathf.RoundToInt(floatValue);
        }

        return int.TryParse(ToStringSafe(value), out var parsed) ? parsed : fallback;
    }

    private static float ToFloat(object value, float fallback)
    {
        if (value == null)
        {
            return fallback;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        if (value is long longValue)
        {
            return longValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return float.TryParse(ToStringSafe(value), out var parsed) ? parsed : fallback;
    }

    [Serializable]
    public struct GyroscopeState
    {
        public float focusX;
        public float focusY;
        public float focusAreaX;
        public float focusAreaY;
        public float focusAreaWidth;
        public float focusAreaHeight;
        public float gyroscopeX;
        public float gyroscopeY;
        public float gyroscopeZ;

        public bool HasValue =>
            !Mathf.Approximately(focusX, 0f) ||
            !Mathf.Approximately(focusY, 0f) ||
            !Mathf.Approximately(gyroscopeX, 0f) ||
            !Mathf.Approximately(gyroscopeY, 0f) ||
            !Mathf.Approximately(gyroscopeZ, 0f);
    }

    [Serializable]
    public struct DeviceState
    {
        public string deviceId;
        public string deviceName;
        public int deviceType;
        public int battery;
        public int sampleRate;
        public int eegChannel;
        public int otherChannel;
        public bool wear;
        public bool connect;
    }

    [Serializable]
    public struct UserInfoState
    {
        public string userName;
        public string realName;
        public string nickName;
        public string userToken;
        public string groupId;
        public string groupName;
        public string subgroupId;
        public string subgroupName;
        public string researchNo;
        public int layoutType;
    }

    public enum ConnectionMode
    {
        Tcp,
        LocalPipe
    }

    public struct ConnectionSettings
    {
        public ConnectionMode mode;
        public string serverName;
        public string host;
        public int port;
    }

    private abstract class PlatformClient : IDisposable
    {
        protected readonly Queue<Action> mainThreadEvents = new Queue<Action>();

        public bool IsConnected { get; protected set; }
        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<string> MessageReceived;

        public abstract bool Connect(ConnectionSettings settings);
        public abstract void Send(string payload);
        public abstract void Dispose();

        public void Poll()
        {
            while (true)
            {
                Action callback = null;
                lock (mainThreadEvents)
                {
                    if (mainThreadEvents.Count > 0)
                    {
                        callback = mainThreadEvents.Dequeue();
                    }
                }

                if (callback == null)
                {
                    break;
                }

                callback.Invoke();
            }
        }

        protected void RaiseConnected()
        {
            lock (mainThreadEvents)
            {
                mainThreadEvents.Enqueue(() =>
                {
                    IsConnected = true;
                    Connected?.Invoke();
                });
            }
        }

        protected void RaiseDisconnected(string reason)
        {
            lock (mainThreadEvents)
            {
                mainThreadEvents.Enqueue(() =>
                {
                    IsConnected = false;
                    Disconnected?.Invoke(reason);
                });
            }
        }

        protected void RaiseMessageReceived(string payload)
        {
            lock (mainThreadEvents)
            {
                mainThreadEvents.Enqueue(() => MessageReceived?.Invoke(payload));
            }
        }

        protected static byte[] PackPayload(string payload)
        {
            var body = Encoding.UTF8.GetBytes(payload);
            var packet = new byte[body.Length + 4];
            var length = body.Length;
            packet[0] = (byte)((length >> 24) & 0xFF);
            packet[1] = (byte)((length >> 16) & 0xFF);
            packet[2] = (byte)((length >> 8) & 0xFF);
            packet[3] = (byte)(length & 0xFF);
            Buffer.BlockCopy(body, 0, packet, 4, body.Length);
            return packet;
        }

        protected static bool TryReadFrame(List<byte> buffer, out string payload)
        {
            payload = null;
            if (buffer.Count < 4)
            {
                return false;
            }

            var length = (buffer[0] << 24) |
                         (buffer[1] << 16) |
                         (buffer[2] << 8) |
                         buffer[3];

            if (length < 0 || buffer.Count < length + 4)
            {
                return false;
            }

            payload = Encoding.UTF8.GetString(buffer.GetRange(4, length).ToArray());
            buffer.RemoveRange(0, length + 4);
            return true;
        }
    }

    private sealed class TcpPlatformClient : PlatformClient
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private byte[] readBuffer;
        private readonly List<byte> recvBuffer = new List<byte>(8192);

        public override bool Connect(ConnectionSettings settings)
        {
            try
            {
                tcpClient = new TcpClient();
                readBuffer = new byte[4096];
                tcpClient.BeginConnect(settings.host, settings.port, ConnectCallback, null);
                return true;
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
                return false;
            }
        }

        public override void Send(string payload)
        {
            if (!IsConnected || stream == null || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            try
            {
                var packet = PackPayload(payload);
                stream.Write(packet, 0, packet.Length);
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
            }
        }

        public override void Dispose()
        {
            try
            {
                stream?.Close();
                tcpClient?.Close();
            }
            catch
            {
            }

            stream = null;
            tcpClient = null;
            IsConnected = false;
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                tcpClient.EndConnect(ar);
                stream = tcpClient.GetStream();
                RaiseConnected();
                stream.BeginRead(readBuffer, 0, readBuffer.Length, ReadCallback, null);
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            try
            {
                var bytesRead = stream.EndRead(ar);
                if (bytesRead <= 0)
                {
                    RaiseDisconnected("Remote endpoint closed");
                    return;
                }

                for (var i = 0; i < bytesRead; i++)
                {
                    recvBuffer.Add(readBuffer[i]);
                }

                while (TryReadFrame(recvBuffer, out var message))
                {
                    RaiseMessageReceived(message);
                }

                stream.BeginRead(readBuffer, 0, readBuffer.Length, ReadCallback, null);
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
            }
        }
    }

    private sealed class NamedPipePlatformClient : PlatformClient
    {
        private NamedPipeClientStream pipeClient;
        private byte[] readBuffer;
        private readonly List<byte> recvBuffer = new List<byte>(8192);

        public override bool Connect(ConnectionSettings settings)
        {
            try
            {
                pipeClient = new NamedPipeClientStream(".", settings.serverName, PipeDirection.InOut, PipeOptions.Asynchronous);
                readBuffer = new byte[4096];
                pipeClient.Connect(2500);
                RaiseConnected();
                pipeClient.BeginRead(readBuffer, 0, readBuffer.Length, ReadCallback, null);
                return true;
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
                return false;
            }
        }

        public override void Send(string payload)
        {
            if (!IsConnected || pipeClient == null || !pipeClient.IsConnected || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            try
            {
                var packet = PackPayload(payload);
                pipeClient.Write(packet, 0, packet.Length);
                pipeClient.Flush();
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
            }
        }

        public override void Dispose()
        {
            try
            {
                pipeClient?.Close();
                pipeClient?.Dispose();
            }
            catch
            {
            }

            pipeClient = null;
            IsConnected = false;
        }

        private void ReadCallback(IAsyncResult ar)
        {
            try
            {
                var bytesRead = pipeClient.EndRead(ar);
                if (bytesRead <= 0)
                {
                    RaiseDisconnected("Pipe closed");
                    return;
                }

                for (var i = 0; i < bytesRead; i++)
                {
                    recvBuffer.Add(readBuffer[i]);
                }

                while (TryReadFrame(recvBuffer, out var message))
                {
                    RaiseMessageReceived(message);
                }

                pipeClient.BeginRead(readBuffer, 0, readBuffer.Length, ReadCallback, null);
            }
            catch (Exception ex)
            {
                RaiseDisconnected(ex.Message);
            }
        }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
#endif
}
