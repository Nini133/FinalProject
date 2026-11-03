using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Entities;
using LibraryApi.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtService _jwt;
    private readonly ILogger<AuthService> _log;

    public AuthService(AppDbContext db, IJwtService jwt, ILogger<AuthService> log)
    {
        _db = db; _jwt = jwt; _log = log;
    }

    // რეგისტრაცია: უნიკალურობა, BCrypt ჰეში, Client როლი
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new ConflictException("ეს Email უკვე დარეგისტრირებულია");
        if (await _db.Users.AnyAsync(u => u.UserName == dto.UserName))
            throw new ConflictException("ეს მომხმარებლის სახელი დაკავებულია");

        var user = new User
        {
            UserName = dto.UserName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = 2
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _log.LogInformation("დარეგისტრირდა მომხმარებელი {User}", user.UserName);

        user.Role = await _db.Roles.FirstAsync(r => r.Id == user.RoleId);
        return Build(user);
    }

    // ავტორიზაცია: პაროლის შემოწმება, ტოკენი
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedException("Email ან პაროლი არასწორია");

        return Build(user);
    }

    private AuthResponseDto Build(User user)
    {
        var (token, expires) = _jwt.Create(user);
        return new AuthResponseDto { Token = token, UserName = user.UserName, Email = user.Email, Role = user.Role.Name, ExpiresAt = expires };
    }
}
