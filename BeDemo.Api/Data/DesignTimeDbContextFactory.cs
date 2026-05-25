using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BeDemo.Api.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
	public ApplicationDbContext CreateDbContext(string[] args)
	{
		var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new InvalidOperationException(
				"Set ConnectionStrings__DefaultConnection for EF design-time tools (see many_faces_backend/README.md).");
		}

		var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
		optionsBuilder.UseNpgsql(connectionString);

		return new ApplicationDbContext(optionsBuilder.Options);
	}
}
