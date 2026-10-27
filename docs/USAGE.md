# 使用、升级与诊断

完整解压ZIP到可写目录，双击角色EXE。保留所有DLL、Assets与许可证。运行不需要SDK、管理员权限或联网。Windows媒体组件需可用，Windows N等精简系统未做适配验证；本程序不自动安装系统组件。

## 设置

包内portable.flag启用便携模式，设置在程序旁UserData/<当前Windows用户SID>/settings.json。首次会导入本用户原LocalAppData中已有的同角色设置。不要公开上传UserData或个人trace.log。

如希望使用LocalAppData，退出后移除portable.flag；此时分别存于%LOCALAPPDATA%/SunPet-Windows、FengGe-Windows、Shaolan-Windows。配置损坏自动恢复默认，无法写入会显示提示。请将便携目录放在有写权限的位置。

默认音量65%。SunPet大小130/180/240（默认180，内部120–260）；峰哥、薄肌190/260/340（默认260，内部160–400）。随机至少保留一句，排除同时作用于自动接话；固定和手选仍可播放被排除的语音。固定模式下手选会更改固定句。

## 升级与卸载

升级前从托盘退出所有角色，解压新包到新目录，再按需复制旧UserData。卸载先退出、再删除解压目录；如果曾用LocalAppData模式，可自行删除对应设置目录。不会留下服务或开机启动项。

## 诊断

EXE支持--self-test --report <文件>、--dump-mode / --dump-state --report <文件>、--trace [--trace-file <文件>]、--quit。PowerShell调用GUI程序并等待结果可使用Start-Process ... -Wait。默认不写诊断日志。

无声音请检查程序音量与系统输出设备。找不到人物可点击托盘头像打开菜单，选择“移回屏幕右下角”。重复运行同角色不会创建第二个，三款可以分别同时存在。
