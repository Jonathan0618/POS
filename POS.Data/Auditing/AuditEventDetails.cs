using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace POS.Data.Auditing
{
    // Typed entry points keep arbitrary entity objects and secret fields out of events.
    public static class AuditEventDetails
    {
        public static string StockCount(int countId, string reason, int productCount, bool posted) =>
            Serialize(new Dictionary<string, string>
            {
                { "CountId", countId.ToString(CultureInfo.InvariantCulture) },
                { "Reason", Limit(reason) },
                { "ProductCount", productCount.ToString(CultureInfo.InvariantCulture) },
                { "Status", posted ? "Posted" : "Draft" }
            });

        public static string Authentication(string username, string outcome) => Serialize(new Dictionary<string, string>
        {
            { "Username", Limit(username ?? string.Empty) }, { "Outcome", Limit(outcome) }
        });

        public static string Permissions(string roleId, int moduleId, bool view, bool add, bool edit, bool delete) =>
            Serialize(new Dictionary<string, string>
            {
                { "RoleId", Limit(roleId) }, { "ModuleId", moduleId.ToString(CultureInfo.InvariantCulture) },
                { "View", view.ToString() }, { "Add", add.ToString() },
                { "Edit", edit.ToString() }, { "Delete", delete.ToString() }
            });

        public static string PreviousRoles(IEnumerable<string> roles) => Serialize(new Dictionary<string, string[]>
        {
            { "Roles", roles.Select(Limit).ToArray() }
        });

        public static string AssignedRole(string roleId, string roleName) => Serialize(new Dictionary<string, string>
        {
            { "RoleId", Limit(roleId) }, { "Role", Limit(roleName) }
        });

        internal static string Limit(string value) => value != null && value.Length > 512
            ? value.Substring(0, 512) + " [truncated]" : value;

        internal static string Serialize<T>(T values)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).WriteObject(stream, values);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
