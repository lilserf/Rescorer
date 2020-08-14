using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rescorer
{
	class Baserunner
	{
		public string playerId { get; set; }
		public string pitcherId { get; set; }

		public Baserunner(string runner, string pitcher)
		{
			playerId = runner;
			pitcherId = pitcher;
		}
	}

	class SingleTeamFourthStrikeAnalyzer
	{
		int m_inning = 0;
		int m_outs = 0;

		Baserunner[] m_bases;

		const int BASE_ADVANCE = 2;

		public SingleTeamFourthStrikeAnalyzer()
		{
			m_inning = 0;
			m_outs = 0;
			m_bases = new Baserunner[4];
		}

		public bool addOuts(int outs)
		{
			m_outs += outs;
			if (m_outs >= 3)
			{
				m_inning++;
				m_outs = 0;
				
				for(int i=0; i < 4; i ++)
				{
					m_bases[i] = null;
				}
				return true;
			}
			return false;
		}

		public bool isStrikeout(IEnumerable<char> pitchList)
		{
			// Temporary while the API doesn't have the pitch list
			if (pitchList == null)
				return true;

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

		static GameEventBaseRunner CreateGebr(string playerId, string pitcherId, int baseBefore, int baseAfter)
		{
			return new GameEventBaseRunner()
			{
				runnerId = playerId,
				responsiblePitcherId = pitcherId,
				baseBeforePlay = baseBefore,
				baseAfterPlay = baseAfter,
				wasBaseStolen = false,
				wasCaughtStealing = false,
				wasPickedOff = false,
			};
		}

		private void putBatterOnBase(GameEvent curr, int newBase)
		{
			if (newBase > 0 && newBase < 4)
			{
				m_bases[newBase] = new Baserunner(curr.batterId, curr.pitcherId);
			}
			var geb = CreateGebr(curr.batterId, curr.pitcherId, 0, newBase);
			var runners = curr.baseRunners.ToList();
			runners.Add(geb);
			curr.baseRunners = runners;
		}

		public void handleHomerun(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Clear the bases!
			for (int i=1; i<4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			curr.baseRunners = runners;

			// Also record the batter scoring
			putBatterOnBase(curr, 4);
		}

		public void handleTriple(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Clear the bases!
			for (int i = 1; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			curr.baseRunners = runners;

			// Batter on 3rd
			putBatterOnBase(curr, 3);
		}

		public void handleDouble(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Clear the bases!
			for (int i = 1; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			curr.baseRunners = runners;

			// Batter on 2nd
			putBatterOnBase(curr, 2);
		}

		public void handleSingle(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Score runners in scoring position
			for (int i = 2; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
				}
			}
			m_bases[2] = null;
			m_bases[3] = null;

			if (m_bases[1] != null)
			{
				// Find new base for guy on first and put him there
				int newBase = 1 + BASE_ADVANCE;
				var whosOnFirst = CreateGebr(m_bases[1].playerId, m_bases[1].pitcherId, 1, newBase);
				runners.Add(whosOnFirst);

				if (newBase > 0 && newBase < 4)
				{
					m_bases[newBase] = new Baserunner(whosOnFirst.runnerId, whosOnFirst.responsiblePitcherId);
				}
			}

			curr.baseRunners = runners;

			// Batter on 1st
			putBatterOnBase(curr, 1);
		}

		public void handleNoHit(GameEvent curr)
		{
			// Just list all the baserunners we're tracking
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			for(int i=1; i<4; i++)
			{
				if(m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i, i);
					runners.Add(geb);
				}
			}

			curr.baseRunners = runners;
		}

		public void handleWalk(GameEvent curr)
		{
			Baserunner[] newBases = new Baserunner[4];
			Baserunner scored = null;

			if(m_bases[1] != null)
			{
				newBases[2] = m_bases[1];
			}
			else
			{
				newBases[2] = m_bases[2];
			}

			if(m_bases[1] != null && m_bases[2] != null)
			{
				newBases[3] = m_bases[2];
			}
			else
			{
				newBases[3] = m_bases[3];
			}

			if(m_bases[1] != null && m_bases[2] != null && m_bases[3] != null)
			{
				scored = m_bases[3];
			}
			else
			{
				newBases[3] = m_bases[3];
			}

			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			for(int i=1; i < 4; i++)
			{
				if(newBases[i] != null)
				{
					runners.Add(CreateGebr(newBases[i].playerId, newBases[i].pitcherId, i - 1, i));
				}

				m_bases[i] = newBases[i];
			}
			curr.baseRunners = runners;

			putBatterOnBase(curr, 1);
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
					if (isStrikeout(curr.pitches))
					{
						curr.eventType = "STRIKEOUT";
						curr.totalStrikes = 3;
						curr.basesHit = 0;
						curr.isSacrificeHit = false;
						curr.isWalk = false;
						curr.outsOnPlay = 1;
					}
				}

				// TODO handle fielder's choice
				// TODO handle sacrifice
				if (curr.eventType == "WALK")
				{
					handleWalk(curr);
				}
				else
				{
					// Track baserunners
					// TODO: handle steals
					// TODO: record runs
					switch (curr.basesHit)
					{
						case 4:
							handleHomerun(curr);
							break;
						case 3:
							handleTriple(curr);
							break;
						case 2:
							handleDouble(curr);
							break;
						case 1:
							handleSingle(curr);
							break;
						case 0:
						default:
							handleNoHit(curr);
							break;
					}
				}

				// TODO check for double play validity?
				// Now increment the outs and/or innings
				addOuts(curr.outsOnPlay);
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
