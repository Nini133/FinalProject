using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Entities;
using LibraryApi.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

public static class FineCalculator
{
    public const int LoanDays = 14;
    public const decimal DailyRate = 0.50m;

    // ჯარიმა: დღეები * ტარიფი
    public static decimal Calculate(DateTime? due, DateTime end)
    {
        if (due == null || end <= due.Value) return 0m;
        return (int)Math.Ceiling((end - due.Value).TotalDays) * DailyRate;
    }
}

public interface IBorrowService
{
    Task<BorrowDto> CreateAsync(int userId, int bookId);
    Task<BorrowDto> ApproveAsync(int id);
    Task<BorrowDto> RejectAsync(int id);
    Task<BorrowDto> ReturnAsync(int id, int userId, bool isAdmin);
    Task<BorrowDto> GetByIdAsync(int id, int userId, bool isAdmin);
    Task<List<BorrowDto>> GetMyAsync(int userId);
    Task<List<BorrowDto>> GetAllAsync(BorrowStatus? status);
    Task<List<BorrowDto>> GetOverdueAsync(int userId, bool isAdmin);
}

public class BorrowService : IBorrowService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BorrowService> _log;
    public BorrowService(AppDbContext db, ILogger<BorrowService> log) { _db = db; _log = log; }

    private IQueryable<BorrowRecord> Records => _db.BorrowRecords.Include(r => r.Book).Include(r => r.User);

    // მოთხოვნა: Pending, აქტიური დუბლის შემოწმება
    public async Task<BorrowDto> CreateAsync(int userId, int bookId)
    {
        if (!await _db.Books.AnyAsync(b => b.Id == bookId))
            throw new NotFoundException("წიგნი ვერ მოიძებნა");

        var active = await _db.BorrowRecords.AnyAsync(r => r.UserId == userId && r.BookId == bookId
            && (r.Status == BorrowStatus.Pending || r.Status == BorrowStatus.Approved));
        if (active) throw new ConflictException("ამ წიგნზე აქტიური მოთხოვნა უკვე გაქვთ");

        var record = new BorrowRecord { UserId = userId, BookId = bookId, BorrowDate = DateTime.UtcNow, Status = BorrowStatus.Pending };
        _db.BorrowRecords.Add(record);
        await _db.SaveChangesAsync();
        return await LoadAsync(record.Id);
    }

    // დადასტურება: მხოლოდ Pending, მარაგი > 0, Available--, DueDate; ერთი SaveChanges = ერთი Transaction + Version კონფლიქტი
    public async Task<BorrowDto> ApproveAsync(int id)
    {
        var r = await Records.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("მოთხოვნა ვერ მოიძებნა");
        if (r.Status != BorrowStatus.Pending) throw new ConflictException("მოთხოვნა უკვე დამუშავებულია");
        if (r.Book.AvailableCopies <= 0) throw new BadRequestException("ხელმისაწვდომი ეგზემპლარი არ არის");

        r.Book.AvailableCopies--;
        r.Book.Version++;
        r.Status = BorrowStatus.Approved;
        r.DueDate = DateTime.UtcNow.AddDays(FineCalculator.LoanDays);
        await _db.SaveChangesAsync();

        _log.LogInformation("დადასტურდა მოთხოვნა {Id}", id);
        return ToDto(r, DateTime.UtcNow);
    }

    // უარყოფა: მხოლოდ Pending
    public async Task<BorrowDto> RejectAsync(int id)
    {
        var r = await Records.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("მოთხოვნა ვერ მოიძებნა");
        if (r.Status != BorrowStatus.Pending) throw new ConflictException("მოთხოვნა უკვე დამუშავებულია");

        r.Status = BorrowStatus.Rejected;
        await _db.SaveChangesAsync();
        return ToDto(r, DateTime.UtcNow);
    }

    // დაბრუნება: მფლობელი ან Admin, ReturnDate, Available++
    public async Task<BorrowDto> ReturnAsync(int id, int userId, bool isAdmin)
    {
        var r = await Records.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("მოთხოვნა ვერ მოიძებნა");
        if (!isAdmin && r.UserId != userId) throw new ForbiddenException("ეს ჩანაწერი თქვენი არ არის");
        if (r.Status != BorrowStatus.Approved) throw new ConflictException("დაბრუნება შესაძლებელია მხოლოდ გაცემული წიგნისთვის");
        if (r.Book.AvailableCopies >= r.Book.TotalCopies) throw new ConflictException("მარაგი უკვე სრულია");

        r.Status = BorrowStatus.Returned;
        r.ReturnDate = DateTime.UtcNow;
        r.Book.AvailableCopies++;
        r.Book.Version++;
        await _db.SaveChangesAsync();

        _log.LogInformation("დაბრუნდა წიგნი, ჩანაწერი {Id}", id);
        return ToDto(r, DateTime.UtcNow);
    }

    // წვდომა: მხოლოდ საკუთარი, Admin - ყველა
    public async Task<BorrowDto> GetByIdAsync(int id, int userId, bool isAdmin)
    {
        var r = await Records.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException("მოთხოვნა ვერ მოიძებნა");
        if (!isAdmin && r.UserId != userId) throw new ForbiddenException("ეს ჩანაწერი თქვენი არ არის");
        return ToDto(r, DateTime.UtcNow);
    }

    // ჩემი ისტორია
    public async Task<List<BorrowDto>> GetMyAsync(int userId)
    {
        var list = await Records.AsNoTracking().Where(r => r.UserId == userId).OrderByDescending(r => r.BorrowDate).ToListAsync();
        var now = DateTime.UtcNow;
        return list.Select(r => ToDto(r, now)).ToList();
    }

    // Admin: ყველა, სტატუსის ფილტრით
    public async Task<List<BorrowDto>> GetAllAsync(BorrowStatus? status)
    {
        var q = Records.AsNoTracking();
        if (status != null) q = q.Where(r => r.Status == status);
        var list = await q.OrderByDescending(r => r.BorrowDate).ToListAsync();
        var now = DateTime.UtcNow;
        return list.Select(r => ToDto(r, now)).ToList();
    }

    // ვადაგადაცილებული: Approved და DueDate < ახლა
    public async Task<List<BorrowDto>> GetOverdueAsync(int userId, bool isAdmin)
    {
        var now = DateTime.UtcNow;
        var q = Records.AsNoTracking().Where(r => r.Status == BorrowStatus.Approved && r.DueDate < now);
        if (!isAdmin) q = q.Where(r => r.UserId == userId);
        var list = await q.OrderBy(r => r.DueDate).ToListAsync();
        return list.Select(r => ToDto(r, now)).ToList();
    }

    private async Task<BorrowDto> LoadAsync(int id)
    {
        var r = await Records.AsNoTracking().FirstAsync(x => x.Id == id);
        return ToDto(r, DateTime.UtcNow);
    }

    // Entity -> DTO, ჯარიმა და Overdue ფლაგი
    private static BorrowDto ToDto(BorrowRecord r, DateTime now) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        UserName = r.User.UserName,
        BookId = r.BookId,
        BookTitle = r.Book.Title,
        BorrowDate = r.BorrowDate,
        DueDate = r.DueDate,
        ReturnDate = r.ReturnDate,
        Status = r.Status.ToString(),
        IsOverdue = r.Status == BorrowStatus.Approved && r.DueDate < now,
        FineAmount = (r.Status == BorrowStatus.Approved || r.Status == BorrowStatus.Returned)
            ? FineCalculator.Calculate(r.DueDate, r.ReturnDate ?? now) : 0m
    };
}
