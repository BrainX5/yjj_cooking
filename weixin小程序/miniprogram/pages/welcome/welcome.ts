Page({
  // 处理点击登录
  handleLogin: function() {
    wx.showLoading({ title: '正在连接森林...' });

    // 调用微信登录接口获取 code
    wx.login({
      success: (res) => {
        if (res.code) {
          console.log('身份凭证获取成功:', res.code);
          
          // 模拟一个 1.5 秒的云端验证过程
          setTimeout(() => {
            wx.hideLoading();
            
            // 登录成功，跳转到带 TabBar 的首页
            // 注意：跳转到 tabBar 页面必须用 switchTab
            wx.switchTab({
              url: '/pages/index/index'
            });
          }, 1500);
        }
      },
      fail: () => {
        wx.hideLoading();
        wx.showToast({ title: '登录失败', icon: 'error' });
      }
    })
  }
})