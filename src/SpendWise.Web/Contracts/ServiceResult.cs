namespace SpendWise.Web.Contracts;

public enum ResultStatus { Success, NotFound, ValidationFailure }

// Expected failures are values that a future form can display without exposing database details.
public sealed record ServiceResult<T>(ResultStatus Status, T? Value, string? Error = null)
{
    public static ServiceResult<T> Success(T value) => new(ResultStatus.Success, value);
    public static ServiceResult<T> NotFound() => new(ResultStatus.NotFound, default, "Record unavailable.");
    public static ServiceResult<T> Invalid(string error) => new(ResultStatus.ValidationFailure, default, error);
}
