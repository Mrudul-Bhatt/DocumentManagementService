using DMS.Domain.Errors;

namespace DMS.Application.Common;

/// <summary>
/// Represents the outcome of an operation that can either succeed or fail with a
/// typed domain error — without throwing an exception.
///
/// Why the Result pattern instead of exceptions?
///   Exceptions are designed for truly exceptional, unexpected conditions (disk full,
///   network timeout, null dereference). Using them for expected business failures
///   (file not found, wrong password, size limit exceeded) has two costs:
///     1. Performance: throwing and catching exceptions is orders of magnitude slower
///        than returning a value, and triggers stack-unwinding on every failure path.
///     2. Clarity: exceptions break normal control flow invisibly. A method signature
///        that returns Result<T> makes it explicit at the call site that failure is a
///        possible, expected outcome — the caller must handle it.
///
/// Design: base class + generic subclass
///   Result (non-generic) represents a void operation result (e.g., delete, revoke).
///   Result<TValue> represents an operation that returns a value on success (e.g., upload).
///   The inheritance relationship means a single ToProblemResult() extension method in
///   the API layer works for both — Result<T> is a Result.
///
/// Usage pattern:
///   // In a handler:
///   if (file is null) return Result.Failure<FileMetadataDto>(DomainErrors.File.NotFound);
///   return Result.Success(dto);
///
///   // At the call site (controller):
///   if (result.IsFailure) return result.ToProblemResult(this);
///   return Ok(result.Value);
/// </summary>
public class Result
{
    /// <summary>
    /// Protected constructor enforces the invariant that a Result is always in a
    /// valid state: either succeeded with no error, or failed with a non-None error.
    /// The two guard clauses prevent silent bugs where a result carries contradictory
    /// state (e.g., IsSuccess = true but Error is set, or IsSuccess = false with no error).
    /// </summary>
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot have an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failure result must have an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Whether the operation completed without error.</summary>
    public bool IsSuccess { get; }

    /// <summary>Computed inverse of IsSuccess — convenience for negative branching.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The domain error describing why the operation failed.
    /// Always <see cref="Error.None"/> on a successful result.
    /// </summary>
    public Error Error { get; }

    // -------------------------------------------------------------------------
    // Static factory methods
    //
    // Why factory methods instead of public constructors?
    //   Constructors can only be named after their type. Factory methods are
    //   self-documenting: Result.Success() and Result.Failure(error) read like
    //   intent, not like plumbing. They also hide which concrete type is
    //   instantiated, which matters for the generic overloads below.
    // -------------------------------------------------------------------------

    /// <summary>Creates a successful void result (for operations with no return value).</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Creates a failed void result carrying the given domain error.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a successful result carrying a value.
    /// The type parameter is inferred by the compiler from the value argument —
    /// callers write Result.Success(dto) rather than Result.Success<FileMetadataDto>(dto).
    /// </summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>
    /// Creates a failed result for an operation that would have returned TValue.
    /// default is passed as the value — it will never be readable because
    /// accessing Result<T>.Value on a failure throws InvalidOperationException.
    /// </summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>
/// Represents the outcome of an operation that returns a value of type
/// <typeparamref name="TValue"/> on success.
///
/// Inherits IsSuccess, IsFailure, and Error from <see cref="Result"/>.
/// Adds a Value property that is safe to read only after confirming IsSuccess.
///
/// Why sealed?
///   Result<T> is a leaf type — there is no meaningful further specialisation.
///   sealed prevents accidental inheritance that could break the invariant enforced
///   by the base constructor.
///
/// Why internal constructor?
///   All construction goes through Result.Success<T>() and Result.Failure<T>().
///   Keeping the constructor internal prevents external code from instantiating
///   Result<T> with arbitrary state, which would bypass the invariant guards.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The value produced by the successful operation.
    ///
    /// Throws <see cref="InvalidOperationException"/> if accessed on a failed result.
    /// This is intentional: accessing Value without first checking IsSuccess is a
    /// programming error, not a runtime condition, and should fail loudly in tests
    /// rather than return null silently in production.
    ///
    /// The null-forgiving operator (!) suppresses the compiler's nullability warning:
    /// when IsSuccess is true, _value was set by Result.Success<T>(value) which
    /// requires a non-null argument — the value is guaranteed to be non-null here.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");
}
