namespace BeDemo.Api.Services.OperatorAi.Skills;

/// <inheritdoc />
public sealed class OperatorAiSkillRegistry : IOperatorAiSkillRegistry
{
	/// <summary>Id of the guaranteed fallback skill (D5). Must be registered.</summary>
	public const string GeneralAssistantId = "general-assistant";

	private readonly IReadOnlyList<IOperatorAiSkill> _all;
	private readonly IReadOnlyDictionary<string, IOperatorAiSkill> _byId;

	public OperatorAiSkillRegistry(IEnumerable<IOperatorAiSkill> skills)
	{
		_all = skills.ToList();
		_byId = _all.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

		if (!_byId.ContainsKey(GeneralAssistantId))
			throw new InvalidOperationException(
				$"Operator AI skill registry requires a '{GeneralAssistantId}' fallback skill (D5).");
	}

	/// <inheritdoc />
	public IReadOnlyList<IOperatorAiSkill> All => _all;

	/// <inheritdoc />
	public IOperatorAiSkill? GetById(string id) =>
		!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var s) ? s : null;

	/// <inheritdoc />
	public IOperatorAiSkill GeneralAssistant => _byId[GeneralAssistantId];
}
