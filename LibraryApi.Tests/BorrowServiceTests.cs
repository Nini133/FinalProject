using LibraryApi.Data;
using LibraryApi.Entities;
using LibraryApi.Exceptions;
using LibraryApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LibraryApi.Tests;

public class BorrowServiceTests
{
    // InMemory ბაზა თითო ტესტზე
    private static async Task<(AppDbContext db, BorrowService svc, int recordId)> SetupAsync(int available)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options);

        db.Users.Add(new User { Id = 10, UserName = "test", Email = "t@t.ge", PasswordHash = "x", RoleId = 2 });
        db.Books.Add(new Book { Id = 1, Title = "Book", ISBN = "1234567890", CategoryId = 1, TotalCopies = 3, AvailableCopies = available });
        db.BorrowRecords.Add(new BorrowRecord { Id = 1, UserId = 10, BookId = 1, BorrowDate = DateTime.UtcNow, Status = BorrowStatus.Pending });
        await db.SaveChangesAsync();

        return (db, new BorrowService(db, NullLogger<BorrowService>.Instance), 1);
    }

    [Fact]
    public async Task Approve_DecreasesAvailableCopies_AndSetsDueDate()
    {
        var (db, svc, id) = await SetupAsync(2);
        var result = await svc.ApproveAsync(id);

        Assert.Equal("Approved", result.Status);
        Assert.NotNull(result.DueDate);
        Assert.Equal(1, (await db.Books.FindAsync(1))!.AvailableCopies);
    }

    [Fact]
    public async Task Approve_Twice_ThrowsConflict()
    {
        var (_, svc, id) = await SetupAsync(2);
        await svc.ApproveAsync(id);

        await Assert.ThrowsAsync<ConflictException>(() => svc.ApproveAsync(id));
    }

    [Fact]
    public async Task Approve_WhenNoCopies_ThrowsBadRequest()
    {
        var (db, svc, id) = await SetupAsync(0);

        await Assert.ThrowsAsync<BadRequestException>(() => svc.ApproveAsync(id));
        Assert.Equal(0, (await db.Books.FindAsync(1))!.AvailableCopies);
    }

    [Fact]
    public async Task Return_RestoresCopies_AndSavesReturnDate()
    {
        var (db, svc, id) = await SetupAsync(2);
        await svc.ApproveAsync(id);
        var result = await svc.ReturnAsync(id, 10, false);

        Assert.Equal("Returned", result.Status);
        Assert.NotNull(result.ReturnDate);
        Assert.Equal(2, (await db.Books.FindAsync(1))!.AvailableCopies);
    }

    [Fact]
    public async Task Return_ByAnotherClient_ThrowsForbidden()
    {
        var (_, svc, id) = await SetupAsync(2);
        await svc.ApproveAsync(id);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.ReturnAsync(id, 99, false));
    }

    [Fact]
    public void Fine_IsCalculatedPerLateDay()
    {
        var due = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1.50m, FineCalculator.Calculate(due, due.AddDays(3)));
        Assert.Equal(0m, FineCalculator.Calculate(due, due.AddDays(-1)));
    }
}
