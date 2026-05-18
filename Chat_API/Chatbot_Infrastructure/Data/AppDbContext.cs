using Chatbot_Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Chatbot_Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly int _embeddingDimension;

        public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration) : base(options)
        {
            _embeddingDimension = configuration.GetValue<int?>("AiSettings:EmbeddingDimension") ?? 1536;
        }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ConversationMessage> ConversationMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired();
                entity.Property(e => e.FilePath).IsRequired();
                entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
                entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            });

            modelBuilder.Entity<DocumentChunk>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Embedding).HasColumnType($"vector({_embeddingDimension})");
                entity.Property(e => e.ContentHash).HasMaxLength(64);
                entity.HasIndex(e => e.ContentHash);
                entity.Property(e => e.EmbeddingStatus).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

                entity.HasOne(d => d.Document)
                      .WithMany(p => p.Chunks)
                      .HasForeignKey(d => d.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Conversation>(entity =>
            {
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<ConversationMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Role).HasConversion<string>();
                entity.Property(e => e.Content).IsRequired();
                entity.HasOne(e => e.Conversation)
                    .WithMany(c => c.Messages)
                    .HasForeignKey(e => e.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}

