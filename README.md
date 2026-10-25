# Pipi

blackappleD 自用的 Dalamud（卫月国服）小功能合集插件。

## 功能

### 停靠折叠

ImGui 无法折叠已停靠的窗口。Pipi 让浮动停靠组可以收起到只剩标签栏：

- 双击浮动停靠组**顶部标签栏的空白处**（不是标签本身）或**右下角的缩放手柄**折叠，再次双击恢复原高度。
  双击缩放手柄时会取代 ImGui 原本的"自适应大小"行为，宽度保持不变。
- 展开高度会被记住，重启游戏后仍然有效。
- `/pipi fold` 切换所有浮动停靠组（有展开的就全部折叠，否则全部展开）。
- `/pipi fold on` 全部折叠，`/pipi fold off` 全部展开。
- 把指令写进用户宏并放上热键栏，即可当快捷键使用。

已知限制：

- 若 ImGui 的窗口最小高度大于标签栏高度，折叠后会保留一条窄边。
- 主视口上的停靠空间不支持；只含一个窗口的浮动节点使用原生标题栏折叠。
- 依赖 ImGui 内部接口，Dalamud 升级后可能需要适配。

### 市场布告板（移植自 Daily Routines）

移植自 [Daily Routines](https://github.com/Dalamud-DailyRoutines/DailyRoutines.ModulesPublic) 的「更好的市场布告板」模块，原作者 Fragile / AtmoOmen。

- `/pipi market` 开关窗口，`/pipi market <物品名/ID>` 直接查看指定物品；背包、聊天物品链接、制作笔记的右键菜单中有「查看市场数据」。
- 不在市场布告板前也能查看、购买本服挂单（支持多选后批量购买）；对比 Universalis 上各服价格、成交记录与价格走势。
- 价格监控：本服挂单低于设定价时提醒，可选自动购买。
- 可在道具说明末尾追加市场数据（设置中可关闭）。
- 「前往该服务器」和「NPC 商店」按钮需要同时安装 Daily Routines 才会生效。

注意：远程查询和购买会向服务器发送平时只在市场布告板前才会发送的请求，请自行评估风险；自动购买无需确认，请设置合理的监控价和上限。

## 安装

在 Dalamud 中添加自定义插件仓库：

```text
https://raw.githubusercontent.com/blackappleD/DalamudPlugins/main/repo.json
```

然后在插件安装器中搜索 `Pipi` 安装。

## 开发

```powershell
dotnet build Pipi.slnx
dotnet test Pipi.Tests/Pipi.Tests.csproj
```

`Dalamud.CN.NET.Sdk` 从 `%AppData%\XIVLauncherCN\addon\Hooks\dev\` 解析 Dalamud 程序集。
本地调试时在 Dalamud 设置 → 实验性 → 开发插件目录中添加 `Pipi/bin/Debug/Pipi.dll`。

## 发布

版本号以 `Pipi/Pipi.csproj` 的 `Version` 为准（四段数字，严格递增）。

1. `dotnet build Pipi/Pipi.csproj -c Release`，产物为 `Pipi/bin/Release/Pipi/latest.zip`。
2. 将 `latest.zip` 复制为 `Pipi.zip`，执行 `gh release create v<版本> Pipi.zip --title "v<版本>" --generate-notes`。
3. 更新 [blackappleD/DalamudPlugins](https://github.com/blackappleD/DalamudPlugins) 的 `repo.json`。

## 许可证

Pipi 以 [GNU AGPL v3](LICENSE) 授权。

`Pipi/Features/BetterMarketBoard/` 下的模块源码移植自 Daily Routines（AGPL-3.0），每个文件头都注明了来源与修改内容：
- `BetterMarketBoard*.cs`：来自 [DailyRoutines.ModulesPublic](https://github.com/Dalamud-DailyRoutines/DailyRoutines.ModulesPublic) `7569a18`。
- `Compat/Common/`：来自 [DailyRoutines.Common](https://github.com/Dalamud-DailyRoutines/DailyRoutines.Common) `2d1b52e`。
- `Compat/` 其余文件为 Pipi 编写的兼容层（替代 Daily Routines 框架与 OmenTools 的相关接口；Universalis 客户端按其公开 API 实现，界面文本由 Pipi 撰写）。
