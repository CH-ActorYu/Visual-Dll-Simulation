# Visual

**Visual** 是一套面向视觉处理的功能组件库，附带一个 WPF 桌面端用于操作和验证。

项目按业务功能拆分成独立类库，统一以 `Visual.<功能>` 命名。业务模块不依赖 WPF，也不直接依赖某一种视觉引擎；具体引擎通过适配器接入。

当前包含的组件：

- `Visual.Distance`：在视频画面中圈选目标，标定后实时输出目标距离（单目 + 标定）

当前默认使用 OpenCV 实现。HALCON、VisionPro 等厂商 SDK 作为可选适配器，只有在具备实际设备、授权和应用需求时再接入；VTK 单独用于后续三维/点云方向，不进入当前二维测距链路。

仓库名沿用最初的"视觉识别与距离"（Visual Recognition and Distance），后续新增功能按 `Visual.<功能>` 扩展，不局限于此。

完整文档入口见 [docs/README](docs/README.md)，详细需求见 [docs/01-需求规格说明书](docs/01-需求规格说明书.md)。

## 技术栈

- .NET 10 / WPF
- MahApps.Metro、MaterialDesignInXaml
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- 视觉引擎适配：OpenCV（当前实现）；HALCON、VisionPro（预留）
- 三维扩展：VTK（后续独立方向）

## 项目结构

```text
Visual/
├─ src/
│  ├─ shared/
│  │  ├─ Visual.Abstractions/    # 通用值对象、结果基类、异常、模块入口
│  │  ├─ Visual.Image/            # 图像帧、像素格式、内存所有权与生命周期
│  │  ├─ Visual.IO/              # 帧源、设备及采集生命周期契约
│  │  ├─ Visual.Vision.Contracts/ # 检测、跟踪、预处理等与引擎无关的接口
│  │  └─ Visual.VisionBase/      # 引擎无关的几何、结果整理和公共流程
│  ├─ engines/
│  │  ├─ Visual.Engine.OpenCv/   # OpenCV 适配器（当前实现）
│  │  ├─ Visual.Engine.Halcon/   # HALCON 适配器（预留）
│  │  └─ Visual.Engine.VisionPro/ # VisionPro 适配器（预留）
│  ├─ modules/
│  │  └─ Visual.Distance/        # 测距模块：Contracts 对外，Internal 对内
│  └─ app/
│     ├─ Visual.App/             # WPF 主程序
│     └─ Visual.AppCore/         # ROI 交互、叠加渲染、配置持久化
├─ tests/                        # 无 UI 测试，不依赖摄像头
└─ samples/
   └─ Visual.Distance.ConsoleDemo/
```

依赖方向单向：业务模块依赖共享契约与公共流程；引擎适配器实现共享契约并依赖对应 SDK；App 作为组合根同时装配业务模块和已选适配器。业务模块不得直接引用 OpenCV、HALCON、VisionPro 或任何 `Visual.Engine.*` 工程。

> 此处仅为结构概述；各项目的类级设计（类名、方法、作用）见 [docs/02-结构与类设计](docs/02-结构与类设计.md)。

## 测距模块用法

```csharp
var engine = engineProvider.Get("OpenCv");
var profile = DetectionProfile.CreateDefault();
var svc = new DistanceServiceBuilder()
    .WithEngine(engine)
    .WithDetectionProfile(profile)
    .Build();
svc.SetCalibration(calibration); // 已知目标宽度 + 参考像素宽度 + 参考距离

var targets = new[] { new TargetRegion("target-1", roi) };
var request = new DistanceRequest(frame, targets);
var results = await svc.MeasureAsync(request, cancellationToken);
```

未标定单帧调用会抛 `NotCalibratedException`。实时场景使用 `MeasureStreamAsync(...)` 返回的 `IAsyncEnumerable<IReadOnlyList<DistanceMeasurement>>`，通过 `CancellationToken` 停止。

## 本期范围

- 实现：单相机、单 ROI、单目标；单目 + 标定测距；软件闭环
- 接口已按多 ROI、多帧源设计，本期 UI 只暴露单路
- Vision.Contracts 定义灰度、二值化、滤波、轮廓、缺陷区域等通用能力接口，VisionBase 负责编排公共流程，具体 OpenCV 实现在引擎适配器中；本期不做缺陷判定、路径分析等独立业务。
- 当前只实现 OpenCV 引擎适配器；HALCON、VisionPro 只保留接入边界，不在本期实现。

## 阶段

1. 通用契约、图像帧、视觉接口、OpenCV 适配器
2. Distance 模块 + 软件闭环 + 模块测试
3. 多目标/多帧源、其他引擎适配、性能优化
