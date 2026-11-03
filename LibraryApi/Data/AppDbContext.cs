using LibraryApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<BorrowRecord> BorrowRecords => Set<BorrowRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        // უნიკალური ინდექსები: Email, UserName
        b.Entity<User>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Email).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.UserName).IsUnique();
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Author>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.FullName).IsUnique();
        });

        // Check constraint: AvailableCopies 0..TotalCopies
        b.Entity<Book>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_Books_Copies", "[AvailableCopies] >= 0 AND [AvailableCopies] <= [TotalCopies]"));
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.ISBN).HasMaxLength(13).IsRequired();
            e.HasIndex(x => x.ISBN).IsUnique();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne(x => x.Category).WithMany(c => c.Books).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        // Many-to-Many: Books <-> Authors
        b.Entity<BookAuthor>(e =>
        {
            e.HasKey(x => new { x.BookId, x.AuthorId });
            e.HasOne(x => x.Book).WithMany(x => x.BookAuthors).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Author).WithMany(x => x.BookAuthors).HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        });

        // ისტორია არ იშლება: Restrict
        b.Entity<BorrowRecord>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.User).WithMany(u => u.BorrowRecords).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Book).WithMany(x => x.BorrowRecords).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.Status });
        });

        // Seed Data
        b.Entity<Role>().HasData(new Role { Id = 1, Name = "Admin" }, new Role { Id = 2, Name = "Client" });

        b.Entity<Category>().HasData(
            new Category { Id = 1, Name = "კლასიკური ლიტერატურა" },
            new Category { Id = 2, Name = "ისტორიული რომანი" },
            new Category { Id = 3, Name = "თანამედროვე პროზა" },
            new Category { Id = 4, Name = "თარგმნილი ლიტერატურა" },
            new Category { Id = 5, Name = "სპორტი" },
            new Category { Id = 6, Name = "გეოგრაფია" });

        b.Entity<Author>().HasData(
            new Author { Id = 1, FullName = "შოთა რუსთაველი" },
            new Author { Id = 2, FullName = "კონსტანტინე გამსახურდია" },
            new Author { Id = 3, FullName = "დათო ტურაშვილი" },
            new Author { Id = 4, FullName = "სუადი" },
            new Author { Id = 5, FullName = "ემილი ბრონტე" },
            new Author { Id = 6, FullName = "Andre Agassi" },
            new Author { Id = 7, FullName = "Christopher McDougall" },
            new Author { Id = 8, FullName = "Tim Marshall" },
            new Author { Id = 9, FullName = "Judith Schalansky" });

        b.Entity<Book>().HasData(
            new Book { Id = 1, Title = "ვეფხისტყაოსანი", ISBN = "9789941100011", CategoryId = 1, TotalCopies = 5, AvailableCopies = 5 },
            new Book { Id = 2, Title = "დიდოსტატის მარჯვენა", ISBN = "9789941100028", CategoryId = 2, TotalCopies = 4, AvailableCopies = 4 },
            new Book { Id = 3, Title = "ჯინსების თაობა", ISBN = "9789941100035", CategoryId = 3, TotalCopies = 3, AvailableCopies = 3 },
            new Book { Id = 4, Title = "ცოცხლად დამწვარი", ISBN = "9789941197857", CategoryId = 4, TotalCopies = 3, AvailableCopies = 3 },
            new Book { Id = 5, Title = "ქარიშხლიანი უღელტეხილი", ISBN = "9789941100042", CategoryId = 4, TotalCopies = 4, AvailableCopies = 4 },
            new Book { Id = 6, Title = "Open: An Autobiography", ISBN = "9789941100059", CategoryId = 5, TotalCopies = 2, AvailableCopies = 2 },
            new Book { Id = 7, Title = "Born to Run", ISBN = "9789941100066", CategoryId = 5, TotalCopies = 2, AvailableCopies = 2 },
            new Book { Id = 8, Title = "Prisoners of Geography", ISBN = "9789941100073", CategoryId = 6, TotalCopies = 3, AvailableCopies = 3 },
            new Book { Id = 9, Title = "Atlas of Remote Islands", ISBN = "9789941100080", CategoryId = 6, TotalCopies = 2, AvailableCopies = 2 });

        b.Entity<BookAuthor>().HasData(
            new BookAuthor { BookId = 1, AuthorId = 1 },
            new BookAuthor { BookId = 2, AuthorId = 2 },
            new BookAuthor { BookId = 3, AuthorId = 3 },
            new BookAuthor { BookId = 4, AuthorId = 4 },
            new BookAuthor { BookId = 5, AuthorId = 5 },
            new BookAuthor { BookId = 6, AuthorId = 6 },
            new BookAuthor { BookId = 7, AuthorId = 7 },
            new BookAuthor { BookId = 8, AuthorId = 8 },
            new BookAuthor { BookId = 9, AuthorId = 9 });
    }
}
