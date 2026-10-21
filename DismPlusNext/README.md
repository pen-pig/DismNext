# DismNext（GUI 工程）

Windows WPF 界面工程（.NET 10）。项目说明、功能列表、构建与使用文档见仓库根目录 [`README.md`](../README.md)。

```
DismPlusNext/            WPF GUI（暗色主题，MVVM）
  Models/                规则数据模型（对应 Data.xml 结构）
  Data/                  DataXmlLoader —— 解析 Data.xml（含嵌套 Group / Combo）
  Localization/          Localizer —— 加载语言包并本地化
  Engine/                引擎：清理 / 条件评估 / 动作执行 / 备份还原 / 离线映像 /
                         服务 / 账户 / 更新 / 驱动 / 任务 / Hosts / 文件关联
  UI/                    MainWindow、BackupWindow、OfflineWindow
  ViewModels/            MainViewModel + NativeModules（模块装配）
DismPlusNext.Cli/        命令行界面
DismPlusNext.Tests/      无头回归测试
```

工程目录名/程序集名保留 `DismPlusNext` 以避免大范围重命名，产品名为 **DismNext**。