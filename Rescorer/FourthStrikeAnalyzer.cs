using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rescorer
{

	class AnalyzerResult
	{
		public IEnumerable<GameEvent> newEvents { get; set; }

		public int numNewStrikeouts { get; set; }
	}

	class FourthStrikeAnalyzer
	{
		SingleTeamFourthStrikeAnalyzer m_home;
		SingleTeamFourthStrikeAnalyzer m_away;

		AnalyzerResult m_result;
		public FourthStrikeAnalyzer()
		{
			m_home = new SingleTeamFourthStrikeAnalyzer(true);
			m_away = new SingleTeamFourthStrikeAnalyzer(false);
			m_result = new AnalyzerResult();
		}

		public AnalyzerResult RescoreGame(IEnumerable<GameEvent> events)
		{
			var first = events.First();
			string homeId = first.pitcherTeamId;
			string awayId = first.batterTeamId;

			var homeEvents = events.Where(x => x.batterTeamId == homeId);
			var awayEvents = events.Where(x => x.batterTeamId == awayId);

			var newHomeResult = m_home.Rescore(homeEvents);
			var newHomeEvents = newHomeResult.newEvents;

			var newAwayResult = m_away.Rescore(awayEvents);
			var newAwayEvents = newAwayResult.newEvents;

			// Combine home and away and sort
			var combined = newHomeEvents.Concat(newAwayEvents).ToList();
			combined.Sort(SortGameEvents);

			// TODO: Each analyzer is going to adjust only the score for the team it's handling, so the all the HOME batting events have the wrong AWAY score and vice versa

			// Re-index after sorting
			int index = 0;
			foreach(var e in combined)
			{
				e.eventIndex = index;
				index++;
			}

			m_result.numNewStrikeouts = newHomeResult.numNewStrikeouts + newAwayResult.numNewStrikeouts;
			m_result.newEvents = combined;
			return m_result;
		}

		private int SortGameEvents(GameEvent x, GameEvent y)
		{
			if (x.inning != y.inning) return x.inning - y.inning;	// Earlier inning wins
			if (x.topOfInning != y.topOfInning) return x.topOfInning ? -1 : 1;  // Top of inning wins
			if (x.outsBeforePlay != y.outsBeforePlay) return x.outsBeforePlay - y.outsBeforePlay;
			return x.eventIndex - y.eventIndex; // Finally within the same half-inning the original event index wins
		}
	}
}
