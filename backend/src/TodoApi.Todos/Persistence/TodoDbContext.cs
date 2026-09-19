using Microsoft.EntityFrameworkCore;
using TodoApi.Todos.Models;

namespace TodoApi.Todos.Persistence;

public sealed class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoModel> Todos => Set<TodoModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoModel>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).IsRequired();
        });
    }
}
