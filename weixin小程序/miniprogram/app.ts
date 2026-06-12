// app.ts
// 定义数据类型，让 TS 不报错，也让你一眼看懂数据结构
export interface IAppOption {
  globalData: {
    // 专门存放从云端同步过来的游戏进度
    gameSyncData: {
      score?: number,       // 蘑菇数量
      recipeName?: string,  // 菜名
      status?: string,      // 状态
      focusMinutes?: number // 专注时长
    }
  }
}

App<IAppOption>({
  // 初始化全局变量，给它一个空对象
  globalData: {
    gameSyncData: {},
    cloudReady: false  // 标记云开发是否初始化完成
  },

  onLaunch() {
    // 1. 初始化云开发能力
    if (!wx.cloud) {
      console.error('请使用 2.2.3 或以上的基础库以使用云能力');
    } else {
      wx.cloud.init({
        env: 'cloud1-d9gz2tmfub107d0ff',
        traceUser: true,
      });
      // cloud.init 是同步方法，调用后即可标记就绪
      (this.globalData as any).cloudReady = true;
    }

    // 2. 日志功能
    const logs = wx.getStorageSync('logs') || []
    logs.unshift(Date.now())
    wx.setStorageSync('logs', logs)

    // 3. 登录
    wx.login({
      success: res => {
        console.log('微信登录 Code为:', res.code)
      },
    })
  },
})
