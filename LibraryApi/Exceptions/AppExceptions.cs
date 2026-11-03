namespace LibraryApi.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public AppException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public class BadRequestException : AppException { public BadRequestException(string m) : base(400, m) { } }
public class UnauthorizedException : AppException { public UnauthorizedException(string m) : base(401, m) { } }
public class ForbiddenException : AppException { public ForbiddenException(string m) : base(403, m) { } }
public class NotFoundException : AppException { public NotFoundException(string m) : base(404, m) { } }
public class ConflictException : AppException { public ConflictException(string m) : base(409, m) { } }
