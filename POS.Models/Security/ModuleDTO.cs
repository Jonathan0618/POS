namespace POS.Models.Security
{
    public class ModuleDTO
    {
        public int ModuleId { get; set; }
        public int? ParentModuleId { get; set; }
        public string Name { get; set; }
    }
}
