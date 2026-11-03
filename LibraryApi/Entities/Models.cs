namespace LibraryApi.Entities;

public enum BorrowStatus { Pending, Approved, Rejected, Returned }

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<User> Users { get; set; } = new();
}

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public List<BorrowRecord> BorrowRecords { get; set; } = new();
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Book> Books { get; set; } = new();
}

public class Author
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public List<BookAuthor> BookAuthors { get; set; } = new();
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string ISBN { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public int Version { get; set; } // Optimistic Concurrency ტოკენი
    public List<BookAuthor> BookAuthors { get; set; } = new();
    public List<BorrowRecord> BorrowRecords { get; set; } = new();
}

public class BookAuthor
{
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public int AuthorId { get; set; }
    public Author Author { get; set; } = null!;
}

public class BorrowRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public DateTime BorrowDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public BorrowStatus Status { get; set; }
}
