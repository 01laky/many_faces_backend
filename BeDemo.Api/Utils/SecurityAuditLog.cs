namespace BeDemo.Api.Utils;

/// <summary>
/// Structured audit templates (ACL A22). Prefer <see cref="Microsoft.Extensions.Logging.LoggerExtensions"/> with these event names
/// so Seq / log aggregation can alert on <c>SecurityAudit</c> without a separate append-only store in this demo.
/// </summary>
public static class SecurityAuditLog
{
	/// <summary>Correlation id: prefer <c>Activity.Current?.Id</c> or HttpContext.TraceIdentifier when wiring callers.</summary>
	public static void FaceRoleChanged(
		Microsoft.Extensions.Logging.ILogger logger,
		string actorUserId,
		int faceId,
		string? previousRoleName,
		string newRoleName,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit FaceRoleChanged actor={ActorUserId} faceId={FaceId} from={PreviousRole} to={NewRole} correlationId={CorrelationId}",
			actorUserId, faceId, previousRoleName ?? "(none)", newRoleName, correlationId);
	}

	public static void GlobalPageTypeMutation(
		Microsoft.Extensions.Logging.ILogger logger,
		string actorUserId,
		string action,
		int? pageTypeId,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit PageTypeMutation actor={ActorUserId} action={Action} pageTypeId={PageTypeId} correlationId={CorrelationId}",
			actorUserId, action, pageTypeId, correlationId);
	}

	public static void FaceConfigurationMutation(
		Microsoft.Extensions.Logging.ILogger logger,
		string actorUserId,
		string action,
		int faceId,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit FaceMutation actor={ActorUserId} action={Action} faceId={FaceId} correlationId={CorrelationId}",
			actorUserId, action, faceId, correlationId);
	}

	public static void OperatorFaceRoleChanged(
		Microsoft.Extensions.Logging.ILogger logger,
		string operatorUserId,
		string targetUserId,
		int faceId,
		string? previousRoleName,
		string newRoleName,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit OperatorFaceRoleChanged operator={OperatorUserId} target={TargetUserId} faceId={FaceId} from={PreviousRole} to={NewRole} correlationId={CorrelationId}",
			operatorUserId, targetUserId, faceId, previousRoleName ?? "(none)", newRoleName, correlationId);
	}

	public static void OperatorGlobalBan(
		Microsoft.Extensions.Logging.ILogger logger,
		string operatorUserId,
		string targetUserId,
		int reasonLength,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit OperatorGlobalBan operator={OperatorUserId} target={TargetUserId} reasonLength={ReasonLength} correlationId={CorrelationId}",
			operatorUserId, targetUserId, reasonLength, correlationId);
	}

	public static void OperatorGlobalUnban(
		Microsoft.Extensions.Logging.ILogger logger,
		string operatorUserId,
		string targetUserId,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit OperatorGlobalUnban operator={OperatorUserId} target={TargetUserId} correlationId={CorrelationId}",
			operatorUserId, targetUserId, correlationId);
	}

	public static void OperatorFaceBan(
		Microsoft.Extensions.Logging.ILogger logger,
		string operatorUserId,
		string targetUserId,
		int faceId,
		int reasonLength,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit OperatorFaceBan operator={OperatorUserId} target={TargetUserId} faceId={FaceId} reasonLength={ReasonLength} correlationId={CorrelationId}",
			operatorUserId, targetUserId, faceId, reasonLength, correlationId);
	}

	public static void OperatorFaceUnban(
		Microsoft.Extensions.Logging.ILogger logger,
		string operatorUserId,
		string targetUserId,
		int faceId,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit OperatorFaceUnban operator={OperatorUserId} target={TargetUserId} faceId={FaceId} correlationId={CorrelationId}",
			operatorUserId, targetUserId, faceId, correlationId);
	}

	public static void OperatorPlatformMessage(
		Microsoft.Extensions.Logging.ILogger logger,
		string operatorUserId,
		string targetUserId,
		int contentLength,
		string correlationId)
	{
		logger.LogInformation(
			"SecurityAudit OperatorPlatformMessage operator={OperatorUserId} target={TargetUserId} contentLength={ContentLength} correlationId={CorrelationId}",
			operatorUserId, targetUserId, contentLength, correlationId);
	}
}
