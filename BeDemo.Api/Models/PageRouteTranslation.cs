using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>
/// PageRouteTranslation entity model - stores route name translations per language for a Page
/// </summary>
public class PageRouteTranslation
{
	[Key]
	public int Id { get; set; }

	[Required]
	public int PageId { get; set; }

	[ForeignKey(nameof(PageId))]
	public Page Page { get; set; } = null!;

	[Required]
	[StringLength(10)]
	public string LanguageCode { get; set; } = string.Empty;

	[Required]
	[StringLength(200)]
	public string TranslatedRoute { get; set; } = string.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime? UpdatedAt { get; set; }
}
