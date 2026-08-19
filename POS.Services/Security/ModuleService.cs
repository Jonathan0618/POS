using POS.Domains.Security;
using POS.Models.Security;
using POS.Services.Repository;
using System.Collections.Generic;
using System.Linq;

namespace POS.Services.Security
{
    public class ModuleService
    {
        private readonly BaseRepository<Module, int> _moduleRepo;
        public ModuleService()
        {
            _moduleRepo = new BaseRepository<Module, int>();
        }

        public IEnumerable<ModuleDTO> GetAllModules()
        {
            return _moduleRepo.GetAll().Select(x => new ModuleDTO
            {
                ModuleId = x.Id,
                Name = x.Name,
                ParentModuleId = x.ParentModuleId,
            }).ToList();
        }

        public void AddModule(ModuleDTO module)
        {
            var newModule = new Module
            {
                Name = module.Name
            };
            _moduleRepo.Add(newModule);
        }

        public void UpdateModule(ModuleDTO module)
        {
            var existing = _moduleRepo.GetById(module.ModuleId);
            existing.ParentModuleId = module.ModuleId;
            existing.Name = module.Name;
            _moduleRepo.Update(existing);
        }

        public void DeleteModule(ModuleDTO module)
        {
        }
    }
}
