// cloudfunctions/submitGameLog/index.js
// 后端接口：同时支持 小程序调用 + Unity HTTP 调用
// 将游戏训练日志写入 main_game_logs 云数据库

const cloud = require('wx-server-sdk');

cloud.init({
  env: cloud.DYNAMIC_CURRENT_ENV
});

const db = cloud.database();

// ==================== 工具函数 ====================

function clamp(val, min, max) {
  return Math.max(min, Math.min(max, val));
}

/**
 * 校验并补全游戏提交的训练日志
 */
/** 保留 n 位小数 */
function round(val, n) {
  const p = Math.pow(10, n);
  return Math.round(val * p) / p;
}

function sanitize(data) {
  const now = Date.now();

  return {
    timestamp:       Number(data.timestamp) || now,
    game_module:     ['camp', 'room', 'kitchen'].includes(data.game_module)
                       ? data.game_module
                       : 'camp',
    durationMinutes: round(Number(data.durationMinutes) || 0, 1),
    recipeName:      String(data.recipeName || ''),

    avgAttention:    clamp(Math.round(Number(data.avgAttention) || 0), 0, 100),
    peakFocus:       clamp(Math.round(Number(data.peakFocus || data.eegMetrics?.peakFocus) || 0), 0, 100),
    distractCount:   Math.max(0, Math.round(Number(data.distractCount) || 0)),

    childId:         String(data.childId || ''),
    score:           Math.round(Number(data.score) || 0),

    dimensionMetrics: {
      sustained:  clamp(Math.round(Number(data.dimensionMetrics?.sustained)  || 0), 0, 100),
      selective:  clamp(Math.round(Number(data.dimensionMetrics?.selective)  || 0), 0, 100),
      executive:  clamp(Math.round(Number(data.dimensionMetrics?.executive)  || 0), 0, 100),
      impulse:    clamp(Math.round(Number(data.dimensionMetrics?.impulse)    || 0), 0, 100),
    },

    eegMetrics: {
      meanFocus:           round(Number(data.eegMetrics?.meanFocus)           || 0, 1),
      peakFocus:           round(Number(data.eegMetrics?.peakFocus)           || 0, 1),
      valFocus:            round(Number(data.eegMetrics?.valFocus)            || 0, 1),
      durationRatio60:     round(clamp(Number(data.eegMetrics?.durationRatio60)  || 0, 0, 100), 1),
      durationRatio80:     round(clamp(Number(data.eegMetrics?.durationRatio80)  || 0, 0, 100), 1),
      avgDistractDuration: round(Number(data.eegMetrics?.avgDistractDuration) || 0, 1),
      focusCv:             round(Number(data.eegMetrics?.focusCv)             || 0, 2),
    },

    clientVersion: String(data.clientVersion || ''),
    deviceInfo:    String(data.deviceInfo || ''),
  };
}

/**
 * 构建 HTTP 响应（供 Unity 调用时返回标准 JSON）
 */
function httpResponse(body, statusCode = 200) {
  return {
    statusCode,
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Headers': 'Content-Type, Authorization',
    },
    body: JSON.stringify(body),
  };
}

// ==================== 云函数入口 ====================
exports.main = async (event, context) => {

  // ========== 判断调用方式 ==========
  const isHttp = !!event.httpMethod;  // HTTP 触发时 event 会包含 httpMethod 字段

  // ========== 解析请求体 ==========
  let data;
  let userOpenid = '';

  if (isHttp) {
    // --- HTTP 方式（Unity 调用）---
    console.log('📦 [DEBUG] 原始请求体 event.body:', JSON.stringify(event.body));
    try {
      data = JSON.parse(event.body || '{}');
    } catch (e) {
      console.error('❌ JSON 解析失败，原始内容:', event.body);
      return httpResponse({ code: -1, msg: '请求体 JSON 解析失败', data: null }, 400);
    }

    // 兼容包装格式：{ envId, collectionName, payload: { ...实际数据 } }
    if (data.payload && typeof data.payload === 'object') {
      console.log('📦 [DEBUG] 检测到 payload 包装格式，自动解包');
      data = data.payload;
    }

    // Unity 端需要传 openid 或 userId 来标识用户
    userOpenid = data.openid || data.userId || 'unity_user';
    console.log(`🌐 HTTP 请求 | user=${userOpenid} | game_module=${data.game_module} | avgAttention=${data.avgAttention} | peakFocus=${data.peakFocus} | durationMinutes=${data.durationMinutes} | distractCount=${data.distractCount}`);
    console.log('📦 [DEBUG] 解析后的 data 对象:', JSON.stringify(data));
  } else {
    // --- 云函数直调方式（小程序内调用）---
    data = event;

    // 兼容小程序端也可能传入 payload 包装
    if (data.payload && typeof data.payload === 'object') {
      console.log('📦 [DEBUG] 小程序调用检测到 payload 包装格式，自动解包');
      data = data.payload;
    }

    const wxContext = cloud.getWXContext();
    userOpenid = wxContext.OPENID;
    console.log(`📱 小程序调用 | openid=${userOpenid}`);
  }

  if (!userOpenid) {
    const errBody = { code: -2, msg: '无法获取用户身份，请传入 openid 字段', data: null };
    return isHttp ? httpResponse(errBody, 401) : errBody;
  }

  // ========== 写入数据库 ==========
  try {
    const record = sanitize(data);
    record._openid = userOpenid;
    console.log('📦 [DEBUG] sanitize 后的 record:', JSON.stringify(record));

    const result = await db.collection('main_game_logs').add({ data: record });

    console.log(`✅ 写入成功 _id=${result._id}`);

    const resBody = {
      code: 0,
      msg: 'ok',
      data: { _id: result._id, timestamp: record.timestamp }
    };

    return isHttp ? httpResponse(resBody) : resBody;

  } catch (err) {
    console.error('❌ 写入失败:', err);
    const errBody = { code: -1, msg: err.message || '写入失败', data: null };
    return isHttp ? httpResponse(errBody, 500) : errBody;
  }
};
