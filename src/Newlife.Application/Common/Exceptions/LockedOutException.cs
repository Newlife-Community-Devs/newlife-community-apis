namespace Newlife.Application.Common.Exceptions;

public sealed class LockedOutException() : AppException("auth.locked_out", "Account is temporarily locked. Try again later.");
