using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Telegram.DB;

public partial class TelegramDB : DbContext
{
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<Record> Records { get; set; } = null!;
    public virtual DbSet<FutureRecord> FutureRecords { get; set; } = null!;
    public virtual DbSet<Specialist> Specialists { get; set; } = null!;


    public class User
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long TelegramId { get; set; }

        public string TelegramName { get; set; }
        public string Language { get; set; }
        public virtual Specialist? Specialist { get; set; }
        public virtual ICollection<Record> History { get; } = new List<Record>();
        public virtual ICollection<Record> HistorySpec { get; } = new List<Record>();
        public uint Role { get; set; }

        public User()
        {
            TelegramId = 0;
            Language = "ru";
        }

        public User(long telegramId, string telegramName, string language, uint role)
        {
            TelegramId = telegramId;
            TelegramName = telegramName;
            Language = language;
            Role = role;
        }
    }

    public partial class FutureRecord
    {
        public int FutureRecordId { get; set; }
        public int RecordId { get; set; }
        public virtual Record Record { get; set; } = null!;

        public FutureRecord(int recordId)
        {
            RecordId = recordId;
        }
    }

    public partial class Record
    {
        public int RecordId { get; set; }
        public long UserId { get; set; }
        public long SpecId { get; set; }
        public DateTime Date { get; set; }
        public int? Feedback { get; set; }
        public virtual User User { get; set; } = null!;
        public virtual User Spec { get; set; } = null!;
        public virtual FutureRecord? FutureRecord { get; set; }

        public Record(long userId, long specId, DateTime date, int? feedback)
        {
            UserId = userId;
            SpecId = specId;
            Date = date;
            Feedback = feedback;
        }
    }

    public partial class Specialist
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long SpecialistId { get; set; }

        public string? DisplayName { get; set; }
        public bool Work { get; set; }
        public float? MeanFeedback { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public int Interval { get; set; }
        public virtual User SpecProfile { get; set; } = null!;


        public Specialist()
        {
        }

        public Specialist(long id, string? name, TimeSpan start, TimeSpan end, int interval)
        {
            SpecialistId = id;
            DisplayName = name;
            Start = start;
            End = end;
            Interval = interval;
            Work = false;
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseLazyLoadingProxies()
            .UseSqlServer("Server=localhost;Database=Telegram;TrustServerCertificate=True;" +
                          "User=sa;Password=adminRELease_15;MultipleActiveResultSets=true");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Record>(entity =>
        {
            entity.HasKey(r => r.RecordId).HasName("PK_Record");

            entity.HasOne(r => r.User).WithMany(u => u.History)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Record_Users_UserTelegramId");

            entity.HasOne(r => r.Spec).WithMany(u => u.HistorySpec)
                .HasForeignKey(r => r.SpecId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Record_Users_SpecTelegramId");
        });

        modelBuilder.Entity<User>(entity => { entity.HasKey(u => u.TelegramId).HasName("PK_Users"); });

        modelBuilder.Entity<Specialist>(entity =>
        {
            entity.HasKey(s => s.SpecialistId).HasName("PK_Spec");

            entity.HasOne(s => s.SpecProfile).WithOne(u => u.Specialist)
                .HasForeignKey<Specialist>(s => s.SpecialistId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Spec_User");
        });

        modelBuilder.Entity<FutureRecord>(entity =>
        {
            entity.HasKey(r => r.FutureRecordId).HasName("PK_FutRec");

            entity.HasOne(f => f.Record).WithOne(u => u.FutureRecord)
                .HasForeignKey<FutureRecord>(f => f.RecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FutRec_Rec");
        });
    }

    public void CheckTime(ref List<int> toDelete)
    {
        foreach (var record in FutureRecords)
        {
            if (DateTime.Now > record.Record.Date.AddMinutes(60))
            {
                toDelete.Add(record.RecordId);
                FutureRecords.Remove(record);
            }

            Thread.Sleep(5000);
        }
    }
}