using System.ComponentModel.DataAnnotations;

namespace FlowDesk.ViewModels.Account
{
    public class VerifyResetCodeViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Doğrulama kodu zorunludur.")]
        [RegularExpression(
            @"^\d{6}$",
            ErrorMessage = "Doğrulama kodu 6 haneli olmalıdır.")]
        [Display(Name = "Doğrulama Kodu")]
        public string Code { get; set; } = string.Empty;
    }
}