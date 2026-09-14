using CampusAI.Api.Data;
using CampusAI.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CampusAI.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "CampusAI";
    public string Audience { get; set; } = "CampusAI.Web";
    public string SigningKey { get; set; } = "CampusAI-Dev-Signing-Key-Change-In-Production-32bytes!";
    public int ExpireMinutes { get; set; } = 480;
}

public record LoginRequest(string Username, string Password);
public record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInMinutes,
    UserDto User
);
public record UserDto(int Id, string Username, string DisplayName, string Role, string? StudentProfileId, string Subtitle);

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _jwt;

    public AuthService(AppDbContext db, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);
        if (user is null) return null;
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;

        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpireMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Role),
            new("student_profile_id", user.StudentProfileId ?? string.Empty)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            _jwt.ExpireMinutes,
            new UserDto(user.Id, user.Username, user.DisplayName, user.Role, user.StudentProfileId, user.Subtitle));
    }
}
