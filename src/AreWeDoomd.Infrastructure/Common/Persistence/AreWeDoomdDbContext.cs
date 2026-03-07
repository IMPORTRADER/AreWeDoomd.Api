using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Domain.Comments;
using AreWeDoomd.Domain.Posts;
using AreWeDoomd.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Persistence;

public class AreWeDoomdDbContext(DbContextOptions<AreWeDoomdDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AreWeDoomdDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}

