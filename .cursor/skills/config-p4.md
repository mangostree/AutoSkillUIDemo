---
name: config-p4
description: 配置当前 workspace 根目录的 Perforce (P4) client，自动检测并写入 P4CONFIG 文件
---

# config-p4 — P4 客户端配置

配置当前 workspace 根目录的 P4 client。只有在根目录存在明确的 `P4CONFIG` 配置后，才继续任何原生 `p4` 操作；不要直接依赖当前机器上已打开的 Perforce workspace 状态。

步骤如下：

1. 在终端运行以下命令获取当前 workspace 对应的 P4 client：

   ```powershell
   powershell -ExecutionPolicy Bypass -File "ExternalTools/DevEnvUtility/Find-P4Client.ps1" -Path "$cwd"
   ```

2. 确定 P4CONFIG 文件名格式：
   - 运行 `p4 set -q P4CONFIG` 命令，解析输出以确定当前机器上的 P4CONFIG 文件名格式
   - 提取文件名（可能是 `.p4config`、`p4config.txt` 等）
   - 如果命令失败或输出为空，默认使用 `p4config.txt`

3. 根据步骤 1 的返回结果：
   - 如果返回非空字符串（client 名称）：在当前 workspace 根目录读取或创建步骤 2 确定的 P4CONFIG 文件，添加或更新 `P4CLIENT=<client名>` 行
   - 如果返回空字符串：告知用户"当前 workspace 不在任何 P4 client 的 root 目录下，请手动配置"

4. 完成后告知用户配置结果，并提示后续 P4 操作统一使用原生 `p4` 命令
