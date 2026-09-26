# Pipi

blackappleD 自用的 Dalamud（卫月国服）小功能合集插件。

## 功能

### 停靠折叠

ImGui 无法折叠已停靠的窗口。Pipi 让浮动停靠组可以收起到只剩标签栏：

- 双击浮动停靠组**顶部标签栏的空白处**（不是标签本身）折叠，再次双击恢复原高度。
- 展开高度会被记住，重启游戏后仍然有效。
- `/pipi fold` 切换所有浮动停靠组（有展开的就全部折叠，否则全部展开）。
- `/pipi fold on` 全部折叠，`/pipi fold off` 全部展开。
- 把指令写进用户宏并放上热键栏，即可当快捷键使用。

已知限制：

- 若 ImGui 的窗口最小高度大于标签栏高度，折叠后会保留一条窄边。
- 主视口上的停靠空间不支持；只含一个窗口的浮动节点使用原生标题栏折叠。
- 依赖 ImGui 内部接口，Dalamud 升级后可能需要适配。

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
