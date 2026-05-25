using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>
/// DisplayMode entity model - lookup table for how a component is rendered.
/// Each mode has a fixed Id and unique Index.
/// </summary>
public class DisplayMode
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public int Id { get; set; }

	[Required]
	[StringLength(50)]
	public string Index { get; set; } = string.Empty;

	[Required]
	[StringLength(100)]
	public string Name { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Fixed enum of display mode IDs.
/// </summary>
public enum DisplayModeId
{
	Item = 1,
	Grid = 2,
	Carousel = 3,
}

/// <summary>
/// String indexes for display modes.
/// </summary>
public static class DisplayModeIndex
{
	public const string Item = "item";
	public const string Grid = "grid";
	public const string Carousel = "carousel";
}
