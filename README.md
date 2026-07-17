# Visual - 视觉识别与距离软件设计

## 1. 项目定位

Visual 是一款基于 **.NET 10 + WPF** 的桌面视觉软件。核心场景为：

1. 在视频画面中圈选目标图形（ROI）。
2. 对目标进行识别与跟踪。
3. 基于标定参数估算目标距离。
4. 对目标路径进行判断与规划，输出可视化结果。

本 README 聚焦于架构分层理念、分层职责与功能模块设计，作为后续实现与迭代的基线文档。

---

## 2. 技术栈

- **运行时与框架**：.NET 10, WPF
- **UI 组件库**：MahApps.Metro, MaterialDesignInXaml
- **MVVM**：CommunityToolkit.Mvvm
- **依赖注入**：Microsoft.Extensions.DependencyInjection
- **视觉处理**：OpenCV（推荐 .NET 封装：OpenCvSharp4 + OpenCvSharp4.runtime.win）
- **模型推理（可选）**：ONNX Runtime（用于后续 DNN 检测模型接入）

---

## 3. 设计目标与原则

### 3.1 设计目标

- 清晰分层，职责单一，降低耦合。
- 算法可替换（识别/测距/路径策略可独立演进）。
- UI 与业务解耦，便于测试与维护。
- 支持从 MVP 到工程化的平滑扩展。

### 3.2 分层原则

- 上层依赖抽象，不依赖下层实现细节。
- 业务规则沉淀在 Domain，不放在 UI 层。
- 外部能力（视频源、模型推理、文件 IO）统一放在 Infrastructure。
- Application 负责编排用例，不承载底层技术细节。

---

## 4. 架构分层

建议目录（可按团队习惯微调）：

```text
Visual/
├─ src/
│  ├─ Visual.Presentation/      # WPF UI 层（View/ViewModel）
│  ├─ Visual.Application/       # 应用服务层（用例编排）
│  ├─ Visual.Domain/            # 领域层（实体、值对象、规则、接口）
│  └─ Visual.Infrastructure/    # 基础设施层（视频、算法、持久化、设备）
└─ tests/
   ├─ Visual.Application.Tests/
   └─ Visual.Domain.Tests/
```

### 4.1 Presentation（UI 表现层）

**职责**

- 展示视频流、ROI 圈选交互、结果叠加（边框、距离、路径）。
- 用户操作入口（开始/暂停、标定、阈值配置、策略切换）。
- 通过 ViewModel 调用 Application 用例，不直接写业务规则。

**关键点**

- 采用 CommunityToolkit.Mvvm（`ObservableObject`、`RelayCommand`）。
- 统一使用 MahApps.Metro + MaterialDesign 进行视觉规范化。
- 对耗时操作使用异步命令，避免 UI 卡顿。

### 4.2 Application（应用服务层）

**职责**

- 编排完整业务流程：`圈选目标 -> 识别 -> 测距 -> 路径判断 -> 回传结果`。
- 组织会话状态（当前视频帧、当前 ROI、当前算法配置）。
- 面向 UI 提供稳定用例接口（Use Cases）。

**关键点**

- 仅依赖 Domain 抽象接口（如识别器、测距器、路径评估器）。
- 返回结构化 DTO 给 Presentation，避免 UI 依赖 Domain 内部细节。

### 4.3 Domain（领域层）

**职责**

- 定义核心对象与规则，不依赖任何 UI/框架细节。
- 维护“目标、测距、路径”的业务语义与约束。

**建议对象**

- 实体/值对象：
  - `TargetObject`（目标对象）
  - `RoiRegion`（圈选区域）
  - `DistanceMeasurement`（距离结果）
  - `PathCandidate`（候选路径）
  - `PathDecision`（路径决策）
- 抽象接口：
  - `ITargetDetector`
  - `IDistanceEstimator`
  - `IPathPlanner`
  - `ICameraCalibrator`

### 4.4 Infrastructure（基础设施层）

**职责**

- 提供外部技术能力实现，注入到 Application/Domain 抽象接口。

**典型实现**

- 视频输入：摄像头/视频文件读取
- 识别实现：传统 CV 或 ONNX 推理适配器
- 测距实现：单目标定换算实现 `IDistanceEstimator`
- 路径实现：几何规则或代价函数实现 `IPathPlanner`
- 配置与日志：本地配置文件、日志落盘

### 4.5 视觉处理层次（CV Pipeline 分层）

为避免“算法代码堆在一起”，视觉处理建议在 Infrastructure 内再细分子层次：

1. **采集层（Capture）**
   - 职责：读取摄像头/视频文件，统一帧格式与时间戳。
2. **预处理层（Preprocess）**
   - 职责：去噪、灰度化、阈值化、形态学处理、畸变校正。
3. **检测与跟踪层（Detect/Track）**
   - 职责：在 ROI 或全图执行目标检测、轮廓提取、特征匹配与跟踪。
4. **测距层（Distance）**
   - 职责：将像素测量值与标定参数换算为真实距离。
5. **路径分析层（Path Analyze）**
   - 职责：结合目标位置、障碍与规则进行路径判断/规划。
6. **结果封装层（Result Compose）**
   - 职责：统一输出 `Detection + Distance + PathDecision` 结构给 Application。

---

## 5. 端到端功能流程

### 5.1 主流程

1. 载入视频流并显示实时画面。
2. 用户在画面圈选 ROI 作为目标区域。
3. 系统在 ROI 内执行识别与目标定位。
4. 基于标定参数计算目标距离。
5. 结合障碍物与业务规则进行路径判断/规划。
6. 将距离值与路径叠加回视频画面并输出状态。

### 5.2 数据流（逻辑）

`Frame -> ROI -> Detection -> Distance -> PathDecision -> OverlayResult`

### 5.3 视觉处理流程（OpenCV 视角）

1. **Frame 输入**：从视频源读取帧（BGR/RGB）。
2. **ROI 裁剪**：根据用户圈选区域生成工作图像。
3. **预处理**：滤波、增强、边缘/轮廓准备。
4. **目标识别**：输出目标框、中心点、像素尺寸、置信度。
5. **距离估算**：基于标定参数将像素量转换为物理距离。
6. **路径判断**：生成候选路径并评估最优路径。
7. **结果叠加**：将框、距离、路径线和状态渲染回 UI。

### 5.4 流程与分层映射

- Presentation：ROI 交互、画面显示、结果叠加
- Application：流程编排、状态管理、用例入口
- Domain：距离规则、路径规则、对象约束
- Infrastructure：OpenCV 处理链、视频输入、模型推理与算法实现

---

## 6. 功能模块说明

### 6.1 视频采集与帧管理模块

- 支持摄像头和本地视频。
- 提供帧缓存与播放控制（暂停/恢复/单帧）。

### 6.2 ROI 交互模块

- 支持鼠标圈选、拖拽、缩放 ROI。
- 统一输出标准化 ROI 坐标（像素/归一化可配置）。

### 6.3 目标识别模块

- 在 ROI 内做目标检测、轮廓提取或模板匹配。
- 输出目标框、中心点、置信度。

### 6.4 距离估算模块（当前默认：单目+标定）

- 输入：目标像素尺寸/位置 + 相机标定参数。
- 输出：结构化距离结果（值、单位、可信度）。
- 要求：与具体算法解耦，后续可替换为双目/深度方案。

### 6.5 路径判断与规划模块

- 根据目标位置、运动趋势与约束条件生成候选路径。
- 使用评分规则筛选最优路径并输出解释信息（为何选择）。

### 6.6 结果可视化与告警模块

- 在视频上叠加目标框、距离文本、路径线。
- 根据阈值输出告警状态（如距离过近、路径阻塞）。

---

## 7. 依赖注入与启动建议

在 `App.xaml.cs` 启动时构建 ServiceCollection，按层注册：

- ViewModel 与用例服务：Scoped/Transient
- 设备与算法服务：Singleton（按资源占用评估）
- 配置服务：Singleton

约定：

- Presentation 仅依赖 Application 接口。
- Application 依赖 Domain 抽象。
- Infrastructure 提供抽象实现并在启动时注入。

---

## 8. 可扩展性设计

- **算法替换**：`IDistanceEstimator` / `ITargetDetector` / `IPathPlanner` 抽象化。
- **多策略切换**：支持运行时切换不同识别与路径策略。
- **离线回放评估**：同一段视频可重复执行，便于调参与对比。
- **工程化演进**：后续可补充配置中心、性能采样、异常追踪与测试基线。

---

## 9. 分阶段落地建议

### Phase 1（MVP）

- 视频加载 + ROI 圈选
- 基础目标识别
- 单目距离估算（标定参数固定）
- 简单路径判断与可视化输出

### Phase 2（稳定化）

- 参数配置界面
- 日志与结果回放
- 策略切换与结果评估

### Phase 3（增强）

- 模型化识别增强
- 更复杂路径规划
- 性能优化与测试完善

---

## 10. 里程碑验收口径（建议）

- 能稳定完成“圈选 -> 识别 -> 测距 -> 路径输出”的闭环。
- 距离估算误差在可接受范围内（由业务场景定义阈值）。
- 在典型视频样本上流程可重复、结果可追踪、策略可调整。
