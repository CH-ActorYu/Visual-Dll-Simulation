# Visual - 视觉识别与距离

基于 .NET 10 + WPF 的视觉软件。当前功能：在视频画面中圈选目标，标定后实时输出目标距离。

代码按"共享基础库 + 业务模块"组织，每个业务模块编译为独立类库，可直接被其他 .NET 项目引用（不依赖 WPF、不需要安装本软件）。

详细需求见 [docs/01-需求规格说明书](docs/01-需求规格说明书.md)。

## 技术栈

- .NET 10 / WPF
- MahApps.Metro、MaterialDesignInXaml
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- OpenCvSharp4 + OpenCvSharp4.runtime.win

## 项目结构

```text
Visual/
├─ src/
│  ├─ shared/
│  │  ├─ Visual.Abstractions/    # 通用值对象、结果基类、异常、模块入口
│  │  ├─ Visual.IO/              # 帧源采集：摄像头 / 视频文件 / 图片序列
│  │  └─ Visual.VisionBase/      # 预处理与分析算子、检测跟踪基类、几何换算
│  ├─ modules/
│  │  └─ Visual.Distance/        # 测距模块：Contracts 对外，Internal 对内
│  └─ app/
│     ├─ Visual.App/             # WPF 主程序
│     └─ Visual.AppCore/         # ROI 交互、叠加渲染、配置持久化
├─ tests/                        # 无 UI 测试，不依赖摄像头
└─ samples/
   └─ Visual.Distance.ConsoleDemo/
```

依赖方向单向：app → modules → shared；模块之间互不引用。

## 测距模块用法

```csharp
var svc = new DistanceServiceBuilder().Build();
svc.SetCalibration(calibration);                 // 参照物真实尺寸 + 像素尺寸 + 拍摄距离
var results = await svc.MeasureAsync(frame, rois); // 每个 ROI 返回一条 DistanceMeasurement
```

未标定调用会抛 `NotCalibratedException`。实时场景可订阅 `IObservable<IReadOnlyList<DistanceMeasurement>>`。

## 本期范围

- 实现：单相机、单 ROI、单目标；单目 + 标定测距；软件闭环
- 接口已按多 ROI、多帧源设计，本期 UI 只暴露单路
- 不做：缺陷检测、路径分析等其他业务模块（算子在 VisionBase 里，有需要再立项）

## 阶段

1. shared 三层 + Distance 模块 + 软件闭环
2. ConsoleDemo + 模块测试，验证类库脱离软件可用
3. 多目标/多帧源、识别算法增强、性能优化
