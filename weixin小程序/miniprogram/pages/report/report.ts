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
/** 从日志数组计算指定 week 的统计 */
function computeWeekStats(logs: any[]) {
  if (!logs.length) return {
    avgFocus: 0, peakFocus: 0, totalDuration: 0,
    avgDistract: 0, stability: 0, count: 0
  };
  const totalDuration = Math.round(logs.reduce((s: number, l: any) => s + (l.durationMinutes || 0), 0));
  const allAttention = logs.map((l: any) => l.avgAttention || 0);
  const allPeak = logs.map((l: any) => l.peakFocus || (l.eegMetrics && l.eegMetrics.peakFocus) || l.avgAttention || 0);
  const allDistract = logs.map((l: any) => l.distractCount || 0);
  const avgFocus = Math.round(allAttention.reduce((a, b) => a + b, 0) / allAttention.length);
  const peakFocus = Math.max(...allPeak);
  const avgDistract = +(allDistract.reduce((a, b) => a + b, 0) / allDistract.length).toFixed(1);
  const stableCount = allAttention.filter((v: number) => v >= 60).length;
  const stability = Math.round((stableCount / allAttention.length) * 100);
  return { avgFocus, peakFocus, totalDuration, avgDistract, stability, count: logs.length };
}
// ========== 默认数据（云端无数据时的兜底） ==========
const DEFAULT_DATA = {
  parentChartData: { xAxisDates: [] as string[], focusScores: [] as number[], distractCounts: [] as number[] },
  doctorRadarData: [0, 0, 0, 0] as number[],
  childInfo: { name: '点击设置姓名', age: 7 },
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
  homeTasks: [] as { id: any; title: string; desc: string }[],
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
    ecParent: { lazyLoad: false } as any,
    ecDoctor: { lazyLoad: false } as any,
    // 医生下发任务表单
    taskTitle: '',
    taskDesc: ''
  },
  // ==================== 生命周期 ====================
  onLoad() {
    console.log("📊 康复报告页加载，查询 main_game_logs...");
    // 读取本地存储的儿童档案
    this.loadChildProfile();
    this.fetchReportData();
  },
  onShow() {
    // 从其他 tab 切回来时刷新
    if (!this.data.loading) {
      this.fetchReportData();
    }
  },
  onPullDownRefresh() {
    this.fetchReportData();
  },
  // ==================== 儿童档案 ====================
  /** 从本地存储加载儿童姓名/年龄 */
  loadChildProfile() {
    try {
      const saved = wx.getStorageSync('childProfile');
      if (saved && saved.name) {
        this.setData({ childInfo: { name: saved.name, age: saved.age || 7 } });
      }
    } catch (e) { /* 忽略 */ }
  },
  /** 点击姓名卡片 → 弹出编辑 */
  onEditChildProfile() {
    const that = this;
    const current = this.data.childInfo;
    wx.showModal({
      title: '设置儿童信息',
      editable: true,
      placeholderText: '输入孩子姓名',
      content: current.name !== '点击设置姓名' ? current.name : '',
      success: (res: any) => {
        if (res.confirm && res.content && res.content.trim()) {
          const name = res.content.trim();
          const profile = { name, age: current.age };
          wx.setStorageSync('childProfile', profile);
          that.setData({ childInfo: profile });
          wx.showToast({ title: '已更新', icon: 'success' });
        }
      }
    });
  },
  /** 点击年龄 → 修改年龄 */
  onEditChildAge() {
    const that = this;
    wx.showActionSheet({
      itemList: ['5岁', '6岁', '7岁', '8岁', '9岁', '10岁', '11岁', '12岁'],
      success: (res: any) => {
        const age = res.tapIndex + 5;
        const profile = { name: that.data.childInfo.name, age };
        wx.setStorageSync('childProfile', profile);
        that.setData({ childInfo: profile });
      }
    });
  },
  // ==================== 居家任务 ====================
  /** 从云数据库拉取医生下发的居家任务 */
  loadHomeTasks() {
    const that = this;
    db.collection('home_tasks')
      .orderBy('createTime', 'desc')
      .limit(10)
      .get({
        success: (res: any) => {
          if (res.data && res.data.length > 0) {
            const tasks = res.data.map((t: any) => ({
              id: t._id,
              title: t.title,
              desc: t.desc
            }));
            that.setData({ homeTasks: tasks });
          }
          // 无数据时保持默认值
        },
        fail: (err: any) => {
          console.log('📋 居家任务拉取失败（使用默认）:', err);
        }
      });
  },
  // ==================== 数据拉取 ====================
  fetchReportData() {
    const that = this;
    const weekStart = getWeekStart();
    const lastWeekStart = weekStart - 7 * 24 * 60 * 60 * 1000;  // 上周一
    console.log(`📅 本周起始: ${toDateText(weekStart)} | 上周起始: ${toDateText(lastWeekStart)}`);
    // 拉取近两周数据用于跨周对比
    db.collection('main_game_logs')
      .where({
        timestamp: _.gte(lastWeekStart)
      })
      .orderBy('timestamp', 'desc')
      .limit(60)
      .get({
        success: (res: any) => {
          console.log(`✅ 拉取到 ${res.data.length} 条训练日志`);
          if (res.data.length > 0) {
            that.applyGameLogs(res.data, weekStart);
          } else {
            console.log("⚠️ 近两周暂无训练记录");
            that.finishLoading();
          }
        },
        fail: (err: any) => {
          console.error("❌ main_game_logs 查询失败:", err);
          wx.showToast({ title: '网络异常，展示本地缓存', icon: 'none' });
          that.finishLoading();
        }
      });
    // 拉取居家任务（医生下发的）
    this.loadHomeTasks();
  },
  // ==================== 数据映射核心 ====================
  applyGameLogs(logs: any[], weekStart: number) {
    // logs 已按 timestamp desc 排序，最新的在前
    // ---- 0. 拆分本周 / 上周 ----
    const thisWeekLogs = logs.filter((l: any) => l.timestamp >= weekStart);
    const lastWeekLogs = logs.filter((l: any) => l.timestamp < weekStart);
    const thisWeek = computeWeekStats(thisWeekLogs);
    const lastWeek = computeWeekStats(lastWeekLogs);
    // 跨周趋势计算
    let focusTrend = 0;
    let focusTrendText = '--';
    let distractTrendText = '--';
    if (lastWeek.count > 0 && thisWeek.count > 0) {
      // 专注力趋势
      focusTrend = lastWeek.avgFocus > 0
        ? Math.round((thisWeek.avgFocus - lastWeek.avgFocus) / lastWeek.avgFocus * 100)
        : 0;
      const arrow = focusTrend >= 0 ? '↑' : '↓';
      focusTrendText = `${arrow}${Math.abs(focusTrend)}%`;
      // 走神趋势（走神减少是好事）
      const distractDiff = thisWeek.avgDistract - lastWeek.avgDistract;
      distractTrendText = distractDiff <= 0
        ? `↓${Math.abs(distractDiff).toFixed(1)}`
        : `↑${distractDiff.toFixed(1)}`;
    } else if (thisWeek.count > 0) {
      focusTrendText = '--';
      distractTrendText = `均${thisWeek.avgDistract}次`;
    }
    // ---- 1. 家长端图表：用本周数据，按时间正序 ----
    const chartLogs = thisWeekLogs.length > 0 ? thisWeekLogs : lastWeekLogs;
    const reversed = [...chartLogs].reverse();
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
    // ---- 4. 儿童信息：优先用已保存的，否则从数据的 childId 推测 ----
    if (this.data.childInfo.name === '点击设置姓名' && latest.childId) {
      this.setData({ childInfo: { name: latest.childId, age: 7 } });
    }
    // ---- 5. 关卡进度：按 game_module 聚合 ----
    const modules: Record<string, number[]> = { camp: [], room: [], kitchen: [] };
    thisWeekLogs.forEach((l: any) => {
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
    const todayLogs = thisWeekLogs.filter((l: any) => l.timestamp >= todayStart);
    const todayAvg = todayLogs.length
      ? Math.round(todayLogs.reduce((s: number, l: any) => s + l.avgAttention, 0) / todayLogs.length)
      : thisWeek.avgFocus;
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
    if (thisWeek.peakFocus >= 85) {
      highlights.push({ emoji: '🎯', text: `本周专注力峰值达到 ${thisWeek.peakFocus} 分，创下新高！` });
    }
    if (thisWeek.stability >= 65) {
      highlights.push({ emoji: '💎', text: `专注稳定率 ${thisWeek.stability}%，说明大部分时间都能保持良好专注` });
    }
    const exec = dim.executive || 0;
    if (exec < 50) {
      highlights.push({ emoji: '📋', text: `执行功能得分 ${exec} 分，建议在失重备菜室多加练习来提升` });
    }
    if (focusTrend > 10) {
      highlights.push({ emoji: '📈', text: `本周专注力较上周提升 ${focusTrend}%，进步明显！` });
    }
    if (highlights.length === 0) {
      highlights.push({ emoji: '🚀', text: '训练数据积累中，坚持训练后这里会出现属于你的成长亮点！' });
    }
    // ---- 8. 一键写入 ----
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
      // 本周指标（含跨周趋势）
      weeklyMetrics: {
        totalDuration: thisWeek.totalDuration,
        avgFocusScore: thisWeek.avgFocus,
        peakFocusScore: thisWeek.peakFocus,
        avgDistractCount: thisWeek.avgDistract,
        stabilityRatio: thisWeek.stability,
        focusTrend: focusTrend,
        focusTrendText: focusTrendText,
        distractTrendText: distractTrendText,
        peakLevelName: latest.recipeName || '--',
        levelProgress
      },
      // 医生端（数值统一保留 1 位小数，focusCv 保留 2 位）
      doctorReport: {
        eegMetrics: {
          meanFocus:           +((eeg.meanFocus || 0).toFixed(1)),
          peakFocus:           +((eeg.peakFocus || 0).toFixed(1)),
          valFocus:            +((eeg.valFocus || 0).toFixed(1)),
          durationRatio60:     +((eeg.durationRatio60 || 0).toFixed(1)),
          durationRatio80:     +((eeg.durationRatio80 || 0).toFixed(1)),
          avgDistractDuration: +((eeg.avgDistractDuration || 0).toFixed(1)),
          focusCv:             +((eeg.focusCv || 0).toFixed(2))
        },
        distractCount: thisWeek.avgDistract
      },
      // 当前阶段
      rehabPlan: {
        currentStage: latest.recipeName || '训练中'
      },
      // 亮点
      childHighlights: highlights,
      // 关闭 loading
      loading: false
    });
    wx.stopPullDownRefresh();
    console.log("📊 报告数据渲染完成 | 本周:", thisWeek.count, "局 | 上周:", lastWeek.count, "局 | 趋势:", focusTrendText);
  },
  /**
   * 兜底：无数据或拉取失败时直接关闭 loading
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
    // 科研配色：深青蓝 #287271 + 柔和暖陶 #D4855E
    const option = {
      color: ['#287271', '#D4855E'],
      tooltip: {
        trigger: 'axis', backgroundColor: '#fff', borderColor: '#E8EDEA', borderWidth: 1,
        textStyle: { color: '#555', fontSize: 12 },
        axisPointer: { type: 'cross', crossStyle: { color: '#CCC' } }
      },
      legend: {
        data: ['专注力', '走神次数'], bottom: 0,
        textStyle: { color: '#888', fontSize: 11 }, itemWidth: 18, itemHeight: 2
      },
      grid: { top: 16, left: 44, right: 52, bottom: 36 },
      xAxis: {
        type: 'category', data: chartData.xAxisDates,
        axisLine: { lineStyle: { color: '#E0E0E0' } }, axisTick: { show: false },
        axisLabel: { color: '#999', fontSize: 10 }
      },
      yAxis: [
        {
          type: 'value', name: '分', nameTextStyle: { color: '#999', fontSize: 10 },
          min: 0, max: 100, interval: 25,
          axisLabel: { color: '#999', fontSize: 10 },
          splitLine: { lineStyle: { color: '#F0F0F0', width: 0.5 } },
          axisLine: { show: false }
        },
        {
          type: 'value', name: '次', nameTextStyle: { color: '#999', fontSize: 10 },
          min: 0,
          axisLabel: { color: '#999', fontSize: 10 },
          splitLine: { show: false }, axisLine: { show: false }
        }
      ],
      series: [
        {
          name: '专注力', type: 'line', yAxisIndex: 0, data: chartData.focusScores,
          smooth: true, symbol: 'circle', symbolSize: 5,
          lineStyle: { width: 2, color: '#287271' }, itemStyle: { color: '#287271' },
          areaStyle: { color: 'rgba(40,114,113,0.08)' }
        },
        {
          name: '走神次数', type: 'bar', yAxisIndex: 1, data: chartData.distractCounts,
          barWidth: 12, itemStyle: { color: '#D4855E', borderRadius: [4, 4, 0, 0] }
        }
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
    if (radarData.every((v: number) => v === 0)) {
      chart.setOption({
        title: { text: '暂无数据', subtext: '完成训练后自动生成', left: 'center', top: 'center' }
      });
      return chart;
    }
    // 四维配色：森绿 / 雾蓝 / 暖杏 / 浅紫
    const option = {
      title: { text: '专注力四维评估', left: 'center', textStyle: { fontSize: 13, color: '#2A2A2A' } },
      tooltip: { trigger: 'item', backgroundColor: '#fff', borderColor: '#E8EDEA', textStyle: { color: '#555' } },
      radar: {
        indicator: [
          { name: '持续注意', max: 100, color: '#68A98A' },
          { name: '选择注意', max: 100, color: '#849FD9' },
          { name: '执行控制', max: 100, color: '#F2B888' },
          { name: '冲动抑制', max: 100, color: '#A89CC8' }
        ],
        center: ['50%', '55%'], radius: '60%', shape: 'circle', splitNumber: 4,
        axisName: { color: '#555', fontSize: 11 },
        splitLine: { lineStyle: { color: '#E8EDEA', width: 1 } },
        splitArea: { show: true, areaStyle: { color: ['#FAFBFA', '#F5F7F5'] } },
        axisLine: { lineStyle: { color: '#E8EDEA' } }
      },
      series: [{
        type: 'radar',
        data: [{
          value: radarData, name: '当前评估',
          itemStyle: { color: '#68A98A' },
          lineStyle: { color: '#68A98A', width: 1.5 },
          areaStyle: { color: 'rgba(104,169,138,0.20)' }
        }],
        symbol: 'circle', symbolSize: 4
      }]
    };
    chart.setOption(option);
    return chart;
  },
  // ==================== 医生端交互 ====================
  toggleRole() {
    const newRole = this.data.userRole === 'parent' ? 'doctor' : 'parent';
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
  },
  // ==================== 医生下发任务 ====================
  onTaskTitleInput(e: any) {
    this.setData({ taskTitle: e.detail.value });
  },
  onTaskDescInput(e: any) {
    this.setData({ taskDesc: e.detail.value });
  },
  submitHomeTask() {
    const title = this.data.taskTitle.trim();
    const desc = this.data.taskDesc.trim();
    if (!title || !desc) {
      wx.showToast({ title: '请填写任务标题和描述', icon: 'none' });
      return;
    }
    const that = this;
    db.collection('home_tasks').add({
      data: {
        title,
        desc,
        createTime: new Date().toISOString()
      },
      success: () => {
        wx.showToast({ title: '任务已下发至家长端', icon: 'success' });
        that.setData({ taskTitle: '', taskDesc: '' });
        // 重新拉取任务列表
        that.loadHomeTasks();
      },
      fail: (err: any) => {
        console.error('任务下发失败:', err);
        wx.showToast({ title: '下发失败，请重试', icon: 'error' });
      }
    });
  }
})
