# 🚀 FlowDesk

FlowDesk, kurum içindeki yazılım taleplerinin oluşturulması, analiz edilmesi, onaylanması ve yazılımcılara atanmasını yöneten web tabanlı bir iş akışı uygulamasıdır.

## 🔄 İş Akışı

```text
İş Birimi
    ↓
Analist
    ↓
Departman Yöneticisi
    ↓
Yazılımcı
```

## 👥 Kullanıcı Rolleri

- **İş Birimi:** Talep oluşturur ve kendi taleplerini takip eder.
- **Analist:** Gelen talepleri analiz eder, düzenler ve maliyet/süre çalışması yapar.
- **Departman Yöneticisi:** Tüm süreci takip eder, talepleri onaylar ve yazılımcılara atar.
- **Yazılımcı:** Kendisine atanan işleri görüntüler ve durumlarını günceller.

## ✅ Mevcut Özellikler

- Talep oluşturma ve takip
- Analist inceleme süreci
- Yönetici onay ve geri gönderme işlemleri
- Excel raporu oluşturma
- ASP.NET Core Identity ile giriş sistemi
- Güvenli şifre saklama
- Rol bazlı kullanıcı yapısı
- Role özel dashboard tasarımları

## 🛠️ Kullanılan Teknolojiler

- .NET 8
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- Bootstrap
- ClosedXML

## 📌 Planlanan Özellikler

- Rol ve kayıt bazlı yetkilendirme
- Admin paneli
- Şifremi unuttum ve şifre sıfırlama
- E-posta doğrulama
- İş Birimi–Analist chat ekranı
- AI destekli talep analizi
- Gelişmiş Excel yönetimi
- Profesyonel UI/UX
- Bildirim ve audit log sistemi

## ▶️ Projeyi Çalıştırma

```powershell
dotnet restore
dotnet ef database update
dotnet run
```

> 🚧 FlowDesk geliştirme aşamasındadır.
