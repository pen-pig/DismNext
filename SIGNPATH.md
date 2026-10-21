# SignPath 开源免费代码签名

本仓库通过 GitHub Actions + [SignPath](https://signpath.io) 对构建产物做 Authenticode 代码签名，
解决“本地构建的未签名 exe/dll 被卡巴斯基等杀软误报/隔离”的问题。

## 工作原理

`.github/workflows/build.yml` 每次 push 到 master 时：

1. 在云端 windows-latest runner 构建 WPF + CLI，并跑回归测试（本地有杀软锁 DLL 时，这是唯一可信验证）。
2. 上传未签名 artifact `DismPlusNext-unsigned`。
3. 若仓库配置了 `SIGNPATH_API_TOKEN` 等 secret，则提交签名请求，等待 SignPath 用其 OV 证书签名，
   下载签名结果并上传为 `DismPlusNext-signed`；未配置则自动跳过，不影响构建。

## 申请 SignPath 免费证书（约 10 分钟）

1. 打开 <https://signpath.io/oss>，用 GitHub 账号登录并提交开源项目申请。
   - 要求：公开仓库、OSI 认可的开源许可证（本仓库为 MIT）、项目处于活跃维护状态。
2. 申请通过后进入 SignPath 控制台，创建：
   - **Project**（如 `Dism-Plus-Next`）
   - **Signing Policy**（如 `release-signing`；OSS 项目通常要求从 CI 构建产物签名）
   - **Artifact Configuration**：根元素类型选 `<zip-file>`（workflow 上传的是 zip artifact），
     并配置对 `*.exe` / `*.dll` 签名。
3. 在用户设置中创建 **API Token**（submitter 权限，绑定上述项目/策略）。

## 配置仓库 Secrets

仓库页面 → Settings → Secrets and variables → Actions → New repository secret，添加：

| Secret | 值 |
|---|---|
| `SIGNPATH_API_TOKEN` | SignPath API Token |
| `SIGNPATH_ORGANIZATION_ID` | SignPath 组织 ID（控制台 URL 中可见） |
| `SIGNPATH_PROJECT_SLUG` | 项目 slug |
| `SIGNPATH_SIGNING_POLICY_SLUG` | 签名策略 slug |

配置后下次 push 即自动签名；在 Actions 运行页下载 `DismPlusNext-signed` artifact 即为签名版。

## 说明

- 签名能显著降低（对有效签名）误报，但无法 100% 保证所有杀软都不报；云端哈希流行度需要随分发量积累。
- 本地开发调试仍建议给项目目录加杀软排除项（卡巴斯基：设置 → 附加 → 威胁和排除项 → 排除项 → 添加文件夹）。
