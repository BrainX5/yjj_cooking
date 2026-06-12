# 森厨小当家 - 后端接口文档

## 整体架构

```
Unity 游戏 ──HTTP POST──▶ submitGameLog 云函数 ──写入──▶ main_game_logs 云数据库
                                                              │
         小程序页面 (index/report) ◀── 读取 ◀─────────────────┘
```

---

## 1. 部署云函数（一次性操作）

### 步骤一：上传云函数

1. 微信开发者工具中打开项目
2. 左侧文件树 → 右键 `cloudfunctions/submitGameLog` 文件夹
3. 点击 **「上传并部署：云端安装依赖」**

### 步骤二：开启 HTTP 触发（Unity 必需）

1. 微信开发者工具 → 顶部 **「云开发」** 标签
2. 左侧菜单 → **「云函数」**
3. 找到 `submitGameLog` → 点击函数名进入详情
4. 切换到 **「HTTP 触发」** 标签页
5. 点击 **「开启」** → 复制生成的 URL

> ⚠️ HTTP 触发 URL 格式类似：
> `https://cloud1-d9gz2tmfub107d0ff.service.tcloudbase.com/submitGameLog`

### 步骤三：配置安全域名（如需）

如果 Unity 打包为 WebGL，需要在云开发控制台 → 「设置」→ 「安全配置」中添加域名白名单。

---

## 2. Unity 端集成

### 文件说明

| 文件 | 作用 |
|------|------|
| [`unity-client/GameLogSender.cs`](../unity-client/GameLogSender.cs) | 核心 HTTP 发送脚本，挂到 GameObject 上 |
| [`unity-client/ExampleUsage.cs`](../unity-client/ExampleUsage.cs) | 调用示例，展示最小参数和完整参数两种方式 |

### 快速使用

```csharp
// 1. 获取 GameLogSender 引用（Inspector 拖拽或 GetComponent）
[SerializeField] private GameLogSender logSender;

// 2. 游戏结束时提交
public void OnLevelComplete()
{
    var log = new GameLogData
    {
        game_module     = "camp",   // 必填: camp / room / kitchen
        avgAttention    = 75,       // 平均专注力 0-100
        durationMinutes = 15,       // 训练时长
        distractCount   = 3         // 走神次数
    };
    logSender.SubmitLog(log);
}
```

### 完整参数

详见 [`ExampleUsage.cs`](../unity-client/ExampleUsage.cs) 中的 `OnGameFinished()` 方法，支持所有 ADHD 四维评估 + EEG 脑电指标。

### openid 怎么获取？

云函数需要 `openid` 来标识是哪个用户。有两种方式：

**方式 A：小程序传 openid 给 Unity**（推荐）

小程序的 `app.ts` 在 `onLaunch()` 中已调用 `wx.login()`。在小程序中获取 openid 后，通过 Unity 与小程序的通信桥传递给 Unity：

```javascript
// 在小程序端获取 openid
wx.cloud.callFunction({ name: 'login' })  // 需要额外部署一个 login 云函数
  .then(res => {
    // 将 res.result.openid 传给 Unity
  });
```

**方式 B：Unity 端直接传 userId**

如果游戏有独立的登录系统，可以把任意唯一 ID（设备ID、账号ID等）直接传给云函数：

```csharp
logSender.userOpenid = "device_xxxxx";  // 任意唯一标识
```

---

## 3. HTTP 接口详细说明

### 请求

```
POST https://cloud1-d9gz2tmfub107d0ff.service.tcloudbase.com/submitGameLog
Content-Type: application/json
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `openid` | string | 是 | 用户唯一标识 |
| `game_module` | string | 是 | `"camp"` / `"room"` / `"kitchen"` |
| `timestamp` | number | 否 | 时间戳(ms) |
| `durationMinutes` | number | 否 | 训练时长(分钟) |
| `avgAttention` | number | 否 | 平均专注力 0-100 |
| `peakFocus` | number | 否 | 专注力峰值 0-100 |
| `distractCount` | number | 否 | 走神次数 |
| `recipeName` | string | 否 | 菜名 |
| `dimensionMetrics` | object | 否 | ADHD 四维: `{sustained, selective, executive, impulse}` |
| `eegMetrics` | object | 否 | EEG 指标: `{meanFocus, peakFocus, valFocus, durationRatio60, durationRatio80, avgDistractDuration, focusCv}` |

### 响应

```json
// 成功
{ "code": 0, "msg": "ok", "data": { "_id": "xxx", "timestamp": 1715000000000 } }

// 失败
{ "code": -1, "msg": "错误描述", "data": null }
```

---

## 4. 小程序端调用（内部调用，不走 HTTP）

如果 Unity 以微信小游戏方式运行，可以直接调云函数：

```javascript
wx.cloud.callFunction({
  name: 'submitGameLog',
  data: {
    game_module: 'camp',
    avgAttention: 75,
    ...
  }
});
```

这种情况下不需要传 `openid`，云函数会自动从微信上下文获取。
