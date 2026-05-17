using EfUi.SampleHost.Models;
using Microsoft.EntityFrameworkCore;

namespace EfUi.SampleHost.Data;

public static class SampleDbSeeder
{
    public static async Task SeedAsync(SampleDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Users.AnyAsync())
        {
            return;
        }

        var admins = new Group { Name = "Admins" };
        var guests = new Group { Name = "Guests" };

        db.Groups.AddRange(admins, guests);
        db.Users.AddRange(
            new User
            {
                Name = "Ada",
                Email = "ada@example.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Group = admins
            },
            new User
            {
                Name = "Linus",
                Email = "linus@example.com",
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                Group = guests
            });

        await db.SaveChangesAsync();
    }
}
