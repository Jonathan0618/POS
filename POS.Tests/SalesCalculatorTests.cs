using POS.Services.Point_of_Sale;
using System;

namespace POS.Tests
{
    internal static class SalesCalculatorTests
    {
        public static void RunAll()
        {
            var calculator = new SalesCalculator();

            AssertEqual(12.00m, calculator.CalculateTax(100.00m, 0.12m, false), "Exclusive VAT calculation");
            AssertEqual(10.71m, calculator.CalculateTax(100.00m, 0.12m, true), "Inclusive VAT calculation");
            AssertEqual(0.01m, calculator.CalculateTax(0.05m, 0.12m, false), "VAT midpoint rounding");
            AssertEqual(1.235m, calculator.RoundMoney(1.2345m, 3), "Configured currency precision");
            AssertEqual(90.00m, calculator.ApplyDiscount(100.00m, 10.00m), "Discount calculation");
            AssertEqual(107.00m, calculator.CalculateTotal(100.00m, 12.00m, 5.00m), "Total calculation");
            AssertThrows<ArgumentOutOfRangeException>(
                () => calculator.ApplyDiscount(10.00m, 11.00m),
                "Excessive discount validation");
        }

        private static void AssertEqual(decimal expected, decimal actual, string testName)
        {
            if (expected != actual)
                throw new InvalidOperationException($"{testName} failed: expected {expected}, actual {actual}.");
        }

        private static void AssertThrows<TException>(Action action, string testName)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException($"{testName} failed: expected {typeof(TException).Name}.");
        }
    }
}
