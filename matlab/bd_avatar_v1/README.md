# avatar_bd：BD-R 版頭像全像（avatar_fill 放大 2×，面積 4×）

2026-11-24

## 內容
| 區塊 | 位置 | 大小 | 說明 |
|---|---|---|---|
| FP（全視差） | r = 41 mm，θ = 0° | 31 × 31 mm（臉寬 30 mm） | 窄頻光看，上下左右都有視差 |
| RB（彩虹） | r = 41 mm，θ = 180° | 31 × 31 mm | 白光看，上下方向換顏色，左右有視差 |

- 深度 −6…+6 mm（avatar_fill 是 ±4 mm；放大後用 ±6 折衷，太深會糊）
- 半徑方向載波 3.52 µm = 11 條 BD 軌（532 nm → 8.7°，450 nm → 7.3°，630 nm → 10.3°）
- Lee 振幅編碼，視角錐 ±3.5°
- 亮暗 byte：9E（暗）/ 22（亮）；區塊外為隨機資料
- 校正：Verbatim CMCMAG-BA5（inner 23.987042157339335、outer 57.987960341783236）

## 檔案
- `avatar_bd.m`：產生物光場（FP.bdaf 512 MB、RB.bdaf 407 MB）＋模擬
- `FP_sim.png`、`RB_sim.png`：中央 4 mm 在 BD 碼字格點（0.32 × 0.929 µm、15% 雜訊）上的重建模擬，各視角線條有位移（視差）
- `BdAmp`：BD ISO 產生器（自包含，不需要裝 .NET），SHA-256 `ac37a6047186bcf03530033112d3ad4ac7f08e6ebcfecbd0c1e00a997e481b43`
- `..\bd\bdamp_src\`：BdAmp 原始碼與 DiscImageStudio 的 BluRayRunLengthMapping 修改（patch）

## 產生 ISO
```
cd /d <WORK>\avatar_bd
BdAmp --inner 23.987042157339335 --outer 57.987960341783236 --dark 9E --light 22 --fields FP.bdaf,RB.bdaf --output avatar_bd.iso
```
- 整張約 25 GB，需要約 1 GB 記憶體；每 10 秒印一次進度
- 區塊外角延伸到 r ≈ 58.6 mm，超出資料區的部分只有背景，不影響臉

## 燒錄
- ImgBurn「Write image file to disc」，BD-R 25 GB（最好用同批 Verbatim BA5），選最低速
- 燒完不用驗證也行

## 觀看
- RB：白光點光源（手機手電筒、LED 小燈）從斜上方照，眼睛在載波方向約 7–10° 處；上下移動換顏色，左右移動看視差
- FP：綠色窄頻 LED（或雷射**擴束後打到牆上再反射**，不要直視雷射），加一張霧面片減少斑點；上下左右都能轉
- 影像浮在碟面前後 ±6 mm

## 風險（BD 第一次做）
1. 9E/22 在 BD-R 上的反射率對比還沒量過，可能比 DVD 暗（估計 0.4–1.0×）
2. BD 記錄層半透明，會有 Selényi 環紋疊在影像上
3. BD 相鄰軌的半徑對位沒實測；對位差會讓載波變糊、影像變暗
4. 斑點（speckle）：FP 尤其明顯，光源越大越好

燒出來後拍照（RB 白光、FP 綠光各一張），可以據此決定要不要調對比 byte、深度或載波。
