# MONOPOSTO GP — Unity Formül Yarış Oyunu

Tek kişilik, açık tekerlekli yarış arabalarıyla (monoposto) oynanan bir Unity yarış oyunu:
kokpit görünümü, gerçekçi araç fiziği, lastik/yakıt stratejisi ve pit stoplar, garajda araç
ayarı (setup), 6 pistlik şampiyona ve ayarlanabilir yapay zekâ rakipler.

Tüm görseller ve sesler **ücretsiz (CC0)** varlıklardan ya da çalışma anında prosedürel olarak
üretiliyor; paralı hiçbir asset kullanılmadı (ayrıntılar: `Assets/ThirdParty/CREDITS.md`).
Takımlar, pilotlar ve pistler tamamen kurgusaldır.

---

## Hızlı başlangıç

1. **Unity 6 (6000.0 LTS)** ile aç (2022.3 LTS de çalışır). Unity Hub → *Add project from disk* → bu klasör.
   Proje **Built-in Render Pipeline** kullanır (URP gerekmez).
2. İlk açılışta Unity *"Input System paketi etkinleştirilsin mi?"* diye sorarsa **Yes** de
   (editör yeniden başlar). Oyun hem yeni Input System'le hem eski Input Manager'la çalışır;
   gamepad desteği için yeni sistem gerekir.
3. `Monoposto GP` editör betiği ilk açılışta otomatik olarak:
   - `Assets/Scenes/Main.unity` sahnesini oluşturur ve Build Settings'e ekler,
   - `Assets/Resources/Materials` altına şablon materyalleri (Standard, Skybox) oluşturur.
   Gerekirse menüden tekrar çalıştır: **Monoposto GP → Projeyi Kur**.
4. **Play**'e bas. Sahnede hiçbir şey kurmana gerek yok: pist, araçlar, kamera, ses ve arayüz
   `GameBootstrap` tarafından çalışma anında oluşturulur.

## Kontroller

| Eylem | Klavye | Gamepad |
|---|---|---|
| Gaz / Fren | W / S (ok tuşları, Boşluk) | RT / LT |
| Direksiyon | A / D (← →) | Sol çubuk |
| Vites yükselt / düşür (manuel) | E / Q (Sol Shift / Sol Ctrl) | RB / LB |
| DRS | F | A |
| Kamera (Takip, Uzak, **Kokpit**, T-Kamera) | C | Y |
| Geriye bak | V | Sağ çubuk tık |
| Pit stratejisi menüsü | P / Tab (1-2-3 hamur, 4/5 yakıt, 6/7 ön kanat) | Back |
| Bu tur pite gir (BOX) | B | D-pad ↓ |
| Aracı piste geri koy | R | X |
| Duraklat (Garaj, Ayarlar, Yeniden başlat) | ESC | Start |

Durunca freni basılı tutmak (otomatik viteste) geri vitese takar. Menüler fareyle kullanılır.

---

## Kullanıcı hikâyeleri → oyunda karşılıkları

### 1. Tek kişilik kokpit ve araç kontrolü
* `CarController` — raycast süspansiyon (yay + amortisör + viraj demiri), yük duyarlı lastik modeli
  (sadeleştirilmiş Pacejka eğrisi + sürtünme dairesi), 8 ileri vites, debriyaj kaydırmalı kalkış,
  motor freni, fren dengesi, kaymalı/kilitlenen tekerlek davranışı.
* Aerodinamik: ön/arka kanattan downforce ve sürtünme, zemin etkisi, **vakum (slipstream)**,
  öndeki aracın **kirli havası** (ön downforce kaybı) ve **DRS**.
* Kokpit kamerası sürücünün kask konumunda: halo, dönen direksiyon ve ekranı, G kuvvetine göre baş
  hareketi, kerb'de titreşim. Ayrıca takip / uzak takip / T-kamera.
* Viraj çizgisi için **dinamik yarış çizgisi** (yeşil = gaz, sarı = gazı kes, kırmızı = fren),
  tur/sektör süreleri, zamana karşı modunda **hayalet araç** ve canlı **delta**.
* Asistler (ayarlardan): çekiş kontrolü, ABS, otomatik vites, direksiyon yardımı, fren yardımı.

### 2. Lastik ve yakıt stratejisi
* 3 hamur: **Yumuşak / Orta / Sert** (tutuş ↔ ömür). Aşınma, sürüş şiddetine (kayma, kilitlenme,
  patinaj, çim) bağlıdır; %60'tan sonra tutuş düşer, %80'den sonra "uçurum".
* Yakıt hem ağırlık (araç kütlesine eklenir) hem de tüketim olarak modellenir; dolu depo yarışın
  ~%70'ini götürür (kısa yarışlarda da strateji önemli kalsın diye yarış uzunluğuna göre ölçeklenir).
* Yarış öncesi ekranda başlangıç hamuru ve yakıtı seçilir; yarışta **P** ile pit planı (hamur,
  eklenecek yakıt, ön kanat ayarı) yapılır, mühendis önerisi gösterilir.
* AI pilotlar da aynı kurallarla strateji yapar (lastik ömrü/yakıta göre pit, kalan tura göre hamur).

### 3. Aerodinamik ve araç ayarları (Garaj)
* Ön/arka kanat, ön/arka süspansiyon sertliği, sürüş yüksekliği, fren dengesi, son dişli oranı,
  1. ve 8. vites oranı. Her pist için ayrı kaydedilir; 3 hazır ayar (düşük/dengeli/yüksek downforce).
* Garaj ekranı canlı analiz gösterir: tahmini son hız, 250 km/sa'da downforce, sürtünme, 100 ve
  250 km/sa'da viraj tutuşu (g), aero denge ve her vitesin hız tablosu — önceki ayara göre farklarıyla.

### 4. Şampiyona ve sezon ilerlemesi
* 6 kurgusal pistlik takvim, 10 takım / 20 pilot (10 araçlık grid seçeneği de var).
* Puanlar 25-18-15-12-10-8-6-4-2-1, ilk 10'daysan en hızlı tura +1. Pilotlar ve takımlar
  klasmanı, galibiyet/podyum sayıları, sezon sonunda **şampiyonluk kupası** ekranı ve kupa dolabı.
* İlerleme `Application.persistentDataPath/monoposto_gp_save.json` dosyasına kaydedilir
  (sezona kaldığın yerden devam edebilirsin).

### 5. Yapay zekâ zorluk seviyesi
* İki bağımsız ayar: **AI Hızı** (yarış çizgisi hız profilinin %84–%104,5'i) ve **AI Agresifliği**
  (takip mesafesi, sollama denemeleri, yer bırakma, geç frenleme). Hazır seviyeler:
  Çaylak, Kolay, Orta, Zor, Efsane — ya da kaydırıcılarla özel ayar.
* AI, oyuncuyla **aynı fizik modelini** sürer (asistler açık): yarış çizgisi + saf takip (pure
  pursuit) direksiyonu, sürtünme elipsli hız planı, trafik/sollama mantığı, mavi bayrakta yol verme.

---

## Kabul kriterleri

**Araç ayarları:** Garajda **KAYDET** dendiğinde `GameManager.SaveGarage()` ayarı pist için
kaydeder ve yarış/antrenman sürerken `CarController.ApplySetup()` ile araca **anında** uygular
(kanat → downforce/sürtünme, yaylar/viraj demirleri, sürüş yüksekliği, fren dengesi, vites oranları).
Yarışta ESC → Garaj → Kaydet → Devam Et; fark pistte hemen hissedilir. Başsız simülasyonda ölçülen
örnek (Tempo Speedway, aynı sürücü): düşük downforce **341 km/sa** son hız / 41.6 sn tur, yüksek
downforce **308 km/sa** / 42.7 sn; çok alçak + yumuşak ayarda araç sürekli zemine vurup (kıvılcım)
turda ~1.1 sn kaybediyor.

**Pit stop:** Lastik aşınması %70'i geçince ya da yakıt kalan tura yetmeyecek kadar azalınca ekranda
yanıp sönen uyarı ve sesli bip çıkar ("LASTİKLER KRİTİK — PİTE GİR!", "YAKIT AZALIYOR…"). **B** ile pit
isteği verilir (ya da pit girişinden şeride girilir). Pit girişinde araç otomatik olarak **80 km/sa hız
sınırlayıcıyla** takım garajına gider, seçilen **lastik hamuru takılır ve yakıt eklenir** (yakıt
miktarı süreyi uzatır), sonra pit çıkışında pistin içinden kontrol oyuncuya geri verilir.

---

## Test

Unity'yi bu geliştirme ortamında çalıştırmak mümkün olmadığından:

1. Tüm scriptler Unity referans DLL'lerine karşı derlendi (eski Input Manager ve yeni Input
   System yolları ayrı ayrı; editör betiği UnityEditor'a karşı).
2. `Tools/HeadlessSim`, **gerçek oyun kodunu** (CarController, AIDriver, RaceManager, TrackData,
   pit stop mantığı…) Unity'nin küçük bir yönetilen kopyası üzerinde çalıştırır: 20 araçlık tam
   yarışlar, pit stoplar, zamana karşı + hayalet. Örnek: `cd Tools/HeadlessSim && ./summary.sh 5 20 60`

5 turluk, 20 araçlık Anadolu Park yarışında (başsız simülasyon) AI zorluğunun etkisi:

| Seviye | En iyi tur | Medyan en iyi tur | Kazananın süresi | Piste geri konma |
|---|---|---|---|---|
| Çaylak | 70.93 | 72.50 | 383.7 sn | 0 |
| Kolay | 67.75 | 70.13 | 367.0 sn | 0 |
| Orta | 64.69 | 66.09 | 354.6 sn | 0 |
| Zor | 62.09 | 63.39 | 345.8 sn | 0 |
| Efsane | 60.02 | 61.42 | 337.9 sn | 1 |

Bu testler sırasında bulunup düzeltilenler: virajda frenlerken arkanın kayması (ABS'in motor freni
ile fren kuvvetini toplam olarak sınırlaması, sürtünme elipsli hız planı), pit çıkışında çimden
pist kenarına bırakılma, yakıt ölçeğinin 3 turluk yarışlarda aşırı olması.

---

## Proje yapısı

```
Assets/
  Scripts/
    Core/     GameBootstrap, GameManager (akış), GameData (takım/pilot/pist), GameSettings,
              SaveSystem (JSON), Championship, CameraRig
    Vehicle/  CarController (fizik), CarSetup, VehicleSpec, Tyres, CarVisual, CarAudio
    Track/    TrackData (spline, yarış çizgisi, hız profili, pit şeridi), TrackBuilder (mesh), TrackWorld
    Race/     RaceManager (zamanlama, sıralama, pit, DRS, sonuçlar), AIDriver, PlayerDriver,
              RacingLineDisplay, GhostRecorder, RaceCar
    UI/       GameUI (+ .Garage, .Hud, .Results), UI (IMGUI stilleri)
    Util/     MeshPack (CC0 model yükleyici), MeshBuilder, MaterialFactory, ProceduralTextures,
              SynthAudio, GameInput, Compat (Unity 2022 ↔ 6 API farkları)
  Editor/     ProjectSetup (sahne + materyaller + build ayarları)
  Resources/FreeAssets/  CC0 modeller (*.bytes) ve sesler
  ThirdParty/ Lisanslar ve teşekkürler
Tools/
  AssetConverter/  Kenney glTF/GLB → MPK1 dönüştürücü (Node.js) + önizleme
  TrackDesigner/   Pist kontrol noktalarını doğrulayan/çizen Python aracı
  HeadlessSim/     Unity'siz yarış simülatörü (fizik/AI/strateji regresyon testi)
```

### Yeni ücretsiz model eklemek
Kenney (CC0) gibi kaynaklardan bir `.glb` indir, sonra:
```
cd Tools/AssetConverter && npm install
node convert.mjs model.glb ../../Assets/Resources/FreeAssets/Models/model.bytes --center
```
Kodda `MeshPack.Load("model")` ile kullan. Araç modeli için `race_car.bytes` ile aynı parça
isimlerini (`body`, `wheel-front-left`, …) taşıyan bir GLB yeterli.

### Yeni pist eklemek
`GameData.Tracks` dizisine kontrol noktaları ekle (0 numaralı nokta ana düzlüğün ortasında olmalı,
pit sağ tarafta kurulur). Önce `Tools/TrackDesigner/tracks.py` ile uzunluk, en dar viraj ve
pist bölümleri arası mesafeyi kontrol et.

---

## Bilinen sınırlamalar / sonraki adımlar
* Çok oyunculu mod yok (başlıkta geçiyor ama hikâyelerde tanımlı değil); Netcode for GameObjects
  ile çevrim içi ya da bölünmüş ekran yerel çok oyunculu eklenebilir.
* Hava durumu, yağmur lastikleri, hasar modeli ve sıralama turları yok (grid, "Başlangıç" ayarı
  ve simüle edilmiş sıralamayla belirlenir).
* Arayüz IMGUI ile yazıldı (sahne/prefab hazırlığı gerektirmemesi için); menüler fareyle kullanılır.
* Pist yükseltisi yok (düz pistler).
