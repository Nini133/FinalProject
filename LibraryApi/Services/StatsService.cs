using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

public interface IStatsService
{
    Task<List<PopularBookDto>> GetPopularAsync(int top);
    Task<StatsDto> GetSummaryAsync();
}

public class StatsService : IStatsService
{
    private readonly AppDbContext _db;
    public StatsService(AppDbContext db) => _db = db;

    // პოპულარული წიგნები: GroupBy + Count
    public Task<List<PopularBookDto>> GetPopularAsync(int top) =>
        _db.BorrowRecords.AsNoTracking()
            .Where(r => r.Status == BorrowStatus.Approved || r.Status == BorrowStatus.Returned)
            .GroupBy(r => new { r.BookId, r.Book.Title })
            .Select(g => new PopularBookDto { BookId = g.Key.BookId, Title = g.Key.Title, BorrowCount = g.Count() })
            .OrderByDescending(x => x.BorrowCount)
            .Take(top)
            .ToListAsync();

    // საერთო სტატისტიკა: სტატუსების ჯგუფები
    public async Task<StatsDto> GetSummaryAsync()
    {
        var groups = await _db.BorrowRecords.AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var now = DateTime.UtcNow;

        return new StatsDto
        {
            ByStatus = groups.ToDictionary(g => g.Status.ToString(), g => g.Count),
            Overdue = await _db.BorrowRecords.CountAsync(r => r.Status == BorrowStatus.Approved && r.DueDate < now),
            Books = await _db.Books.CountAsync(),
            Users = await _db.Users.CountAsync()
        };
    }
}
