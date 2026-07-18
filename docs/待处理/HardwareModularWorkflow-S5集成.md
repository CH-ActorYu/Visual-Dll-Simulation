# HardwareModularWorkflow S5 集成待处理事项

## 1. 当前决定

- **现在不修改**相邻的 `HardwareModularWorkflow` 仓库；
- Visual 项目继续独立开发、测试和分发；
- S5“真实宿主集成”保持挂起，后续单独授权、设计和实施；
- S4 独立 DLL 消费者已经证明测距包可脱离 WPF 使用，S5 只负责证明特定宿主的真实工作流接入。

记录基线：

| 项目 | 基线 |
| --- | --- |
| Visual | `41e840e15a9a5c8bb7a2fe2fc50ad800bbc6894a` |
| HardwareModularWorkflow | `5e23db8897250d67378605d5962e7371cf73abaa` |
| 当前发现 | HardwareModularWorkflow 中尚无 `Visual.Distance`、`DistanceMeasurement`、`Visual.Image` 或 `MeasureAsync` 引用 |

## 2. 后续目标

满足 SRS S5：HardwareModularWorkflow 从其相机驱动取得标准 `ImageFrame`，或通过已选引擎适配器取得帧；调用 `IDistanceService.MeasureAsync(...)`；最终把标准测距结果放入 `CommandResult.Data`。宿主业务层不得接触 `Mat` 等具体引擎类型。

期望链路：

```text
HardwareStep / IHardwareCommand
  → 视觉测距驱动或步骤执行器
  → ImageFrame + TargetRegion
  → IDistanceService.MeasureAsync(...)
  → IReadOnlyList<DistanceMeasurement>
  → CommandResult.Success(duration, measurements)
```

## 3. 建议边界

### HardwareModularWorkflow 侧

建议新增独立视觉集成层或插件，职责仅包括：

1. 把宿主相机帧转换或封装为 `ImageFrame`；
2. 从步骤参数构造 `TargetRegion` 和标定配置；
3. 调用 `IDistanceService`；
4. 把 `DistanceMeasurement` 集合写入 `CommandResult.Data`；
5. 映射取消、未标定、设备丢失和模块错误。

不要把测距公式、目标检测或 OpenCV 算法复制进 HardwareModularWorkflow。

### Visual 侧

原则上不需要为 S5 修改 `Visual.Distance` 公共契约。若真实集成发现缺口，应先确认它是否属于通用宿主边界，再决定是否扩展 DTO；不得加入 HardwareModularWorkflow 专用类型或依赖。

## 4. 依赖方式

后续需在以下方式中明确选择一种：

- **DLL 分发引用**：使用 S4 生成的 OpenCV 测距 DLL 组，最接近真实交付；
- **开发期 ProjectReference**：便于联调，但最终仍须用 DLL 组复验；
- **宿主提供帧 + Visual 提供检测能力**：宿主只认识 Visual 公共 DTO；
- **OpenCV 帧源适配器**：由独立组合根引用 `Visual.Engine.OpenCv`，宿主核心层不引用具体引擎。

若采用原始 DLL 引用，必须同时部署：

- `OpenCvSharpExtern.dll`；
- `opencv_videoio_ffmpeg4130_64.dll`；
- 并将它们复制到宿主可执行文件的原生搜索目录或输出根目录。

## 5. 待实施步骤

1. 单独确认允许修改 HardwareModularWorkflow 仓库；
2. 阅读该仓库 README、docs、分层规则及当前硬件驱动注册方式；
3. 确定视觉步骤/命令应归属的工程和依赖方向；
4. 定义最小命令参数：`TargetId`、ROI、标定信息、超时和帧来源；
5. 实现 `IDistanceService` 生命周期与引擎注入；
6. 实现 `CommandResult.Data` 返回标准测距集合；
7. 增加无摄像头集成测试，使用固定帧或生成视频；
8. 再使用真实相机/工作流执行路径复验；
9. 分别在两个仓库记录提交号、依赖版本和回滚方式。

## 6. 正式验收条件

只有以下条件全部满足，S5 才能由 `Blocked` 改为 `Pass`：

- 集成代码实际位于 HardwareModularWorkflow 的正式执行路径；
- 工作流编排能够触发一次测距；
- `CommandResult.IsSuccess == true`；
- `CommandResult.Data` 包含 `IReadOnlyList<DistanceMeasurement>` 或约定的标准结果包装；
- 结果包含正确的 `TargetId`、状态、距离、单位和帧关联字段；
- 未标定、取消、设备丢失和模块异常均转换为明确结果；
- HardwareModularWorkflow 核心业务层不引用 OpenCvSharp/WPF 类型；
- Debug/Release 构建、双方架构测试和集成测试通过；
- 使用分发 DLL 组再次验证，而非只验证源码项目引用。

## 7. 恢复处理时的入口

- Visual 验收记录：[M7.1 S1～S8](../验收记录/2026-07-19-M7.1-S1-S8.md)
- 独立 DLL 验证：[S4 验收工具](../../acceptance/README.md)
- 公共接口与生命周期：[05-公共接口与帧生命周期](../05-公共接口与帧生命周期.md)
- 验收口径：[06-测试计划与验收用例](../06-测试计划与验收用例.md)
