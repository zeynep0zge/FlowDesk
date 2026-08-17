# FlowDesk

FlowDesk, kurum içindeki yazılım taleplerini fikir aşamasından analize, yönetici onayından geliştirici görünürlüğüne kadar yöneten web tabanlı bir iş akışı uygulamasıdır.

Proje; kimlik doğrulama, rol ve kayıt bazlı yetkilendirme, durum geçişleri, analist–İş Birimi iletişimi, insan denetimli yapay zekâ desteği, Excel çıktısı ve e-posta süreçlerini tek bir ASP.NET Core uygulamasında bir araya getirir.

> Proje aktif geliştirme aşamasındadır. Bu README, yalnızca hedeflenen yapıyı değil, mevcut kodun gerçekten uyguladığı davranışları anlatır. Henüz tamamlanmamış akışlar ayrıca belirtilmiştir.

## İçindekiler

- [Projenin çözdüğü problem](#projenin-çözdüğü-problem)
- [Kullanıcı rolleri](#kullanıcı-rolleri)
- [Uçtan uca iş akışı](#uçtan-uca-iş-akışı)
- [Workflow durumları](#workflow-durumları)
- [Mevcut özellikler](#mevcut-özellikler)
- [Teknik mimari](#teknik-mimari)
- [Dizin yapısı](#dizin-yapısı)
- [Temel veri modeli](#temel-veri-modeli)
- [Önemli teknik bileşenler](#önemli-teknik-bileşenler)
- [Güvenlik modeli](#güvenlik-modeli)
- [Kullanılan teknolojiler](#kullanılan-teknolojiler)
- [Yerel geliştirme ortamı](#yerel-geliştirme-ortamı)
- [Docker ile çalıştırma](#docker-ile-çalıştırma)
- [Yapılandırma referansı](#yapılandırma-referansı)
- [Migration ve başlangıç verileri](#migration-ve-başlangıç-verileri)
- [Testler](#testler)
- [Bilinen sınırlar ve geliştirme alanları](#bilinen-sınırlar-ve-geliştirme-alanları)

## Projenin çözdüğü problem

Kurumsal bir yazılım talebi yalnızca açıklama metninden oluşmaz. Talebin:

- kim tarafından oluşturulduğu,
- hangi departmanla ilgili olduğu,
- hangi analist tarafından incelendiği,
- hangi geliştiriciye yönlendirildiği,
- hangi aşamada bulunduğu,
- yönetici tarafından neden onaylandığı, iade edildiği veya reddedildiği,
- taraflar arasında hangi mesajların paylaşıldığı

kontrollü ve izlenebilir biçimde yönetilmelidir.

FlowDesk bu süreci rol bazlı ekranlara ve merkezi iş kurallarına ayırır. Böylece kullanıcılar yalnızca yetkili oldukları kayıtları görür; bir talep, tanımlı workflow kuralları dışında değiştirilemez.

## Kullanıcı rolleri

| Kullanıcıya görünen rol | Teknik rol | Temel sorumluluk |
|---|---|---|
| İş Birimi | `ProjectManager` | Talep oluşturur, kendi taleplerini takip eder, uygun aşamada düzenler/siler ve analistle mesajlaşır. |
| Analist | `Analyst` | Talebi sahiplenir, analiz alanlarını doldurur, AI desteğini kullanır ve yönetici onayına gönderir. |
| Departman Yöneticisi | `DepartmentManager` | Bekleyen talepleri onaylar, analiste iade eder veya reddeder; kullanıcı hesaplarını onaylar ve ortak Excel'i yönetir. |
| Yazılımcı | `Employee` | Kendisine atanmış ve onaylanmış talepleri salt okunur biçimde görüntüler. |

İş Birimi, Analist ve Yazılımcı rolleri kayıt ekranından hesap açabilir. Departman Yöneticisi rolü self-registration kapsamı dışındadır.

Mevcut kodda Departman Yöneticisi kapsamı `Tüm Departmanlar` olarak çözülür. Bu nedenle yöneticiler departman filtresiyle sınırlandırılmadan yönetim işlemlerini gerçekleştirebilir.

## Uçtan uca iş akışı

```text
Kullanıcı kayıt olur
        │
        ├── E-posta adresini 6 haneli kodla doğrular
        │
        └── Departman Yöneticisi hesabı onaylar ve rolü atar
                │
                ▼
İş Birimi talep oluşturur
        │
        ▼
Talep analist havuzuna düşer
        │
        ▼
Analist talebi sahiplenir ve analiz eder
        │
        ├── İş Birimi ile talep bağlamında mesajlaşır
        ├── AI ile metni yeniden düzenleyebilir
        └── Geliştirici, tarihler, statüler ve notları belirler
                │
                ▼
Departman Yöneticisi karar verir
        │
        ├── Onay ──► Approved ──► Ortak Excel'e yazılır
        ├── İade  ──► ReturnedToAnalyst ──► Analist yeniden çalışır
        └── Ret   ──► Rejected
                │
                ▼
Atanmış Yazılımcı onaylanan işi görüntüler
```

### Hesap aktivasyonu

Yeni hesabın kullanılabilmesi iki ayrı doğrulamaya bağlıdır:

1. Kullanıcı e-posta adresine gönderilen doğrulama kodunu girer.
2. Departman Yöneticisi hesabı ve talep edilen rolü onaylar.
3. Sistem kullanıcıya rol bazlı benzersiz bir iş kodu üretir.
4. Kullanıcı giriş yaparak rolüne ait ekrana yönlendirilir.

Bu ayrım, e-posta sahipliğinin doğrulanması ile kurumsal rol yetkisinin verilmesini birbirinden ayırır.

### İş Birimi akışı

- Talep numarası kullanıcı tarafından girilmez; sistem tarafından üretilir.
- Kullanıcı yalnızca kendi oluşturduğu taleplere erişebilir.
- Talep yalnızca `Submitted` durumundayken düzenlenebilir veya silinebilir.
- Analist atandıktan sonra talep detayından mesaj gönderilebilir.
- Detay ekranı açıldığında karşı tarafın okunmamış mesajları okunmuş olarak işaretlenir.

### Analist akışı

- Analist, havuzdaki yeni talebi sahiplenir.
- Sahiplenilen talep üzerinde başka bir analistin işlem yapması engellenir.
- Geliştirici, sürüm tarihi, Banksoft teslim tarihi, beklenen/mevcut statü ve analist notu girilir.
- Gemini destekli yeniden yazım ile orijinal talep değiştirilmeden ayrı bir AI taslağı oluşturulur.
- Belirsiz terimler ayrıca araştırılabilir ve kaynaklarla zenginleştirilebilir.
- Analiz tamamlandığında talep yönetici onayına gönderilir.
- Yönetici tarafından iade edilen talepler ayrı listede gösterilir ve yeniden işlenebilir.

### Departman Yöneticisi akışı

- Onay bekleyen talepleri inceler.
- Talebi onaylayabilir, açıklamayla analiste iade edebilir veya reddedebilir.
- Onaylanan kayıt, ClosedXML kullanılarak ortak Excel dosyasına eklenir veya güncellenir.
- Onaylanmış talepleri uygulama üzerinden düzenleyebilir ve Excel dosyasını indirebilir.
- E-postasını doğrulamış fakat henüz aktif edilmemiş kullanıcıları onaylar.

### Yazılımcı akışı

- Yalnızca kendisine atanmış ve `Approved` durumundaki kayıtları görür.
- Liste ve detay ekranları mevcut uygulamada salt okunurdur.
- `InProgress` ve `Completed` durumlarına geçiş yapacak üretim aksiyonları henüz uygulanmamıştır.

## Workflow durumları

`WorkflowStatus`, sistemin geçiş ve yetki kararlarında kullandığı gerçek durumdur.

| Değer | Teknik ad | Anlam |
|---:|---|---|
| 1 | `Submitted` | Analist incelemesi bekliyor |
| 2 | `UnderAnalystReview` | Analist incelemesinde |
| 3 | `WaitingManagerApproval` | Departman Yöneticisi onayı bekliyor |
| 4 | `ReturnedToAnalyst` | Yönetici tarafından analiste iade edildi |
| 5 | `Approved` | Yönetici tarafından onaylandı |
| 6 | `Assigned` | Yazılımcıya atandı |
| 7 | `InProgress` | Geliştirme devam ediyor |
| 8 | `Completed` | Tamamlandı |
| 9 | `Rejected` | Reddedildi |
| 10 | `ReturnedToBusinessUnit` | İş Birimine iade edildi |

Production kodunda aktif ana geçişler:

```text
Submitted → UnderAnalystReview
UnderAnalystReview → WaitingManagerApproval
ReturnedToAnalyst → UnderAnalystReview
WaitingManagerApproval → Approved
WaitingManagerApproval → ReturnedToAnalyst
WaitingManagerApproval → Rejected
```

`Assigned`, `InProgress`, `Completed` ve `ReturnedToBusinessUnit` modelde tanımlıdır; ancak bunların tamamını yöneten uçtan uca production aksiyonları henüz mevcut değildir.

`CurrentStatus`, workflow durumundan farklı olarak kullanıcıya işin operasyonel aşamasını anlatan metinsel bir alandır. Desteklenen değerler:

- Analist İncelemesinde
- Geliştirme Bekliyor
- Geliştirme Devam Ediyor
- Test Bekliyor
- Sürüme Hazır

## Mevcut özellikler

### Talep ve workflow yönetimi

- Talep oluşturma, listeleme, detay, düzenleme ve silme
- Sistem tarafından benzersiz talep numarası üretimi
- Öncelik ve departman seçimi
- Analist sahiplenme ve analiz ekranı
- Yönetici onay, iade ve ret işlemleri
- Merkezi `WorkflowStatusPolicy` ile durum geçişi koruması
- Eşzamanlı güncellemeler için `RowVersion` kontrolü

### Hesap ve kimlik yönetimi

- ASP.NET Core Identity tabanlı cookie authentication
- E-posta doğrulama
- Yönetici onaylı hesap aktivasyonu
- Şifremi unuttum ve doğrulama koduyla şifre sıfırlama
- Rol bazlı benzersiz kullanıcı iş kodları
- Role özel yönlendirme ve ekranlar

### İletişim ve bildirim

- İş Birimi–Analist mesajlaşması
- Mesaj geçmişinin talep bazında saklanması
- Okundu/okunmadı durumu ve okunma zamanı
- Navbar üzerinde okunmamış mesaj bildirimi
- Analist geri bildirim durumu ve açıklaması

### AI destekli analiz

- Google Gemini ile talep metnini yapılandırılmış biçimde yeniden yazma
- Kısaltma ve belirsizlik analizi
- Çözülemeyen terimler için Google Search grounding tabanlı araştırma
- JSON Schema ile beklenen çıktı biçiminin sınırlandırılması
- Orijinal metni koruyan kalıcı ve düzenlenebilir AI taslağı
- AI taslağında optimistic concurrency kontrolü

### Excel yönetimi

- Onaylanan taleplerin `.xlsx` dosyasına yazılması
- Aynı talep numarası için satır güncelleme
- Uygulama içinden ortak Excel görüntüleme ve düzenleme
- Excel dosyasını indirme
- Aynı process içindeki eşzamanlı yazmaları `SemaphoreSlim` ile seri hale getirme

### Operasyon ve kalite

- Docker multi-stage build
- SQL Server ve uygulama için Docker Compose ortamı
- SQL healthcheck ve isteğe bağlı startup migration
- Rol ve geliştirme kullanıcılarının seed edilmesi
- xUnit tabanlı characterization/integration testleri
- Hesap işlemlerinde IP tabanlı rate limiting

## Teknik mimari

FlowDesk, katmanlara ayrılmış bir **modüler monolit** olarak tasarlanmıştır. MVC arayüzü, uygulama servisleri ve veri erişimi aynı deploy edilebilir .NET uygulamasında bulunur; sorumluluklar sınıf ve klasör seviyesinde ayrıştırılır.

```text
Tarayıcı
   │ HTTP GET/POST
   ▼
Razor View + MVC Controller
   │ DTO / ViewModel
   ▼
Application Service
   │ doğrulama + kayıt yetkisi + workflow kuralı
   ▼
Repository
   │ LINQ / EF Core
   ▼
SQL Server

Application Service gerektiğinde:
   ├── SMTP / MailKit ile e-posta gönderir
   ├── Gemini HTTPS API ile AI analizi yapar
   └── ClosedXML ile Excel dosyası üretir
```

### Katmanların sorumlulukları

| Katman | Sorumluluk |
|---|---|
| Razor Views | Formları, listeleri ve rol bazlı ekranları server-rendered HTML olarak sunar. |
| Controllers | HTTP girdisini alır, antiforgery/rol sınırını uygular, servisi çağırır ve sonucu HTTP cevabına dönüştürür. |
| Services | İş kuralları, validasyon, sahiplik kontrolü, workflow geçişi ve dış servis koordinasyonunu yürütür. |
| Repositories | EF Core sorgularını ve veri yazma işlemlerini kapsüller. |
| Models/Entities | Domain ve kalıcı veri yapılarını tanımlar. |
| DTOs/ViewModels | HTTP form verisi ile ekrana özel veri şekillerini domain modelinden ayırır. |
| Options | SMTP, Gemini ve Excel ayarlarını strongly typed configuration olarak temsil eder. |

Controller'lar doğrudan karmaşık veri sorgusu veya workflow kararı vermek yerine servis sonuçlarını `View`, redirect, `404`, `403`, `400` veya `409` cevaplarına dönüştürür.

Servisler beklenen iş hatalarını çoğunlukla `ServiceResult` ile ifade eder:

- `Success`
- `Failure`
- `NotFound`
- `Forbidden`
- `Conflict`

### Tipik bir istek nasıl ilerler?

Analistin talebi yönetici onayına göndermesi örneğinde:

1. Razor formu analiz alanlarını controller'a POST eder.
2. Identity cookie kullanıcının kimliğini belirler; `[Authorize]` analist rolünü doğrular.
3. Controller, DTO ve mevcut kullanıcı ID'si ile `AnalystWorkflowService` çağırır.
4. Servis kayıt sahipliğini, workflow durumunu, zorunlu alanları ve tarihleri doğrular.
5. Seçilen geliştiricinin uygun rol ve hesap koşullarını kontrol eder.
6. Repository, EF Core üzerinden `WorkItem` kaydını günceller.
7. Talep `WaitingManagerApproval` durumuna geçer.
8. Controller kullanıcıyı analist gelen kutusuna yönlendirir.

## Dizin yapısı

```text
FlowDesk/
├── Ai/                       # Gemini entegrasyonu, prompt, schema ve AI taslakları
├── Common/                   # Departman seçenekleri ve ortak yardımcı yapılar
├── Constants/                # Roller ve sabit durum seçenekleri
├── Controllers/              # MVC HTTP giriş noktaları
├── Data/                     # AppDbContext ve Identity başlangıç verileri
├── DTOs/                     # Form/komut veri nesneleri
├── Migrations/               # EF Core SQL Server migration geçmişi
├── Models/                   # Domain ve Identity modelleri
├── Options/                  # Strongly typed configuration sınıfları
├── Repositories/             # EF Core veri erişim katmanı
├── Services/                 # İş kuralları ve dış servis koordinasyonu
├── ViewComponents/           # Ortak UI bileşenlerinin server-side mantığı
├── ViewModels/               # Ekrana özel veri modelleri
├── Views/                    # Razor görünümleri
├── wwwroot/                  # CSS, JavaScript ve statik dosyalar
├── tests/                    # xUnit characterization/integration test projesi
├── Dockerfile                # Uygulama container tanımı
├── docker-compose.yml        # Uygulama + SQL Server geliştirme ortamı
├── FlowDesk.csproj           # Ana .NET 8 web projesi
└── FlowDesk.sln              # Uygulama ve test çözümü
```

## Temel veri modeli

### `ApplicationUser`

`IdentityUser<int>` sınıfını genişletir:

- `FullName`
- `Department`
- `RequestedRole`
- `BusinessCode`
- `IsApproved`
- `CreatedAtUtc`

`BusinessCode` veritabanında benzersizdir. Rol bazlı ön ekler:

| Rol | Ön ek | Örnek biçim |
|---|---|---|
| İş Birimi | `ISB` | `ISB-ddMMyyyy-0000` |
| Analist | `ANL` | `ANL-ddMMyyyy-0000` |
| Departman Yöneticisi | `DYN` | `DYN-ddMMyyyy-0000` |
| Yazılımcı | `ENG` | `ENG-ddMMyyyy-0000` |

### `WorkItem`

Talebin merkezi kaydıdır. Başlıca alan grupları:

- Talep numarası, açıklama, departman ve öncelik
- Oluşturan kullanıcı, analist ve geliştirici ID'leri
- Sürüm ve Banksoft teslim tarihleri
- Beklenen/mevcut statü
- Analist ve yönetici notları
- Analist geri bildirim bilgileri
- Workflow, oluşturulma, güncellenme ve onay zamanları
- Eşzamanlılık için `RowVersion`

Talep numarası `TLP-ddMMyyyy-0000` biçiminde kriptografik rastgele sayı kullanılarak üretilir ve unique index ile korunur.

### `FeedbackMessage`

Talep üzerindeki mesajları ayrı satırlar halinde saklar:

- `WorkItemId`
- `SenderUserId`
- `Message`
- `CreatedAt`
- `IsRead`
- `ReadAt`

Talep silindiğinde mesajlar cascade silinir. Gönderen kullanıcı ilişkisi `Restrict` davranışındadır.

### `WorkItemAiDraft`

Her taleple bire bir ilişkili kalıcı AI taslağıdır:

- Üretilen ve analist tarafından düzenlenen talep metni
- Kısaltma, belirsizlik ve çözülemeyen terim JSON verileri
- Kullanılan model ve zaman bilgileri
- Optimistic concurrency için `RowVersion`

### `EmailVerificationRequest` ve `PasswordResetRequest`

Doğrulama kodlarının açık değerini değil hash'ini, son kullanma zamanını, yanlış deneme sayısını ve tamamlanma/geçersizleştirme durumunu saklar.

## Önemli teknik bileşenler

### Kayıt bazlı yetkilendirme

Rol kontrolü tek başına yeterli kabul edilmez. Servis ve repository sorguları kayıt kapsamını da uygular:

| Rol | Kayıt kapsamı |
|---|---|
| İş Birimi | `CreatedByUserId == currentUserId` |
| Analist | Yeni havuz talepleri veya kendisine atanmış talepler |
| Departman Yöneticisi | Mevcut uygulamada tüm departmanlar |
| Yazılımcı | `DeveloperId == currentUserId` ve `WorkflowStatus == Approved` |

`AuthenticatedActorContextResolver`, kullanıcının veritabanında mevcut, e-postası doğrulanmış, hesabı onaylı ve tam olarak bir desteklenen role sahip olmasını doğrular.

### AI çalışma biçimi

AI, workflow üzerinde tek başına karar vermez ve orijinal talebi doğrudan değiştirmez:

1. Analist yeniden yazım isteği gönderir.
2. Servis talep sahipliğini ve workflow durumunu doğrular.
3. Gemini'den JSON Schema'ya uygun yapılandırılmış yanıt ister.
4. Çözülemeyen terimler için ayrı araştırma servisi Google Search grounding kullanır.
5. Sonuç `WorkItemAiDraft` olarak saklanır.
6. Analist metni düzenleyebilir; kaydetmede `RowVersion` kontrol edilir.

Gemini çağrıları HTTP isteği içinde beklenir; projede message queue veya background AI worker bulunmaz.

### Mesaj ve bildirim modeli

- Mesajlar talep bağlamında kronolojik tutulur.
- Bir kullanıcı talep detayını açtığında karşı tarafın okunmamış mesajları işaretlenir.
- Navbar'daki `FeedbackNotificationsViewComponent`, okunmamış sayısını ve son mesajları veritabanından alır.
- Bildirimler SignalR/push tabanlı değildir; sayfa render edildiğinde güncellenir.

### Excel senkronizasyonu

Onaylanan talepler `App_Data/approved-work-items.xlsx` dosyasına yazılır. Talep numarası satır anahtarı olarak kullanılır; aynı talep tekrar yazıldığında mevcut satır güncellenir.

Veritabanı işlemi ile Excel dosya yazımı tek transaction değildir. Excel yazımı başarısız olursa veritabanındaki onay korunur ve kullanıcı uyarılır.

### Asenkron işlemler

SQL, SMTP, Gemini ve dosya erişimi `async/await` kullanır. Ancak queue veya background worker bulunmadığından dış servis işlemleri kullanıcı HTTP isteği tamamlanmadan sonuçlanır.

## Güvenlik modeli

### Kimlik doğrulama

- ASP.NET Core Identity ve cookie authentication
- Giriş için doğrulanmış e-posta zorunluluğu
- Uygulama seviyesinde yönetici onayı
- 5 başarısız girişten sonra 15 dakika hesap kilidi
- En az 8 karakter; rakam, büyük ve küçük harf gerektiren parola politikası
- Identity parola hash mekanizması
- Hash olarak saklanan e-posta ve şifre sıfırlama kodları

### Web güvenliği

- Veri değiştiren POST aksiyonlarında antiforgery doğrulaması
- Production ortamında exception handler ve HSTS
- HTTPS yönlendirmesi
- Kayıt, şifremi unuttum ve doğrulama kodu yeniden gönderme işlemlerinde IP tabanlı fixed-window rate limit
- Yetkisiz erişimde rol ve kayıt seviyesinde kontrol
- Gemini hata loglarında hassas içerik redaksiyonu
- Secret değerlerinin `.env`, environment variable veya .NET User Secrets üzerinden alınması


## Kullanılan teknolojiler

| Alan | Teknoloji |
|---|---|
| Runtime | .NET 8 |
| Web | ASP.NET Core MVC, Razor Views |
| Authentication | ASP.NET Core Identity |
| ORM | Entity Framework Core 8 |
| Veritabanı | SQL Server |
| UI | Bootstrap, HTML, CSS, JavaScript |
| E-posta | MailKit / SMTP |
| AI | Google Gemini Generate Content API |
| Excel | ClosedXML |
| Test | xUnit, `Microsoft.AspNetCore.Mvc.Testing`, SQLite test veritabanı |
| Container | Docker, Docker Compose |

## Yerel geliştirme ortamı

### Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server veya SQL Server Express
- EF Core CLI (`dotnet-ef`)
- E-posta akışlarını kullanmak için SMTP hesabı
- AI akışını kullanmak için Gemini API anahtarı

### 1. Depoyu klonlayın

```powershell
git clone https://github.com/zeynep0zge/FlowDesk.git
cd FlowDesk
```

### 2. Bağımlılıkları yükleyin

```powershell
dotnet restore FlowDesk.sln
```

EF Core CLI yüklü değilse:

```powershell
dotnet tool install --global dotnet-ef
```

### 3. Geliştirme secret'larını ayarlayın

Proje `UserSecretsId` içerir. Hassas değerleri `appsettings.json` yerine User Secrets ile saklayın:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\\SQLEXPRESS;Database=FlowDeskDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"

dotnet user-secrets set "Gemini:ApiKey" "GEMINI_API_ANAHTARINIZ"

dotnet user-secrets set "EmailSettings:Host" "SMTP_HOST"
dotnet user-secrets set "EmailSettings:Port" "587"
dotnet user-secrets set "EmailSettings:SenderName" "FlowDesk"
dotnet user-secrets set "EmailSettings:SenderEmail" "noreply@example.com"
dotnet user-secrets set "EmailSettings:Username" "SMTP_KULLANICI_ADI"
dotnet user-secrets set "EmailSettings:Password" "SMTP_SIFRESI"
```

Bağlantı dizesini kendi SQL Server instance adınıza göre değiştirin.

### 4. Veritabanını oluşturun

```powershell
dotnet ef database update
```

### 5. Uygulamayı çalıştırın

```powershell
dotnet run --project FlowDesk.csproj
```

Terminalde gösterilen `https://localhost:...` adresini açın.

### Development test kullanıcıları

Uygulama `Development` ortamında aşağıdaki hesapları otomatik oluşturur:

| Rol | E-posta | Parola |
|---|---|---|
| İş Birimi | `projectmanager@flowdesk.local` | `FlowDesk123!` |
| Analist | `analyst@flowdesk.local` | `FlowDesk123!` |
| Departman Yöneticisi | `manager@flowdesk.local` | `FlowDesk123!` |
| Yazılımcı | `employee@flowdesk.local` | `FlowDesk123!` |

Bu hesaplar yalnızca geliştirme/test kolaylığı içindir. Production ortamında seed edilmez.

Development ortamındaki yetkili bir kullanıcı, SMTP bağlantısını şu endpoint üzerinden deneyebilir:

```text
GET /dev/send-test-email
```

Bunun için `EmailSettings:TestRecipient` ayarı da tanımlanmalıdır.

## Docker ile çalıştırma

Docker Compose, uygulamayı ve SQL Server 2022 Developer container'ını birlikte başlatır.

### 1. Ortam dosyasını oluşturun

```powershell
Copy-Item .env.example .env
```

`.env` içindeki tüm `CHANGE_ME_...` değerlerini doldurun. SQL Server parolası güçlü olmalıdır.

### 2. Container'ları başlatın

```powershell
docker compose up --build
```

Uygulama:

```text
http://localhost:8080
```

SQL Server host portu:

```text
localhost:1433
```

Compose ortamında:

- SQL Server healthcheck tamamlanmadan uygulama başlatılmaz.
- `Database__ApplyMigrationsOnStartup=true` ile migration'lar otomatik uygulanır.
- Migration bağlantısı için en fazla 10 deneme yapılır.
- SQL verisi `flowdesk-sql-data` volume'unda tutulur.
- Excel ve `App_Data` içeriği `flowdesk-app-data` volume'unda tutulur.
- Uygulama container içinde root olmayan `app` kullanıcısıyla çalışır.

### Container yönetimi

```powershell
# Logları takip et
docker compose logs -f flowdesk-app

# Container'ları durdur
docker compose down

# Container'ları ve volume'ları kaldır
# Dikkat: Bu komut veritabanı ve App_Data verilerini siler.
docker compose down -v
```


## Migration ve başlangıç verileri

Yeni model değişikliğinde migration oluşturmak için:

```powershell
dotnet ef migrations add MigrationAdi
dotnet ef database update
```

Migration dosyaları ve `AppDbContextModelSnapshot.cs` birlikte commit edilmelidir.

Uygulama açılışında:

1. Ayar etkinse veritabanı migration'ları uygulanır.
2. Dört Identity rolü oluşturulur.
3. `Development` ortamında test kullanıcıları seed edilir.
4. Eski kullanıcıların eksik iş kodları tamamlanır.

Production ortamında test kullanıcıları oluşturulmaz.

## Testler

Test projesi, uygulamanın mevcut davranışını koruyan characterization ve integration testlerinden oluşur.

Kapsanan başlıca alanlar:

- Kayıt, e-posta doğrulama ve şifre sıfırlama
- Hesap rate limiting
- Rol ve kayıt bazlı yetkilendirme
- İş Birimi, Analist, Yönetici ve Yazılımcı akışları
- Workflow durum kuralları
- Kullanıcı ve talep numarası üretimi
- AI taslakları ve Gemini response işleme
- Analist geri bildirimi ve mesaj okuma durumu
- Excel oluşturma, düzenleme ve indirme

Tüm testleri çalıştırmak için:

```powershell
dotnet test FlowDesk.sln
```

Release build doğrulaması:

```powershell
dotnet build FlowDesk.sln --configuration Release
```

Coverage toplamak için:

```powershell
dotnet test FlowDesk.sln --collect:"XPlat Code Coverage"
```

Test altyapısı `WebApplicationFactory`, sahte authentication/e-posta/Excel servisleri ve hızlı izolasyon için SQLite kullanır. SQL Server'a özgü migration ve `rowversion` davranışları ayrıca gerçek SQL Server ortamında doğrulanmalıdır.



## Kısa teknik özet

FlowDesk, .NET 8 üzerinde çalışan server-rendered bir ASP.NET Core MVC uygulamasıdır. Identity kullanıcıyı doğrular; controller rol sınırını, servis kayıt sahipliğini ve workflow kuralını uygular; repository EF Core üzerinden SQL Server ile konuşur. Gemini analiste yardımcı taslak üretir, SMTP hesap süreçlerindeki kodları iletir, ClosedXML ise onaylı talepleri ortak Excel dosyasına aktarır.

Mimari; tek deploy edilebilir uygulamanın operasyonel sadeliğini korurken controller, service, repository, DTO/ViewModel ve entity ayrımlarıyla iş kurallarını test edilebilir tutar. En güçlü tarafları kayıt bazlı yetkilendirme, insan denetimli AI kullanımı, optimistic concurrency ve kapsamlı characterization testleridir. Temel geliştirme alanları ise tamamlanmamış workflow geçişleri, dosya tabanlı Excel senkronizasyonu ve background processing/observability eksikliğidir.
