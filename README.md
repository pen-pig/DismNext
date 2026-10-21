# DismNext

开源的 Windows 系统维护工具（C# / .NET 10 / WPF）：空间回收、系统优化、驱动/服务/计划任务管理、
离线映像服务、备份还原等，全部功能可在 GUI 与 CLI 中使用。

DismNext 是对经典闭源免费软件 **Dism++** 的干净室开源重写：只复用其公开的资源数据
（`Data.xml` 清理/优化规则、19 种语言包），引擎全部为新写的 C# 实现，**不包含 Dism++ 的任何二进制或闭源代码**。

## 下载

预编译版本（签名版）发布在 **GitHub Releases**：<https://github.com/pen-pig/DismPlusNext/releases>
未签名版本可在 Actions 运行页下载 `DismPlusNext-unsigned` artifact。

> 运行清理/优化/离线映像/服务等操作需要**管理员权限**。

## 功能

| 模块 | 状态 | 说明 |
|---|---|---|
| 空间回收 | 可用 | 按 `Data.xml` 规则扫描并清理系统垃圾（下载缓存、缩略图、日志、临时文件、组件存储等）。**扫描阶段完全只读** |
| 系统优化 | 可用 | 160+ 项注册表/服务级优化，支持多选项结构，可应用推荐默认值，也可逐项自选目标值 |
| 系统信息 | 可用 | 操作系统版本/架构/卷空间/内存等概览 |
| 启动项 | 可用 | 注册表 Run 键、启动文件夹、开机/登录触发的计划任务；禁用时自动备份，可还原 |
| 应用管理 | 可用 | 已安装程序枚举与卸载入口 |
| 可选功能 | 可用 | Windows 可选功能（Feature on Demand）启用/禁用 |
| 更新管理 | 可用 | 已安装更新（HotFix）列表与卸载 |
| 计划任务 | 可用 | 计划任务枚举、启用/禁用、立即运行、删除 |
| 服务 | 可用 | 服务枚举、启动/停止、修改启动类型 |
| 驱动管理 | 可用 | 第三方驱动包枚举与删除（`pnputil`） |
| 账户管理 | 可用 | 本地账户枚举、启用/禁用 |
| Hosts 编辑 | 可用 | hosts 解析、注释/启用条目、增删记录 |
| 文件关联浏览 | 只读 | 浏览器默认打开程序与"打开方式"覆盖状态 |
| 离线映像服务 | 可用 | 用系统自带 `dism.exe` 挂载 WIM/ESD，使清理与优化直接作用于离线映像 |
| 备份还原 | 可用 | 基于 `dism.exe` 的 WIM 映像捕获 / 应用 / 列举 |
| 多语言 | 可用 | 复用 19 种语言包，运行时可切换 |

## 构建

需要 **.NET 10 SDK**：

```bash
dotnet build DismPlusNext/DismPlusNext.csproj -c Release        # WPF GUI
dotnet build DismPlusNext.Cli/DismPlusNext.Cli.csproj -c Release # CLI
dotnet run --project DismPlusNext.Tests -c Release              # 无头回归测试
```

仓库内置 GitHub Actions（`.github/workflows/build.yml`）：每次 push 在云端 runner 完成构建与回归测试，
上传 artifact，并在配置了 [SignPath](SIGNPATH.md) 密钥时自动完成 Authenticode 代码签名。

## 命令行

```bash
# 列出模块与可执行项
DismNext.Cli list

# 扫描（只读）
DismNext.Cli scan CleanCollection4
DismNext.Cli scan SystemOptimization

# 执行（破坏性操作需 --yes 确认）
DismNext.Cli apply CleanCollection4 --yes

# 备份 / 还原 / 映像
DismNext.Cli backup D:\backup.wim -d C:\ -n MyPC
DismNext.Cli images D:\backup.wim
DismNext.Cli restore D:\backup.wim -i 1 -d C:\ --yes

# 离线映像
DismNext.Cli offline wiminfo D:\backup.wim
DismNext.Cli offline mount D:\backup.wim M:\img
DismNext.Cli offline unmount M:\img

# 系统类模块
DismNext.Cli services list
DismNext.Cli tasks list
DismNext.Cli hosts list
DismNext.Cli updates list
DismNext.Cli drivers list
DismNext.Cli accounts list
```

## GUI

运行 `DismNext.exe`：左侧选择模块 → "扫描"预览（只读）→ 勾选 → "清理/应用"（二次确认）。

## 安全设计

- **扫描/预览阶段完全只读**：仅枚举文件、计算大小、读取注册表，不修改系统。
- **执行阶段**会删除文件、写注册表、调用 `dism.exe` 等系统工具，GUI 有确认对话框、CLI 需 `--yes`。
- 标注为高风险的内部例程（如 Installer 目录、VS Package Cache）默认跳过，避免破坏系统修复能力。
- 服务/账户等模块使用原生 Windows API（advapi32 / netapi32 / 注册表），尽量不派生额外进程。

## 许可证

**MIT**（见根目录 `LICENSE`）。

- 本重写的代码与文档：MIT。
- `Data.xml`、`Languages/`、`www.chuyu.me/`、`chuyu-analysis/` 等资源来自 Chuyu-Team 的公开仓库
  `Dism-plus-plus`，同为 MIT，版权归 [Chuyu-Team](https://github.com/Chuyu-Team) 所有。
  语言包贡献者名单见 [`Languages/README.md`](Languages/README.md)。

## 致谢

- [Chuyu-Team](https://github.com/Chuyu-Team) —— `Dism++` 原版及其公开的资源仓库（规则数据与语言包）。
- 社区各位语言包贡献者。