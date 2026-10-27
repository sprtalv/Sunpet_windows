# Windows 实现与诊断

## 从源码构建

需要Windows x64与.NET 10 SDK；首次还原NuGet需要网络。在仓库根运行`./build.ps1`，或`./build.ps1 -Pet SunPet`构建单款。需要指定SDK时使用`-Dotnet <dotnet.exe路径>`。运行`./integration-test.ps1`可测试三个发布进程；此前需已构建全部三款。

完整ZIP及SHA256位于artifacts/packages，测试报告位于artifacts/test-results，缓存位于.build-cache。每次使用全新staging目录，防止个人设置进入下载包。`-SkipTests`仅供没有音频设备的构建环境使用；跳过时不能宣称通过音频或GUI自检。不得修改系统安全策略来运行脚本；可在已有允许脚本的PowerShell环境运行，或参考脚本逐条执行dotnet命令。

使用 C# / WPF / .NET 10、NAudio 2.2.1。透明置顶窗口通过 WS_EX_NOACTIVATE 与 WM_MOUSEACTIVATE 保持非激活，普通模式无任务栏按钮，NotifyIcon 提供托盘菜单。PerMonitorV2 manifest，位置存物理像素、大小按 DIP，拖动阈值按当前 DPI 换算；显示变化后重新限制到可见工作区。

Media Foundation 直接解码原始 m4a/wav，PCM 内存缓存，单个 WaveOutEvent，60ms 缓冲。SunPet 六条短句/峰哥全部十一句预加载；首通道前 .6 秒使用 max(.004, peak*.06) 阈值测前导静音。Media Foundation 与 AVFoundation 的 AAC 延迟读数可能不同，按各自实际解码结果补偿；不承诺采样级同步。

SunPet 动画 phase += .38，旋转 .055 弧度、浮动 3 DIP；峰哥/薄肌 phase += .60，竖向最多压缩 12%，遵循 Windows 客户区动画设置。所有动画约 30fps，停止和结束归位。

## IPC v1

Named Pipes：`SunPetBridge-v1-<SID>-<session>-<role>`。服务端 CurrentUserOnly ACL，客户端只连接本机 `.`。没有 TCP、HTTP 或互联网依赖。2 秒存活探测，连接400ms超时，读1秒超时；UTF-8单行JSON最多8192字节，应答 `ok`。

保留 `from/clipID/duration/label/hop/root`，补充 `type=spoke/version=1/eventId`。最近256个eventId去重；过滤自己、未知角色、异常duration和hop，hop最大3。hop=0时root必须等于from。接话广播hop+1并保留root。

SunPet回应峰哥和薄肌；峰哥仅回应SunPet且root=薄肌；薄肌不回应。延迟=max(.02,duration-overlap-leading)，默认overlap=.3秒，输入hop=1时=.2秒。到期时若自己在播就跳过。手动播放、停止、修改筛选和关闭联动取消pending。接话池随筛选更新。

`ping` 用于存活，`activate/quit` 用于同角色控制；不是防御恶意同用户程序的认证协议。与macOS通知机制不兼容。

## 命令行

- `--self-test --report <路径>`：独立临时设置与管道、静音真实窗口/输出测试，最后PASS；不修改普通用户设置。
- `--dump-mode` 或 `--dump-state --report <路径>`：查看配置。
- `--trace [--trace-file <路径>]`：记录诊断；默认不记录。
- `--quit`：请求当前角色正常退出。
- `--bridge-test`：启动后播一句检查接话。
- `--simulate-peer fengge|shaolan`：约8秒无声模拟peer。
- `--integration-test --ipc-scope <标识> [--play <ID>]`：静音、禁止鼠标输入、临时设置、12秒后自动退出。三个角色相同scope即可隔离联调。
- `--ui-check`：临时显示任务栏图标，让窗口检查工具发现；正式普通启动不要使用。

不移植Mac的私有硬件传感器接口；首版不支持机身摇晃。无录音、摄像、全局键盘监听或开机启动。
