using LibraryApi.Data;
using LibraryApi.Dtos;
using LibraryApi.Entities;
using LibraryApi.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Services;

public interface ICatalogService
{
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<CategoryDto> CreateCategoryAsync(string name);
    Task<List<AuthorDto>> GetAuthorsAsync();
    Task<AuthorDto> CreateAuthorAsync(string name);
}

public class CatalogService : ICatalogService
{
    private readonly AppDbContext _db;
    public CatalogService(AppDbContext db) => _db = db;

    public Task<List<CategoryDto>> GetCategoriesAsync() =>
        _db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryDto { Id = c.Id, Name = c.Name }).ToListAsync();

    // კატეგორია: სახელის უნიკალურობა
    public async Task<CategoryDto> CreateCategoryAsync(string name)
    {
        name = name.Trim();
        if (await _db.Categories.AnyAsync(c => c.Name == name)) throw new ConflictException("კატეგორია უკვე არსებობს");
        var c = new Category { Name = name };
        _db.Categories.Add(c);
        await _db.SaveChangesAsync();
        return new CategoryDto { Id = c.Id, Name = c.Name };
    }

    public Task<List<AuthorDto>> GetAuthorsAsync() =>
        _db.Authors.AsNoTracking().OrderBy(a => a.FullName).Select(a => new AuthorDto { Id = a.Id, FullName = a.FullName }).ToListAsync();

    // ავტორი: სახელის უნიკალურობა
    public async Task<AuthorDto> CreateAuthorAsync(string name)
    {
        name = name.Trim();
        if (await _db.Authors.AnyAsync(a => a.FullName == name)) throw new ConflictException("ავტორი უკვე არსებობს");
        var a = new Author { FullName = name };
        _db.Authors.Add(a);
        await _db.SaveChangesAsync();
        return new AuthorDto { Id = a.Id, FullName = a.FullName };
    }
}
