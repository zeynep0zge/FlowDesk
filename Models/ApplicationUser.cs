using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        [Required]
        [StringLength(150)]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Department { get; set; }
    }
}