using POS.Services.Inventory;
using System;

namespace POS.Tests
{
    internal static class InventoryMovementValidatorTests
    {
        public static void RunAll()
        {
            InventoryMovementValidator.ValidateAndThrow(1, 5, "Received stock");
            AssertThrows<ArgumentOutOfRangeException>(() => InventoryMovementValidator.ValidateAndThrow(0, 1, "Reason"));
            AssertThrows<ArgumentOutOfRangeException>(() => InventoryMovementValidator.ValidateAndThrow(1, 0, "Reason"));
            AssertThrows<ArgumentException>(() => InventoryMovementValidator.ValidateAndThrow(1, 1, " "));
        }

        private static void AssertThrows<TException>(Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
        }
    }
}
