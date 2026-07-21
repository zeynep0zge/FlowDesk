namespace FlowDesk.Common
{
    public static class DepartmentOptions
    {
        public static readonly IReadOnlyList<string> All =
        [
            "Çağrı Merkezi Uygulamaları",
            "Üye İşyeri ve POS",
            "ATM Uygulamaları",
            "Kartlı Sistemler 1",
            "Kartlı Sistemler 2",
            "Katılım Kartlı Sistemler",
            "Katılım Üye İşyeri POS ve ATM",
            "Ödeme Teknolojileri"
        ];

        public static bool Contains(string? department)
        {
            return department != null && All.Contains(department.Trim());
        }
    }
}
