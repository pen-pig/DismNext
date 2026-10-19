# Dism-Plus-Next

开源、现代化重写的 Windows 系统维护工具，复用经典 **Dism++** 的 `Data.xml` 规则数据与多语言资源，采用全新引擎实现。

> **背景**：原 `Dism++.exe` 应用程序为闭源免费软件，GitHub 仅公开其资源仓库（`pen-pig/Dism-plus-plus`，含 `Data.xml`、`Languages/`、`RCMake/` 等）。本项目的目标是以干净室方式，基于这些公开资源重新实现一个**可自由修改、可构建**的开源替代版。

## 功能

| 模块 | 状态 | 说明 |
|---|---|---|
| 空间回收 (CleanCollection4) | ✅ 可用 | 按 `Data.xml` 规则扫描并清理系统垃圾文件（下载缓存、缩略图、日志、临时文件、组件存储等），扫描阶段只读、不改动系统 |
| 系统优化 (SystemOptimization) | ✅ 可用 | 163 项注册表/服务级优化，支持 Combo 多选项结构，应用"推荐默认"配置 |
| 备份 / 还原 | ✅ 引擎+CLI | 基于 `dism.exe` 的 WIM 映像捕获/应用/列举（GUI 集成待补） |
| 多语言 | ✅ 可用 | 复用仓库 19 个语言包，运行时可切换 |
| 自定义清理过程 | ✅ 可用 | `Dism*` 映射到真实 `dism/compact` 命令；`NC*` 映射到等效 PowerShell 脚本；高风险项明确跳过并提示 |

## 架构

```
DismPlusNext/            WPF GUI（.NET 10 / WPF，暗色主题，MVVM）
  Models/                规则数据模型（与 Data.xml 结构对应）
  Data/                  DataXmlLoader —— 解析 Data.xml（含嵌套 Group / Combo 结构）
  Localization/          Localizer —— 加载语言包并本地化
  Engine/                CleanEngine / ConditionEvaluator / ActionExecutor / BackupRestore
  ViewModels/           MainViewModel（绑定层）
DismPlusNext.Cli/        命令行界面（脚本化 / 无显示环境可用）
DismPlusNext.Tests/      无头逻辑验证（解析/本地化/条件/扫描）
Data.xml, Languages/     来自上游资源仓库（规则与多语言）
```

## 构建

需要 **.NET 10 SDK**（本项目在用户目录安装了独立 SDK，未污染系统）。

```bash
dotnet build DismPlusNext/DismPlusNext.csproj -c Debug
dotnet build DismPlusNext.Cli/DismPlusNext.Cli.csproj -c Debug
```

GUI 程序需要**管理员权限**（注册表/服务/组件存储操作），已在 `app.manifest` 声明。

## 使用

### 命令行（CLI）

```bash
# 列出模块与适用项
DismPlusNext.Cli list

# 扫描空间回收项（只读，报告可回收空间）
DismPlusNext.Cli scan CleanCollection4

# 扫描系统优化项
DismPlusNext.Cli scan SystemOptimization

# 实际清理（破坏性，需 --yes 确认）
DismPlusNext.Cli apply CleanCollection4 --yes

# 备份 / 还原（基于 DISM）
DismPlusNext.Cli backup D:\backup.wim -d C:\ -n MyPC
DismPlusNext.Cli images D:\backup.wim
DismPlusNext.Cli restore D:\backup.wim -i 1 -d C:\ --yes
```

### GUI

运行 `DismPlusNext.exe`，左侧选择模块 → 点击"扫描"查看可回收空间/优化项 → 勾选后"清理/应用"（会二次确认）。

## 安全说明

- **扫描（预览）阶段完全只读**，仅枚举文件、计算大小、读取注册表，不修改系统。
- **应用/清理阶段**会实际删除文件、写入注册表、调用 `dism.exe`，执行前均有确认对话框（GUI）或 `--yes` 标志（CLI）。
- 自定义清理过程中标注为"高风险"的内部例程（如 Installer 目录、VS Package Cache）默认跳过，避免破坏系统修复能力。

## 许可证

本程序复用上游资源（`Data.xml`、语言包等），上游许可证为 **MIT**（Chuyu-Team，见仓库根 `LICENSE`）。本重实现遵循同样的开源精神，保留原作者版权声明。

## 已知 TODO

- GUI 接入"备份/还原"模块
- 系统优化项支持用户在下拉选项中**自选**目标值（当前应用"推荐默认"）
- 接入 `DefaultFilterList` 作为扫描排除规则
- 增量更新 `Data.xml` 规则以适配最新 Windows 10/11（原仓库自 2019 年起未更新）
