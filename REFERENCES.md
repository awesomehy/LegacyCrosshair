# 参考与实现边界

检索与开发日期：2026-10-26。本项目独立编写，没有引入下列仓库的源码、图片或其他资源；这些项目用于比较功能、架构与旧版几何思路。许可证名称来自检索时的仓库，后续复用前应重新核实对应版本。

| 来源 | 用途 |
| --- | --- |
| [coolbriggs123/CrossHair-CS2](https://github.com/coolbriggs123/CrossHair-CS2) · MIT | 参考全局 WASD 驱动桌面动态准星的产品方向。该项目的粗略按键扩散不等同于游戏速度。 |
| [dropbop/crosshair-overlay](https://github.com/dropbop/crosshair-overlay) · MIT | 参考轻量 Win32 覆盖层、实时预览和显示器选择。 |
| [akiver/csgo-sharecode](https://github.com/akiver/csgo-sharecode) · MIT | 参考旧版准星参数 / 分享码的可扩展方向；本版没有移植分享码实现。 |
| [sebastianspicker/small-indie-crosshair-company](https://github.com/sebastianspicker/small-indie-crosshair-company) · MIT | 参考社区对旧版静态像素量化与负间距的重建；其结论没有被本项目的旧版游戏实机截图独立证实。 |
| [lacymorrow/crossover](https://github.com/lacymorrow/crossover) · FSL-1.1-MIT | 参考设置与预览交互；没有复制其源码或资源。 |
| [setposgg/cs2-crosshair](https://github.com/setposgg/cs2-crosshair) · MIT | 用于比较更新后的准星表示；没有用更新后几何替代本项目的旧版体验目标。 |

Valve 官方更新背景：[2026 年 9 月 22 日公告](https://www.counter-strike.net/newsentry/674006995886407685)、[9 月 23 日后续说明](https://www.counter-strike.net/newsentry/674006995886408819)。这些公告用于确定版本背景，不提供本项目动态运动模型的精确参数。

Windows 实现参考 Microsoft 官方文档：[Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/using-raw-input)、[UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow)、[WinForms 高 DPI 配置](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms)、[DWM 标题栏主题属性](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)。

0.2.1 的资源复用与事件驱动实现参考：[GetRawInputData](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdata)、[CreateDIBSection](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection)、[SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)、[释放 WinForms 绘图缓冲](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.bufferedgraphicscontext.invalidate?view=windowsdesktop-10.0)。

这是首个可运行原型。后续精确还原需要带明确游戏版本、分辨率、缩放方式和参数的旧版截图，以及移动、反向急停和松键的逐帧样本。
