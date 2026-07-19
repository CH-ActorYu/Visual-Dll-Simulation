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

正式 S8（720p、预热 30 分钟、随后统计完整 2 小时，总运行约 2.5 小时）：

```powershell
dotnet run --project acceptance/Visual.AcceptanceRunner -c Release -- --mode stability
```

JSON 报告默认写入 `artifacts/acceptance/`。运行器每 10 秒输出进度，并记录吞吐、处理耗时平均/P95/P99、丢帧、托管堆、Working Set 和 Private Bytes。S8 的内存增长率使用正式统计期首尾各 10 分钟样本均值计算，报告中的 `ResourceWindows` 保存窗口秒数、样本数及基线/结束均值；最终还要求帧池无未归还租约且总租借数等于总归还数。

两小时测试建议使用后台管理脚本：

```powershell
./tools/acceptance/Start-S8.ps1
./tools/acceptance/Get-S8Status.ps1
```

启动脚本拒绝重复实例，并记录 PID、开始时间、预计结束时间、标准输出、错误输出和退出码。状态脚本不会修改运行中的进程。
