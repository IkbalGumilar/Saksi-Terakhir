# Render Budget — Saksi Terakhir

Target: horor psikologis FPS 3D, indoor.
Unity 6000.4.0f1 · URP 17.4.0 · Linear color space · Forward+ · MSAA off · HDR on.

Dokumen ini mencatat *kenapa* setiap knob rendering disetel begitu, supaya
keputusannya tidak diulang dari nol tiap kali frame rate turun.

## Mesin target (ini yang menentukan segalanya)

| | |
|---|---|
| GPU | **Intel HD Graphics 4600 (HSW GT2)** — Haswell, 2013, 20 EU |
| Memori GPU | dibagi dengan sistem, DDR3, bandwidth ~25 GB/s |
| CPU | Core i7-4790 @ 3.6 GHz (4C/8T) — lapang, **bottleneck murni GPU** |
| Graphics API | **OpenGL Core 4.6** (Mesa 25.2.8), bukan Vulkan |

Ini bukan iGPU modern. Kira-kira **1/10 performa Iris Xe**. Angka yang wajar untuk
"iGPU" secara umum terlalu longgar di sini.

**Target frame: 1280×720 @ 30 FPS.** Bukan 1080p, bukan 60. Di bandwidth ~25 GB/s,
satu fullscreen pass di 1080p sudah memakan porsi besar dari frame budget; di 720p
biayanya kurang dari setengahnya. Horor justru diuntungkan oleh 30 FPS yang stabil.

Konsekuensi API yang perlu diingat:

- **GPU Resident Drawer mustahil di OpenGL** — butuh `BatchBufferTarget.RawBuffer`
  yang tidak ada di GL. Sudah dimatikan.
- **Native Render Pass diabaikan** — itu jalur Vulkan/Metal. Penghematan bandwidth
  dari situ tidak tersedia untuk kita.
- **Compute shader mahal di Haswell.** Relevan untuk light culling Forward+.
- Driver Vulkan Haswell (`hasvk`) terpasang di mesin ini dan Unity bisa dipaksa lewat
  `-force-vulkan`, tapi hasvk adalah driver legacy Gen7.5. Boleh diuji dan diukur,
  **jangan dijadikan asumsi desain.**

## Prinsip

Mood horor (gelap, bayangan senter, kabut, AO) adalah **fitur inti**, bukan hiasan.
Jadi urutan pemotongan biaya selalu: buang yang tidak terlihat dulu, baru turunkan
kualitas yang terlihat, dan `RenderScale` adalah pilihan terakhir.

## Setelan yang diubah dari template URP

Semua ada di `Assets/Settings/PC_RPAsset.asset` kecuali baris terakhir.

| Knob | Template | Sekarang | Alasan |
|---|---|---|---|
| `m_RequireOpaqueTexture` | 1 | **0** | Menyalin seluruh buffer warna tiap frame. Hanya dibutuhkan shader refraksi/distorsi — belum ada. Hidupkan lagi kalau bikin kaca atau heat haze. |
| `m_ShadowCascadeCount` | 4 | **2** | Cascade = satu render pass shadowmap masing-masing. Koridor indoor tidak butuh 4. |
| `m_ShadowDistance` | 50 | **30** | Jarak pandang indoor pendek. Efek samping bagus: texel density naik karena atlas yang sama menutup area lebih kecil. |
| `m_Cascade2Split` | 0.25 | **0.3** | Cascade dekat jadi 0–9 m, cukup untuk jarak aksi FPS. |
| `m_SoftShadowQuality` | High (3) | **Low (1)** | Filter PCF per-pixel. Di scene gelap bedanya nyaris tak terlihat. |
| `m_AdditionalLightsCookieResolution` | 2048 | **512** | Atlas cookie. Cookie senter tunggal tidak butuh 2048. |
| `m_AdditionalLightsCookieFormat` | ColorHigh (3) | **GrayscaleHigh (1)** | Cookie senter cuma butuh grayscale. |
| `m_SupportScreenSpaceLensFlare` | 1 | **0** | Fullscreen pass, dan lens flare tidak cocok dengan tone game ini. Menghemat varian shader juga. |
| `m_GPUResidentDrawerMode` | InstancedDrawing (1) | **Disabled (0)** | Tidak didukung di OpenGL — ini sumber warning `GPUResidentDrawer The current platform does not support BatchBufferTarget`. Tidak ada yang hilang: GRD berguna untuk instancing masif (foliage open-world), bukan koridor indoor, dan CPU kita justru lapang. |
| `m_AdditionalLightsShadowmapResolution` | 2048 | **1024** | Direvisi turun setelah tahu GPU-nya HD 4600. Senter me-render ulang scene dari sudut lampu tiap frame — salah satu biaya terbesar di scene horor. |
| SSAO `Downsample` (`PC_Renderer.asset`) | 0 | **1** | AO dihitung di setengah resolusi. Penghematan terbesar per-pixel; AO tetap terlihat karena sifatnya low-frequency. |

## Yang sengaja TIDAK diubah

- **`m_RenderScale: 1`** — knob darurat. Jangan dipakai sebelum knob lain habis;
  turun ke 0.85 kalau frame budget masih jebol.
- **SSAO tetap aktif** — ini penyumbang mood terbesar per rupiah GPU. Kalau harus
  dipangkas lagi, turunkan `Samples` ke Low (2) dulu, jangan matikan fiturnya.
- **`m_MainLightShadowmapResolution: 2048`** — dengan 2 cascade, tiap cascade dapat
  1024×1024 untuk 30 m. Kualitas justru naik dibanding default sambil biaya turun.
- **Bayangan realtime senter tetap ada** — meski atlasnya diturunkan ke 1024, ini
  biaya yang memang dibeli dengan sadar. Senter tanpa bayangan membunuh mood.
- **`DefaultVolumeProfile`** — semua komponennya terdaftar tapi nilainya netral
  (Bloom `intensity: 0`, MotionBlur `active: 0`). URP melewati efek berintensitas
  nol, jadi tidak ada biaya. Tidak perlu diganti profil ramping.
- **Forward+ (`m_RenderingMode: 2`)** — menghapus batas 4 lampu per objek, cocok
  untuk ruangan dengan beberapa lampu kecil. `m_AdditionalLightsPerObjectLimit`
  diabaikan di mode ini. **Tapi ini kandidat kuat untuk diukur:** Forward+ memakai
  light culling berbasis compute, dan compute mahal di Haswell. Kalau scene hanya
  punya ≤4 lampu terlihat sekaligus, Forward biasa (`m_RenderingMode: 0`) mungkin
  lebih murah. Jangan ditebak — ukur dua-duanya di scene nyata.

## Kalau frame rate masih kurang

Urutan yang disarankan:

1. SSAO `Samples` → Low (2), lalu `AfterOpaque` → 1 (lebih murah, AO ikut menggelapkan
   direct light — secara fisika kurang benar tapi justru pas untuk horor).
2. `m_RenderScale` → 0.85, dengan `m_UpscalingFilter` FSR kalau perlu ketajaman.
3. Coba `m_DepthPrimingMode` → Forced di `PC_Renderer.asset`. Ini kandidat terkuat
   yang belum dipakai: karena SSAO memakai `Source: DepthNormals`, prepass-nya
   **sudah dibayar setiap frame apa pun yang terjadi**. Depth priming tinggal
   memanfaatkan prepass itu untuk memaksa `ZTest Equal` di pass opaque, sehingga
   overdraw tidak ikut di-shading. Sudah diverifikasi memenuhi syarat URP 17
   (desktop Linux, MSAA off, bukan deferred). Peringatan dari source URP: bisa
   bermasalah dengan rendering TextMesh — cek UI setelah menyalakannya.
4. Matikan bayangan realtime untuk lampu statis; pindah ke lightmap baked.

## Cara mengukur

Window → Analysis → **Rendering Debugger** (frame stats) dan **Profiler** modul GPU.
Ukur di scene nyata dengan senter menyala, bukan di SampleScene.
