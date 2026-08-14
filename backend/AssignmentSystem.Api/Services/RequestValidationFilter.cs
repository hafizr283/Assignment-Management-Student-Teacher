using System.ComponentModel.DataAnnotations;
using AssignmentSystem.Api.DTOs;

namespace AssignmentSystem.Api.Services;

public sealed class RequestValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var fieldErrors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in context.Arguments.Where(IsRequestDto))
        {
            var results = new List<ValidationResult>();
            if (Validator.TryValidateObject(argument!, new ValidationContext(argument!), results, validateAllProperties: true))
                continue;

            foreach (var result in results)
            {
                var members = result.MemberNames.DefaultIfEmpty("request");
                foreach (var member in members)
                {
                    if (!fieldErrors.TryGetValue(member, out var messages))
                    {
                        messages = [];
                        fieldErrors[member] = messages;
                    }

                    messages.Add(result.ErrorMessage ?? "The value is invalid.");
                }
            }
        }

        if (fieldErrors.Count == 0)
            return await next(context);

        var errors = fieldErrors.ToDictionary(x => x.Key, x => x.Value.Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
        var message = errors.SelectMany(x => x.Value).FirstOrDefault() ?? "Check the highlighted fields and try again.";
        return Results.BadRequest(new
        {
            data = (object?)null,
            error = new { code = "VALIDATION_FAILED", message, fields = errors }
        });
    }

    private static bool IsRequestDto(object? argument) =>
        argument?.GetType().Namespace == typeof(LoginRequest).Namespace;
}
