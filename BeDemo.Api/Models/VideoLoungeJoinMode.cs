namespace BeDemo.Api.Models;

/// <summary>How a participant connects to a live VideoLounge session (SFU grants).</summary>
public enum VideoLoungeJoinMode
{
	/// <summary>Subscribe only — no publish.</summary>
	Viewer = 0,

	/// <summary>Publish audio + subscribe.</summary>
	Listener = 1,

	/// <summary>Publish audio + video + subscribe.</summary>
	Full = 2,

	/// <summary>Operator stealth watch — subscribe only, hidden from portal roster.</summary>
	AdminStealth = 3,
}
