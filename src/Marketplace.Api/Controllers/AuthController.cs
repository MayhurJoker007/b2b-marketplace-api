using Marketplace.Api.Contracts;
using Marketplace.Api.Data;
using Marketplace.Api.Domain;
using Marketplace.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(MarketplaceDb db, TokenService tokens) : ControllerBase
{
    private static readonly PasswordHasher<AppUser> Hasher = new();

    [HttpPost("token")]
    public async Task<ActionResult<TokenResponse>> Token(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null ||
            Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }
        return tokens.Issue(user);
    }
}
