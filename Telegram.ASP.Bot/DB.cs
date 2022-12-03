using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Telegram.DB;

public sealed class TelegramDB : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Record> Records { get; set; } = null!;
    public DbSet<Record> FutureRecords { get; set; } = null!;


    public class User
    {
        [Key] public long TelegramId { get; set; }
        public string Language { get; set; }
        public List<Record>? History { get; set; }
        public uint Role { get; set; }

        public User()
        {
            TelegramId = 0;
            Language = "ru";
        }

        public User(long telegramId, string language, List<Record>? history, uint role)
        {
            TelegramId = telegramId;
            Language = language;
            History = history;
            Role = role;
        }
    }

    public class Record
    {
        [Key] public int RecordId { get; set; }
        public float UserId { get; set; }
        public float SpecId { get; set; }
        public DateTime Date { get; set; }
        public int? Feedback { get; set; }

        
        public Record(int recordId, float userId, float specId, DateTime date, int? feedback)
        {
            RecordId = recordId;
            UserId = userId;
            SpecId = specId;
            Date = date;
            Feedback = feedback;
        }
    }

    public TelegramDB()
    {
        Database.EnsureDeleted();
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={Environment.CurrentDirectory}/tel.db");
    }

    /*protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Record>()
            .HasOne(d => d.UserId)
            .WithOne()
            .OnDelete(DeleteBehavior.NoAction);
    }*/
}