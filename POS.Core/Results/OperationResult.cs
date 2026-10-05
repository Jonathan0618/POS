using System;
using System.Collections.Generic;
using System.Linq;

namespace POS.Core.Results
{
    public class OperationResult
    {
        protected OperationResult(bool succeeded, IEnumerable<string> errors)
        {
            Succeeded = succeeded;
            Errors = (errors ?? Enumerable.Empty<string>()).ToArray();
        }

        public bool Succeeded { get; }
        public IReadOnlyList<string> Errors { get; }

        public static OperationResult Success() => new OperationResult(true, null);

        public static OperationResult Failure(params string[] errors)
        {
            ValidateErrors(errors);
            return new OperationResult(false, errors);
        }

        protected static void ValidateErrors(IEnumerable<string> errors)
        {
            if (errors == null || !errors.Any() || errors.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("At least one non-empty error is required.", nameof(errors));
        }
    }

    public sealed class OperationResult<T> : OperationResult
    {
        private OperationResult(bool succeeded, T value, IEnumerable<string> errors)
            : base(succeeded, errors)
        {
            Value = value;
        }

        public T Value { get; }

        public static OperationResult<T> Success(T value) =>
            new OperationResult<T>(true, value, null);

        public new static OperationResult<T> Failure(params string[] errors)
        {
            ValidateErrors(errors);
            return new OperationResult<T>(false, default(T), errors);
        }
    }
}
