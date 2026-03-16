using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using StudentTestingSystem.Data;
using StudentTestingSystem.Models.Entities;

namespace StudentTestingSystem.Services;

public interface IAuthService
{
    Task<User?> AuthenticateStudentAsync(string email, string password);
    Task<User?> AuthenticateTeacherAsync(string email, string password);
    Task<(bool success, string error)> RegisterStudentAsync(
    string fullName,
    string email,
    string groupNumber,
    string password,
    int? groupId);
    Task<bool> CreatePasswordResetRequestAsync(string email);
    ClaimsPrincipal CreateClaimsPrincipal(User user);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;

    public AuthService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> AuthenticateStudentAsync(string email, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.Role == UserRole.Student);

        if (user == null || user.IsBlocked)
            return null;

        if (!BCryptHelper.VerifyPassword(password, user.PasswordHash))
            return null;

        return user;
    }

    public async Task<User?> AuthenticateTeacherAsync(string email, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.Role == UserRole.Teacher);

        if (user == null)
            return null;

        if (!BCryptHelper.VerifyPassword(password, user.PasswordHash))
            return null;

        return user;
    }

    public async Task<(bool success, string error)> RegisterStudentAsync(string fullName,
    string email,
    string groupNumber,
    string password,
    int? groupId)
    {
        if (await _context.Users.AnyAsync(u => u.Email == email))
            return (false, "ѕользователь с таким email уже существует");

        var user = new User
        {
            FullName = fullName,
            Email = email,
            GroupNumber = groupNumber,
            PasswordHash = BCryptHelper.HashPassword(password),
            Role = UserRole.Student,
            GroupId = groupId
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<bool> CreatePasswordResetRequestAsync(string email)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.Role == UserRole.Student);

        if (user == null)
            return false;

        var existingRequest = await _context.PasswordResetRequests
            .AnyAsync(r => r.UserId == user.Id && !r.IsProcessed);

        if (existingRequest)
            return true;

        user.IsBlocked = true;

        var request = new PasswordResetRequest
        {
            UserId = user.Id,
            RequestedAt = DateTime.Now,
            IsProcessed = false
        };

        _context.PasswordResetRequests.Add(request);
        await _context.SaveChangesAsync();

        return true;
    }

    public ClaimsPrincipal CreateClaimsPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("GroupNumber", user.GroupNumber ?? "")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
