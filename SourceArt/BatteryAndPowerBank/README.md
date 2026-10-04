# Baterai litium-ion 18650 dan power bank

Dua model Blender untuk barang inventaris Bab 1. Baterai awal berlabel A telah diganti sesuai foto sel litium-ion biru. Teks gelap dicetak pada tekstur PVC yang mengikuti lengkungan silinder; tidak ada huruf timbul. Bentuk power bank mengikuti foto referensi pengguna: tipis, sudut membulat, dua output USB-A dan satu input USB-C. Model tidak memakai merek pada foto. Kabel USB-A ke USB-C pada keadaan awal terpasang di output pertama; ujung USB-C dibiarkan bebas.

| Aset | Sumber yang dapat diedit | FBX untuk Unity |
|---|---|---|
| Baterai litium-ion 18650 | `Baterai_LiIon_18650.blend` | `Assets/_Project/Art/Models/Props/Inventory/Baterai_LiIon_18650.fbx` |
| Power bank dan kabel | `Power_Bank_USB_A_to_C.blend` | `Assets/_Project/Art/Models/Props/Inventory/Power_Bank_USB_A_to_C.fbx` |

Baterai dibuat sekitar 18,6 mm diameter dan 65 mm panjang, masih lebih kecil dari diameter pegangan senter yang ada di `/home/ikbal/Dokumen/Flashlight.blend`. Tulisan generik **+ ICR 18650 2200mAh 3.7V** berada pada tekstur `LiIon_18650_Label.png`; gambar ini juga dikemas dalam file Blender. Power bank sekitar 68 × 133 × 13 mm.

Di Blender, pilih `KABEL_ROOT__GESER_UNTUK_MELEPAS` pada Outliner untuk memindahkan seluruh kabel beserta kedua konektornya. Port memiliki rongga fisik yang terbuka. Render detail port menyembunyikan kabel hanya saat pratinjau dibuat, sehingga ketiga port bisa diperiksa; file `.blend` tetap menyimpan kabel dalam keadaan tertancap. Render sementara disimpan di `/tmp/saksi-terakhir-generated/previews/BatteryAndPowerBank/`.

`build_models.py` membuat ulang kedua sumber Blender dan langsung mengekspor FBX ke folder Inventory Unity. `make_label_texture.py` menggunakan Pillow pada Python sistem untuk membuat tekstur baterai; dipanggil otomatis oleh skrip Blender. Argumen `--battery-only` membangun baterai saja.
