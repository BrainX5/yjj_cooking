// 康复报告页 — 从 main_game_logs 云数据库拉取真实数据画图
const echarts = require('../../components/ec-canvas/echarts');

const app = getApp<IAppOption>();
const db = wx.cloud.database();
const _ = db.command;  // 云数据库查询指令

// ========== 工具函数 ==========

/** 时间戳 → "M.D" 短日期 */
function toShortDate(ts: number): string {
  const d = new Date(ts);
  return `${d.getMonth() + 1}.${d.getDate()}`;
}

/** 时间戳 → "M月D日" */
function toDateText(ts: number): string {
  const d = new Date(ts);
  return `${d.getMonth() + 1}月${d.getDate()}日`;
}

/** 计算本周一 00:00:00 的时间戳 */
function getWeekStart(): number {
  const now = new Date();
  const day = now.getDay() || 7; // 周日=7
  const monday = new Date(now.getFullYear(), now.getMonth(), now.getDate() - day + 1);
  return monday.getTime();
}

// ========== 默认数据（云端无数据时的兜底） ==========
const DEFAULT_DATA = {
  parentChartData: { xAxisDates: [] as string[], focusScores: [] as number[], distractCounts: [] as number[] },
  doctorRadarData: [0, 0, 0, 0] as number[],
  childInfo: { name: '小森', age: 7 },
  rehabPlan: { currentStage: '等待首次训练数据…' },
  parentReport: {
    trainLevel: 'good',
    trainLevelText: '等待数据同步',
    progressText: '完成第一次训练后，这里会出现你的专注力战报 🚂',
    avgAttention: 0,
    parentTips: '孩子刚开始训练，建议先从简单的关卡入手，保持每次 10-15 分钟即可。'
  },
  weeklyMetrics: {
    totalDuration: 0, avgFocusScore: 0, peakFocusScore: 0, avgDistractCount: 0,
    stabilityRatio: 0, focusTrend: 0, focusTrendText: '--', distractTrendText: '--',
    peakLevelName: '--',
    levelProgress: { camp: 0, room: 0, kitchen: 0 }
  },
  childHighlights: [] as { emoji: string; text: string }[],
  dimensionMetrics: { sustained: 0, selective: 0, executive: 0, impulse: 0 },
  doctorComment: { updateTime: '--', content: '暂无医嘱', doctorName: '--' },
  homeTasks: [
    { id: 1, title: '安静阅读 15 分钟', desc: '选择一本感兴趣的绘本，在安静的环境中独立阅读' },
    { id: 2, title: '舒尔特方格训练', desc: '每天完成 3 组 5×5 舒尔特方格，目标 ≤ 45 秒/组' },
    { id: 3, title: '听觉注意力游戏', desc: '家长朗读一串数字，孩子听到"3"时拍手' }
  ],
  doctorReport: {
    eegMetrics: { meanFocus: 0, peakFocus: 0, valFocus: 0, durationRatio60: 0, durationRatio80: 0, avgDistractDuration: 0, focusCv: 0 },
    distractCount: 0
  },
  inputComment: ''
};

Page({
  data: {
    loading: true,
    userRole: 'parent' as 'parent' | 'doctor',
    ...JSON.parse(JSON.stringify(DEFAULT_DATA)),  // 深拷贝默认值

    // ec-canvas 需要 ec 属性才能工作；lazyLoad: false 自动初始化
    // 数据在 loading 变为 false 之前已写入 this.data，组件挂载时直接可用
    ecParent: { lazyLoad: false } as any,
    ecDoctor: { lazyLoad: false } as any
  },

  // ==================== 生命周期 ====================

  onLoad() {
    console.log("📊 康复报告页加载，查询 main_game_logs...");
    this.fetchReportData();
  },

  onPullDownRefresh() {
    this.fetchReportData();
  },

  // ==================== 数据拉取 ====================

  fetchReportData() {
    const that = this;
    const weekStart = getWeekStart();

    console.log(`📅 本周起始时间戳: ${weekStart} (${toDateText(weekStart)})`);

    // 从 main_game_logs 拉取本周所有训练记录，按时间降序
    db.collection('main_game_logs')
      .where({
        _openid: '{openid}',
        timestamp: _.gte(weekStart)  // 只拉本周数据
      })
      .orderBy('timestamp', 'desc')
      .limit(30)
      .get({
        success: (res: any) => {
          console.log(`✅ 拉取到 ${res.data.length} 条训练日志`);
          if (res.data.length > 0) {
            that.applyGameLogs(res.data);
          } else {
            console.log("⚠️ 本周暂无训练记录");
            that.finishLoading();
          }
        },
        fail: (err: any) => {
          console.error("❌ main_game_logs 查询失败:", err);
          wx.showToast({ title: '网络异常，展示本地缓存', icon: 'none' });
          that.finishLoading();
        }
      });
  },

  // ==================== 数据映射核心 ====================

  applyGameLogs(logs: any[]) {
    // logs 已按 timestamp desc 排序，最新的在前

    // ---- 1. 家长端图表：日期轴 + 专注力/走神 ----
    // 图表要按时间正序显示，所以先反转
    const reversed = [...logs].reverse();
    const xAxisDates = reversed.map((l: any) => toShortDate(l.timestamp));
    const focusScores = reversed.map((l: any) => l.avgAttention || 0);
    const distractCounts = reversed.map((l: any) => l.distractCount || 0);

    // ---- 2. 雷达图 & 维度柱状图：取最新一条 ----
    const latest = logs[0];
    const dim = latest.dimensionMetrics || {};
    const radarData = [
      dim.sustained || 0,
      dim.selective || 0,
      dim.executive || 0,
      dim.impulse || 0
    ];

    // ---- 3. 医生端 EEG 指标：取最新一条 ----
    const eeg = latest.eegMetrics || {};

    // ---- 4. 本周聚合指标 ----
    const totalDuration = logs.reduce((sum: number, l: any) => sum + (l.durationMinutes || 0), 0);
    const allAttention = logs.map((l: any) => l.avgAttention || 0);
    const allDistract = logs.map((l: any) => l.distractCount || 0);
    const avgFocus = Math.round(allAttention.reduce((a: number, b: number) => a + b, 0) / allAttention.length);
    const peakFocus = Math.max(...allAttention);
    const avgDistract = +(allDistract.reduce((a: number, b: number) => a + b, 0) / allDistract.length).toFixed(1);
    // 稳定率：avgAttention >= 60 的占比
    const stableCount = allAttention.filter((v: number) => v >= 60).length;
    const stability = Math.round((stableCount / allAttention.length) * 100);

    // ---- 5. 关卡进度：按 game_module 聚合 ----
    const modules: Record<string, number[]> = { camp: [], room: [], kitchen: [] };
    logs.forEach((l: any) => {
      const m = l.game_module;
      if (modules[m]) modules[m].push(l.avgAttention || 0);
    });
    const modAvg = (arr: number[]) => arr.length ? Math.round(arr.reduce((a, b) => a + b, 0) / arr.length) : 0;
    const levelProgress = {
      camp: modAvg(modules['camp']),
      room: modAvg(modules['room']),
      kitchen: modAvg(modules['kitchen'])
    };

    // ---- 6. 今日战报 ----
    const todayStart = new Date(new Date().toDateString()).getTime();
    const todayLogs = logs.filter((l: any) => l.timestamp >= todayStart);
    const todayAvg = todayLogs.length
      ? Math.round(todayLogs.reduce((s: number, l: any) => s + l.avgAttention, 0) / todayLogs.length)
      : avgFocus;

    const trainLevel = todayAvg >= 80 ? 'excellent' : todayAvg >= 60 ? 'good' : 'attention';
    const trainLevelText = todayAvg >= 80 ? '专注力优秀 ✨' : todayAvg >= 60 ? '专注力良好 👍' : '需要加油 💪';
    const progressText = todayLogs.length
      ? `今日完成 ${todayLogs.length} 局训练，平均专注力 ${todayAvg} 分。小火车前进了 ${todayAvg}% 的路程！`
      : '今天还没有训练记录哦，快去森厨世界冒险吧 🌲';

    // ---- 7. 成长亮点（基于数据自动生成） ----
    const highlights: { emoji: string; text: string }[] = [];
    const impulse = dim.impulse || 0;
    if (impulse >= 75) {
      highlights.push({ emoji: '🌟', text: `冲动控制能力得分 ${impulse} 分，表现优秀！能很好地抑制分心行为` });
    }
    if (peakFocus >= 85) {
      highlights.push({ emoji: '🎯', text: `本周专注力峰值达到 ${peakFocus} 分，创下新高！` });
    }
    if (stability >= 65) {
      highlights.push({ emoji: '💎', text: `专注稳定率 ${stability}%，说明大部分时间都能保持良好专注` });
    }
    const exec = dim.executive || 0;
    if (exec < 50) {
      highlights.push({ emoji: '📋', text: `执行功能得分 ${exec} 分，建议在失重备菜室多加练习来提升` });
    }
    if (highlights.length === 0) {
      highlights.push({ emoji: '🚀', text: '训练数据积累中，坚持训练后这里会出现属于你的成长亮点！' });
    }

    // ---- 8. 一键写入，组件挂载后会自动 init ----
    this.setData({
      // 图表
      parentChartData: { xAxisDates, focusScores, distractCounts },
      doctorRadarData: radarData,

      // 维度
      dimensionMetrics: {
        sustained: dim.sustained || 0,
        selective: dim.selective || 0,
        executive: dim.executive || 0,
        impulse: dim.impulse || 0
      },

      // 今日战报
      parentReport: {
        trainLevel,
        trainLevelText,
        progressText,
        avgAttention: todayAvg,
        parentTips: todayAvg < 50
          ? '孩子今天专注力偏低，建议检查训练环境是否安静，或者适当降低关卡难度。'
          : todayAvg < 75
            ? '孩子今天状态不错！建议保持每天 20 分钟的训练节奏，减少干扰。'
            : '孩子今天状态很棒！可以适当挑战更高难度的关卡，促进能力进一步提升。'
      },

      // 本周指标
      weeklyMetrics: {
        totalDuration,
        avgFocusScore: avgFocus,
        peakFocusScore: peakFocus,
        avgDistractCount: avgDistract,
        stabilityRatio: stability,
        focusTrend: 0,
        focusTrendText: '本周',
        distractTrendText: `均 ${avgDistract} 次/局`,
        peakLevelName: latest.recipeName || '--',
        levelProgress
      },

      // 医生端
      doctorReport: {
        eegMetrics: {
          meanFocus: eeg.meanFocus || 0,
          peakFocus: eeg.peakFocus || 0,
          valFocus: eeg.valFocus || 0,
          durationRatio60: eeg.durationRatio60 || 0,
          durationRatio80: eeg.durationRatio80 || 0,
          avgDistractDuration: eeg.avgDistractDuration || 0,
          focusCv: eeg.focusCv || 0
        },
        distractCount: avgDistract
      },

      // 当前阶段
      rehabPlan: {
        currentStage: latest.recipeName || '训练中'
      },

      // 亮点
      childHighlights: highlights,

      // 关闭 loading → ec-canvas 挂载时 auto-init，此时图表数据已在 this.data 中
      loading: false
    });

    wx.stopPullDownRefresh();
    console.log("📊 报告数据渲染完成，图表将自动初始化");
  },

  /**
   * 兜底：无数据或拉取失败时直接关闭 loading
   * ec-canvas 挂载后自动 init，读取到空数据会显示占位提示
   */
  finishLoading() {
    this.setData({ loading: false });
    wx.stopPullDownRefresh();
  },

  // ==================== ECharts 图表挂载 ====================

  /** 家长端：折线 + 柱状混合图 */
  onInitParentChart(e: any) {
    const { canvas, width, height, dpr } = e.detail;
    if (!canvas) return;

    const chart = echarts.init(canvas, null, { width, height, devicePixelRatio: dpr });
    canvas.setChart(chart);

    const chartData = this.data.parentChartData;

    // 无数据时显示占位提示
    if (!chartData.xAxisDates.length) {
      chart.setOption({
        title: { text: '暂无训练数据', subtext: '完成一局游戏后自动生成', left: 'center', top: 'center' }
      });
      return chart;
    }

    const option = {
      color: ['#3B82F6', '#EF4444'],
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      grid: { top: '15%', left: '8%', right: '8%', bottom: '12%', containLabel: true },
      legend: { data: ['专注力均值', '单局走神次数'], top: 'top', textStyle: { color: '#666' } },
      xAxis: [{
        type: 'category',
        data: chartData.xAxisDates,
        axisTick: { alignWithLabel: true },
        axisLine: { lineStyle: { color: '#E5E7EB' } },
        axisLabel: { color: '#4B5563' }
      }],
      yAxis: [
        { type: 'value', name: '专注度(分)', min: 0, max: 100, axisLabel: { formatter: '{value}' }, splitLine: { lineStyle: { type: 'dashed', color: '#F3F4F6' } } },
        { type: 'value', name: '走神(次)', min: 0, max: 10, interval: 2, axisLabel: { formatter: '{value} 次' }, splitLine: { show: false } }
      ],
      series: [
        { name: '专注力均值', type: 'line', smooth: true, yAxisIndex: 0, data: chartData.focusScores, lineStyle: { width: 3 }, markPoint: { data: [{ type: 'max', name: '峰值日' }] } },
        { name: '单局走神次数', type: 'bar', barWidth: '35%', yAxisIndex: 1, data: chartData.distractCounts, itemStyle: { borderRadius: [4, 4, 0, 0] } }
      ]
    };

    chart.setOption(option);
    return chart;
  },

  /** 医生端：ADHD 四维雷达图 */
  onInitDoctorChart(e: any) {
    const { canvas, width, height, dpr } = e.detail;
    if (!canvas) return;

    const chart = echarts.init(canvas, null, { width, height, devicePixelRatio: dpr });
    canvas.setChart(chart);

    const radarData = this.data.doctorRadarData;

    // 全是 0 表示无数据
    if (radarData.every((v: number) => v === 0)) {
      chart.setOption({
        title: { text: '暂无数据', subtext: '完成训练后自动生成', left: 'center', top: 'center' }
      });
      return chart;
    }

    const option = {
      title: { text: '专注力四维评估', left: 'center', textStyle: { fontSize: 13, color: '#1F2937' } },
      tooltip: { trigger: 'item' },
      radar: {
        indicator: [
          { name: '持续注意', max: 100 },
          { name: '选择注意', max: 100 },
          { name: '执行控制', max: 100 },
          { name: '冲动抑制', max: 100 }
        ],
        center: ['50%', '55%'],
        radius: '65%',
        shape: 'circle',
        splitNumber: 4,
        axisName: { color: '#374151', fontWeight: 'bold' },
        splitLine: { lineStyle: { color: ['#E5E7EB'].reverse() } },
        splitArea: { show: true, areaStyle: { color: ['#F9FAFB', '#F3F4F6'] } },
        polygon: { lineStyle: { color: '#9CA3AF' } }
      },
      series: [{
        name: '专注力评估',
        type: 'radar',
        data: [{
          value: radarData,
          name: '当前评估得分',
          itemStyle: { color: '#8B5CF6' },
          areaStyle: { opacity: 0.25 },
          lineStyle: { width: 2 }
        }]
      }]
    };

    chart.setOption(option);
    return chart;
  },

  // ==================== 医生端交互 ====================

  toggleRole() {
    const newRole = this.data.userRole === 'parent' ? 'doctor' : 'parent';
    // ec-canvas 有 lazyLoad: false，切换后新组件 mount 时会自动 init
    this.setData({ userRole: newRole });
  },

  onCommentInput(e: any) {
    this.setData({ inputComment: e.detail.value });
  },

  useTemplate(e: any) {
    this.setData({ inputComment: e.currentTarget.dataset.text });
  },

  submitDoctorComment() {
    const comment = this.data.inputComment.trim();
    if (!comment) {
      wx.showToast({ title: '请输入评语', icon: 'none' });
      return;
    }

    db.collection('doctor_comments').add({
      data: {
        content: comment,
        updateTime: new Date().toLocaleString(),
        doctorName: '张明华'
      },
      success: () => {
        wx.showToast({ title: '医嘱已下发至家长端', icon: 'success' });
        this.setData({
          inputComment: '',
          'doctorComment.content': comment,
          'doctorComment.updateTime': new Date().toLocaleString()
        });
      },
      fail: (err: any) => {
        console.error('医嘱提交失败:', err);
        wx.showToast({ title: '提交失败，请重试', icon: 'error' });
      }
    });
  }
})
