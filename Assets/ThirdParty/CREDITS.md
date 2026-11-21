# Üçüncü taraf (ücretsiz) varlıklar

Bu projedeki tüm 3B modeller ve sesler **CC0 1.0 (Kamu Malı)** lisanslıdır: ticari dahil her
amaçla, atıf zorunluluğu olmadan kullanılabilir. Yine de teşekkür ederiz.

| Dosya (Assets/Resources/FreeAssets/…) | Kaynak paket | Yazar | Lisans |
|---|---|---|---|
| Models/race_car.bytes | Car Kit (`race.glb`) | Kenney — https://kenney.nl/assets/car-kit | CC0 |
| Models/tree_pine, tree_pine_tall, tree_oak, tree_palm, tree_fat, tree_autumn, bush_large, rock_large, tent | Nature Kit | Kenney — https://kenney.nl/assets/nature-kit | CC0 |
| Models/building_a, building_c, building_e, skyscraper_a, skyscraper_b | City Kit (Commercial) 2.1 | Kenney — https://kenney.nl/assets/city-kit-commercial | CC0 |
| Models/crate | Blaster Kit 2.1 (`crate-medium`) | Kenney — https://kenney.nl/assets/blaster-kit | CC0 |
| Audio/ui.ogg, confirm.ogg, impact.ogg, level_up.ogg | Interface / Impact / Digital Audio sound packs | Kenney — https://kenney.nl/assets | CC0 |
| Audio/menu_music.mp3 | "Battle Theme A" | cynicmusic — https://opengameart.org/content/battle-theme-a | CC0 |

Modeller, `@netforge/asset-library-files` npm paketindeki (CC0 dosyaların optimize edilmiş
kopyaları) GLB dosyalarından `Tools/AssetConverter/convert.mjs` ile projenin kendi küçük ikili
formatına (`MPK1`) dönüştürülmüştür; böylece Unity'de ek bir glTF içe aktarma paketi gerekmez.
CC0 lisans metni: `CC0-1.0.txt`.

Motor sesi, lastik sesi, rüzgâr ve bip sesleri oyun içinde prosedürel olarak sentezlenir
(`SynthAudio.cs`); pist, bariyer, tribün, pit binası ve dokular da çalışma anında üretilir.
