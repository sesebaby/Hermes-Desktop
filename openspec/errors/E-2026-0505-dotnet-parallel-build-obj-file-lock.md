# E-2026-0505-dotnet-parallel-build-obj-file-lock

- id: E-2026-0505-dotnet-parallel-build-obj-file-lock
- title: 并行运行同一项目的 dotnet test/build 会抢占 obj 输出文件
- updated_at: 2026-05-05
- keywords: [dotnet, build, test, parallel, file-lock, cs2012]

## symptoms

- 两个 `dotnet test` 或 `dotnet build` 同时触发同一项目编译时，其中一个失败。
- 错误类似：`CSC : error CS2012: 无法打开 ... obj ... Hermes.Core.dll 以进行写入`。
- 失败原因是同一个 `obj` 输出文件正在被另一个编译进程占用。

## trigger_scope

- 在同一工作区内并行运行共享项目引用的 `dotnet test` / `dotnet build`。
- 例如同时跑两个 `HermesDesktop.Tests.csproj` 过滤测试，都会尝试构建 `src/Hermes.Core.csproj`。

## root_cause

MSBuild / Roslyn 对同一个项目的默认 `obj` 输出目录不是并行安全的。并行测试命令不是独立任务，因为它们共享还原、编译和中间产物路径。

## bad_fix_paths

- 继续重试并行测试，误判为源码编译不稳定。
- 删除 `bin/obj` 或重启进程作为常规方案，而不是修正执行方式。
- 把 `CS2012` 当作业务代码错误定位。

## corrective_constraints

- 同一解决方案或共享项目引用的 `dotnet build/test` 要顺序执行。
- 如果必须并行，必须显式隔离输出目录，否则不要并行。
- 对同一测试项目的多个 `--filter` 验证优先合并成一个过滤表达式或逐条顺序运行。

## verification_evidence

- 本次并行运行两个 `dotnet test .\Desktop\HermesDesktop.Tests\HermesDesktop.Tests.csproj` 命令时，一个命令失败于 `CS2012` 文件占用。
- 后续验证应改为顺序执行。
