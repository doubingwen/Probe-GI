# Probe GI

一个基于 Unity URP 的实验性 Probe Global Illumination 项目。

项目通过 Probe 捕获场景表面信息，将采样得到的 Surfel 在 Compute Shader 中重新光照并投影为 SH9 系数，最后在屏幕空间插值相邻 Probe 的 SH 数据，为场景叠加间接光照。

## 运行效果

### Sponza 场景整体效果

![Sponza 场景中的 Probe GI 整体效果](pictures/probe_gi_1.png)

### 拱廊区域间接光效果

![Sponza 拱廊暗部中的 Probe GI 间接光效果](pictures/probe_gi_2.png)

## 环境

- Unity `6000.0.67f1`
- Universal Render Pipeline `17.0.4`
- Windows / DirectX 11 或支持 Compute Shader 的图形 API

## 核心流程

1. `ProbeVolume` 按网格创建和管理 Probe。
2. `Probe` 使用临时相机捕获 Albedo、Normal 和 World Position。
3. `SurfelSampleCS.compute` 从捕获结果中生成 Surfel 数据。
4. `SurfelReLightCS.compute` 计算 Surfel 光照，并将结果投影到 SH9。
5. `PRTRelight` 每帧更新当前帧与上一帧的 SH 系数缓冲区。
6. `SH.hlsl` 对附近 Probe 的 SH 系数进行插值。
7. `Composite.shader` 将间接光合成到相机颜色结果中。

## 运行

```bash
git lfs install
git clone https://github.com/doubingwen/Probe-GI.git
cd Probe-GI
git lfs pull
```

使用 Unity Hub 打开项目，然后打开 `Assets/Scenes/SampleScene.unity` 并进入 Play Mode。

> `Assets/Material/ProbeVolumeData.asset` 包含已采样的 Probe 数据，文件通过 Git LFS 管理。未安装或未拉取 Git LFS 数据时，场景中的 GI 数据可能无法正常加载。

## 主要文件

- `Assets/Script/Probe.cs`：单个 Probe 的捕获、Surfel 管理和重新光照。
- `Assets/Script/ProbeVolume.cs`：Probe 网格、SH 缓冲区及历史帧管理。
- `Assets/Script/PRTRelight.cs`：URP Probe 重光照 Render Pass。
- `Assets/Shaders/SurfelSampleCS.compute`：Surfel 采样。
- `Assets/Shaders/SurfelReLightCS.compute`：光照计算与 SH 投影。
- `Assets/Shaders/SH.hlsl`：SH 计算及 Probe 插值。
- `Assets/Shaders/Composite.shader`：间接光合成。
- `Assets/Debug`：Probe 和 SH 调试显示。

## 说明

这是一个用于学习和验证 Probe GI 流程的实验项目，目前仍包含部分 URP Compatibility Mode API。间接光效果会受到 Probe 数量、间距、场景尺度、天空光强度和间接光强度等参数影响。
