namespace SeismicFlow.Shared.Results
{
    /// <summary>
    /// Represents the outcome of an operation that can either succeed or fail.
    /// Eliminates the need to throw exceptions for expected business failures.
    ///
    /// Usage:
    ///   Result&lt;DeviceDto&gt; result = await handler.Handle(command);
    ///   if (result.IsFailure) return BadRequest(result.Error);
    ///   return Ok(result.Value);
    /// </summary>
    public sealed class Result<T>
    {
        public T? Value { get; }
        public Error? Error { get; }

        public bool IsSuccess => Error is null;
        public bool IsFailure => Error is not null;

        private Result(T value)
        {
            Value = value;
            Error = null;
        }

        private Result(Error error)
        {
            Value = default;
            Error = error;
        }

        public static Result<T> Success(T value) => new(value);
        public static Result<T> Failure(Error error) => new(error);

        /// <summary>
        /// Transforms the value if successful, propagates the error otherwise.
        /// </summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
            IsSuccess
                ? Result<TOut>.Success(mapper(Value!))
                : Result<TOut>.Failure(Error!);

        /// <summary>
        /// Pattern matching on the result.
        /// </summary>
        public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
            IsSuccess ? onSuccess(Value!) : onFailure(Error!);

        // Implicit conversions — lets you return values directly without wrapping:
        //   return device;              instead of Result<DeviceDto>.Success(device)
        //   return Error.NotFound(...); instead of Result<DeviceDto>.Failure(Error.NotFound(...))
        public static implicit operator Result<T>(T value) => Success(value);
        public static implicit operator Result<T>(Error error) => Failure(error);
    }

    /// <summary>
    /// Non-generic Result for operations that return no value.
    /// </summary>
    public sealed class Result
    {
        public Error? Error { get; }
        public bool IsSuccess => Error is null;
        public bool IsFailure => Error is not null;

        private Result() { Error = null; }
        private Result(Error error) { Error = error; }

        public static Result Success() => new();
        public static Result Failure(Error error) => new(error);

        public static implicit operator Result(Error error) => Failure(error);
    }
}
