using System.ComponentModel.DataAnnotations;

namespace POS.Models.Security
{
    public class UserActivityDTO
    {
        public long ActivityId { get; set; }
        public System.DateTime DateLogged { get; set; }
        public string Category { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
    }

    public class UserDTO
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string Salutation { get; set; }
        [Required]
        public string NameExtension { get; set; }
        [Required]
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        [Required]
        public string LastName { get; set; }
        public string NameExt { get; set; }
        [Required]
        public string Role { get; set; }
        [Required]
        public string Password { get; set; }
    }

    public class EditUserDTO : UserDTO
    {
        [Required]
        public string ConfirmPassword { get; set; }
    }
}
