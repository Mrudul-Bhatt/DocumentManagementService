using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Auth.Commands.RevokeToken;

public sealed record RevokeTokenCommand(string Token) : IRequest<Result>;
