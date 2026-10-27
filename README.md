<h1 align="center">Windows 桌宠三连</h1>
<p align="center">太对了 · 这是个好事 · AI 救不了你<br>三个独立桌宠，点一下说话，一起打开还能接话。</p>
<p align="center"><b>Windows 11 x64 · 解压即用 · 无需安装 .NET · 离线运行</b></p>

## 点头像，下载 Windows 版

<table>
<tr>
<td align="center" width="33%"><a href="https://github.com/sprtalv/Sunpet_windows/releases/download/v1.0.0-beta.1/SunPet.Windows-win-x64.zip"><img src="pets/SunPet/Assets/face.png" width="140" alt="下载孙宇晨桌宠"></a></td>
<td align="center" width="33%"><a href="https://github.com/sprtalv/Sunpet_windows/releases/download/v1.0.0-beta.1/FengGe.Windows-win-x64.zip"><img src="pets/FengGe/Assets/face.png" width="140" alt="下载峰哥桌宠"></a></td>
<td align="center" width="33%"><a href="https://github.com/sprtalv/Sunpet_windows/releases/download/v1.0.0-beta.1/Shaolan.Windows-win-x64.zip"><img src="pets/Shaolan/Assets/face.png" width="140" alt="下载邵艾伦桌宠"></a></td>
</tr>
<tr>
<td align="center"><b>孙宇晨 · 太对了</b><br>28 句原声<br><a href="https://github.com/sprtalv/Sunpet_windows/releases/download/v1.0.0-beta.1/SunPet.Windows-win-x64.zip"><b>下载完整 ZIP</b></a></td>
<td align="center"><b>峰哥 · 好事桌宠</b><br>11 句原声<br><a href="https://github.com/sprtalv/Sunpet_windows/releases/download/v1.0.0-beta.1/FengGe.Windows-win-x64.zip"><b>下载完整 ZIP</b></a></td>
<td align="center"><b>邵艾伦 · 薄肌桌宠</b><br>37 句原声，默认轮播 8 句<br><a href="https://github.com/sprtalv/Sunpet_windows/releases/download/v1.0.0-beta.1/Shaolan.Windows-win-x64.zip"><b>下载完整 ZIP</b></a></td>
</tr>
</table>

**下载上面的完整 ZIP 即可。** GitHub 绿色 `Code → Download ZIP` 下载的是源码，不能直接运行。也可进入 [发布下载页](https://github.com/sprtalv/Sunpet_windows/releases/tag/v1.0.0-beta.1)，校验文件为 `SHA256SUMS.txt`。

## 三步用起来

1. 下载喜欢的角色，一个也能玩，三个可以一起开。
2. **完整解压**到一个可写文件夹，不要直接在压缩包里运行。
3. 双击对应的 `SunPet.Windows.exe` / `FengGe.Windows.exe` / `Shaolan.Windows.exe`。

文件夹里的 DLL、Assets 等文件要保留，不能只拿走 EXE。默认不显示普通任务栏窗口；桌面右下角能看到人物，系统托盘里也有头像。三个角色初次可能叠在一起，拖开即可。

## 怎么玩

| 操作 | 效果 |
| --- | --- |
| 左键单击 | 随机说一句，一轮不重复；再次点击替换上一句，不叠音 |
| 按住拖动 | 移动人物，重启记住位置 |
| 右键人物 / 点击托盘头像 | 选句、固定模式、筛选轮播、音量和大小 |
| 轮播内容 | 连续勾选不关菜单，至少留一句；“全部恢复”开启全部语音 |
| 移回屏幕右下角 | 找回跑到屏幕外的桌宠 |
| 退出桌宠 | 从菜单彻底退出 |

薄肌默认启用 07、16、17、19、26、30、31、32，其余句子可以手选或在轮播内容里开启。默认音量 65%，播放时有原句字幕和动画；设置会保存。

**三个一起开，就能接话：**

```text
点薄肌：薄肌 → 太对了 → 峰哥 → 太对了 → 停止
点峰哥：峰哥 → 太对了 → 停止
点太对了：只播放自己
```

无需配对、不走互联网。只支持同一 Windows 用户会话内联动；与 macOS 原版不互通。正在播放时不会被另一句自动接话打断，菜单可以关闭联动。

## 原版从哪里来

这是 **sprtalv 维护的非官方 Windows 移植版**，使用 C# / WPF / .NET 10 实现，开发和测试使用了 AI 辅助。原素材、交互设计与联动规则来自：

- 太对了：[Ganzhe2028/SunPet](https://github.com/Ganzhe2028/SunPet)
- 峰哥：[Ganzhe2028/FengGe-Desktop-Pet](https://github.com/Ganzhe2028/FengGe-Desktop-Pet)，初版来自 [yuhanfu513-ops/FengGe-Desktop-Pet](https://github.com/yuhanfu513-ops/FengGe-Desktop-Pet)
- 薄肌：[Ganzhe2028/ShaolanPet](https://github.com/Ganzhe2028/ShaolanPet)

感谢原作者！移植基准 SHA、保留的原版源码与许可见 [来源记录](docs/UPSTREAM.md)。维护者确认已获得原项目作者对 Windows 移植发布的同意；不代表相关人物的官方合作或背书。

**代码 MIT；头像、音频不在 MIT 授权范围内，版权归原权利人，仅供非商业学习交流。** 原项目发布同意不改变素材权利归属。完整声明见 [NOTICE.md](NOTICE.md)。

## 其他说明

- 便携设置在程序旁 `UserData/<用户 SID>/`；退出后删除整个文件夹即可卸载，不添加开机启动或服务。升级和诊断见 [使用说明](docs/USAGE.md)。
- 本版是 `v1.0.0-beta.1`：三款音频、播放规则、发布自检及真实三进程联动已通过本机测试。Windows 10、多显示器热拔插和所有混合 DPI 组合未完全验证，详见 [测试范围](docs/TESTING.md)。
- 未做商业代码签名。出现发布者提示时核对下载来源与 SHA256，不要关闭安全防护。
- 无机身摇晃功能，不读取麦克风、摄像头或全局键盘。
- 想自己编译：[构建与技术说明](docs/PORTING.md)。遇到问题可在 [Issues](https://github.com/sprtalv/Sunpet_windows/issues) 提供系统版本、复现步骤和角色名称。
