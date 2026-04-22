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
        public static readonly Error IdMissing = new("User.IdMissing", "The X-User-Id header is required.");
    }
}

public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
