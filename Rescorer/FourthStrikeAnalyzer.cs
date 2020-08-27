using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rescorer
{

	class AnalyzerResult
	{
		public Statistics statistics { get; private set; }
		public IEnumerable<GameEvent> newEvents { get; set; }

		public int numNewStrikeouts { get; set; }
		public List<string> eventTypesChangedToStrikeouts { get; private set; }

		public AnalyzerResult()
		{
			eventTypesChangedToStrikeouts = new List<string>();
			statistics = new Statistics();
		}

		public void MergeStats(AnalyzerResult other)
		{
			numNewStrikeouts += other.numNewStrikeouts;
			eventTypesChangedToStrikeouts.Concat(other.eventTypesChangedToStrikeouts);
			numDroppedAppearances += other.numDroppedAppearances;
			statistics.Merge(other.statistics);
		}

		// Only used by outer FourthStrikeAnalyzer
		public int numDroppedAppearances { get; set; }

		// Only used by outer FourthStrikeAnalyzer
		public bool outcomeUnknown { get; set; }


	}

	class FourthStrikeAnalyzer
	{
		SingleTeamFourthStrikeAnalyzer m_home;
		SingleTeamFourthStrikeAnalyzer m_away;


		AnalyzerResult m_result;
		public FourthStrikeAnalyzer(Dictionary<string, Tuple<float,float>> singleAdvance,
			Dictionary<string, Tuple<int, float, float>> groundOutAdvance,
			Dictionary<string, Tuple<int, float, float>> flyOutAdvance)
		{
			m_home = new SingleTeamFourthStrikeAnalyzer(true, singleAdvance, groundOutAdvance, flyOutAdvance);
			m_away = new SingleTeamFourthStrikeAnalyzer(false, singleAdvance, groundOutAdvance, flyOutAdvance);
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

			// Each analyzer is going to adjust only the score for the team it's handling, so the all the HOME batting events have the wrong AWAY score and vice versa
			float homeScore = 0;
			float awayScore = 0;
			foreach(var e in combined)
			{
				if(e.topOfInning)
				{
					// Batting team has its own score correct
					awayScore = e.awayScore;
					// But should take the opposing score we last saw
					e.homeScore = homeScore;
				}
				else
				{
					// Batting team has its own score correct
					homeScore = e.homeScore;
					// But should take the opposing score we last saw
					e.awayScore = awayScore;
				}
			}

			// Re-index after sorting
			int index = 0;
			foreach(var e in combined)
			{
				e.eventIndex = index;
				index++;
			}

			// Find end of game
			int inning = 8; // 9th inning
			bool top = true;
			bool gameOver = false;
			while(!gameOver)
			{
				var lastEvent = combined.Where(x => x.inning == inning && x.topOfInning == top).LastOrDefault();

				if(lastEvent == null)
				{
					break;
				}

				if(top && lastEvent.homeScore > lastEvent.awayScore)
				{
					// Home team wins after the top of the 9th (or greater) if leading
					gameOver = true;
					break;
				}
				else if(!top && lastEvent.homeScore != lastEvent.awayScore)
				{
					// Some team wins at bottom of 9th if not tied
					gameOver = true;
					break;
				}

				// Increment to the next half-inning
				if(top)
				{
					top = false;
				}
				else
				{
					inning++;
					top = true;
				}
			}

			m_result.MergeStats(newHomeResult);
			m_result.MergeStats(newAwayResult);

			if (gameOver)
			{
				var dropped = combined.Where(x => x.inning > inning || (top && x.inning == inning && !x.topOfInning));
				m_result.numDroppedAppearances = dropped.Count();
				combined = combined.Except(dropped).ToList();
			}
			else
			{
				m_result.outcomeUnknown = true;
			}

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
