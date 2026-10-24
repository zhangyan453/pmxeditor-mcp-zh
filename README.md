# PMXEditor MCP 中文版

把 PMXEditor（MikuMikuDance 模型编辑器）变成一个 MCP 服务器：AI 客户端可以直接读取和编辑当前打开的 PMX 模型，不需要额外的桥接进程。

本仓库是 [chiqurjin/PMXEditor_MCP_Plugin](https://github.com/chiqurjin/PMXEditor_MCP_Plugin) 0.2.0 的简体中文分支（MIT），并针对 PMXEditor 0254e 做了兼容适配。

> PMXEditor 是 極北P (kkhk22) 的著作物。本插件是**非官方**插件，与 PMXEditor 作者无关，请勿向 PMXEditor 作者反馈本插件的问题。

## 本分支改了什么

- 52 个 MCP 工具描述、259 条参数描述全部翻译为简体中文；工具名和参数名保持英文，调用方式不变。
- `capture_viewport` 改为用 Win32 `PrintWindow` 抓取 PmxView 窗口。PMXEditor 0254e 的 `IPXPmxViewConnector` 没有上游使用的 `GetClientImage()`，这样截图也不会混入编辑器面板。
- 使用 PMXEditor 0254e（PEPlugin 0.0.8.4）编译，在 Windows + .NET Framework 4.8 环境实测通过；未在 0273 上验证。
- 其余功能与上游 0.2.0 保持一致。

## 功能

共 52 个工具，覆盖：

- 模型信息、PMX 文件头、打开/保存、撤销/重做
- 骨骼（含 IK、追加变形、固定轴、局部坐标系）
- 顶点、面、材质
- 表情及其各类偏移
- 显示枠
- 刚体、Joint、SoftBody（PMX 2.1）
- 当前选择、相机、视口截图

完整参数说明见 [docs/TOOLS.md](docs/TOOLS.md)（上游日文/英文文档）。MCP 客户端实际收到的是中文描述。

## 安装

要求：Windows、PMXEditor 0254e（或 PEPlugin API 兼容的版本）、.NET Framework 4.x。

1. 复制 [dist/PmxMcpPlugin.dll](dist/PmxMcpPlugin.dll) 到 PMXEditor 安装目录的 `_plugin\User\`。
2. 启动 `PmxEditor_x64.exe`。插件随编辑器启动，并在 DLL 旁生成 `PmxMcpPlugin.json`。
3. MCP 地址：`http://127.0.0.1:38731/mcp`。

Trae 手动配置：

```json
{
  "mcpServers": {
    "pmx-editor": {
      "url": "http://127.0.0.1:38731/mcp"
    }
  }
}
```

Claude Code：

```bash
claude mcp add --transport http pmx-editor http://127.0.0.1:38731/mcp
```

PMXEditor 必须先运行，MCP 客户端才能连接。

## 从源码构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 `
  -PmxEditorPath "C:\PmxEditor" -Install
```

`-Install` 会把编译结果复制到 `_plugin\User`；重启 PMXEditor 后生效。构建只引用你本机的 PMXEditor DLL，仓库不包含也不转发这些文件。

## 使用示例

接入 MCP 后可以直接用自然语言下指令，例如：

- “打开 `D:\MMD\model.pmx`，告诉我骨骼和材质数量。”
- “把左腕骨骼绕 Z 轴旋转 30 度，然后撤销。”
- “把材质 3 的 diffuse 改成红色，边缘变细。”
- “把相机转到正面，截一张图。”
- “把当前模型另存到 `D:\out\model.pmx`。”

工具名保持英文，例如 `list_bones`、`set_bone`、`capture_viewport`。

## 配置与安全

`PmxMcpPlugin.json` 会在 DLL 旁生成，字段如下：

```json
{
  "host": "127.0.0.1",
  "port": 38731,
  "path": "/mcp",
  "token": "",
  "allowWrite": true,
  "allowFileAccess": true,
  "logFile": ""
}
```

- 默认只监听 `127.0.0.1`，不对外网开放。
- `token` 为空时不校验 `Authorization`；需要时请设置 Bearer token。
- `allowWrite` 控制写操作，`allowFileAccess` 控制打开/保存文件。
- AI 可能覆盖现有 PMX 文件，操作前请备份；建议先另存为，再覆盖原文件。
- 一次只能打开一个模型；截图需要 PmxView 窗口可见。

## 目录结构

```text
.
├─ src/PmxMcpPlugin/       插件源码（C#）
├─ dist/PmxMcpPlugin.dll   编译产物
├─ docs/                   上游文档与安装手册
├─ tests/                  MCP 冒烟测试脚本
├─ build.ps1               构建/安装脚本
├─ README.upstream.md      上游原始说明（日文/英文）
├─ LICENSE                 MIT 许可证
└─ NOTICE                  版权、非官方声明与再分发说明
```

## 许可证与致谢

- 本仓库及上游代码采用 [MIT License](LICENSE)。
- 上游项目：[chiqurjin/PMXEditor_MCP_Plugin](https://github.com/chiqurjin/PMXEditor_MCP_Plugin)
- PMXEditor：極北P (kkhk22)。本仓库不包含、不转发 `PmxEditor*.exe`、`PEPlugin.dll`、`SlimDX.dll` 等 PMXEditor 本体文件。
- MCP（Model Context Protocol）：<https://modelcontextprotocol.io/>

本分支的汉化与兼容性改动同样以 MIT 许可证发布。再分发时请保留 `LICENSE` 和 `NOTICE`，并保留“非官方插件”的声明。

## 上游文档

- [上游 README（日文/英文）](README.upstream.md)
- [架构说明](docs/ARCHITECTURE.md)
- [工具参考](docs/TOOLS.md)
- [安装手册（PDF）](docs/INSTALL-MANUAL-ja.pdf)
