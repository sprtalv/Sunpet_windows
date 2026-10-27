# 移植来源

2026-10-27 核对三份上游 main：

| 本仓库目录 | 上游 | SHA |
| --- | --- | --- |
| pets/SunPet | https://github.com/Ganzhe2028/SunPet | 9b68d0911552d8c880a46ca72980d3f9af9743ee |
| pets/FengGe | https://github.com/Ganzhe2028/FengGe-Desktop-Pet | d3336debfafc37963ec5612cb27793e250228b9b |
| pets/Shaolan | https://github.com/Ganzhe2028/ShaolanPet | 7ce7e2b1ed49c4da66d19d5aac419794af0d32db |

Sources 内 Swift 与 Assets 内原 PNG/音频/清单保留原样；新增 face.ico 仅做透明图像格式转换。Windows 是独立 C#/WPF 实现。各角色 docs/upstream 保存原 README 与 TECHNICAL 供来源查阅，其相对链接按原仓库语境理解。

三个 Sources/Bridge.swift 的 SHA256 均为：
`EEB252566E7D1BB6108EB240342F6F0D363190F80520447E9712D01E4FDDF991`

未向原作者仓库推送变更。峰哥初版及 SunPet 衍生许可分别保留于其原 NOTICE 和 licenses/SunPet-MIT.txt。
