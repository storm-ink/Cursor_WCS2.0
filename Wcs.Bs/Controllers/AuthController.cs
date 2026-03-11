using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Wcs.Bs.Domain;
using Wcs.Bs.Infrastructure;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly WcsDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<string> _hasher = new();

    public AuthController(WcsDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    /// <summary>用户名密码登录</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "用户名和密码不能为空" });

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
            return Unauthorized(new { error = "用户名或密码错误" });

        var result = _hasher.VerifyHashedPassword(user.Username, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "用户名或密码错误" });

        var token = GenerateJwtToken(user.Username, user.Role);
        return Ok(new
        {
            token,
            username = user.Username,
            role = user.Role
        });
    }

    /// <summary>游客登录（无需账户，仅能查看任务看板）</summary>
    [HttpPost("guest")]
    [AllowAnonymous]
    public IActionResult GuestLogin()
    {
        var token = GenerateJwtToken("guest", "guest");
        return Ok(new
        {
            token,
            username = "guest",
            role = "guest"
        });
    }

    private string GenerateJwtToken(string username, string role)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryHours = _configuration.GetValue<int>("Jwt:ExpiryHours", 8);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string Username, string Password);
