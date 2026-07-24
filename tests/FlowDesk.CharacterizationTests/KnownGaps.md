# Known gaps

| Problem | Neden Chapter 1A'da test edilmedi? | Ele alınacağı chapter |
|---|---|---|
| ProjectManager Create sırasında boş/null `RequestNumber` için erken `Trim()` hatası | Hatalı 500 davranışı başarı beklentisi olarak sabitlenmedi; invalid ModelState testi başka zorunlu alan üzerinden kuruldu | Chapter 2 — input validation |
| ProjectManager record-level authorization ve null `CreatedByUserId` davranışı | Mevcut güvenlik açığını başarı davranışı olarak sabitlememek için | Chapter 2 — authorization |
| Analyst kayıt sahipliği | Başka analistin kaydını değiştirebilme davranışı karakterize edilmedi | Chapter 2 — record-level authorization |
| DepartmentManager departman kapsamı | Departmanlar arası erişim beklenen davranış kabul edilmedi | Chapter 2 — authorization |
| WorkItem kullanıcı foreign key'leri ve sahte AnalystId/DeveloperId | SQLite karakterizasyon kapsamı veri modelini değiştirmiyor | Chapter 2 — veri bütünlüğü |
| Rate limiting | Bu chapter mevcut iş akışlarını koruyor; güvenlik kontrolü eklemiyor | Chapter 2 — endpoint güvenliği |
| RowVersion ve eşzamanlı güncelleme | SQLite, SQL Server `rowversion` davranışını temsil etmez | İleri concurrency chapter'ı / Testcontainers |
| Paralel OTP denemeleri | Concurrency ve kötüye kullanım testi bu chapter dışında | Chapter 2 — identity güvenliği |
| Reset token query-string riski | Mevcut risk başarı beklentisine dönüştürülmedi | Chapter 2 — password reset güvenliği |
| Çok adımlı işlem atomikliği | Transaction davranışı SQLite karakterizasyon kapsamı dışında | İleri veri bütünlüğü chapter'ı |
| Employee workflow eksikliği | Uygulamada korunacak çalışan Employee use-case'i bulunmuyor | Employee workflow chapter'ı |