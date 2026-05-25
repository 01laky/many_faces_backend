namespace BeDemo.Api.Models.Requests.Albums;

public class CreateAlbumDto
{
	public string Title { get; set; } = string.Empty;
	public string? Description { get; set; }
	public AlbumTypeEnum AlbumType { get; set; } = AlbumTypeEnum.Public;
	public MediaTypeEnum MediaType { get; set; } = MediaTypeEnum.Image;
	public List<int>? FaceIds { get; set; }
}

public class UpdateAlbumDto
{
	public string? Title { get; set; }
	public string? Description { get; set; }
	public AlbumTypeEnum? AlbumType { get; set; }
	public MediaTypeEnum? MediaType { get; set; }
	public List<int>? FaceIds { get; set; }
}

public class CreateAlbumCommentDto
{
	public string Content { get; set; } = string.Empty;
}

public class UpdateAlbumCommentDto
{
	public string Content { get; set; } = string.Empty;
}

