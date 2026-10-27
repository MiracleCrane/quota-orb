# Quota Orb

面向 Windows Codex Desktop 的极简额度悬浮球。

当前阶段：**Windows 原生测试版**。原生客户端通过本机 Codex app-server 读取真实额度；`preview/index.html` 保留为独立设计预览，里面的数字仍是演示数据。

## Windows 使用

要求 Windows 10/11、.NET Framework 4.8，以及已经登录的官方 Codex Windows 客户端或 CLI。无需 API Key，不读取或复制 `auth.json`。

```powershell
./build.ps1 -Test
./install.ps1 -StartWithWindows
```

从桌面或开始菜单的 **Quota Orb** 启动。默认安装到 `%LOCALAPPDATA%\Programs\QuotaOrb`；可用 `-Destination` 指定位置。构建使用 Windows 自带的 .NET Framework 编译器，不需要安装 .NET SDK 或下载 NuGet 包。

- 默认球体直径 24 个逻辑像素，接近头像大小；可在显示选项中用 `− / ＋` 调整到 24–80。双额度时自动放大到至少 40px，始终保留两项数字。
- 默认每 15 秒读取额度；读取失败会保留旧读数并标记更新时间，重试逐步退避。
- Codex 仍是最前面的普通应用窗口时保持显示，鼠标移到任务栏或系统菜单不会因此隐藏；切到其他应用或最小化 Codex 后隐藏。
- 前台切换、移动和恢复窗口通过系统事件响应，切回时直接恢复已有读数，不等待下一次额度刷新。进程识别和配色采样在后台进行。
- 鼠标悬停展开、点击固定、Esc 收起；拖动松手后贴近左右边缘。
- 外圈以约 3.6 秒周期变亮扩开、变淡收回，在原有边界内运动，数字和额度环保持稳定。可关闭动画，系统减少动态效果和高对比度设置也会被尊重。
- 暂时读取失败只会置灰旧额度并提示更新时间，呼吸仍保持运行，不受读取重试退避影响。
- 主球和卡片均不创建任务栏入口。退出入口在卡片显示选项中。

配置和只含显示状态的诊断文件位于 `%LOCALAPPDATA%\QuotaOrb`。如无法自动找到 CLI，可在 `settings.json` 的 `CodexPath` 指定 `codex.exe`。支持 `CODEX_HOME` 及已有代理环境变量。

开发验证入口：`QuotaOrb.exe --render <目录>` 导出原生深浅色样例；`--demo` 使用演示数据；`--snapshot <文件>` 进行一次只读额度查询。只有不带这些参数的正常启动模式才会持续跟随 Codex。

## 设计范围

- 球体常驻在 Codex 窗口边缘，直接显示剩余百分比。
- 同时存在 5 小时和周额度时，分别显示两个数字；账户没有 5 小时限制时，整个 5h 项隐藏。
- 悬停展开重置时间，点击固定展开，拖动后贴近左右边缘。
- 重置券数量与到期时间作为可开关的扩展项，默认关闭。
- Segoe UI、单色额度环、轻微动效，支持减少动态效果。
- 数据过期和读取失败应明确标记；缺失额度不能解释成 100% 或无限制。

不包含聊天侧栏徽章、上下文与 Token 统计、重置雷达、模型调用、客户端更新器或宣传内容。

## 查看预览

打开 [preview/index.html](preview/index.html)。右上角可切换仅周额度、双额度、额度较低和刷新失败等演示状态。卡片的“显示选项”中可开启重置券或关闭呼吸动画。

原型源文件为 `preview/orb.fragment.html`，独立页面为 `preview/index.html`。交互约定见 [docs/interaction.md](docs/interaction.md)。

## 代码来源与许可证

此仓库包含独立编写的原生客户端、预览和设计文档。没有包含、复制或改编其他额度助手的程序源码、品牌素材或二进制文件，也不是其 fork。

通过本机官方 Codex app-server 的 `account/rateLimits/read` 只读获取额度。接口名和数据字段按本机生成的协议 Schema 核验；账户登录仍由 Codex 管理。客户端没有模型调用、重置券兑换或写入账户的入口。

本项目独立编写的代码采用 [MIT](LICENSE) 许可证。预览运行时与开发工具的通用依赖见 [THIRD_PARTY.md](THIRD_PARTY.md)。Codex 是 OpenAI 的产品名称，本项目为非官方独立工具。
