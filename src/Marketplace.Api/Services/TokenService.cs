using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Marketplace.Api.Contracts;
using Marketplace.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Api.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "marketplace-api";
    public string Audience { get; set; } = "marketplace-clients";
    public string SigningKey { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 60;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class AppClaims
{
    public const string OutletId = "outlet_id";
    public const string DistributorId = "distributor_id";
}

public class TokenService(JwtOptions options, TimeProvider clock)
{
    public TokenResponse Issue(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (user.OutletId is { } outlet) claims.Add(new(AppClaims.OutletId, outlet.ToString()));
        if (user.DistributorId is { } dist) claims.Add(new(AppClaims.DistributorId, dist.ToString()));

        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(options.LifetimeMinutes);
        var token = new JwtSecurityToken(
            options.Issuer, options.Audience, claims,
            expires: expires,
            signingCredentials: new SigningCredentials(options.Key(), SecurityAlgorithms.HmacSha256));
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
