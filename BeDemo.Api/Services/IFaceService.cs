/*
 * IFaceService.cs - Interface for Face service
 * 
 * This interface defines methods for retrieving faces from database.
 * Used by RoutingMiddleware to get face information for routing.
 */

using BeDemo.Api.Models;

namespace BeDemo.Api.Services;

/// <summary>
/// Interface for Face service
/// </summary>
public interface IFaceService
{
	/// <summary>
	/// Gets all faces from database
	/// Used by routing middleware to match face prefixes with face IDs
	/// </summary>
	/// <returns>List of all faces</returns>
	List<Face> GetFaces();
}
