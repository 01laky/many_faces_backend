/*
 * GenerateDatabaseDiagram.cs - Script to generate Mermaid ERD diagram from PostgreSQL database
 * 
 * This script extracts the database schema from PostgreSQL and generates a Mermaid ERD diagram
 * that is automatically saved to db_demo/README.md or a separate documentation file.
 */

using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BeDemo.Api.Data;

namespace BeDemo.Api.Scripts;

/// <summary>
/// Generates Mermaid ERD diagram from PostgreSQL database schema
/// </summary>
public static class DatabaseDiagramGenerator
{
    /// <summary>
    /// Generates Mermaid ERD diagram and saves it to documentation file
    /// </summary>
    public static async Task GenerateDiagramAsync(ApplicationDbContext context, string connectionString)
    {
        try
        {
            var diagram = await GenerateMermaidDiagramAsync(connectionString);
            await SaveDiagramToFileAsync(diagram);
            Console.WriteLine("✅ Database ERD diagram generated successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Failed to generate database diagram: {ex.Message}");
            // Don't throw - diagram generation is optional
        }
    }

    /// <summary>
    /// Generates Mermaid ERD diagram from PostgreSQL database
    /// </summary>
    private static async Task<string> GenerateMermaidDiagramAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database ?? "bedemo";
        
        // Build connection string to 'postgres' database to query information schema
        var infoSchemaConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = "postgres"  // Connect to postgres database to query information_schema
        }.ToString();

        var tables = new List<TableInfo>();
        
        // Extract table information from PostgreSQL information_schema
        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync();
            
            // Get all tables (excluding system tables)
            var tablesQuery = @"
                SELECT 
                    table_name,
                    table_type
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name NOT LIKE '__EFMigrationsHistory%'
                ORDER BY table_name;
            ";

            await using (var cmd = new NpgsqlCommand(tablesQuery, conn))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var tableName = reader.GetString(0);
                    var columns = await GetTableColumnsAsync(conn, tableName);
                    var foreignKeys = await GetForeignKeysAsync(conn, tableName);
                    
                    tables.Add(new TableInfo
                    {
                        Name = tableName,
                        Columns = columns,
                        ForeignKeys = foreignKeys
                    });
                }
            }
        }

        return BuildMermaidDiagram(tables);
    }

    /// <summary>
    /// Gets column information for a table
    /// </summary>
    private static async Task<List<ColumnInfo>> GetTableColumnsAsync(NpgsqlConnection conn, string tableName)
    {
        var columns = new List<ColumnInfo>();
        
        var query = @"
            SELECT 
                column_name,
                data_type,
                is_nullable,
                column_default,
                CASE 
                    WHEN tc.constraint_type = 'PRIMARY KEY' THEN true
                    ELSE false
                END as is_primary_key
            FROM information_schema.columns c
            LEFT JOIN information_schema.key_column_usage kcu
                ON c.table_name = kcu.table_name 
                AND c.column_name = kcu.column_name
                AND c.table_schema = kcu.table_schema
            LEFT JOIN information_schema.table_constraints tc
                ON kcu.constraint_name = tc.constraint_name
                AND tc.constraint_type = 'PRIMARY KEY'
            WHERE c.table_schema = 'public'
              AND c.table_name = @tableName
            ORDER BY c.ordinal_position;
        ";

        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("tableName", tableName);
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString(0);
                    var dataType = reader.GetString(1);
                    var isNullable = reader.GetString(2) == "YES";
                    var defaultValue = reader.IsDBNull(3) ? null : reader.GetString(3);
                    var isPrimaryKey = !reader.IsDBNull(4) && reader.GetBoolean(4);

                    // Format data type for better readability
                    var formattedType = FormatDataType(dataType);

                    columns.Add(new ColumnInfo
                    {
                        Name = columnName,
                        Type = formattedType,
                        IsNullable = isNullable,
                        IsPrimaryKey = isPrimaryKey,
                        DefaultValue = defaultValue
                    });
                }
            }
        }

        return columns;
    }

    /// <summary>
    /// Gets foreign key relationships for a table
    /// </summary>
    private static async Task<List<ForeignKeyInfo>> GetForeignKeysAsync(NpgsqlConnection conn, string tableName)
    {
        var foreignKeys = new List<ForeignKeyInfo>();
        
        var query = @"
            SELECT
                kcu.column_name,
                ccu.table_name AS foreign_table_name,
                ccu.column_name AS foreign_column_name,
                rc.delete_rule
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
                ON ccu.constraint_name = tc.constraint_name
                AND ccu.table_schema = tc.table_schema
            LEFT JOIN information_schema.referential_constraints AS rc
                ON tc.constraint_name = rc.constraint_name
                AND tc.table_schema = rc.constraint_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = 'public'
              AND tc.table_name = @tableName;
        ";

        await using (var cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("tableName", tableName);
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    foreignKeys.Add(new ForeignKeyInfo
                    {
                        ColumnName = reader.GetString(0),
                        ReferencedTable = reader.GetString(1),
                        ReferencedColumn = reader.GetString(2),
                        DeleteRule = reader.IsDBNull(3) ? "NO ACTION" : reader.GetString(3)
                    });
                }
            }
        }

        return foreignKeys;
    }

    /// <summary>
    /// Formats PostgreSQL data type for better readability
    /// </summary>
    private static string FormatDataType(string dataType)
    {
        return dataType switch
        {
            "character varying" => "varchar",
            "timestamp with time zone" => "timestamp",
            "timestamp without time zone" => "timestamp",
            "double precision" => "double",
            "numeric" => "decimal",
            _ => dataType
        };
    }

    /// <summary>
    /// Builds Mermaid ERD diagram from table information
    /// </summary>
    private static string BuildMermaidDiagram(List<TableInfo> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("erDiagram");
        sb.AppendLine();

        // Define all entities (tables) with their columns
        foreach (var table in tables)
        {
            var tableName = SanitizeTableName(table.Name);
            sb.AppendLine($"    {tableName} {{");
            
            foreach (var column in table.Columns)
            {
                var type = column.Type;
                var nullable = column.IsNullable ? "" : " NOT NULL";
                var pk = column.IsPrimaryKey ? " PK" : "";
                var displayName = column.Name;
                
                // Truncate long type names
                if (type.Length > 20)
                {
                    type = type.Substring(0, 17) + "...";
                }
                
                sb.AppendLine($"        {type} {displayName}{pk}{nullable}");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Define relationships (foreign keys)
        foreach (var table in tables)
        {
            var tableName = SanitizeTableName(table.Name);
            
            foreach (var fk in table.ForeignKeys)
            {
                var referencedTable = SanitizeTableName(fk.ReferencedTable);
                var relationshipType = GetRelationshipType(fk.DeleteRule);
                
                // Determine cardinality (simplified - assumes many-to-one)
                sb.AppendLine($"    {referencedTable} ||--o{{ {tableName} : \"has\"");
            }
        }

        sb.AppendLine("```");
        
        return sb.ToString();
    }

    /// <summary>
    /// Sanitizes table name for Mermaid syntax
    /// </summary>
    private static string SanitizeTableName(string tableName)
    {
        // Replace spaces and special characters with underscores for Mermaid compatibility
        return tableName.Replace(" ", "_").Replace("-", "_");
    }

    /// <summary>
    /// Gets relationship type description from delete rule
    /// </summary>
    private static string GetRelationshipType(string deleteRule)
    {
        return deleteRule switch
        {
            "CASCADE" => "cascades",
            "SET NULL" => "sets null",
            "RESTRICT" => "restricts",
            _ => "has"
        };
    }

    /// <summary>
    /// Saves diagram to documentation file in db_demo directory
    /// </summary>
    private static async Task SaveDiagramToFileAsync(string diagram)
    {
        // Try multiple paths to find db_demo directory
        var possiblePaths = new List<string>();
        
        // 1. Try relative to current execution directory (for local development)
        var currentDir = Directory.GetCurrentDirectory();
        var currentDirDbDemo = Path.GetFullPath(Path.Combine(currentDir, "..", "db_demo"));
        possiblePaths.Add(currentDirDbDemo);
        
        // 2. Try from be_demo/BeDemo.Api structure (from be_demo root)
        var beDemoDbDemo = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "db_demo"));
        possiblePaths.Add(beDemoDbDemo);
        
        // 3. Try from assembly location (for compiled builds)
        var assemblyDir = Path.GetDirectoryName(typeof(DatabaseDiagramGenerator).Assembly.Location);
        if (!string.IsNullOrEmpty(assemblyDir))
        {
            // From bin/Debug/net10.0 -> BeDemo.Api -> be_demo -> _mfai_demo -> db_demo
            var assemblyDbDemo = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "..", "db_demo"));
            possiblePaths.Add(assemblyDbDemo);
            // Alternative: from bin -> be_demo -> _mfai_demo -> db_demo
            var assemblyDbDemo2 = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "..", "..", "db_demo"));
            possiblePaths.Add(assemblyDbDemo2);
        }
        
        // 4. Try from project root (_mfai_demo)
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".."));
        if (File.Exists(Path.Combine(projectRoot, "README.md")) || File.Exists(Path.Combine(projectRoot, "start-all-dev.sh")))
        {
            var projectRootDbDemo = Path.Combine(projectRoot, "db_demo");
            possiblePaths.Add(projectRootDbDemo);
        }

        // Find the first existing db_demo directory
        string? dbDemoDir = null;
        foreach (var path in possiblePaths.Distinct())
        {
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "README.md")))
            {
                dbDemoDir = path;
                break;
            }
        }

        if (dbDemoDir == null)
        {
            Console.WriteLine($"⚠️  db_demo directory not found. Tried paths:");
            foreach (var path in possiblePaths)
            {
                Console.WriteLine($"   - {path}");
            }
            Console.WriteLine("   Skipping diagram save");
            return;
        }

        var readmePath = Path.Combine(dbDemoDir, "README.md");

        string existingContent = "";
        if (File.Exists(readmePath))
        {
            existingContent = await File.ReadAllTextAsync(readmePath);
        }

        // Check if diagram section already exists
        var diagramStartMarker = "<!-- AUTO-GENERATED DATABASE DIAGRAM - DO NOT EDIT -->";
        var diagramEndMarker = "<!-- END AUTO-GENERATED DATABASE DIAGRAM -->";

        var newContent = existingContent;

        if (existingContent.Contains(diagramStartMarker))
        {
            // Replace existing diagram section
            var startIndex = existingContent.IndexOf(diagramStartMarker);
            var endIndex = existingContent.IndexOf(diagramEndMarker);
            
            if (endIndex > startIndex)
            {
                var beforeDiagram = existingContent.Substring(0, startIndex);
                var afterDiagram = existingContent.Substring(endIndex + diagramEndMarker.Length);
                newContent = beforeDiagram + diagramStartMarker + "\n\n" + diagram + "\n\n" + diagramEndMarker + afterDiagram;
            }
            else
            {
                // End marker not found, append to end
                newContent = existingContent + "\n\n" + diagramStartMarker + "\n\n" + diagram + "\n\n" + diagramEndMarker;
            }
        }
        else
        {
            // Add diagram section at the end
            newContent = existingContent.TrimEnd() + "\n\n" + diagramStartMarker + "\n\n## Database Schema\n\n" + diagram + "\n\n" + diagramEndMarker;
        }

        await File.WriteAllTextAsync(readmePath, newContent);
        Console.WriteLine($"📊 Database diagram saved to: {readmePath}");
    }

    private class TableInfo
    {
        public string Name { get; set; } = string.Empty;
        public List<ColumnInfo> Columns { get; set; } = new();
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    }

    private class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public string? DefaultValue { get; set; }
    }

    private class ForeignKeyInfo
    {
        public string ColumnName { get; set; } = string.Empty;
        public string ReferencedTable { get; set; } = string.Empty;
        public string ReferencedColumn { get; set; } = string.Empty;
        public string DeleteRule { get; set; } = string.Empty;
    }
}
