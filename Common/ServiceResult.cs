namespace FlowDesk.Common;

public class ServiceResult
{
    public bool IsSuccess { get; init; }

    public bool IsNotFound { get; init; }

    public bool IsForbidden { get; init; }

    public string? ErrorMessage { get; init; }

    public static ServiceResult Success()
    {
        return new ServiceResult
        {
            IsSuccess = true
        };
    }

    public static ServiceResult Failure(string errorMessage)
    {
        return new ServiceResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    public static ServiceResult NotFound(string errorMessage)
    {
        return new ServiceResult
        {
            IsSuccess = false,
            IsNotFound = true,
            ErrorMessage = errorMessage
        };
    }

    public static ServiceResult Forbidden(string errorMessage)
    {
        return new ServiceResult
        {
            IsSuccess = false,
            IsForbidden = true,
            ErrorMessage = errorMessage
        };
    }
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }

    public static ServiceResult<T> Success(T data)
    {
        return new ServiceResult<T>
        {
            IsSuccess = true,
            Data = data
        };
    }

    public new static ServiceResult<T> Failure(string errorMessage)
    {
        return new ServiceResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    public new static ServiceResult<T> NotFound(string errorMessage)
    {
        return new ServiceResult<T>
        {
            IsSuccess = false,
            IsNotFound = true,
            ErrorMessage = errorMessage
        };
    }

    public new static ServiceResult<T> Forbidden(string errorMessage)
    {
        return new ServiceResult<T>
        {
            IsSuccess = false,
            IsForbidden = true,
            ErrorMessage = errorMessage
        };
    }
}