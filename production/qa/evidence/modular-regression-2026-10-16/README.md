# 模块化回归工具：2026-10-16开始，2026-10-17收口

用户要求按修改范围运行测试、减少频繁全量耗时。实现仅Editor：`ModularRegressionCatalog`、`ModularRegressionRunner`、`ModularRegressionWindow`、明确的JSONfixture归属及12项新护栏；不改已有测试、玩法、包或快速播放设置。起点f0a12b01e181d1ae9cf3009bc446da003f7c6c56，原用户修改及未跟踪文件基线79项保留。

## 新原生结果

| 本次入口 | 总数 | 通过 | 失败 | 跳过 | 不确定 | 原生秒数 |
|---|---:|---:|---:|---:|---:|---:|
| tools | 12 | 12 | 0 | 0 | 0 | 0.409484 |
| tech,drivers＋core | 1051 | 1051 | 0 | 0 | 0 | 13.8328204 |
| fast-all（窗口缓存收尾前） | 2986 | 2986 | 0 | 0 | 0 | 30.7405407 |
| fast-all（窗口缓存后复核） | 2986 | 2986 | 0 | 0 | 0 | 34.0922539 |
| fast-all（最终程序集） | 2986 | 2986 | 0 | 0 | 0 | 33.4249368 |

独立结果保留为tools-12-native.json、tech-drivers-core-native.json、fast-all-2986-native.json、fast-all-window-cache-native.json、fast-all-final-native.json。集合有重叠，不相加作独立覆盖数；前两次后改善Editor窗口SessionState/隐藏API回收与保留nativeState，第一次短集后补窗口结果/计数缓存及重载自动刷新，最后补异步发现期间清除旧结果与准备态防重入，再由最终短集验证。每份实际RunFinished记录包含逐用例数据，`selectionMatches=true`，无漏执行/意外执行。未把普通工具API返回“已启动”当作测试通过。

最终请求开始同帧实际核验 `request-guard.json`：preparing/busy=true、旧结果立即清空、第二次Run被拒绝且选择/错误状态未被覆盖。最终RunFinished为UTC2026-10-16T16:17:07.1258829Z（北京时间10月17日00:17），不是前一轮旧结果。

## 清单及功能核验

- 初始真实发现2990，新增12后3002；14模块fixture精确归属。plan-full.json / plan-fast-all.json / plan-long-races.json为新程序集真实发现后选出的名单。
- partition-audit.json：短2986＋长16并集精确3002、交集0、unmapped0/stale0。独立PowerShell核查最初默认Sort-Object -Unique误合并2个参数值大小写不同的用例，已改Ordinal HashSet后实际重核；生产选择与结果比较从始至终使用Ordinal，未受该审计脚本错误影响。
- 实际打开1个模块窗口、默认预览tech/drivers/core选择1051/发现3002/长0，与原生执行相符；随后fast-all真实执行包含原UI/音频协程，完成后窗口仍存在、运行中自有隐藏API实例0，未重复留存API。
- 空/未知选择、全量/短集与模块混用、依赖闭包、重复fixture、未知依赖、误引入长依赖、未归属/过期清单、精确fixture边界及全/短/长分区的12项护栏通过。
- 使用本地固定Test Framework1.1.33源码核实Filter.testNames精确匹配、ExecutionSettings.runSynchronously会排除协程（因此不启用）、原生RunFinished/结果接口及IsRunActive内部忙碌查询。不安装或修改任何包。
- 初次只读execute_code误用了不存在的public IsTestRunActive方法，CodeDOM编译失败未执行；查明当前包仅有internal IsRunActive后采用只读反射并在缺失时拒绝启动。没有将该尝试称为成功，也没有改生产脚本修API。

## 明确未执行

本轮没有再跑16项真实长比赛，也没有跑全量3002；此前全量2990/2990、710.715秒只作历史耗时参考。短集通过不等于长流程通过。本轮无游戏UI视觉改动，不伪造比赛截图或主观操作验收；模块窗口通过代码打开/读取实际预览与原生执行，没有冒称真人鼠标逐项点击。未专门强制测试运行中重编译/取消；Editor回调重载恢复按已有原生证据模式实现，普通模块原生UI/音频用例确已执行。

使用方式及扩大验证条件见 [tests/regression-suite.md](../../../../tests/regression-suite.md)。最终Unity/Console、基线保留及源码哈希另存本目录。无commit/push，保留既有本地文件。

最终状态已实查：MainMenu干净，无播放/编译/导入/原生测试，许可证有效、idle/ready；Console仅缺失赛道负向测试的预期error1，warning0，无新增运行错误。79基线文件79原哈希不变、0missing，已有生产/测试脚本的Git差异为空，HEAD及暂存区保持；分别见final-unity-state.json、preservation-audit.json和final-source-hashes.json。
