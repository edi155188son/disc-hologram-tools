# holo_hpo.iso — 白光 HPO（只沿軌道有條紋）全息測試盤（2026-11-16）

依 RED_SIM / pohanschiu 的結果重做白光路線。與之前 holo_white（holo_imageplane.m）的差別：
條紋**只沿切線方向**、無 2D 擴散器、擴散角縮到 ±0.5–3°、物體深度限制在 ±7 mm 內。
產生：`holo_hpo.m`（引擎）、`holo_hpo_run.m`（四個區塊）、`DvdDiffractionRings`，sectors 2297888。

| 區塊 | 位置 | 內容 | 載波 α | 擴散 |
|---|---|---|---|---|
| A | r32 @0° | 心(+5 mm 前) / 環(0) / 方框(−5 mm 後) | 4.5° | ±1.5° |
| B | r32 @180° | 傾斜圓環（Z = 0.8·T，−2.4…+2.4 mm，連續深度）＋中央浮點 +7 mm | 4.5° | ±1.5° |
| C | r46 @90° | 紅心 / 綠環 / 藍方框（同平面，彩色測試） | 4.5° | ±0.5° |
| D | r46 @270° | 同 A，寬視區 | 6° | ±3° |

## 怎麼看
1. 暗一點的房間，點光源（手機手電筒），光源放在眼睛旁邊或遠處燈。
2. 把區塊轉到 12 點鐘方向（切線方向 = 水平），直視盤面。
3. 慢慢左右傾斜光碟，讓鏡面反光點（或 0 階亮斑）移到區塊的**水平方向旁邊**約 4–7°（30 cm 外約 2–4 cm）。影像應在區塊內亮起，顏色隨角度變（彩虹）；反光點在另一側會看到共軛像（深度相反）。
4. 區塊 A/B/D：左右移動頭看前後層有沒有相對位移；兩眼視差需要兩眼都在同一側（反光點在區塊旁、不要夾在兩眼之間）。
5. 區塊 C：找到紅心同時綠環、藍方框的角度。
6. 另外在 92 光柵階（約 15°）附近也會看到一份影像。

---
# holo_img.iso — 圖片 → 白光 HPO 全息（holo_hpo_image.m，2026-11-16）

`holo_hpo_image(LATTICE, VALUES, IMG, OPT, PNG)`：任何 PNG/JPG（或 MATLAB 矩陣）→ 場景層 → `holo_hpo`。
- `width_mm`（預設 7，視窗 8 mm）、`center [R T]`
- `color`：`'mono'`（550 nm）或 `'rgb'`（650/550/450 三組載波，白光下固定視角各出一色）
- `gamma` 2.2 線性化後取 sqrt 當振幅；`floor`（預設 0.02）以下的像素不寫，避免暗部雜訊
- `invert`（深色線稿在白底 → true）、`bg`：`'alpha'`/`'white'`/`'black'`/`'none'`
- `depth`：`[]` 平面｜`'tilt'`（左→右漸遠到近）｜`'lum'`（越亮越近）｜深度圖檔或矩陣（255 = 最近）；`zrange`、`nlayers`
- 模擬 PNG：白光 11 個波長疊加 + 0.1 mm 眼睛模糊，比先前的單波長雜斑圖接近真實觀感

| 區塊 | 位置 | 內容 |
|---|---|---|
| A | r32 @0° | ngc6543a（貓眼星雲）彩色、平面 |
| B | r32 @180° | 同圖單色，亮度當深度 ±4 mm（8 層） |
| C | r46 @90° | 頭像線稿 MA_（40×40，6 mm）單色平面 |
| D | r46 @270° | 同圖彩色，左右傾斜深度 ±3 mm（10 層） |

已知限制：每一列（軌道）的二值化／不可控 byte 雜訊會攤在整列，線稿這種填充率低的圖對比較差；
填充率高的大色塊（RED_SIM 的角色圖那種）最有利。

---
# holo_big.iso — 大面積（RED_SIM 式）白光 HPO 圖片（2026-11-16）

## 新工具
- `DvdDiffractionRings`：原程式加 `--pattern polar --raster a.hpo[,b.hpo]`。
  以 DiscImageStudio GitHub main（commit 28310c87，2026-11-16）重新編譯；已驗證幾何與舊版完全一致
  （同參數匯出 lattice，可控 codeword 的 ur/ut 完全相同）。原始碼 `source\Polar.cs`、`source\Program.cs`。
- 格式 `DVDHPO1`：以「半徑列 × 角度欄」索引的 byte 圖（列距 DR、欄距 dθ），byte 直接寫入，0 = 隨機。
  不再需要匯出 lattice，任意大小區域都可以（46 MB 就涵蓋 28–54 mm × 40°）。
- `holo_hpo_big.m`：圖片 → 極座標光柵。每一列（半徑）沿弧長做 1-D Fresnel 全息，物體沿弧取樣，
  所以圖在盤面上不變形；選項同 `holo_hpo_image`（color / depth / zrange / bg / floor / invert …），
  另有 `rc`、`thc_deg`、`span_deg`、`r0/r1`、`dead`（場太弱的地方直接填 92，不產生雜訊——暗部與圖外都安靜）。
- 垂直（半徑方向）視區由列距 DR 決定：相鄰列相位獨立 → 徑向散射約 ±λ/(2·DR) ≈ ±1.5°（DR = 10 µm）。
  DR 越大越亮但垂直可見角越窄。

## 這張盤
| 區塊 | 位置 | 內容 |
|---|---|---|
| A | r 28–54，角度 70–110°（12 點方向） | 貓眼星雲 20 mm 寬，彩色，平面 |
| B | r 28–54，角度 250–290°（6 點方向） | 同圖單色，亮度當深度 ±4 mm（8 層） |

## 換成自己的圖
```matlab
cd <WORK>\DvdDiffractionRings-win-x64\publish\matlab
holo_hpo_big('my.hpo','my.png', struct('rc',41,'thc_deg',90,'span_deg',40,'width_mm',22, ...
             'color','rgb','bg','alpha'), 'my.png_sim.png');
cd ..; system('DvdDiffractionRings --sectors 2297888 --output my.iso --pattern polar --raster matlab\my.hpo');
```
去背 PNG（alpha）最好；大色塊、填滿的圖對比最好；線稿最差。`span_deg` 可到 60°，`r1` 最大 57。

## holo_full.iso — 整盤重複版（`copies` 參數）
HPO 區塊只有在「切線方向落在燈—盤—眼平面」時才亮；RED_SIM / pohanschiu 看起來整盤都有圖，是因為同一張圖繞整圈重複，
任何角度總有 1–2 份剛好對準（亮起的圖永遠在溝軌彩虹亮斑的 90° 方位）。
`holo_hpo_big(..., struct('copies',8, ...))`：span 自動 = 360/N，圖每 45° 一份，第一份在 thc_deg。
holo_full.iso = 貓眼星雲 20 mm 彩色平面 × 8 份，r 28–54 mm。

---
# holo_lenti.iso — 光柵卡式多視角立體圖（2026-11-16）

pohanschiu / RED_SIM 的立體變化強，推測是**多視角交錯（lenticular）**而不是真全息：每個視角用一種 byte 序列寫，
byte 本身的 EFM 光柵週期（rings7 實測：5F 2.13、C5DB 1.70、F9 1.43、CC67 1.23、6F 1.07、DDB6 0.93、A5 0.85 µm）
決定它射向哪個角度（550 nm：15 / 19 / 23 / 27 / 31 / 36 / 40°）。視區 25°，是 byte 級全息（5°）的 5 倍。

- `render_views.m`：網格（suzanne_v/f.txt）→ N 張正交投影灰階視角圖（camera 角度 PHI，寬度已乘 1/cosφ）。
- `holo_lenti_big.m`：沿軌每 `hogel_mm`（0.2 mm）切 N 個槽，槽 k 的前 I_k 比例寫視角 k 的序列、其餘隨機；
  N=2..7 自動選序列；兩位元組序列透過 exe 的 `--pairs`（光柵檔中 0x01–0x0F 佔位）。
- `DvdDiffractionRings` 已加 `--pairs C5DB,CC67,...`。

holo_lenti.iso：Suzanne 20 mm，4 視角（5F/F9/6F/A5，相機 −12°/−5°/+4°/+13°），每 45° 一份共 8 份。

## 怎麼看（和 HPO 盤不同！）
- 燈要離眼睛**遠得多**：影像出現在離鏡面反射方向 15–40° 的地方（不是 4–7°）。燈放在眼睛側邊約 30° 的位置、同高。
- 左右移頭會依序切換 4 個視角，顏色也跟著變（每個視角在固定角度各是一種色）。
- 若猴子看起來凹進去（內外翻轉），把 `phi` 取負號重做即可（±1 階的方向我無法事先判定）。
- 兩眼在 30 cm 外相距約 12°，正好跨過相鄰視角 → 應該有立體感。
