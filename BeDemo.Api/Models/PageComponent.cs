using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>
/// PageComponent entity - a single component placed on a page grid.
/// Has FK relations to Page, ComponentType, and DisplayMode.
/// </summary>
public class PageComponent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int PageId { get; set; }

    [ForeignKey(nameof(PageId))]
    public Page Page { get; set; } = null!;

    [Required]
    public int ComponentTypeId { get; set; }

    [ForeignKey(nameof(ComponentTypeId))]
    public ComponentType ComponentType { get; set; } = null!;

    [Required]
    public int DisplayModeId { get; set; }

    [ForeignKey(nameof(DisplayModeId))]
    public DisplayMode DisplayMode { get; set; } = null!;

    /// <summary>Unique grid key used by react-grid-layout (the "i" property).</summary>
    [Required]
    [StringLength(100)]
    public string GridKey { get; set; } = string.Empty;

    /// <summary>Grid column position.</summary>
    public int X { get; set; }

    /// <summary>Grid row position.</summary>
    public int Y { get; set; }

    /// <summary>Width in grid columns.</summary>
    public int W { get; set; } = 3;

    /// <summary>Height in row units.</summary>
    public int H { get; set; } = 2;

    /// <summary>Minimum width in grid columns.</summary>
    public int MinW { get; set; } = 1;

    /// <summary>Minimum height in row units.</summary>
    public int MinH { get; set; } = 1;

    [StringLength(200)]
    public string? Label { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(100)]
    public string? Icon { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
