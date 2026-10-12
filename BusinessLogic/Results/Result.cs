namespace BusinessLogic.Results;

public enum ResultStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed class Result<T>
{
    private readonly T? _value;

    private Result(ResultStatus status, T? value, ConflictReason? reason)
    {
        Status = status;
        _value = value;
        Reason = reason;
    }

    public ResultStatus Status { get; }

    public ConflictReason? Reason { get; }

    public bool IsSuccess => Status == ResultStatus.Success;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result has no value, status is {Status}");

    public static Result<T> Success(T value)
    {
        return new Result<T>(ResultStatus.Success, value, null);
    }

    public static Result<T> NotFound() 
    {
        return new Result<T>(ResultStatus.NotFound, default, null);
    }

    public static Result<T> Conflict(ConflictReason reason)
    {
        return new Result<T>(ResultStatus.Conflict, default, reason);
    }

    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    public static implicit operator Result<T>(ConflictReason reason)
    {
        return Conflict(reason);
    }
}
