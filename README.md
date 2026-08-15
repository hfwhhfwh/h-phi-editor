# HPhiEditor

基于 Godot 4 (C#) 开发的音游谱面编辑器，支持 RPE (Re:PhiEdit) 格式谱面的创建、编辑与预览。

> **⚠️ 官方声明**：本项目完全免费开源。唯一官方渠道为 GitHub: https://github.com/hfwhhfwh/h-phi-editor/。
> 任何第三方付费版本均与原作者无关。

## 功能特性

- **谱面管理**：创建、导入、导出、删除谱面，支持曲绘与音频文件管理
- **多判定线编辑**：支持添加、删除、编辑多条判定线（JudgeLine）
- **Note 编辑**：支持多种 Note 类型的添加、删除、移动与属性编辑
- **事件编辑**：支持判定线的移动、旋转、透明度、速度等事件曲线编辑（含缓动类型与贝塞尔曲线）
- **实时预览**：支持谱面播放、暂停、进度跳转，带音频同步
- **资源包系统**：支持自定义资源包（皮肤、音效、打击特效等）的加载与管理
- **属性面板**：选中对象后可在右侧面板实时编辑详细属性
- **框选与拖拽**：支持框选多个对象、拖拽放置 Note/事件
- 更多功能正在开发中

## 技术栈

- **引擎**：Godot 4.6 stable mono
- **语言**：C# (.NET)
- **渲染**：Forward Plus / Mobile 渲染器

## 项目结构

```
Script/
├── Models/              # 数据模型（谱面、Note、事件等）
├── EditorScene/         # 编辑器主场景逻辑
│   ├── EditorScene.cs   # 主控制器
│   ├── NoteEditPanel.cs # Note 编辑面板
│   ├── EventEditPanel.cs# 事件编辑面板
│   └── ...
├── Editors/             # 属性编辑器组件（浮点、布尔、缓动等）
├── UI/                  # 通用 UI 组件
├── StartMenu/           # 开始菜单（谱面列表、创建、导入）
├── ResourcePack/        # 资源包加载与管理
├── ChartPlayer.cs       # 谱面播放器
├── ChartRenderer.cs     # 谱面渲染器
└── ChartService.cs      # 谱面业务逻辑服务
Scene/                   # Godot 场景文件
```

## 运行方式

1. 安装 [Godot 4.6](https://godotengine.org/) 并配置 .NET 环境
2. 克隆本仓库
3. 在 Godot 中打开项目根目录
4. 点击运行（F5）

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.
