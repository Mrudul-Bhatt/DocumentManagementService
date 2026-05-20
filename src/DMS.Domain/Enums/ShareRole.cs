namespace DMS.Domain.Enums;

/// <summary>
/// Ordered access levels for a share grant.
/// Values are intentionally assigned so that numeric comparison works:
/// role >= ShareRole.Viewer means "can read", role >= ShareRole.Editor means "can write".
/// </summary>
public enum ShareRole
{
    Viewer    = 0,
    Commenter = 1,
    Editor    = 2,
}
