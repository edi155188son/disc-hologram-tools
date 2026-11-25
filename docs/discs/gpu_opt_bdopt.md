# gpu_opt：byte 級迭代最佳化（帶不在乎區）2026-11-25

## 這是什麼
把「物光場 → 亮暗 byte」這一步從雙相位（解析式）換成在 GPU 上的迭代最佳化。疊在所有既有成果上：
- 目標：MATLAB 物光場（彩色頻率多工）＋邊緣錐化（`--taper`）。
- 初始解：雙相位。
- 模型：真實 BD byte 佈局（DiscImageStudio 的幾何與 ECC 排列：每個 cluster 496 個記錄框 × 155 byte，含同步、DC 位元、BIS 每 38 byte 一個），每條軌的 byte 位置都照實算。
- 變數：區塊內的 payload byte 與 LDC 校驗 byte（後者由 BdAmp 的校驗碼反解寫入）。EDC、BIS、同步視為未知。
- 損失：只計觀看窗口（目標頻譜的支撐範圍，±1 階）內與理想場的差，加上低頻項（碟面不出現照片）；其他頻率不管。
- 方法：tanh 鬆弛 + 退火 + Adam，200 次，最後取二值。區塊切成 1 mm 小塊（加 40 µm 重疊邊）分別最佳化。

## 檔案
- `bdgeom.py`：BD 幾何與 ECC byte 佈局（與 BdAmp `--geo-check` 逐位相同）。
- `bdopt.py`：最佳化主程式，輸出 `.bdopt`（每個 cluster：payload 旗標/值、校驗旗標/值）。
- `.venv\`：PyTorch 2.14.1 + CUDA 13（RTX 3090）。
- `run_color_v3.bat`：用 color_bd 的 5 塊欄位重算（v3），輸出 `..\color_bd\color_bd_v3.bdopt`，記錄在 `run_color_v3.log`。

## 速度與效果（RTX 3090）
- 每塊 1 mm 小塊約 1.5–3 s；31 mm 區塊約 1085 塊 ≈ 35 分鐘；5 塊約 3 小時。
- 觀看窗口內的編碼訊噪比（不含 BIS 等真實雜訊）：中央小塊雙相位約 11 → 最佳化約 80–110。

## 產生 ISO（BdAmp 已更新，`--opt-bits` 會自動開啟校驗碼反解）
```
cd /d <WORK>\color_bd
BdAmp --inner 23.987042157339335 --outer 57.987960341783236 --dark 9E --light 22 --encoding dp --combine sum --taper 1.5 --opt-bits color_bd_v3.bdopt --fields C1_R.bdaf,C1_G.bdaf,C1_B.bdaf,C2_R.bdaf,C2_G.bdaf,C2_B.bdaf,C3_R.bdaf,C3_G.bdaf,C3_B.bdaf,C4_R.bdaf,C4_G.bdaf,C4_B.bdaf,C5_R.bdaf,C5_G.bdaf,C5_B.bdaf,L1.bdaf,L2.bdaf,L3.bdaf,L4.bdaf,L5.bdaf --output color_bd_v3.iso
```
- 結尾應印出「Optimized bytes applied: …」與「Parity steering … forced x/y」。
- 這張和 v2 是同一組設計，只差編碼方式，可以直接比較。

## 風險
- 模型把每個 byte 當成長 12 個通道位元、亮度均勻的矩形；實際是 1-7PP 記號加 DC 控制位元。差異大時效果會往雙相位退。
- 模擬的穩健性：每軌位置誤差 ±0.3 µm 仍約 3 倍於雙相位。
