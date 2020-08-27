using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rescorer
{
	public class Record
	{
		public int Wins { get; set; }
		public int Losses { get; set; }
		public int Ties { get; set; }

		public Record(int wins, int losses, int ties)
		{
			Wins = wins;
			Losses = losses;
			Ties = ties;
		}

		public static Record operator +(Record a, Record b) => new Record(a.Wins + b.Wins, a.Losses + b.Losses, a.Ties + b.Ties);
		
		public static Record operator -(Record a, Record b) => new Record(a.Wins - b.Wins, a.Losses - b.Losses, a.Ties - b.Ties);
	}

	public class GameResult
	{
		public enum ScoreOutcome
		{
			SameScore,
			BiggerWin,
			SmallerWin,
			SameWin,
			Tied,
			Reversed,
			ExtraInningsUnknown,
			Error
		}

		public string GameId { get; set; }

		public string HomeTeamId { get; set; }
		public string AwayTeamId { get; set; }

		public string OldWinnerTeamId
		{
			get
			{
				return OldAwayScore > OldHomeScore ? AwayTeamId : HomeTeamId;
			}
		}

		public Record OldHomeRecord
		{
			get
			{
				return OldAwayScore > OldHomeScore ? new Record(0, 1, 0) : new Record(1, 0, 0);
			}
		}

		public Record OldAwayRecord
		{
			get
			{
				return OldAwayScore > OldHomeScore ? new Record(1, 0, 0) : new Record(0, 1, 0);
			}
		}

		public Record NewHomeRecord
		{
			get
			{
				if (NewHomeScore == NewAwayScore) return new Record(0, 0, 1);
				else if (NewHomeScore > NewAwayScore) return new Record(1, 0, 0);
				else return new Record(0, 1, 0);
			}
		}

		public Record NewAwayRecord
		{
			get
			{
				if (NewHomeScore == NewAwayScore) return new Record(0, 0, 1);
				else if (NewAwayScore > NewHomeScore) return new Record(1, 0, 0);
				else return new Record(0, 1, 0);
			}
		}

		public int OldAwayScore { get; set; }
		public int OldHomeScore { get; set; }

		public int NewAwayScore { get; set; }
		public int NewHomeScore { get; set; }

		// TODO ExtraInningsUnknown
		public ScoreOutcome Outcome
		{
			get 
			{
				if(OldAwayScore == NewAwayScore && OldHomeScore == NewHomeScore)
				{
					return ScoreOutcome.SameScore;
				}
				else if(NewAwayScore == NewHomeScore)
				{
					return ScoreOutcome.Tied;
				}
				else
				{
					int oldDifference = OldAwayScore - OldHomeScore;
					int newDifference = NewAwayScore - NewHomeScore;

					if (oldDifference >= 0 ^ newDifference >= 0)
					{
						return ScoreOutcome.Reversed;
					}
					else
					{
						oldDifference = Math.Abs(oldDifference);
						newDifference = Math.Abs(newDifference);
						if (oldDifference == newDifference)
						{
							return ScoreOutcome.SameWin;
						}
						else if (oldDifference > newDifference)
						{
							return ScoreOutcome.SmallerWin;
						}
						else if (oldDifference < newDifference)
						{
							return ScoreOutcome.BiggerWin;
						}
					}
				}
				return ScoreOutcome.Error;
			}
		}

		public bool WasOutcomeReversed
		{
			get
			{
				return Outcome == ScoreOutcome.Reversed;
			}
		}

		public int NewHomeStrikeouts { get; set; }
		public int NewAwayStrikeouts { get; set; }

		public int DroppedAppearances { get; set; }

		public Statistics Stats { get; private set; }

		public GameResult(IEnumerable<GameEvent> events, int awayScore, int homeScore, int dropped)
		{
			OldAwayScore = awayScore;
			OldHomeScore = homeScore;
			DroppedAppearances = dropped;

			GameId = events.First().gameId;
			NewAwayScore = (int)events.Last().awayScore;
			NewHomeScore = (int)events.Last().homeScore;

			HomeTeamId = events.First().pitcherTeamId;
			AwayTeamId = events.First().batterTeamId;

			// Strikeouts for home team happen in top of inning when away is batting
			NewHomeStrikeouts = events.Where(x => x.topOfInning == true).Sum(x => x.rescoreNewStrikeout ? 1 : 0);
			NewAwayStrikeouts = events.Where(x => x.topOfInning == false).Sum(x => x.rescoreNewStrikeout ? 1 : 0);

			Stats = new Statistics();
		}
	}
}
