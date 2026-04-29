using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Email,
    string Password) : IRequest<Result<AuthTokensDto>>;
