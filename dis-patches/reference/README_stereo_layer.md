# DiscImageStudio 1.6.0-stereo.1：立體圖層（多視角，DVD）

自己編譯的 DiscImageStudio（GitHub main 28310c87 + 立體圖層功能）。其他功能與官方版相同，設定、工程檔、標定共用。
版號帶 `-stereo.1`，程式若提示更新，更新後會變回沒有這個功能的官方版。

## 用法
1. 設計頁照原本方式匯入圖片、拖到想要的位置、縮放。
2. 選取該圖層，右側「屬性」最下方勾 **立體圖層（多視角，DVD）**（只在 DVD 工程出現）。
3. 視角來源三選一：
   - **選擇視角圖…**：一次選 2–7 張（依檔名排序：view1、view2…）。範例：`suzanne_views\view1–4.png`（就是燒成功的那 4 個角度）。
   - 不選視角圖：用本圖層圖片當每個視角（平面，但 25° 視區、彩虹色）。
   - **深度圖…**：灰階、白＝近、黑＝遠，用本圖層圖片自動合成視角；「視差」控制立體強度。
4. 參數：
   - 視角數 2–7（用視角圖時由檔案數決定）。序列由長到短週期自動選：5F / C5DB / F9 / CC67 / 6F / DDB6 / A5（550 nm 約 15°–40°）。
   - 交錯單元 0.2 mm（已驗證）。
   - 繞圈份數：8 = 每 45° 一份，轉到哪都看得到。畫布上會淡淡畫出副本。
   - 反轉視角順序：立體凹凸看起來相反時勾。
   - **朝外對齊**：把圖片上方轉向盤外（視角沿軌道切換，這個方向看起來才是正的）。
5. 照原本方式匯出 ISO 或直接燒錄。透明（PNG alpha）區域照常顯示底下的普通圖案；不透明的暗部寫隨機（暗）。

## 運作方式
- 匯出時在合成圖旁寫一個 `*.png.stereo.json`；DVD 引擎（匯出、串流燒錄、CLI `solve`）讀到就在那些位置改寫成光柵卡編碼。
- 只有碰到立體圖層的 ECC 區塊改走逐字取樣，其他區塊維持原本的 SIMD 快速路徑。
- 預覽頁（讀回模擬）和色調預覽不會顯示立體效果，只看得到原圖。

## 驗證
- 「合成圖 + sidecar」的實際匯出路徑，與直接逐 codeword 呼叫立體取樣器的參考結果**逐 byte 完全相同**（r 30–50 mm、2.7 GB）。
- 每個序列只帶自己的視角（view 1/2/3/4 分得乾淨）；8 份副本位置、旋轉正確；深度圖合成時近處與遠處往相反方向移動。
- DvdImageSolver selftest、DvdScrambledPattern、DvdStableRegion、ProjectStore 測試通過；立體設定可存檔、讀回，無效值會被拒絕。
- 沒法在 Linux 上跑 WPF 介面測試：介面編譯通過，但沒有實際開過視窗，第一次用請留意屬性面板是否正常。

## 原始碼
`stereo-layer.patch`（對 DiscImageStudio main 28310c87 的單一 commit，可 `git am`）。主要檔案：
`src/DvdImageSolver/Solver/StereoOverlay.cs`、`FastDispersionImageWriter.cs`、`ProjectJsonStore.cs`、
`DesignLayerViewModel.cs`、`DesignViewModel.cs`、`DesignView.xaml`、`DiscDesignCanvas.cs`、語系檔。
