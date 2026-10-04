# Simple Inventory Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. Follow the tasks below in the current checkout; the user authorized continuation.

**Goal:** Mengaktifkan UI inventory yang sudah ada dengan grid yang dapat diperbesar dan dock dokumen terpisah.

**Architecture:** InventoryState mengelola koleksi sesi tanpa persistence. View menduplikasi template Canvas, controller mengatur navigasi dan kunci input; bootstrap memasang komponen saat scene dimainkan dan menu Editor menyediakan binding permanen opsional.

**Tech Stack:** Unity 6000.3.24f1, uGUI, TextMeshPro, Input System.

**Spec:** docs/superpowers/specs/2026-10-03-simple-inventory-design.md

## Global Constraints

- Awal 3×3, setiap dimensi 1–8, maksimum 8×8.
- Langkah slot grid dan baris dokumen 100 unit Canvas.
- Dokumen tidak memakai kapasitas item dan tidak dibatasi jumlahnya.
- Pertahankan desain UI, scene terbuka dan pekerjaan pengguna; tanpa autosave/commit.
- Belum membangun item/pickup/use/stack/shop/quick use.

## Review Focus

- Grid 8×8 tetap terjangkau melalui scroll dan tidak menumpuk.
- Membuka berulang tidak membuat duplikat slot.
- Upgrade mempertahankan urutan dan item; penyusutan yang kehilangan item ditolak.
- Cursor dan kunci input dipulihkan saat tutup/disable; kunci cerita tetap terpisah.
- Bootstrap mendukung objek inactive dan reload scene tanpa memasang komponen dua kali.

## Files

- Runtime/Inventory/InventoryEntry.cs, InventoryState.cs: data minimal dan kapasitas.
- Runtime/Inventory/InventoryView.cs: binding template, cloning slot, scroll, informasi pilihan dan panah.
- Runtime/Inventory/InventoryPanelController.cs: state, buka/tutup dan input locks.
- Runtime/Inventory/InventorySceneBootstrap.cs: integrasi runtime untuk scene yang sudah ada.
- Editor/InventorySceneSetup.cs: menu binding authoring dengan Undo.
- PlayerInputReader.cs, PlayerController.cs, PlayerCameraController.cs, PlayerInteractor.cs: aksi Journal/Pause dan kunci UI tersendiri.
- Tests/Editor/InventoryStateTests.cs: batas kapasitas dan resize.

## Tasks

- [x] Inspeksi hierarchy UI, input, controller dan status Editor.
- [x] Tambahkan state dan tes kapasitas, dokumen, resize, remove/clear.
- [x] Tambahkan view grid/dock berbasis template dan scroll.
- [x] Hubungkan toggle Tab/Esc, panah, detail pilihan dan kunci UI.
- [x] Tambahkan bootstrap runtime dan menu Editor opsional.
- [x] Kompilasi, pemeriksaan kritikus, dan verifikasi keterbatasan runtime secara jujur.

## Hasil

16 kasus tes data lulus menggunakan NUnit dan referensi Unity asli melalui runner sementara di /tmp. Kompilasi source runtime, Editor dan kedua assembly tes lulus. Referensi uGUI memakai nama assembly yang sebenarnya, UnityEngine.UI. Kritikus meninjau source dan binding scene, menemukan pelepasan cursor saat kamera dinonaktifkan; masalah itu diperbaiki dan ditinjau ulang tanpa temuan material tersisa.

Unity sedang terbuka, Pipeline tidak dapat dijangkau melalui CLI (termasuk pemeriksaan di luar sandbox). Tidak menjalankan Editor kedua atau menulis scene YAML. Integrasi runtime dipakai agar scene bisa langsung dicoba saat Play. Tampilan, scroll dan kontrol di Play Mode belum diverifikasi langsung. Menu setup menyediakan binding permanen dan pengaturan kapasitas di Inspector, dengan Undo dan tanpa autosave.
