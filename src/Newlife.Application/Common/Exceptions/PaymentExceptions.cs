namespace Newlife.Application.Common.Exceptions;

public sealed class PaymentProviderUnavailableException(string message)
    : AppException("payment.provider_unavailable", message)
{
    public string? OrderNumber { get; init; }

    public Guid? OrderToken { get; init; }
}

public sealed class UnprocessableEntityException(string code, string message) : AppException(code, message);
