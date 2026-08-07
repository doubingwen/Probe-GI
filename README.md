# Probe GI

一个基于 Unity URP 的实验性 Probe Global Illumination 项目。

项目通过 Probe 捕获场景表面信息，将采样得到的 Surfel 在 Compute Shader 中重新光照并投影为 SH9 系数，最后在屏幕空间插值相邻 Probe 的 SH 数据，为场景叠加间接光照。

## 运行效果

### 开启 Probe GI

![Sponza 场景中的 Probe GI 整体效果](pictures/probe_gi_1.png)

### 间接光对比

| 未开启 Probe GI | 开启 Probe GI |
| --- | --- |
| ![未开启 Probe GI 时的拱廊暗部](pictures/no_probe.png) | ![开启 Probe GI 后的拱廊间接光效果](pictures/probe_gi_2.png) |

开启 Probe GI 后，拱廊中的墙面、立柱和顶部暗部能够接收到由 Probe 重建的间接光照，减少完全发黑的区域。

## 核心流程

1. `ProbeVolume` 按网格创建和管理 Probe。
2. `Probe` 使用临时相机捕获 Albedo、Normal 和 World Position。
3. `SurfelSampleCS.compute` 从捕获结果中生成 Surfel 数据。
4. `SurfelReLightCS.compute` 计算 Surfel 光照，并将结果投影到 SH9。
5. `PRTRelight` 每帧更新当前帧与上一帧的 SH 系数缓冲区。
6. `SH.hlsl` 对附近 Probe 的 SH 系数进行插值。
7. `Composite.shader` 将间接光合成到相机颜色结果中。

## 说明

这是一个用于学习和验证 Probe GI 流程的实验项目，目前仍包含部分 URP Compatibility Mode API。间接光效果会受到 Probe 数量、间距、场景尺度、天空光强度和间接光强度等参数影响。
