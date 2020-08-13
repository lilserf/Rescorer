﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Rescorer
{
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

		public override string ToString()
		{
			string topbot = topOfInning ? "Top" : "Bot";
			return $"[{eventIndex}] {topbot}{inning}, {outsBeforePlay} out: {eventType}";
		}

	}
}
