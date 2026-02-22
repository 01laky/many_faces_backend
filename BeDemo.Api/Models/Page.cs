using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>
/// Page entity model - belongs to a Face
/// </summary>
public class Page
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int FaceId { get; set; }

    [ForeignKey(nameof(FaceId))]
    public Face Face { get; set; } = null!;

    [Required]
    public int PageTypeId { get; set; }

    [ForeignKey(nameof(PageTypeId))]
    public PageType PageType { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(500)]
    public string Path { get; set; } = string.Empty;

    public int Index { get; set; } = 0;

    /// <summary>
    /// JSON schema for the responsive grid layout (react-grid-layout format)
    /// </summary>
    public string? GridSchema { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property - one Page has many RouteTranslations
    public ICollection<PageRouteTranslation> RouteTranslations { get; set; } = new List<PageRouteTranslation>();
}
