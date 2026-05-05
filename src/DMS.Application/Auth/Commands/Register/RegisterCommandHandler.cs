using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Application.Services;
using DMS.Application.Settings;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DomainRefreshToken = DMS.Domain.Entities.RefreshToken;
using DMS.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMS.Application.Auth.Commands.Register;

/// <summary>
/// Handles RegisterCommand: creates a new user account and immediately issues tokens.
/// </summary>
internal sealed class RegisterCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions,
    ILogger<RegisterCommandHandler> logger)
    : IRequestHandler<RegisterCommand, Result<AuthTokensDto>>
{
    // Resolve once from IOptions rather than reading jwtOptions.Value on every call.
    // IOptions<T>.Value is already cached by the framework, but the field avoids the
    // property access overhead and makes usage sites cleaner.
    private readonly JwtSettings _jwt = jwtOptions.Value;

    /// <summary>
    /// Executes the registration flow.
    ///
    /// Flow:
    ///   1. Check for duplicate email — return 409 if already registered.
    ///   2. Hash the password with BCrypt.
    ///   3. Create and persist the User entity via its factory method.
    ///   4. Issue an access token + refresh token pair immediately.
    ///   5. Log the registration event and return the token pair.
    ///
    /// Why issue tokens immediately instead of requiring a separate login?
    ///   The user just provided their credentials — forcing them to call POST /login
    ///   immediately after registration is redundant and degrades UX. Issuing tokens
    ///   here makes registration and authentication a single round-trip.
    ///
    /// Why DomainRefreshToken alias?
    ///   The namespace DMS.Application.Auth.Commands.Register and the class name
    ///   RefreshToken would collide with the command namespace RefreshToken.
    ///   The alias DomainRefreshToken = DMS.Domain.Entities.RefreshToken resolves the
    ///   ambiguity without renaming either the namespace or the domain entity.
    /// </summary>
    public async Task<Result<AuthTokensDto>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(command.Email, ct);
        if (existing is not null)
            return Result.Failure<AuthTokensDto>(DomainErrors.User.EmailAlreadyExists);

        // Hash before creating the entity — the entity factory receives the hash,
        // never the plaintext password, so it can never accidentally expose it.
        var passwordHash = passwordHasher.Hash(command.Password);
        var user = User.Create(command.Email, passwordHash);
        await userRepository.AddAsync(user, ct);

        var tokens = await IssueTokenPairAsync(user, ct);

        logger.LogInformation("User {UserId} registered with email {Email}", user.Id, user.Email);

        return Result.Success(tokens);
    }

    /// <summary>
    /// Generates an access token + refresh token pair and persists the refresh token.
    ///
    /// Extracted into a private method because RegisterCommandHandler and
    /// LoginCommandHandler both issue tokens after a successful authentication event.
    /// Keeping the issuance logic in one place ensures both paths produce
    /// tokens with identical expiry and storage semantics.
    /// </summary>
    private async Task<AuthTokensDto> IssueTokenPairAsync(User user, CancellationToken ct)
    {
        var accessToken    = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role);
        var rawRefreshToken = jwtTokenService.GenerateRefreshToken();

        // Persist the refresh token so it can be looked up on POST /auth/refresh.
        // Expiry is calculated here (application-side) so it matches the JwtSettings
        // configuration rather than being computed independently in the domain entity.
        var refreshToken = DomainRefreshToken.Create(
            user.Id, rawRefreshToken, DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays));
        await refreshTokenRepository.AddAsync(refreshToken, ct);

        // ExpiryMinutes * 60 converts to seconds — the standard "expires_in" unit
        // used by OAuth 2.0 and expected by the frontend's session management logic.
        return new AuthTokensDto(accessToken, rawRefreshToken, _jwt.ExpiryMinutes * 60);
    }
}
