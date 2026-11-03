using LibraryApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Data;

public static class DbSeeder
{
    // საწყისი Admin: კონფიგურაციიდან, ჰეშირებული პაროლი
    public static async Task SeedAdminAsync(AppDbContext db, IConfiguration cfg)
    {
        var email = cfg["Admin:Email"]!.ToLower();
        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        db.Users.Add(new User
        {
            UserName = cfg["Admin:UserName"]!,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(cfg["Admin:Password"]!),
            RoleId = 1
        });
        await db.SaveChangesAsync();
    }
}
