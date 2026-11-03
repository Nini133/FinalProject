using System.Security.Claims;
using LibraryApi.Dtos;
using LibraryApi.Entities;
using LibraryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LibraryApi.Controllers;

[ApiController, Route("api/auth"), AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto) => Ok(await _auth.RegisterAsync(dto));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto) => Ok(await _auth.LoginAsync(dto));
}

[ApiController, Route("api/books")]
public class BooksController : ControllerBase
{
    private readonly IBookService _books;
    public BooksController(IBookService books) => _books = books;

    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<PagedResult<BookDto>>> GetAll([FromQuery] BookQuery query) => Ok(await _books.GetPagedAsync(query));

    [HttpGet("{id:int}"), AllowAnonymous]
    public async Task<ActionResult<BookDto>> GetById(int id) => Ok(await _books.GetByIdAsync(id));

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<BookDto>> Create(BookSaveDto dto)
    {
        var book = await _books.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = book.Id }, book);
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<BookDto>> Update(int id, BookSaveDto dto) => Ok(await _books.UpdateAsync(id, dto));

    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _books.DeleteAsync(id);
        return NoContent();
    }
}

[ApiController, Route("api/borrows"), Authorize]
public class BorrowsController : ControllerBase
{
    private readonly IBorrowService _borrows;
    public BorrowsController(IBorrowService borrows) => _borrows = borrows;

    // მიმდინარე მომხმარებელი ტოკენიდან
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpPost]
    public async Task<ActionResult<BorrowDto>> Create(BorrowCreateDto dto)
    {
        var r = await _borrows.CreateAsync(UserId, dto.BookId);
        return CreatedAtAction(nameof(GetById), new { id = r.Id }, r);
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<BorrowDto>>> GetAll([FromQuery] BorrowStatus? status) => Ok(await _borrows.GetAllAsync(status));

    [HttpGet("my")]
    public async Task<ActionResult<List<BorrowDto>>> My() => Ok(await _borrows.GetMyAsync(UserId));

    [HttpGet("overdue")]
    public async Task<ActionResult<List<BorrowDto>>> Overdue() => Ok(await _borrows.GetOverdueAsync(UserId, IsAdmin));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BorrowDto>> GetById(int id) => Ok(await _borrows.GetByIdAsync(id, UserId, IsAdmin));

    [HttpPatch("{id:int}/approve"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<BorrowDto>> Approve(int id) => Ok(await _borrows.ApproveAsync(id));

    [HttpPatch("{id:int}/reject"), Authorize(Roles = "Admin")]
    public async Task<ActionResult<BorrowDto>> Reject(int id) => Ok(await _borrows.RejectAsync(id));

    [HttpPatch("{id:int}/return")]
    public async Task<ActionResult<BorrowDto>> Return(int id) => Ok(await _borrows.ReturnAsync(id, UserId, IsAdmin));
}

[ApiController, Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalog;
    public CategoriesController(ICatalogService catalog) => _catalog = catalog;

    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<List<CategoryDto>>> GetAll() => Ok(await _catalog.GetCategoriesAsync());

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoryDto>> Create(NameDto dto) => StatusCode(201, await _catalog.CreateCategoryAsync(dto.Name));
}

[ApiController, Route("api/authors")]
public class AuthorsController : ControllerBase
{
    private readonly ICatalogService _catalog;
    public AuthorsController(ICatalogService catalog) => _catalog = catalog;

    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<List<AuthorDto>>> GetAll() => Ok(await _catalog.GetAuthorsAsync());

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<ActionResult<AuthorDto>> Create(NameDto dto) => StatusCode(201, await _catalog.CreateAuthorAsync(dto.Name));
}

[ApiController, Route("api/stats"), Authorize(Roles = "Admin")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _stats;
    public StatsController(IStatsService stats) => _stats = stats;

    [HttpGet("popular-books")]
    public async Task<ActionResult<List<PopularBookDto>>> Popular([FromQuery] int top = 5) => Ok(await _stats.GetPopularAsync(Math.Clamp(top, 1, 20)));

    [HttpGet("summary")]
    public async Task<ActionResult<StatsDto>> Summary() => Ok(await _stats.GetSummaryAsync());
}
