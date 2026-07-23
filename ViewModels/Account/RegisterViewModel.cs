using FlowDesk.Constants;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace FlowDesk.ViewModels.Account
{
    public class RegisterViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Ad soyad zorunludur.")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Ad soyad 3 ile 150 karakter arasında olmalıdır.")]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(256)]
        [Display(Name = "E-posta")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Departman seçimi zorunludur.")]
        [StringLength(100)]
        [Display(Name = "Departman")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Rol seçimi zorunludur.")]
        [Display(Name = "Talep Edilen Rol")]
        public string RequestedRole { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
        [Display(Name = "Şifre Tekrar")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [ValidateNever]
        public IReadOnlyList<SelectListItem> RoleOptions { get; set; } = [];

        [ValidateNever]
        public IReadOnlyList<SelectListItem> DepartmentOptions { get; set; } = [];

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!AppRoles.IsSelfRegistrable(RequestedRole))
            {
                yield return new ValidationResult(
                    "Geçerli bir rol seçiniz.",
                    [nameof(RequestedRole)]);
            }

            if (!FlowDesk.Common.DepartmentOptions.Contains(Department))
            {
                yield return new ValidationResult(
                    "Geçerli bir departman seçiniz.",
                    [nameof(Department)]);
            }
        }
    }
}
