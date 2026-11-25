<!-- Draft of the first issue for JiaFeiMiao-K-Cat/DiscImageStudio. Title on the first line below. -->
BD-R：9E/22 字节对的实盘对比证据，以及「单色可偏转全息图层」功能提议 / BD-R: real-disc evidence for the 9E/22 byte pair and a proposed monochrome off-axis hologram layer

---

你好，我在用 Disc Image Studio 的 BD 引擎做 BD-R 上的计算全息（CGH），想先分享实盘结果，并询问是否欢迎以下功能以 PR 形式加入。看到 #309 里提到团队也在研究全息，希望这些结果对你们有帮助，也欢迎交流。

**环境**：Pioneer BDR-S12JX（原厂固件）、Verbatim BD-R（BA5），一般 ISO + ImgBurn，未修改任何固件或硬件。

### 1. 字节对对比（实盘，目测）

16 µm 径向光栅测试：AA/44、9E/44、9E/AA、44/随机、AA/随机、9E/22 都能看到 ±1 阶，其中 **9E/22 最清楚**；9E/随机看不到（9E 的平均反射率接近随机内容）。用 DIS 的 17PP 编码器统计：9E 区几乎全是 2T/3T，22 区几乎全是 6T，而两者 mark 占空比都是 50%，所以对比应来自短 mark 的形成非线性。

### 2. 在此基础上做到的事（实盘，目测）

- 径向直线载波 + 双相位编码（相邻轨 arg S ∓ arccos|S|），盘面平均反射率均匀、看不到「照片」，头像在绿光与白光下都有视差。
- 区块内的 LDC 校验字节以区块外的自由字节反解（GF(256)/GF(2)），用 DIS 自己的 LDC 编码器重编码验证 100% 吻合；加上反解和边缘渐弱后，实盘可见度明显提升。

目前都是目测，尚无仪器量测；定量测试正在进行。

**模拟图（设计已实盘烧录并目测确认，暂无校准照片与仪器量测，之后补上）：**

![双相位头像全息，三个视角与平均反射率（模拟）](https://github.com/edi155188son/disc-hologram-tools/blob/main/docs/figures/bd_dp_head_rb_sim.png?raw=true)

*双相位径向载波头像（彩虹区块），中央 4 mm、532 nm、−6°/0°/+6° 三个视角；最右为平均反射率，均匀无「照片」。实盘：绿光与白光下可见水平视差（目测）。*

![双相位在 BD 字节格上的载波周期扫描（模拟）](https://github.com/edi155188son/disc-hologram-tools/blob/main/docs/figures/bd_dp_ksweep_sim.png?raw=true)

*每周期轨数 k 与 +1 阶窗口信噪比；k≈8、12 附近有凹陷。彩色测试盘上唯一完全避开凹陷的区块表现最好（目测），尚未直接量测。*

### 3. 提议

我们在本地实现了「单色可偏转全息图层」：图片 + 深度图 → 物光场 → 径向载波，可选设计波长与偏转角 θ0，提供全视差与彩虹两种模式，BD 用双相位 + 校验字节反解，DVD 用 Lee 编码。BD 输出已与独立参考实现逐字节一致。

想请教：

1. 这类功能是否欢迎进入主仓库？还是更适合做成外部工具？
2. 如果欢迎，你希望怎么拆分 PR（例如先送任意字节对的 `BluRayRunLengthMapping` 构造，再送引擎、CLI，最后送 WPF 面板）？是否有需要遵守的模块边界？

代码、文档与模拟：https://github.com/edi155188son/disc-hologram-tools（GPL-3.0）

说明：代码与分析大量使用了 AI 助手（Claude）协助完成，实验与结论由我本人负责。

---

English summary: real-disc evidence that 9E/22 is the strongest BD-R byte pair among those tried; double-phase radial-carrier holograms and LDC parity steering built on DIS's BD engine (figures above are simulations of designs that were burned and checked by eye; calibrated measurements to follow); asking whether a monochrome off-axis hologram layer would be welcome as a PR series, and how to split it.
