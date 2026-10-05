using System.ComponentModel.DataAnnotations;

namespace POS.Domains.practicefolder
{
    public class MyInfo
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string FirtName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public int age { get; set; }
        [Required]
        public string courses { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public long PhoneNumber { get; set; }
    }
}
