using System.Linq.Expressions;
using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Entities;
using LibraryApi.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

public interface IBookService
{
    Task<PagedResult<BookDto>> GetPagedAsync(BookQuery q);
    Task<BookDto> GetByIdAsync(int id);
    Task<BookDto> CreateAsync(BookSaveDto dto);
    Task<BookDto> UpdateAsync(int id, BookSaveDto dto);
    Task DeleteAsync(int id);
}

public class BookService : IBookService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BookService> _log;
    public BookService(AppDbContext db, ILogger<BookService> log) { _db = db; _log = log; }

    private static readonly Expression<Func<Book, BookDto>> Map = b => new BookDto
    {
        Id = b.Id,
        Title = b.Title,
        ISBN = b.ISBN,
        CategoryId = b.CategoryId,
        Category = b.Category.Name,
        TotalCopies = b.TotalCopies,
        AvailableCopies = b.AvailableCopies,
        Authors = b.BookAuthors.Select(x => x.Author.FullName).ToList(),
        AuthorIds = b.BookAuthors.Select(x => x.AuthorId).ToList()
    };
    private static readonly Func<Book, BookDto> ToDto = Map.Compile();

    // სია: ფილტრები (Title/Author/Category/ISBN), დალაგება, Pagination
    public async Task<PagedResult<BookDto>> GetPagedAsync(BookQuery q)
    {
        var books = _db.Books.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Title)) books = books.Where(b => b.Title.Contains(q.Title));
        if (!string.IsNullOrWhiteSpace(q.Author)) books = books.Where(b => b.BookAuthors.Any(x => x.Author.FullName.Contains(q.Author)));
        if (!string.IsNullOrWhiteSpace(q.Category)) books = books.Where(b => b.Category.Name.Contains(q.Category));
        if (!string.IsNullOrWhiteSpace(q.ISBN)) books = books.Where(b => b.ISBN.Contains(q.ISBN));

        IQueryable<Book> sorted = q.SortBy?.ToLower() switch
        {
            "isbn" => q.Desc ? books.OrderByDescending(b => b.ISBN) : books.OrderBy(b => b.ISBN),
            "available" => q.Desc ? books.OrderByDescending(b => b.AvailableCopies) : books.OrderBy(b => b.AvailableCopies),
            "category" => q.Desc ? books.OrderByDescending(b => b.Category.Name) : books.OrderBy(b => b.Category.Name),
            _ => q.Desc ? books.OrderByDescending(b => b.Title) : books.OrderBy(b => b.Title)
        };

        var total = await sorted.CountAsync();
        var items = await sorted.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(Map).ToListAsync();
        return new PagedResult<BookDto> { Items = items, Page = q.Page, PageSize = q.PageSize, TotalCount = total };
    }

    // ერთი წიგნი: Include / ThenInclude
    public async Task<BookDto> GetByIdAsync(int id)
    {
        var book = await _db.Books.AsNoTracking()
            .Include(b => b.Category)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("წიგნი ვერ მოიძებნა");
        return ToDto(book);
    }

    // დამატება: ISBN უნიკალურობა, Available = Total
    public async Task<BookDto> CreateAsync(BookSaveDto dto)
    {
        await ValidateAsync(dto, null);
        var book = new Book
        {
            Title = dto.Title.Trim(),
            ISBN = dto.ISBN,
            CategoryId = dto.CategoryId,
            TotalCopies = dto.TotalCopies,
            AvailableCopies = dto.TotalCopies,
            BookAuthors = dto.AuthorIds.Distinct().Select(id => new BookAuthor { AuthorId = id }).ToList()
        };
        _db.Books.Add(book);
        await _db.SaveChangesAsync();
        _log.LogInformation("დაემატა წიგნი {Id}", book.Id);
        return await GetByIdAsync(book.Id);
    }

    // შეცვლა: Total-ის კორექტირება გაცემულის გათვალისწინებით, ავტორების სინქრონი
    public async Task<BookDto> UpdateAsync(int id, BookSaveDto dto)
    {
        var book = await _db.Books.Include(b => b.BookAuthors).FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new NotFoundException("წიგნი ვერ მოიძებნა");
        await ValidateAsync(dto, id);

        var borrowed = book.TotalCopies - book.AvailableCopies;
        if (dto.TotalCopies < borrowed)
            throw new BadRequestException($"ეგზემპლართა რაოდენობა ვერ იქნება {borrowed}-ზე ნაკლები (გაცემულია)");

        book.Title = dto.Title.Trim();
        book.ISBN = dto.ISBN;
        book.CategoryId = dto.CategoryId;
        book.TotalCopies = dto.TotalCopies;
        book.AvailableCopies = dto.TotalCopies - borrowed;
        book.Version++;

        var ids = dto.AuthorIds.Distinct().ToList();
        book.BookAuthors.RemoveAll(ba => !ids.Contains(ba.AuthorId));
        foreach (var authorId in ids.Where(a => book.BookAuthors.All(ba => ba.AuthorId != a)))
            book.BookAuthors.Add(new BookAuthor { AuthorId = authorId });

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    // წაშლა: აკრძალულია თუ გაცემის ისტორია აქვს
    public async Task DeleteAsync(int id)
    {
        var book = await _db.Books.FindAsync(id) ?? throw new NotFoundException("წიგნი ვერ მოიძებნა");
        if (await _db.BorrowRecords.AnyAsync(r => r.BookId == id))
            throw new ConflictException("წიგნს გაცემის ისტორია აქვს და ვერ წაიშლება");

        _db.Books.Remove(book);
        await _db.SaveChangesAsync();
        _log.LogInformation("წაიშალა წიგნი {Id}", id);
    }

    // ვალიდაცია: კატეგორია, ავტორები, ISBN დუბლი
    private async Task ValidateAsync(BookSaveDto dto, int? currentId)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId))
            throw new BadRequestException("კატეგორია ვერ მოიძებნა");

        var ids = dto.AuthorIds.Distinct().ToList();
        if (await _db.Authors.CountAsync(a => ids.Contains(a.Id)) != ids.Count)
            throw new BadRequestException("ერთი ან მეტი ავტორი ვერ მოიძებნა");

        if (await _db.Books.AnyAsync(b => b.ISBN == dto.ISBN && b.Id != currentId))
            throw new ConflictException("ამ ISBN-ით წიგნი უკვე არსებობს");
    }
}
