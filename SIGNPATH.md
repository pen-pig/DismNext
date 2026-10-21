# SignPath 开源免费代码签名

- 项目名（申请表填）：**DismNext**
- 仓库地址（申请表填）：<https://github.com/pen-pig/DismPlusNext>

本仓库通过 GitHub Actions + [SignPath](https://signpath.io) 对构建产物做 Authenticode 代码签名，
解决“本地构建的未签名 exe/dll 被卡巴斯基等杀软误报/隔离”的问题。

- 开源免费计划（SignPath Foundation）：<https://signpath.org/>
- 申请入口：<https://signpath.org/apply.html>
- OSS 条款全文：<https://signpath.org/terms>

## 工作原理

`.github/workflows/build.yml` 每次 push 到 master 时：

1. 在云端 windows-latest runner 构建 WPF + CLI，并跑回归测试（本地有杀软锁 DLL 时，这是唯一可信验证）。
2. 上传未签名 artifact `DismPlusNext-unsigned`。
3. 若仓库配置了 `SIGNPATH_API_TOKEN` 等 secret，则提交签名请求，等待 SignPath Foundation 证书签名，
   下载签名结果并上传为 `DismPlusNext-signed`；未配置则自动跳过，不影响构建。

## 申请流程

1. 在 <https://signpath.org/apply.html> 提交申请，需提供：
   - 公开仓库 URL（本仓库）
   - 许可证名称（MIT，根目录有 LICENSE）
   - 下载/发布页 URL（需先有 GitHub Release）
   - 项目描述（用途、目标用户、发布产物形式）
   - 可收信的联系方式（审核以邮件沟通，建议用英文）
2. 审核通常约 1 周。通过后获得 SignPath Foundation 签发的 OV 证书（私钥存于其 HSM，不下发）。
3. 控制台创建：Project / Signing Policy（如 `release-signing`）/ Artifact Configuration
   （根元素类型选 `<zip-file>`，配置对 `*.exe`、`*.dll` 签名）。
4. 创建 submitter 权限的 API Token。

## 配置仓库 Secrets

仓库 Settings → Secrets and variables → Actions → New repository secret：

| Secret | 值 |
|---|---|
| `SIGNPATH_API_TOKEN` | SignPath API Token |
| `SIGNPATH_ORGANIZATION_ID` | SignPath 组织 ID（控制台可见） |
| `SIGNPATH_PROJECT_SLUG` | 项目 slug |
| `SIGNPATH_SIGNING_POLICY_SLUG` | 签名策略 slug |

配置后下次 push 即自动签名；Actions 页下载 `DismPlusNext-signed` 即签名版。

## 本项目需注意的条款风险（见 signpath.org/terms）

Foundation 条款里有几条对本项目不利，审核可能被拒：

- **不得含非开源组件**：本仓库是 Chuyu-Team Dism++ 的 fork，需确认上游代码许可允许再分发。
- **不得含“规避安全措施/识别利用漏洞”类功能**：本项目的系统优化、服务/注册表/驱动管理属于灰色地带，
  条款明确不豁免运行时诊断类功能，只有开发期扫描工具被豁免。
- **必须已有可下载的发布版本**：审核要求先发 GitHub Release。
- **所有提交者需启用 MFA**，并建立 Author/Reviewer/Approver 角色。

若被拒的备选：Microsoft Trusted Signing（需 Partner，费用低、SmartScreen 信誉起量快）、
自签证书+引导用户导入（AV 不信任，仅内部可用）。

## 说明

- 签名可显著降低误报，但无法 100% 保证所有杀软都不报；云端哈希流行度需随分发量积累。
- 本地开发调试仍建议给项目目录加杀软排除项（卡巴斯基：设置 → 附加 → 威胁和排除项 → 排除项 → 添加文件夹）。