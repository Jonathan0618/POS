using POS.Domains.Security;
using POS.Models.Security;
using POS.Data.Context;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Core.Results;
using POS.Common.Enumerations;
using POS.Services.Repository;
using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Services.Security
{
    public class ModuleService : IDisposable
    {
        private readonly POSContext _context;
        private readonly BaseRepository<Module, int> _moduleRepo;
        private readonly bool _ownsContext;
        private readonly IAuthorizationService _authorization;

        public ModuleService() : this(new POSContext(), new ClaimsAuthorizationService(new CurrentUserAccessor()), true) { }
        public ModuleService(POSContext context) : this(context, new ClaimsAuthorizationService(new CurrentUserAccessor()), false) { }
        public ModuleService(POSContext context, IAuthorizationService authorization) : this(context, authorization, false) { }

        private ModuleService(POSContext context, IAuthorizationService authorization, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _ownsContext = ownsContext;
            _moduleRepo = new BaseRepository<Module, int>(_context);
        }

        public IEnumerable<ModuleDTO> GetAllModules()
        {
            Demand(ClaimActionType.View);
            return _moduleRepo.GetAll().Select(x => new ModuleDTO
            {
                ModuleId = x.Id,
                Name = x.Name,
                ParentModuleId = x.ParentModuleId,
            }).ToList();
        }

        public OperationResult AddModule(ModuleDTO module)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Add);
                var validation = Validate(module, false);
                if (!validation.Succeeded)
                    return validation;

                var newModule = new Module
                {
                    Name = module.Name.Trim(),
                    ParentModuleId = module.ParentModuleId
                };
                _moduleRepo.Add(newModule);
                module.ModuleId = newModule.Id;
                return OperationResult.Success();
            }
        }

        public OperationResult UpdateModule(ModuleDTO module)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Edit);
                var validation = Validate(module, true);
                if (!validation.Succeeded)
                    return validation;

                var existing = _moduleRepo.GetById(module.ModuleId);
                if (existing == null)
                    return OperationResult.Failure("The selected module no longer exists.");

                existing.ParentModuleId = module.ParentModuleId;
                existing.Name = module.Name.Trim();
                _moduleRepo.Update(existing);
                return OperationResult.Success();
            }
        }

        public OperationResult DeleteModule(ModuleDTO module)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Delete);
                if (module == null || module.ModuleId <= 0)
                    return OperationResult.Failure("A saved module is required.");

                var existing = _moduleRepo.GetById(module.ModuleId);
                if (existing == null)
                    return OperationResult.Failure("The selected module no longer exists.");
                if (_context.Set<Module>().Any(x => x.ParentModuleId == existing.Id))
                    return OperationResult.Failure("A module with child modules cannot be deleted.");
                if (_context.Set<RoleClaim>().Any(x => x.ModuleId == existing.Id))
                    return OperationResult.Failure("A module used by role permissions cannot be deleted.");

                _moduleRepo.Delete(existing);
                return OperationResult.Success();
            }
        }

        private OperationResult Validate(ModuleDTO module, bool isUpdate)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.Name))
                return OperationResult.Failure("Module name is required.");
            if (isUpdate && module.ModuleId <= 0)
                return OperationResult.Failure("A saved module is required.");
            if (isUpdate && module.ParentModuleId == module.ModuleId)
                return OperationResult.Failure("A module cannot be its own parent.");
            if (module.ParentModuleId.HasValue &&
                !_context.Set<Module>().Any(x => x.Id == module.ParentModuleId.Value))
                return OperationResult.Failure("The selected parent module no longer exists.");
            if (isUpdate && CreatesParentCycle(module.ModuleId, module.ParentModuleId))
                return OperationResult.Failure("The selected parent would create a module hierarchy cycle.");
            if (_context.Set<Module>().Any(x =>
                x.Name == module.Name.Trim() && (!isUpdate || x.Id != module.ModuleId)))
                return OperationResult.Failure("A module with this name already exists.");

            return OperationResult.Success();
        }

        private bool CreatesParentCycle(int moduleId, int? parentModuleId)
        {
            var visited = new HashSet<int>();
            while (parentModuleId.HasValue && visited.Add(parentModuleId.Value))
            {
                if (parentModuleId.Value == moduleId)
                    return true;
                parentModuleId = _context.Set<Module>()
                    .Where(x => x.Id == parentModuleId.Value)
                    .Select(x => x.ParentModuleId)
                    .SingleOrDefault();
            }

            return parentModuleId.HasValue;
        }

        private void Demand(ClaimActionType action)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Modules, action);
        }

        public void Dispose()
        {
            _moduleRepo.Dispose();
            if (_ownsContext) _context.Dispose();
        }
    }
}
