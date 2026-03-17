/*
 * GenerateDiagramManually.cs - Manual script to generate database diagram
 * 
 * Run this with: dotnet run --project BeDemo.Api -- generate-diagram
 * Or compile and run as standalone script
 */

using Microsoft.EntityFrameworkCore;
using BeDemo.Api.Data;
using BeDemo.Api.Scripts;

namespace BeDemo.Api.Scripts;

public class GenerateDiagramManually
{
    public static async Task RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] != "generate-diagram")
        {
            return;
        }

        Console.WriteLine("📊 Generating database diagram...");

        var connectionString = "Host=localhost;Port=54320;Database=bedemo;Username=bedemo_user;Password=bedemo_password";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var context = new ApplicationDbContext(options);

        try
        {
            await DatabaseDiagramGenerator.GenerateDiagramAsync(context, connectionString);
            Console.WriteLine("✅ Diagram generated successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
