using System;

namespace POS.Services.Point_of_Sale
{
    public class SalesCalculator
    {
        public decimal CalculateTax(decimal amount, decimal taxRate, bool taxInclusive, int decimalPlaces = 2)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (taxRate < 0 || taxRate > 1)
                throw new ArgumentOutOfRangeException(nameof(taxRate));

            return taxInclusive
                ? RoundMoney(amount - (amount / (1 + taxRate)), decimalPlaces)
                : RoundMoney(amount * taxRate, decimalPlaces);
        }

        public decimal CalculateVat(decimal subtotal, decimal vatRate) =>
            CalculateTax(subtotal, vatRate, false);

        public decimal RoundMoney(decimal amount, int decimalPlaces = 2)
        {
            if (decimalPlaces < 0 || decimalPlaces > 4)
                throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
            return decimal.Round(amount, decimalPlaces, MidpointRounding.AwayFromZero);
        }

        public decimal ApplyDiscount(decimal subtotal, decimal discount)
        {
            if (subtotal < 0)
                throw new ArgumentOutOfRangeException(nameof(subtotal));
            if (discount < 0 || discount > subtotal)
                throw new ArgumentOutOfRangeException(nameof(discount));

            return subtotal - discount;
        }

        public decimal CalculateTotal(decimal subtotal, decimal vat, decimal discount)
        {
            if (subtotal < 0)
                throw new ArgumentOutOfRangeException(nameof(subtotal));
            if (vat < 0)
                throw new ArgumentOutOfRangeException(nameof(vat));
            if (discount < 0 || discount > subtotal + vat)
                throw new ArgumentOutOfRangeException(nameof(discount));

            return RoundMoney(subtotal + vat - discount);
        }
    }
}
