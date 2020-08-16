using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Rescorer
{
	public static class GameEventType
	{
		public const string UNKNOWN = "UNKNOWN";
		public const string NONE = "NONE";
		public const string OUT = "OUT";
		public const string STRIKEOUT = "STRIKEOUT";
		public const string STOLEN_BASE = "STOLEN_BASE";
		public const string CAUGHT_STEALING = "CAUGHT_STEALING";
		public const string PICKOFF = "PICKOFF";
		public const string WILD_PITCH = "WILD_PITCH";
		public const string BALK = "BALK";
		public const string OTHER_ADVANCE = "OTHER_ADVANCE";
		public const string WALK = "WALK";
		public const string INTENTIONAL_WALK = "INTENTIONAL_WALK";
		public const string HIT_BY_PITCH = "HIT_BY_PITCH";
		public const string FIELDERS_CHOICE = "FIELDERS_CHOICE";
		public const string SINGLE = "SINGLE";
		public const string DOUBLE = "DOUBLE";
		public const string TRIPLE = "TRIPLE";
		public const string HOME_RUN = "HOME_RUN";
	}

	/// <summary>
	/// Player events
	/// </summary>
	public class PlayerEvent
	{
		public string playerId { get; set; }
		public string eventType { get; set; }
	}

	/// <summary>
	/// Serializable class following DB schema from SIBR for baserunning info
	/// </summary>
	public class GameEventBaseRunner
	{
		public string runnerId { get; set; }
		public string responsiblePitcherId { get; set; }
		public int baseBeforePlay { get; set; }
		public int baseAfterPlay { get; set; }
		public bool wasBaseStolen { get; set; }
		public bool wasCaughtStealing { get; set; }
		public bool wasPickedOff { get; set; }
	}

	/// <summary>
	/// Serializable class following the DB schema from SIBR for game events
	/// </summary>
	public class GameEvent
	{
		public string gameId { get; set; }
		public string eventType { get; set; }
		public int eventIndex { get; set; }
		public int inning { get; set; }
		public int outsBeforePlay { get; set; }
		public string batterId { get; set; }
		public string batterTeamId { get; set; }
		public string pitcherId { get; set; }
		public string pitcherTeamId { get; set; }
		public float homeScore { get; set; }
		public float awayScore { get; set; }
		public int homeStrikeCount { get; set; }
		public int awayStrikeCount { get; set; }
		public int batterCount { get; set; }
		public IEnumerable<char> pitchesList { get; set; }
		public IEnumerable<char> pitches { get; set; }

		[JsonIgnore]
		public IEnumerable<char> RealPitches
		{ 
			get
			{
				return pitchesList ?? pitches;
			}
		}
		public int totalStrikes { get; set; }
		public int totalBalls { get; set; }
		public int totalFouls { get; set; }
		public bool isLeadoff { get; set; }
		public bool isPinchHit { get; set; }
		public int lineupPosition { get; set; }
		public bool isLastEventForPlateAppearance { get; set; }
		public int basesHit { get; set; }
		public int runsBattedIn { get; set; }
		public bool isSacrificeHit { get; set; }
		public bool isSacrificeFly { get; set; }
		public int outsOnPlay { get; set; }
		public bool isDoublePlay { get; set; }
		public bool isTriplePlay { get; set; }
		public bool isWildPitch { get; set; }
		public string battedBallType { get; set; }
		public bool isBunt { get; set; }
		public int errorsOnPlay { get; set; }
		public int batterBaseAfterPlay { get; set; }
		public IEnumerable<GameEventBaseRunner> baseRunners { get; set; }
		public bool isLastGameEvent { get; set; }
		public string additionalContext { get; set; }
		public bool topOfInning { get; set; }
		public IEnumerable<string> eventText { get; set; }
		public bool isSteal { get; set; }
		public bool isWalk { get; set; }
		public IEnumerable<PlayerEvent> playerEvents { get; set; }

		public int id { get; set; }
		public override string ToString()
		{
			string topbot = topOfInning ? "Top" : "Bot";
			return $"[{eventIndex}] {topbot}{inning}, {outsBeforePlay} out: {eventType}";
		}

	}
}
