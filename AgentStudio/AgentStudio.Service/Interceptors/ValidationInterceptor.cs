using FluentValidation;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace AgentStudio.Service.Interceptors;

public class ValidationInterceptor : Interceptor
{
    private readonly IServiceProvider serviceProvider;

    public ValidationInterceptor(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var validator = this.serviceProvider.GetService<IValidator<TRequest>>();

        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(request);
            
            if (!validationResult.IsValid)
            {
                var errors = string.Join(" | ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new RpcException(new Status(StatusCode.InvalidArgument, errors));
            }
        }

        return await continuation(request, context);
    }
}