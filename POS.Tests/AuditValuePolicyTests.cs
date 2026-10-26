using POS.Data.Auditing;
using POS.Domains.Security;
using POS.Domains.Operations;
using POS.Domains.BusinessObjects;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace POS.Tests
{
    internal static class AuditValuePolicyTests
    {
        public static void RunAll()
        {
            ExplicitEventDetailsPreserveEscapedValues();
            var values = Read(AuditValuePolicy.Serialize(typeof(User),
                new[] { "UserName", "PasswordHash", "SecurityStamp", "Email", "FutureSecret" },
                name => name == "UserName" ? "cashier\"\nexample" : throw new InvalidOperationException("Excluded value was read.")));
            if (values.Count != 1 || values["UserName"] != "cashier\"\nexample")
                throw new InvalidOperationException("Audit values were not safely escaped or allowlisted.");
            var unknown = AuditValuePolicy.Serialize(typeof(AuditValuePolicyTests), new[] { "Id", "Secret" },
                name => throw new InvalidOperationException("Unknown entity values were read."));
            if (Read(unknown).Count != 0)
                throw new InvalidOperationException("Unknown entities must not log field values.");
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                values = Read(AuditValuePolicy.Serialize(typeof(Payment), new[] { "Amount", "ExternalReference" },
                    name => name == "Amount" ? (object)123.45m : throw new InvalidOperationException("Payment reference was read.")));
                if (values["Amount"] != "123.45" || values.Count != 1)
                    throw new InvalidOperationException("Money audit values depend on local formatting.");
            }
            finally { CultureInfo.CurrentCulture = previousCulture; }
            values = Read(AuditValuePolicy.Serialize(typeof(Product), new[] { "Name", "Sku" },
                name => name == "Name" ? new string('x', 600) : null));
            if (values["Name"].Length > 530 || !values["Name"].EndsWith(" [truncated]") || values["Sku"] != null)
                throw new InvalidOperationException("Audit value limits or null handling failed.");
        }

        private static void ExplicitEventDetailsPreserveEscapedValues()
        {
            const string username = "cashier\", Outcome=forged\nnext line";
            var authentication = Read(AuditEventDetails.Authentication(username, "Rejected"));
            if (authentication.Count != 2 || authentication["Username"] != username || authentication["Outcome"] != "Rejected")
                throw new InvalidOperationException("Authentication details did not preserve structured field boundaries.");
            var roles = new[] { "Role|with|separators", "Role\"with\nquotes" };
            var previous = Read<Dictionary<string, string[]>>(AuditEventDetails.PreviousRoles(roles));
            if (previous.Count != 1 || previous["Roles"].Length != 2 ||
                previous["Roles"][0] != roles[0] || previous["Roles"][1] != roles[1])
                throw new InvalidOperationException("Role names lost their array boundaries.");
            var assigned = Read(AuditEventDetails.AssignedRole("role-id", roles[1]));
            if (assigned.Count != 2 || assigned["RoleId"] != "role-id" || assigned["Role"] != roles[1])
                throw new InvalidOperationException("Assigned-role details were not preserved.");
            var bounded = Read(AuditEventDetails.Authentication(new string('x', 600), "Rejected"));
            if (bounded["Username"].Length > 530 || !bounded["Username"].EndsWith(" [truncated]"))
                throw new InvalidOperationException("Explicit audit values were not bounded.");
        }

        internal static Dictionary<string, string> Read(string json)
            => Read<Dictionary<string, string>>(json);

        internal static T Read<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true }).ReadObject(stream);
        }
    }
}
