品牌资源目录。两个文件都可缺席，缺席时程序回落到内置标记，编译与运行不受影响。

logo.png —— 顶栏应用标与运行时窗口图标（Window.Icon）
  · 仅图形部分，不含文字；正方形画布，图形居中
  · 背景透明，否则深色主题下会出现白底方块
  · 建议 256×256 或 512×512（顶栏按 24px 显示，窗口图标由系统缩放）

app.ico —— 可执行文件自身的图标
  · Windows 资源管理器、任务栏固定项、Alt+Tab 读的是它，与 Window.Icon 是两套
  · 多尺寸 ICO，至少包含 16 / 32 / 48 / 256 四档，否则小尺寸下会由系统降采样，边缘发糊
  · 由 logo.png 转换：ImageMagick 命令
      magick logo.png -define icon:auto-resize=256,48,32,16 app.ico
    或使用任意在线 PNG→ICO 工具，注意勾选多尺寸

另有两处品牌位不在本目录，需手工上传：
  · README 顶部标识    docs/images/logo.png（完整版，含文字）
  · GitHub 社交预览图  仓库 Settings → General → Social preview（1280×640）
