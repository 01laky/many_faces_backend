/*
 * FaceService.cs - Service for retrieving faces from database
 * 
 * This service provides methods to get faces from database.
 * Used by RoutingMiddleware to match face prefixes in URLs with face IDs.
 */

using BeDemo.Api.Data;
using BeDemo.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeDemo.Api.Services;

/// <summary>
/// Service for retrieving faces from database
/// </summary>
public class FaceService : IFaceService
{
	private readonly ApplicationDbContext _context;
	private readonly ILogger<FaceService> _logger;

	public FaceService(
		ApplicationDbContext context,
		ILogger<FaceService> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>
	/// Gets all faces from database
	/// Used by routing middleware to match face prefixes with face IDs
	/// </summary>
	/// <returns>List of all faces</returns>
	public List<Face> GetFaces()
	{
		try
		{
			// Get all faces from database, ordered by Index
			// AsNoTracking() improves performance since we don't need change tracking for routing
			var faces = _context.Faces
				.AsNoTracking()
				.OrderBy(f => f.Index)
				.ToList();

			_logger.LogDebug("Retrieved {Count} faces from database", faces.Count);
			return faces;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving faces from database");
			// Return empty list on error to prevent middleware from failing
			// Middleware will handle empty list gracefully (no faces = no face routing)
			return new List<Face>();
		}
	}
}
