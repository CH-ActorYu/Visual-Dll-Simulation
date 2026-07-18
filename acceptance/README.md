# 验收工具

## S4：独立 DLL 消费者

执行：

```powershell
./tools/acceptance/Invoke-S4.ps1
```

脚本先把 OpenCV 测距包发布到 `artifacts/distribution/OpenCvDistance/`，再运行
`S4.ConsoleConsumer`。消费者工程不包含 `ProjectReference` 或 `PackageReference`，只引用分发目录中的 DLL；运行时生成短视频并验证 30 帧均输出 `1000.00 mm`。

原始 DLL 引用不会像 NuGet 那样解析 runtime asset，因此调用方必须把
`runtimes/win-x64/native/OpenCvSharpExtern.dll` 和
`opencv_videoio_ffmpeg4130_64.dll` 复制到可执行文件目录。消费者工程同时保留
`runtimes/` 布局，并显式执行这一步。

## S7/S8：性能与稳定性运行器

短时验证：

```powershell
dotnet run --project acceptance/Visual.AcceptanceRunner -c Release -- --mode smoke
```

正式 S7（720p、预热 30 秒、统计 10 分钟）：

```powershell
dotnet run --project acceptance/Visual.AcceptanceRunner -c Release -- --mode performance
```

正式 S8（720p、预热 30 秒、统计 2 小时）：

```powershell
dotnet run --project acceptance/Visual.AcceptanceRunner -c Release -- --mode stability
```

JSON 报告默认写入 `artifacts/acceptance/`。运行器每 10 秒输出进度，并记录吞吐、处理耗时平均/P95/P99、丢帧、托管堆、Working Set 和 Private Bytes。
