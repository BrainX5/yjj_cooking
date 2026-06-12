// pages/index/index.ts — 时间胶囊首页
export {};
const db = wx.cloud.database();

Page({
  data: {
    loading: true,

    // 顶部
    todayDate: '',
    syncTime: '尚未同步',

    // 今日战报
    trainLevel: 'good' as 'excellent' | 'good' | 'attention',
    trainLevelText: '等待数据同步',
    progressText: '完成第一次训练后，这里会出现你的专注力战报 🚂',
    todayFocus: 0,

    // 当前游戏
    gameIcon: '🎮',
    currentGame: '等待开始',
    gameCount: 0,

    // 关卡进度
    levelProgress: { camp: 0, room: 0, kitchen: 0 } as Record<string, number>,

    // 今日小结
    todayDuration: 0,
    todayPeak: 0,
    todayDistract: 0
  },

  // ========== 游戏图标映射 ==========
  gameIcons: {
    camp: '🥕',
    room: '🥩',
    kitchen: '🍳'
  } as Record<string, string>,
  gameNames: {
    camp: '食材准备营',
    room: '失重备菜室',
    kitchen: '林间小厨房'
  } as Record<string, string>,

  // ========== 生命周期 ==========

  onLoad() {
    this.setData({ todayDate: this.formatDate(new Date()) });
    this.loadCapsuleData();
  },

  onShow() {
    this.loadCapsuleData();
  },

  onPullDownRefresh() {
    this.loadCapsuleData();
  },

  // ========== 核心：加载时间胶囊数据 ==========

  loadCapsuleData() {
    const that = this;
    const todayStart = new Date(new Date().toDateString()).getTime();

    db.collection('main_game_logs')
      .where({ _openid: '{openid}' })
      .orderBy('timestamp', 'desc')
      .limit(100)
      .get({
        success: (res: any) => {
          console.log(`✅ 时间胶囊拉取到 ${res.data.length} 条记录`);
          if (res.data.length > 0) {
            that.buildCapsule(res.data, todayStart);
          } else {
            that.setData({ loading: false });
          }
        },
        fail: (err: any) => {
          console.error("❌ 时间胶囊查询失败:", err);
          that.setData({ loading: false });
          wx.stopPullDownRefresh();
        }
      });
  },

  // ========== 数据处理 ==========

  buildCapsule(allLogs: any[], todayStart: number) {
    const that = this;
    const latest = allLogs[0];

    // ---- 今日数据 ----
    const todayLogs = allLogs.filter((l: any) => l.timestamp >= todayStart);
    const todayFocus = todayLogs.length
      ? Math.round(todayLogs.reduce((s: number, l: any) => s + (l.avgAttention || 0), 0) / todayLogs.length)
      : Math.round(latest.avgAttention || 0);

    const todayDuration = todayLogs.reduce((s: number, l: any) => s + (l.durationMinutes || 0), 0);

    const todayPeak = todayLogs.length
      ? Math.max(...todayLogs.map((l: any) => l.peakFocus || l.avgAttention || 0))
      : (latest.peakFocus || latest.avgAttention || 0);

    const todayDistract = todayLogs.reduce((s: number, l: any) => s + (l.distractCount || 0), 0);

    // ---- 战报等级 ----
    const level = todayFocus >= 80 ? 'excellent' : todayFocus >= 60 ? 'good' : 'attention';
    const levelText = todayFocus >= 80 ? '专注力优秀 ✨' : todayFocus >= 60 ? '专注力良好 👍' : '需要加油 💪';
    const progressText = todayLogs.length
      ? `今日完成 ${todayLogs.length} 局训练，小火车前进了 ${todayFocus}% 的路程！`
      : `最新一局平均专注力 ${todayFocus} 分，继续加油哦 🌲`;

    // ---- 当前游戏 ----
    const gameModule: string = latest.game_module || 'camp';
    const gameIcon = that.gameIcons[gameModule] || '🎮';
    const currentGame = that.gameNames[gameModule] || gameModule;
    const gameCount = allLogs.length;

    // ---- 关卡进度（基于所有记录的平均专注力） ----
    const modules: Record<string, number[]> = { camp: [], room: [], kitchen: [] };
    allLogs.forEach((l: any) => {
      const m = l.game_module;
      if (modules[m] !== undefined) {
        modules[m].push(l.avgAttention || 0);
      }
    });
    const modAvg = (arr: number[]) =>
      arr.length ? Math.round(arr.reduce((a: number, b: number) => a + b, 0) / arr.length) : 0;
    const levelProgress = {
      camp: modAvg(modules['camp']),
      room: modAvg(modules['room']),
      kitchen: modAvg(modules['kitchen'])
    };

    // ---- 写入 ----
    this.setData({
      syncTime: new Date().toLocaleTimeString(),
      trainLevel: level,
      trainLevelText: levelText,
      progressText,
      todayFocus,
      gameIcon,
      currentGame,
      gameCount,
      levelProgress,
      todayDuration,
      todayPeak,
      todayDistract,
      loading: false
    });

    wx.stopPullDownRefresh();
  },

  // ========== 工具 ==========

  formatDate(d: Date): string {
    const weekMap = ['日', '一', '二', '三', '四', '五', '六'];
    return `${d.getMonth() + 1}月${d.getDate()}日 · 星期${weekMap[d.getDay()]}`;
  }
})
