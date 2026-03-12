using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wcs.Bs.Domain;
using Wcs.Bs.Infrastructure;

namespace Wcs.Bs.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly WcsDbContext _db;
    private readonly PasswordHasher<string> _hasher = new();

    public UsersController(WcsDbContext db)
    {
        _db = db;
    }

    /// <summary>获取所有用户（仅管理员）</summary>
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Select(u => new { u.Id, u.Username, u.Role, u.CreatedAt })
            .ToListAsync();
        return Ok(users);
    }

    /// <summary>创建用户（仅管理员）</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "用户名和密码不能为空" });

        if (request.Username.Length > UserEntity.MaxUsernameLength)
            return BadRequest(new { error = $"用户名不能超过{UserEntity.MaxUsernameLength}个字符" });

        if (!new[] { "admin", "user" }.Contains(request.Role))
            return BadRequest(new { error = "角色只能是 admin 或 user" });

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return Conflict(new { error = "用户名已存在" });

        var user = new UserEntity
        {
            Username = request.Username,
            PasswordHash = _hasher.HashPassword(request.Username, request.Password),
            Role = request.Role,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { user.Id, user.Username, user.Role, user.CreatedAt });
    }

    /// <summary>删除用户（仅管理员，不能删除自己）</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUsername = User.FindFirst(ClaimTypes.Name)?.Value;
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "用户不存在" });

        if (user.Username == currentUsername)
            return BadRequest(new { error = "不能删除自己" });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Ok(new { message = "删除成功" });
    }

    /// <summary>修改自身密码（admin 和 user 均可）</summary>
    [HttpPut("me/password")]
    [Authorize(Roles = "admin,user")]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OldPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "密码不能为空" });

        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return NotFound(new { error = "用户不存在" });

        var verifyResult = _hasher.VerifyHashedPassword(user.Username, user.PasswordHash, request.OldPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
            return BadRequest(new { error = "原密码错误" });

        user.PasswordHash = _hasher.HashPassword(user.Username, request.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "密码修改成功" });
    }
}

public record CreateUserRequest(string Username, string Password, string Role);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
