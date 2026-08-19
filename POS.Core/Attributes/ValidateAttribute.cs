using POS.Common.Enumerations;
using System;

namespace POS.Core.Attributes
{
    [AttributeUsage(
    AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
    public sealed class ValidateAttribute : Attribute
    {
        public string Resource { get; }
        public ClaimActionType Action { get; }

        public ValidateAttribute(string resource, ClaimActionType action)
        {
            Resource = resource;
            Action = action;
        }
    }
}
