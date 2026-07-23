using System.ComponentModel.DataAnnotations;

namespace FlowDesk.ViewModels.Account
{
    public class VerifyEmailCodeViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Doğrulama kodu zorunludur.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Doğrulama kodu tam 6 haneli ve yalnızca rakamlardan oluşmalıdır.")]
        [Display(Name = "Doğrulama Kodu")]
        public string Code { get; set; } = string.Empty;
    }
}
