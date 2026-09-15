using POS.Common.Enumerations;

namespace POS.Core.Abstractions
{
    public interface IAuthorizationService
    {
        bool HasPermission(string resource, ClaimActionType action);
    }
}
