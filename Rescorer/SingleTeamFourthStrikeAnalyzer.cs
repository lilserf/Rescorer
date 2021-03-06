﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rescorer
{
	class Baserunner
	{
		public string runnerId { get; set; }
		public string pitcherId { get; set; }

		public Baserunner(string runner, string pitcher)
		{
			runnerId = runner;
			pitcherId = pitcher;
		}
	}

	class SingleTeamFourthStrikeAnalyzer
	{
		AnalyzerResult m_result;
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
		static bool s_allowRealityChanges = false;
		
		Dictionary<string, Tuple<float, float>> m_singleAdvance;
		Dictionary<string, Tuple<int, float, float>> m_groundOutAdvance;
		Dictionary<string, Tuple<int, float, float>> m_flyOutAdvance;

		Random m_random;

		public SingleTeamFourthStrikeAnalyzer(
			bool isHome, 
			Dictionary<string, Tuple<float,float>> singleAdvance,
			Dictionary<string, Tuple<int, float, float>> groundOutAdvance,
			Dictionary<string, Tuple<int, float, float>> flyOutAdvance)
		{
			// TODO: pass in seed
			m_random = new Random(0);
			m_singleAdvance = singleAdvance;
			m_groundOutAdvance = groundOutAdvance;
			m_flyOutAdvance = flyOutAdvance;
			m_inning = 0;
			m_outs = 0;
			m_bases = new Baserunner[4];
			m_score = 0;
			m_isHome = isHome;
			m_result = new AnalyzerResult();
		}

		static int GROUNDOUT_ATTEMPTS = 10;
		static int FLYOUT_ATTEMPTS = 12;
		private int GetGroundOutAdvance(string runnerId) => GetTagupAdvance(m_groundOutAdvance, runnerId, GROUNDOUT_ATTEMPTS);
		private int GetFlyOutAdvance(string runnerId) => GetTagupAdvance(m_flyOutAdvance, runnerId, FLYOUT_ATTEMPTS);
		private int GetTagupAdvance(Dictionary<string, Tuple<int, float, float>> advance, string runnerId, int min)
		{
			Tuple<int, float, float> odds;
			if(advance.TryGetValue(runnerId, out odds))
			{
				// Do nothing
			}
			else
			{
				odds = advance[""];
			}

			if(odds.Item1 < min)
			{
				odds = advance[""];
			}

			if(m_random.Next(0,100) < odds.Item2 * 100)
			{
				return 0;
			}
			else
			{
				return 1;
			}
		}

		private int GetSingleAdvanceFromFirst(string runnerId)
		{
			Tuple<float, float> odds;
			if (m_singleAdvance.TryGetValue(runnerId, out odds))
			{
				// Do nothing
			}
			else
			{
				Console.WriteLine($"Couldn't find advance from first statistics for player ID {runnerId}!");
				// Average is stored as empty string
				odds = m_singleAdvance[""];
			}

			if(m_random.Next(0,100) < odds.Item1 * 100)
			{
				return 1;
			}
			else
			{
				return 2;
			}
		}



		public bool AddOuts(int outs)
		{
			m_outs += outs;
			if (m_outs >= 3)
			{
				m_inning++;
				m_outs = 0;

				for (int i = 0; i < 4; i++)
				{
					m_bases[i] = null;
				}
				return true;
			}
			return false;
		}

		public static bool IsStrikeout(IEnumerable<char> pitchList)
		{
			// Temporary while the API doesn't have the pitch list
			if (pitchList == null)
				return true;

			int strikes = 0;
			foreach (var pitch in pitchList)
			{
				switch (pitch)
				{
					case 'C':
					case 'S':
						strikes++;
						break;
					case 'F':
						if (strikes < 2)
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

		private void CopyRunnersToEvent(GameEvent curr, IEnumerable<GameEventBaseRunner> runners)
		{
			if (s_allowRealityChanges || m_alternateReality)
			{
				curr.baseRunners = runners;
			}
		}

		private void IncrementScore()
		{
			if(s_allowRealityChanges || m_alternateReality)
			{
				m_score++;
			}
		}

		private void PutBatterOnBase(GameEvent curr, int newBase)
		{
			if (newBase > 0 && newBase < 4)
			{
				m_bases[newBase] = new Baserunner(curr.batterId, curr.pitcherId);
			}
			var geb = CreateGebr(curr.batterId, curr.pitcherId, 0, newBase);
			var runners = curr.baseRunners.ToList();
			runners.Add(geb);
			CopyRunnersToEvent(curr, runners);
		}

		public void HandleHomerun(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Clear the bases!
			for (int i = 1; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].runnerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
					IncrementScore();
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			CopyRunnersToEvent(curr, runners);

			// Also record the batter scoring
			PutBatterOnBase(curr, 4);
			IncrementScore();
		}

		public void HandleTriple(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Clear the bases!
			for (int i = 1; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].runnerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
					IncrementScore();
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			CopyRunnersToEvent(curr, runners);

			// Batter on 3rd
			PutBatterOnBase(curr, 3);
		}

		public void HandleDouble(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Clear the bases!
			for (int i = 1; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].runnerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
					IncrementScore();
				}
			}
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;
			CopyRunnersToEvent(curr, runners);

			// Batter on 2nd
			PutBatterOnBase(curr, 2);
		}

		public void HandleSingle(GameEvent curr)
		{
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			// Score runners in scoring position
			for (int i = 2; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].runnerId, m_bases[i].pitcherId, i, 4);
					runners.Add(geb);
					IncrementScore();
				}
			}
			m_bases[2] = null;
			m_bases[3] = null;

			if (m_bases[1] != null)
			{
				// Find new base for guy on first and put him there
				int newBase = 1 + GetSingleAdvanceFromFirst(m_bases[1].runnerId);
				var whosOnFirst = CreateGebr(m_bases[1].runnerId, m_bases[1].pitcherId, 1, newBase);
				runners.Add(whosOnFirst);

				if (newBase == 4)
				{
					IncrementScore();
				}

				if (newBase > 0 && newBase < 4)
				{
					m_bases[newBase] = new Baserunner(whosOnFirst.runnerId, whosOnFirst.responsiblePitcherId);
				}
				m_bases[1] = null;
			}

			CopyRunnersToEvent(curr, runners);

			// Batter on 1st
			PutBatterOnBase(curr, 1);
		}

		public void PersistBaserunners(GameEvent curr)
		{
			// Just list all the baserunners we're tracking
			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			for (int i = 1; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var geb = CreateGebr(m_bases[i].runnerId, m_bases[i].pitcherId, i, i);
					runners.Add(geb);
				}
			}

			CopyRunnersToEvent(curr, runners);
		}

		public void HandleWalk(GameEvent curr)
		{
			Baserunner[] newBases = new Baserunner[4];
			Baserunner scored = null;
			int[] prevBases = new int[4];

			if (m_bases[1] != null)
			{
				newBases[2] = m_bases[1];
				prevBases[2] = 1;
			}
			else
			{
				newBases[2] = m_bases[2];
				prevBases[2] = 2;
			}

			if (m_bases[1] != null && m_bases[2] != null)
			{
				newBases[3] = m_bases[2];
				prevBases[3] = 2;
			}
			else
			{
				newBases[3] = m_bases[3];
				prevBases[3] = 3;
			}

			if (m_bases[1] != null && m_bases[2] != null && m_bases[3] != null)
			{
				scored = m_bases[3];
			}
			else
			{
				newBases[3] = m_bases[3];
				prevBases[3] = 3;
			}

			List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
			for (int i = 1; i < 4; i++)
			{
				if (newBases[i] != null)
				{
					runners.Add(CreateGebr(newBases[i].runnerId, newBases[i].pitcherId, prevBases[i], i));
				}

				m_bases[i] = newBases[i];
			}
			if (scored != null)
			{
				runners.Add(CreateGebr(scored.runnerId, scored.pitcherId, 3, 4));
				IncrementScore();
			}

			CopyRunnersToEvent(curr, runners);

			PutBatterOnBase(curr, 1);
		}

		public void HandleFieldersChoice(GameEvent curr)
		{
			// Figure out who's out
			Baserunner whosOut = null;
			if (m_bases[1] != null && m_bases[2] != null && m_bases[3] != null)
			{
				whosOut = m_bases[3];
				m_bases[3] = null;
			}
			else if (m_bases[1] != null && m_bases[2] != null)
			{
				whosOut = m_bases[2];
				m_bases[2] = null;
			}
			else if (m_bases[1] != null)
			{
				whosOut = m_bases[1];
				m_bases[1] = null;
			}
			else
			{
				curr.eventType = GameEventType.OUT;
			}

			Baserunner scored = null;
			// Move the non-out folks and put the batter on first
			if (m_bases[3] != null && curr.outsBeforePlay < 2)
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
			for (int i = 0; i < 4; i++)
			{
				if (m_bases[i] != null)
				{
					var gebr = CreateGebr(m_bases[i].runnerId, m_bases[i].pitcherId, i - 1, i);
					runners.Add(gebr);
				}
			}
			if (scored != null)
			{
				// Only scoring from 3rd on fielder's choice
				runners.Add(CreateGebr(scored.runnerId, scored.pitcherId, 3, 4));
				IncrementScore();
			}
			CopyRunnersToEvent(curr, runners);

			// Make sure there's an out on this play
			curr.outsOnPlay = 1;
		}

		// Handle hits and track baserunners
		private void HandleHit(GameEvent curr)
		{
			// TODO: handle steals
			switch (curr.basesHit)
			{
				case 4:
					HandleHomerun(curr);
					break;
				case 3:
					HandleTriple(curr);
					break;
				case 2:
					HandleDouble(curr);
					break;
				case 1:
					HandleSingle(curr);
					break;
			}
		}

		private void HandleOuts(GameEvent curr)
		{
			// TODO: use battedBallType once it's available
			// If the batter grounds out, runners advance
			if (curr.eventText.Any(x => x.Contains("ground out")))
			{
				if (curr.outsBeforePlay < 2)
				{
					List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();

					// Does guy on 3rd score?
					if (m_bases[3] != null)
					{
						int advance = GetGroundOutAdvance(m_bases[3].runnerId);
						runners.Add(CreateGebr(m_bases[3].runnerId, m_bases[3].pitcherId, 3, 3 + advance));
						if (advance == 1)
						{
							m_bases[3] = null;
							IncrementScore();
						}
					}

					// Does guy on 2nd move to 3rd (if he can)?
					if(m_bases[2] != null)
					{
						int advance = GetGroundOutAdvance(m_bases[2].runnerId);
						if(advance == 1 && m_bases[3] == null)
						{
							runners.Add(CreateGebr(m_bases[2].runnerId, m_bases[2].pitcherId, 2, 3));
							m_bases[3] = m_bases[2];
							m_bases[2] = null;
						}
						else
						{ 
							runners.Add(CreateGebr(m_bases[2].runnerId, m_bases[2].pitcherId, 2, 2));
						}
					}

					// Does guy on 1st move to 2nd (if he can)?
					if (m_bases[1] != null)
					{
						int advance = GetGroundOutAdvance(m_bases[1].runnerId);
						if (advance == 1 && m_bases[2] == null)
						{
							runners.Add(CreateGebr(m_bases[1].runnerId, m_bases[1].pitcherId, 1, 2));
							m_bases[2] = m_bases[1];
							m_bases[1] = null;
						}
						else
						{
							runners.Add(CreateGebr(m_bases[1].runnerId, m_bases[1].pitcherId, 1, 1));
						}
					}

					CopyRunnersToEvent(curr, runners);
				}

			}
			else if (curr.isSacrificeHit || curr.isSacrificeFly || curr.eventText.Any(x => x.Contains("sacrifice") || x.Contains("flyout")))
			{

				// Possible TODO: Does the runner on 3rd always score on a SACRIFICE as compared to a normal fly out
				if (curr.outsBeforePlay < 2)
				{
					List<GameEventBaseRunner> runners = new List<GameEventBaseRunner>();
					// Does guy on 3rd score?
					if (m_bases[3] != null)
					{
						int advance = GetFlyOutAdvance(m_bases[3].runnerId);
						runners.Add(CreateGebr(m_bases[3].runnerId, m_bases[3].pitcherId, 3, 3 + advance));
						if (advance == 1)
						{
							m_bases[3] = null;
							IncrementScore();
						}
					}

					// Does guy on 2nd move to 3rd (if he can)?
					if(m_bases[2] != null)
					{
						int advance = GetFlyOutAdvance(m_bases[2].runnerId);
						if(advance == 1 && m_bases[3] == null)
						{
							runners.Add(CreateGebr(m_bases[2].runnerId, m_bases[2].pitcherId, 2, 3));
							m_bases[3] = m_bases[2];
							m_bases[2] = null;
						}
						else
						{ 
							runners.Add(CreateGebr(m_bases[2].runnerId, m_bases[2].pitcherId, 2, 2));
						}
					}

					// Does guy on 1st move to 2nd (if he can)?
					if (m_bases[1] != null)
					{
						int advance = GetFlyOutAdvance(m_bases[1].runnerId);
						if (advance == 1 && m_bases[2] == null)
						{
							runners.Add(CreateGebr(m_bases[1].runnerId, m_bases[1].pitcherId, 1, 2));
							m_bases[2] = m_bases[1];
							m_bases[1] = null;
						}
						else
						{
							runners.Add(CreateGebr(m_bases[1].runnerId, m_bases[1].pitcherId, 1, 1));
						}
					}

					CopyRunnersToEvent(curr, runners);
				}
			}
			else
			{
				// Persist our baserunners for any other kind of out
				PersistBaserunners(curr);
			}

		}

		private void StoreEventBaserunners(GameEvent curr)
		{
			m_bases[1] = null;
			m_bases[2] = null;
			m_bases[3] = null;

			if (curr.baseRunners != null)
			{
				foreach (var br in curr.baseRunners)
				{
					if (br.baseAfterPlay < 4)
					{
						m_bases[br.baseAfterPlay] = new Baserunner(br.runnerId, br.responsiblePitcherId);
					}
				}
			}
		}

		public AnalyzerResult Rescore(IEnumerable<GameEvent> events)
		{
			foreach (GameEvent curr in events)
			{

				if (s_allowRealityChanges || m_alternateReality)
				{
					// First replace this event's inning and outs with our own tracking
					curr.inning = m_inning;
					curr.outsBeforePlay = m_outs;
					// And score
					if (m_isHome)
						curr.homeScore = m_score;
					else
						curr.awayScore = m_score;
				}
				else
				{
					// We haven't diverged from reality yet; take the event's tracking as correct
					m_inning = curr.inning;
					m_outs = curr.outsBeforePlay;
					if(m_isHome)
					{
						m_score = (int)curr.homeScore;
					}
					else
					{
						m_score = (int)curr.awayScore;
					}
				}

				// This batter should have struck out!
				if (curr.totalStrikes >= 3 && curr.eventType != GameEventType.STRIKEOUT)
				{
					var oldEventType = curr.eventType;
					if (IsStrikeout(curr.RealPitches))
					{
						curr.eventType = GameEventType.STRIKEOUT;
						curr.totalStrikes = 3;
						curr.basesHit = 0;
						curr.isSacrificeHit = false;
						curr.isWalk = false;
						curr.outsOnPlay = 1;
						curr.rescoreNewStrikeout = true;
						m_result.numNewStrikeouts++;
						m_result.eventTypesChangedToStrikeouts.Add(oldEventType);
						m_result.statistics.AddStat(curr.pitcherId, "STRIKEOUT-pitched", 1);
						m_result.statistics.AddStat(curr.batterId, "STRIKEOUT-batted", 1);
						// Subtract whatever was before
						m_result.statistics.AddStat(curr.pitcherId, oldEventType+"-pitched", -1);
						m_result.statistics.AddStat(curr.batterId, oldEventType+"-batted", -1);

						// If this event was already an OUT, we're still "in sync" with reality
						// So we can track the new strikeout and add a K to that pitcher or whatever
						// But we shouldn't go into "alternate reality" mode yet
						if (oldEventType != GameEventType.OUT)
						{
							m_alternateReality = true;
						}

					}
				}

				if (s_allowRealityChanges || m_alternateReality)
				{
					switch (curr.eventType)
					{
						case GameEventType.FIELDERS_CHOICE:
							HandleFieldersChoice(curr);
							break;
						case GameEventType.WALK:
							HandleWalk(curr);
							break;
						case GameEventType.OUT:
							HandleOuts(curr);
							break;
						case GameEventType.HOME_RUN:
						case GameEventType.TRIPLE:
						case GameEventType.DOUBLE:
						case GameEventType.SINGLE:
							HandleHit(curr);
							break;
						default:
							break;
					}

					// TODO check for double play validity?
					// Now increment the outs and/or innings
					var inningEnded = AddOuts(curr.outsOnPlay);

					// BOOO this is actually matching lousy behavior in Cauldron, somebody should really fix that:/
					//if (inningEnded)
					//{
					//	copyRunnersToEvent(curr, new List<GameEventBaseRunner>());
					//}
				}
				else
				{
					// Store the event's baserunners (post-play state) locally since we're still in reality
					StoreEventBaserunners(curr);
				}
			}

			m_result.newEvents = events;
			return m_result;
		}

	}
}
