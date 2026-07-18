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
