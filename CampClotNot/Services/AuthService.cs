using System.Security.Claims;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using CampClotNot.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

public class AuthService(IUserRepository users, IDbContextFactory<AppDbContext> factory)
{
    public async Task<bool> LoginAsync(HttpContext httpContext, string email, string password)
    {
        var user = await users.GetByEmailAsync(email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return false;

        using var db = factory.CreateDbContext();
        var roleAuthorities = await db.UserRoleAuthorityLinks
            .Where(l => l.UserRoleId == user.UserRoleId)
            .Select(l => l.Authority.SystemName)
            .ToListAsync();

        var userAuthorities = await db.UserAuthorityLinks
            .Where(l => l.UserId == user.UserId)
            .Select(l => l.Authority.SystemName)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.UserRole.SystemName)
        };

        foreach (var authority in roleAuthorities.Union(userAuthorities).Distinct())
            claims.Add(new Claim("authority", authority));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return true;
    }

    public async Task LogoutAsync(HttpContext httpContext) =>
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    public async Task<User> CreateUserAsync(string firstName, string lastName, string email, string plainPassword, Role role)
    {
        var userRole = await users.GetRoleAsync(role)
            ?? throw new InvalidOperationException($"Role '{role}' has not been seeded.");

        return await users.CreateAsync(new User
        {
            UserId       = Guid.NewGuid(),
            UserRoleId   = userRole.UserRoleId,
            FirstName    = firstName,
            LastName     = lastName,
            Email        = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword),
            IsActive     = true
        });
    }

    public async Task ResetPasswordAsync(Guid userId, string newPassword)
    {
        var all = await users.GetAllAsync();
        var user = all.FirstOrDefault(u => u.UserId == userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await users.UpdateAsync(user);
    }

    public Task<List<User>> GetAllUsersAsync() => users.GetAllAsync();

    public async Task DeactivateUserAsync(Guid userId)
    {
        var all = await users.GetAllAsync();
        var user = all.FirstOrDefault(u => u.UserId == userId);
        if (user is not null)
        {
            user.IsActive = false;
            await users.UpdateAsync(user);
        }
    }
}
