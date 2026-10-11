using System.Net;

namespace SAPS.DDMS.API.Security;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The browser cancelled the request; there is no client response to write.
        }
        catch (Exception exception)
        {
            var status = exception switch
            {
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                ArgumentException => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.InternalServerError
            };
            var message = status switch
            {
                HttpStatusCode.Unauthorized => "Your session is not authorised for this request.",
                HttpStatusCode.BadRequest => exception.Message,
                _ => "The request could not be completed. Please try again or contact support with the reference below."
            };

            logger.LogError(exception,
                "Unhandled API error. TraceId={TraceId}, Method={Method}, Path={Path}",
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message,
                reference = context.TraceIdentifier
            });
        }
    }
}
