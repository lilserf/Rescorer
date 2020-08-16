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
		int m_score = 0;
		bool m_isHome = false;
		// Have we entered an alternate reality?
		bool m_alternateReality = false;
		// Quick bookkeeping of base runners
		Baserunner[] m_bases;

		// Should we change runners in the "reality" phase before we've diverged?
		// Useful for debugging, but should be turned off for real analysis so that games can only diverge after a fourth strike change
		static bool s_changeRunnersInReality = false;
		const int BASE_ADVANCE = 2;

		public SingleTeamFourthStrikeAnalyzer(bool isHome)
		{
			m_inning = 0;
			m_outs = 0;
			m_bases = new Baserunner[4];
			m_score = 0;
			m_isHome = isHome;
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

		private void storeRunners(GameEvent curr, IEnumerable<GameEventBaseRunner> runners)
		{
			if(s_changeRunnersInReality || m_alternateReality)
			{
				curr.baseRunners = runners;
			}
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
			storeRunners(curr, runners);
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
					m_score++;
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			storeRunners(curr, runners);

			// Also record the batter scoring
			putBatterOnBase(curr, 4);
			m_score++;
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
					m_score++;
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			storeRunners(curr, runners);

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
					m_score++;
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			storeRunners(curr, runners);

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
					m_score++;
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

				if(newBase == 4)
				{
					m_score++;
				}

				if (newBase > 0 && newBase < 4)
				{
					m_bases[newBase] = new Baserunner(whosOnFirst.runnerId, whosOnFirst.responsiblePitcherId);
				}
			}

			storeRunners(curr, runners);

			// Batter on 1st
			putBatterOnBase(curr, 1);
		}

		public void persistBaserunners(GameEvent curr)
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

			storeRunners(curr, runners);
		}

		public void handleWalk(GameEvent curr)
		{
			Baserunner[] newBases = new Baserunner[4];
			Baserunner scored = null;
			int[] prevBases = new int[4];

			if(m_bases[1] != null)
			{
				newBases[2] = m_bases[1];
				prevBases[2] = 1;
			}
			else
			{
				newBases[2] = m_bases[2];
				prevBases[2] = 2;
			}

			if(m_bases[1] != null && m_bases[2] != null)
			{
				newBases[3] = m_bases[2];
				prevBases[3] = 2;
			}
			else
			{
				newBases[3] = m_bases[3];
				prevBases[3] = 3;
			}

			if(m_bases[1] != null && m_bases[2] != null && m_bases[3] != null)
			{
				scored = m_bases[3];
			}
			else
			{
				newBases[3] = m_bases[3];
				prevBases[3] = 3;
			}

			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			for(int i=1; i < 4; i++)
			{
				if(newBases[i] != null)
				{
					runners.Add(CreateGebr(newBases[i].playerId, newBases[i].pitcherId, prevBases[i], i));
				}

				m_bases[i] = newBases[i];
			}
			if(scored != null)
			{
				runners.Add(CreateGebr(scored.playerId, scored.pitcherId, 3, 4));
				m_score++;
			}

			storeRunners(curr, runners);

			putBatterOnBase(curr, 1);
		}

		public void handleFieldersChoice(GameEvent curr)
		{
			// Figure out who's out
			Baserunner whosOut = null;
			if(m_bases[1] != null && m_bases[2] != null && m_bases[3] != null)
			{
				whosOut = m_bases[3];
				m_bases[3] = null;
			}
			else if(m_bases[1] != null && m_bases[2] != null)
			{
				whosOut = m_bases[2];
				m_bases[2] = null;
			}
			else if(m_bases[1] != null)
			{
				whosOut = m_bases[1];
				m_bases[1] = null;
			}
			else
			{
				curr.eventType = "OUT";
			}

			Baserunner scored = null;
			// Move the non-out folks and put the batter on first
			if(m_bases[3] != null && curr.outsBeforePlay < 2)
			{
				// If there's a runner on 3rd they'll score
				scored = m_bases[3];
			}
			m_bases[3] = m_bases[2];
			m_bases[2] = m_bases[1];
			if (whosOut != null)
			{
				// Except if whosOut is null, it's the batter who's out
				m_bases[1] = new Baserunner(curr.batterId, curr.pitcherId);
			}
			

			// Build GEBRs
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			for(int i = 0; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var gebr = CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i-1, i);
					runners.Add(gebr);
				}
			}
			if(scored != null)
			{
				// Only scoring from 3rd on fielder's choice
				runners.Add(CreateGebr(scored.playerId, scored.pitcherId, 3, 4));
				m_score++;
			}
			storeRunners(curr, runners);

			// Make sure there's an out on this play
			curr.outsOnPlay = 1;
		}

		// Handle hits and track baserunners
		private void handleHit(GameEvent curr)
		{
			// TODO: handle steals
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
			}
		}

		private void handleOuts(GameEvent curr)
		{
			// TODO: use battedBallType once it's available
			// TODO: correct final state of runners?
			// If the batter grounds out, runners advance
			if (curr.eventText.Any(x => x.Contains("ground out")))
			{
				List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();

				Baserunner scored = m_bases[3];
				m_bases[3] = m_bases[2];
				m_bases[2] = m_bases[1];
				m_bases[1] = null;

				// Guy on 3rd scores if less than 2 outs
				if(scored != null && curr.outsBeforePlay < 2)
				{
					runners.Add(CreateGebr(scored.playerId, scored.pitcherId, 3, 4));
					m_score++;
				}

				for(int i=1; i < 4; i++)
				{
					if(m_bases[i] != null)
					{
						runners.Add(CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i - 1, i));
					}
				}
				storeRunners(curr, runners);
			}
			else if(curr.isSacrificeHit || curr.isSacrificeFly || curr.eventText.Any(x => x.Contains("sacrifice") || x.Contains("flyout")))
			{
				if (curr.outsBeforePlay < 2)
				{
					List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
					// On a sac or flyout with less than 2 outs, runners on 2nd and 3rd advance
					Baserunner scored = m_bases[3];
					m_bases[3] = m_bases[2];
					m_bases[2] = null;

					if (scored != null)
					{
						runners.Add(CreateGebr(scored.playerId, scored.pitcherId, 3, 4));
						m_score++;
					}

					for(int i=1; i<4; i++)
					{
						if(m_bases[i] != null)
						{
							runners.Add(CreateGebr(m_bases[i].playerId, m_bases[i].pitcherId, i - 1, i));
						}
					}

					storeRunners(curr, runners);
				}
			}
			else
			{
				// Persist our baserunners for any other kind of out
				persistBaserunners(curr);
			}

		}

		public IEnumerable<GameEvent> Rescore(IEnumerable<GameEvent> events)
		{
			foreach(GameEvent curr in events)
			{
				// First set the correct current inning and outs
				curr.inning = m_inning;
				curr.outsBeforePlay = m_outs;
				// And score
				if (m_isHome)
					curr.homeScore = m_score;
				else
					curr.awayScore = m_score;


				// This batter should have struck out!
				if (curr.totalStrikes >= 3 && curr.eventType != GameEventType.STRIKEOUT)
				{
					if (isStrikeout(curr.pitches))
					{
						curr.eventType = GameEventType.STRIKEOUT;
						curr.totalStrikes = 3;
						curr.basesHit = 0;
						curr.isSacrificeHit = false;
						curr.isWalk = false;
						curr.outsOnPlay = 1;
						m_alternateReality = true;
					}
				}

				switch (curr.eventType)
				{
					case GameEventType.FIELDERS_CHOICE:
						handleFieldersChoice(curr);
						break;
					case GameEventType.WALK:
						handleWalk(curr);
						break;
					case GameEventType.OUT:
						handleOuts(curr);
						break;
					case GameEventType.HOME_RUN:
					case GameEventType.TRIPLE:
					case GameEventType.DOUBLE:
					case GameEventType.SINGLE:
						handleHit(curr);
						break;
					default:
						break;
				}

				// TODO check for double play validity?
				// Now increment the outs and/or innings
				var inningEnded = addOuts(curr.outsOnPlay);

				// BOOO this is actually matching lousy behavior in Cauldron, somebody should really fix that:/
				if(inningEnded)
				{
					storeRunners(curr, new List<GameEventBaseRunner>());
				}
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
			m_home = new SingleTeamFourthStrikeAnalyzer(true);
			m_away = new SingleTeamFourthStrikeAnalyzer(false);
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

			// TODO: Each analyzer is going to adjust only the score for the team it's handling, so the all the HOME batting events have the wrong AWAY score and vice versa

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
