namespace SAPS.DDMS.API.Services;

public class ServiceResult<T>
{
    public bool Success { get; set; }

    public string Message { get; set; }

    public T Data { get; set; }

    public List<string> Errors { get; set; } = new();

    public static ServiceResult<T> SuccessResult(T data, string message = null)
    {
        return new ServiceResult<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ServiceResult<T> Failure(string message)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Message = message
        };
    }

    public static ServiceResult<T> Failure(string message, List<string> errors)
    {
        return new ServiceResult<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}