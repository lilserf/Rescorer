using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rescorer
{
	class SingleTeamFourthStrikeAnalyzer
	{
		int m_inning = 0;
		int m_outs = 0;

		public SingleTeamFourthStrikeAnalyzer()
		{
			m_inning = 0;
			m_outs = 0;
		}

		public bool AddOuts(int outs)
		{
			m_outs += outs;
			if (m_outs >= 3)
			{
				m_inning++;
				m_outs = 0;
				return true;
			}
			return false;
		}

		public bool isStrikeout(IEnumerable<char> pitchList)
		{
			int strikes = 0;
			foreach(var pitch in pitchList)
			{
				switch(pitch)
				{
					case 'C':
					case 'S':
						strikes++;
						break;
					case 'F':
						if(strikes < 2)
						{
							strikes++;
						}
						break;
				}
			}

			return strikes >= 3;
		}

		public IEnumerable<GameEvent> Rescore(IEnumerable<GameEvent> events)
		{
			foreach(GameEvent curr in events)
			{
				// First set the correct current inning and outs
				curr.inning = m_inning;
				curr.outsBeforePlay = m_outs;

				// This batter should have struck out!
				if(curr.totalStrikes >= 3 && curr.eventType != "STRIKEOUT")
				{
					if (isStrikeout(curr.pitchesList))
					{
						curr.eventType = "STRIKEOUT";
						curr.totalStrikes = 3;
						curr.basesHit = 0;
						curr.isSacrificeHit = false;
						curr.isWalk = false;
						curr.outsOnPlay = 1;
					}
				}

				// TODO check for double play validity?
				// Now increment the outs and/or innings
				AddOuts(curr.outsOnPlay);

				// TODO baserunners
			}

			return events;
		}

	}

	class FourthStrikeAnalyzer
	{
		SingleTeamFourthStrikeAnalyzer m_home;
		SingleTeamFourthStrikeAnalyzer m_away;

		public FourthStrikeAnalyzer()
		{
			m_home = new SingleTeamFourthStrikeAnalyzer();
			m_away = new SingleTeamFourthStrikeAnalyzer();
		}


		public IEnumerable<GameEvent> RescoreGame(IEnumerable<GameEvent> events)
		{
			var first = events.First();
			string homeId = first.pitcherTeamId;
			string awayId = first.batterTeamId;

			var homeEvents = events.Where(x => x.batterTeamId == homeId);
			var awayEvents = events.Where(x => x.batterTeamId == awayId);

			var newHomeEvents = m_home.Rescore(homeEvents);

			var newAwayEvents = m_away.Rescore(awayEvents);

			// Combine home and away and sort
			var combined = newHomeEvents.Concat(newAwayEvents).ToList();
			combined.Sort(SortGameEvents);

			// Re-index after sorting
			int index = 0;
			foreach(var e in combined)
			{
				e.eventIndex = index;
				index++;
			}

			return combined;
		}

		private int SortGameEvents(GameEvent x, GameEvent y)
		{
			if (x.inning != y.inning) return x.inning - y.inning;	// Earlier inning wins
			if (x.topOfInning != y.topOfInning) return x.topOfInning ? -1 : 1;  // Top of inning wins
			return x.eventIndex - y.eventIndex; // Finally within the same half-inning the original event index wins
		}
	}
}
