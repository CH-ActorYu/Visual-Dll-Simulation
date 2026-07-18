# Visual - 视觉识别与距离

一款视觉功能软件（.NET 10 + WPF）。核心特点：**业务功能以独立模块 DLL 形式组织**，每个模块可单独拆出，被其他 .NET 项目直接引用使用。

本期功能：**实时目标测距**——在视频画面中圈选目标，基于标定参数实时输出目标距离。

> 完整需求、约束、输入输出与验收标准见 [docs/01-需求规格说明书](docs/01-需求规格说明书.md)（V3.4 基线）。

---

## 1. 产品形态

交付物分两层：

| 层 | 交付物 | 说明 |
| --- | --- | --- |
| 软件层 | `Visual.App`（WPF 可执行程序） | 视频源、ROI 圈选、标定、实时叠加显示 |
| 模块层 | `Visual.Distance.dll` 等功能模块 | 纯类库、零 UI 依赖；外部项目添加引用即可调用 |

典型消费场景：[HardwareModularWorkflow](https://github.com/CH-ActorYu/HardwareModularWorkflow) 硬件工作流平台引用测距 DLL 组，把"实时测距"作为工作流中的一个硬件步骤调用。

```
┌─────────────────────────────┐   ┌──────────────────────────────────┐
│ Visual.App (WPF 软件)        │   │ HardwareModularWorkflow          │
│  └─ 引用 → Visual.Distance  │   │  └─ OpenCvCameraDriver           │
│            .dll 组           │   │       └─ 引用 → 测距 DLL 组       │
└─────────────────────────────┘   └──────────────────────────────────┘
              同一组 DLL，两种消费方式
```

---

## 2. 技术栈

- **运行时与框架**：.NET 10 / WPF
- **UI 组件库**：MahApps.Metro、MaterialDesignInXaml
- **MVVM**：CommunityToolkit.Mvvm
- **依赖注入**：Microsoft.Extensions.DependencyInjection
- **视觉处理**：OpenCvSharp4 + OpenCvSharp4.runtime.win
- **模型推理（预留）**：ONNX Runtime

---

## 3. 分层结构

```text
Visual/
├─ src/
│  ├─ shared/                        # 共享基础层（独立 DLL，被所有模块依赖）
│  │  ├─ Visual.Abstractions/        #   通用值对象 / 结果基类 / 异常 / 模块入口 IVisionModule
│  │  ├─ Visual.IO/                  #   帧源采集：IFrameSource（摄像头 / 视频文件 / 图片序列）
│  │  └─ Visual.VisionBase/          #   视觉公共件：预处理与分析算子、检测跟踪基类、几何换算
│  ├─ modules/                       # 业务模块层（对外交付的 DLL）
│  │  └─ Visual.Distance/            #   ★ 测距模块（本期）：Contracts 对外，Internal 对内
│  └─ app/                           # 软件层（WPF 耦合，不对外分发）
│     ├─ Visual.App/                 #   可执行程序（View / ViewModel / 启动）
│     └─ Visual.AppCore/             #   ROI 交互、叠加渲染、菜单挂载、配置持久化
├─ tests/                            # 各层独立测试（无 UI，不依赖摄像头）
└─ samples/
   └─ Visual.Distance.ConsoleDemo/   # 无 UI 调用示例（验证模块可拆性）
```

**依赖方向（单向）**：App → modules → shared（VisionBase / IO → Abstractions）。业务模块之间禁止互相依赖。

**DLL 拆分原则**：DLL 边界 = 独立消费边界 + 独立版本化需求；层内细分用命名空间解决，不滥用程序集。基础（通用算子）与业务（含结论输出）的归类判定见 SRS §4.5。

---

## 4. 模块对外契约（以 Distance 为例）

- 入口：`IDistanceService`（`SetCalibration` / `MeasureAsync(frame, rois)` / 配置 JSON 导入导出）
- 输出：`IReadOnlyList<DistanceMeasurement>`——距离值、单位、置信度、目标中心、`TargetId`、`SourceId`、时间戳
- 实时模式：`IObservable<IReadOnlyList<DistanceMeasurement>>` 订阅式连续测量
- 异常：未标定抛 `NotCalibratedException`，不返回假数据

最小调用序列：

```csharp
var svc = new DistanceServiceBuilder().Build();
svc.SetCalibration(calibration);
var results = await svc.MeasureAsync(frame, rois);
```

---

## 5. 分发方式

| 分发包 | 包含 DLL | 适用调用方 |
| --- | --- | --- |
| 完整测距包 | Distance + VisionBase + IO + Abstractions | 无相机体系的外部项目（默认） |
| 仅取帧包 | IO + Abstractions | 已有算法、只需采集的项目 |
| 仅算法包 | Distance + VisionBase + Abstractions | 自带帧源的宿主（如硬件工作流平台） |

---

## 6. 本期范围与预留

**本期实现**：单相机、单 ROI、单目标；单目 + 标定测距；软件闭环 + DLL 可拆。

**设计预留**（API/DTO 已兼容，本期不实现）：多目标（ROI 集合 + `TargetId`）、多相机（`SourceId`）、DNN/ONNX 识别增强（内部算法替换，契约不变）。

**明确不做**："识别""缺陷检测"等独立业务模块（无消费者；其基础算子已沉淀在 VisionBase）。

---

## 7. 分阶段落地

| 阶段 | 内容 |
| --- | --- |
| Phase 1（MVP） | shared 三层 + Distance 模块 + 软件闭环（视频/圈选/标定/实时叠加） |
| Phase 2（可拆验证） | ConsoleDemo + 模块测试 + DLL 分发包；宿主平台集成验证 |
| Phase 3（增强） | 多目标/多帧源启用、DNN 识别增强、性能优化 |

## 8. 验收口径（摘要）

- 软件完成"圈选 → 识别 → 标定 → 实时距离输出"闭环，精度误差 ≤ ±10%；
- **核心验收**：全新控制台项目仅引用 DLL 组即可跑通测距（无 WPF、无需安装本软件）；宿主平台集成成功；
- 720p 下测距 ≥ 15 次/秒；连续运行 2 小时无崩溃。

详见 SRS §9 验收场景 S1~S8。
