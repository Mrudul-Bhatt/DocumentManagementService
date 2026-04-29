namespace DMS.Domain.Errors;

public static class DomainErrors
{
    public static class File
    {
        public static readonly Error NotFound = new("File.NotFound", "The requested file does not exist.");
        public static readonly Error Forbidden = new("File.Forbidden", "You do not have permission to access this file.");
        public static readonly Error TooLarge = new("File.TooLarge", "File exceeds the maximum allowed size of 25 MB.");
        public static readonly Error Empty = new("File.Empty", "Uploaded file cannot be empty.");
    }

    public static class User
    {
        public static readonly Error NotFound = new("User.NotFound", "The requested user does not exist.");
        public static readonly Error EmailAlreadyExists = new("User.EmailAlreadyExists", "An account with this email address already exists.");
        public static readonly Error InvalidCredentials = new("User.InvalidCredentials", "The email or password is incorrect.");
        public static readonly Error Suspended = new("User.Suspended", "This account has been suspended.");
    }

    public static class Token
    {
        public static readonly Error Invalid = new("Token.Invalid", "The token is invalid.");
        public static readonly Error Expired = new("Token.Expired", "The token has expired.");
    }
}

public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
