using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>
/// PageType entity model - type/category for pages
/// </summary>
public class PageType
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }

	[Required]
	[StringLength(100)]
	public string Index { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }

	// Navigation property - one PageType has many Pages
	public ICollection<Page> Pages { get; set; } = new List<Page>();
}
