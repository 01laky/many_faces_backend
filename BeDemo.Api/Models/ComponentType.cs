using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BeDemo.Api.Models;

/// <summary>
/// ComponentType entity model - lookup table for grid component types.
/// Each type has a fixed Id and unique Index.
/// </summary>
public class ComponentType
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
/// Fixed enum of component type IDs with their indexes.
/// </summary>
public enum ComponentTypeId
{
    Ad = 1,
    Album = 2,
    Blog = 3,
    ChatRoom = 4,
    UserProfile = 5,
    Story = 6,
    Reel = 7,
}

/// <summary>
/// String indexes for component types (used in FE GridComponentType).
/// </summary>
public static class ComponentTypeIndex
{
    public const string Ad = "ad";
    public const string Album = "album";
    public const string Blog = "blog";
    public const string ChatRoom = "chatRoom";
    public const string UserProfile = "userProfile";
    public const string Story = "story";
    public const string Reel = "reel";
}
