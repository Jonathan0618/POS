using System;

namespace POS.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                SalesCalculatorTests.RunAll();
                StoreSettingsValidatorTests.RunAll();
                AuditValuePolicyTests.RunAll();
                InventoryMovementValidatorTests.RunAll();
                SalesServiceIntegrationTests.RunAll();
                CrossCuttingAbstractionTests.RunAll();
                MaintenanceTests.RunAll();
                MigrationIntegrationTests.RunAll();
                Console.WriteLine("All POS tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }
    }
}
