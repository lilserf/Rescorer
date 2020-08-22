using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rescorer
{
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

		public GameResult(IEnumerable<GameEvent> events, int awayScore, int homeScore, int dropped)
		{
			OldAwayScore = awayScore;
			OldHomeScore = homeScore;
			DroppedAppearances = dropped;

			GameId = events.First().gameId;
			NewAwayScore = (int)events.Last().awayScore;
			NewHomeScore = (int)events.Last().homeScore;

			// Strikeouts for home team happen in top of inning when away is batting
			NewHomeStrikeouts = events.Where(x => x.topOfInning == true).Sum(x => x.rescoreNewStrikeout ? 1 : 0);
			NewAwayStrikeouts = events.Where(x => x.topOfInning == false).Sum(x => x.rescoreNewStrikeout ? 1 : 0);
		}
	}
}
