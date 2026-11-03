using System.ComponentModel.DataAnnotations;

namespace LibraryApi.Dtos;

public class RegisterDto
{
    [Required, StringLength(50, MinimumLength = 3)] public string UserName { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 6)] public string Password { get; set; } = "";
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class AuthResponseDto
{
    public string Token { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class BookSaveDto
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, RegularExpression(@"^\d{10}(\d{3})?$", ErrorMessage = "ISBN უნდა შედგებოდეს 10 ან 13 ციფრისგან")]
    public string ISBN { get; set; } = "";
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Range(1, 10000)] public int TotalCopies { get; set; }
    [MinLength(1, ErrorMessage = "მიუთითეთ მინიმუმ ერთი ავტორი")] public List<int> AuthorIds { get; set; } = new();
}

public class BookDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string ISBN { get; set; } = "";
    public int CategoryId { get; set; }
    public string Category { get; set; } = "";
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public List<string> Authors { get; set; } = new();
    public List<int> AuthorIds { get; set; } = new();
}

public class BookQuery
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Category { get; set; }
    public string? ISBN { get; set; }
    public string? SortBy { get; set; }
    public bool Desc { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 50)] public int PageSize { get; set; } = 8;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class BorrowCreateDto
{
    [Range(1, int.MaxValue)] public int BookId { get; set; }
}

public class BorrowDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public int BookId { get; set; }
    public string BookTitle { get; set; } = "";
    public DateTime BorrowDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string Status { get; set; } = "";
    public bool IsOverdue { get; set; }
    public decimal FineAmount { get; set; }
}

public class NameDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
}

public class CategoryDto { public int Id { get; set; } public string Name { get; set; } = ""; }
public class AuthorDto { public int Id { get; set; } public string FullName { get; set; } = ""; }

public class PopularBookDto
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public int BorrowCount { get; set; }
}

public class StatsDto
{
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public int Overdue { get; set; }
    public int Books { get; set; }
    public int Users { get; set; }
}
