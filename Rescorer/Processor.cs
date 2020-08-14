using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rescorer
{
	// Stolen from upcoming dotnet versions :)
	internal class SnakeCaseNamingPolicy : JsonNamingPolicy
	{
		internal enum SnakeCaseState
		{
			Start,
			Lower,
			Upper,
			NewWord
		}

		public override string ConvertName(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return name;
			}

			var sb = new StringBuilder();
			var state = SnakeCaseState.Start;

			var nameSpan = name.AsSpan();

			for (int i = 0; i < nameSpan.Length; i++)
			{
				if (nameSpan[i] == ' ')
				{
					if (state != SnakeCaseState.Start)
					{
						state = SnakeCaseState.NewWord;
					}
				}
				else if (char.IsUpper(nameSpan[i]))
				{
					switch (state)
					{
						case SnakeCaseState.Upper:
							bool hasNext = (i + 1 < nameSpan.Length);
							if (i > 0 && hasNext)
							{
								char nextChar = nameSpan[i + 1];
								if (!char.IsUpper(nextChar) && nextChar != '_')
								{
									sb.Append('_');
								}
							}
							break;
						case SnakeCaseState.Lower:
						case SnakeCaseState.NewWord:
							sb.Append('_');
							break;
					}
					sb.Append(char.ToLowerInvariant(nameSpan[i]));
					state = SnakeCaseState.Upper;
				}
				else if (nameSpan[i] == '_')
				{
					sb.Append('_');
					state = SnakeCaseState.Start;
				}
				else
				{
					if (state == SnakeCaseState.NewWord)
					{
						sb.Append('_');
					}

					sb.Append(nameSpan[i]);
					state = SnakeCaseState.Lower;
				}
			}

			return sb.ToString();
		}
	}

	class Processor
	{
		HttpClient m_client;

		public Processor()
		{
			m_client = new HttpClient();
		}

		public async Task<IEnumerable<GameEvent>> FetchGame(string gameId)
		{
			// Update port # in the following line.
			m_client.BaseAddress = new Uri("http://api.blaseball-reference.com/v1/");
			m_client.DefaultRequestHeaders.Accept.Clear();
			m_client.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));

			HttpResponseMessage response = await m_client.GetAsync($"events?gameId={gameId}&baseRunners=true");

			if(response.IsSuccessStatusCode)
			{
				string strResponse = await response.Content.ReadAsStringAsync();
				
				EventsApiResponse r =  JsonSerializer.Deserialize<EventsApiResponse>(strResponse, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy()});
				return r.results;
			}
			else
			{
				return null;
			}
		}


		struct Summary
		{
			public int inning;
			public bool topOfInning;
			public int outs;
			public string type;
			public string batterId;
			public int eventId;
			public int dbId;

			public override string ToString()
			{
				string topbot = topOfInning ? "Top" : "Bot";
				return $"{dbId,6} [{eventId,3}]: {topbot}{inning+1,2}, {outs} out  {type,16}";
			}
		}

		class Inning
		{
			public List<Summary> awayBefore;
			public List<Summary> homeBefore;
			public List<Summary> awayAfter;
			public List<Summary> homeAfter;

			public Inning()
			{
				awayBefore = new List<Summary>();
				homeBefore = new List<Summary>();
				awayAfter = new List<Summary>();
				homeAfter = new List<Summary>();
			}
			public void Add(Summary s, bool after=false)
			{
				if(after)
				{
					if (s.topOfInning)
						awayAfter.Add(s);
					else
						homeAfter.Add(s);
				}
				else
				{
					if (s.topOfInning)
						awayBefore.Add(s);
					else
						homeBefore.Add(s);
				}
			}
		}

		private Summary MakeSummary(GameEvent e)
		{
			return new Summary { dbId = e.id, eventId = e.eventIndex, inning = e.inning, topOfInning=e.topOfInning, outs = e.outsBeforePlay, type = e.eventType, batterId = e.batterId };
		}

		public void Run(string gameId)
		{
			IEnumerable<GameEvent> events = FetchGame(gameId).GetAwaiter().GetResult();
			var sorted = events.OrderBy(x => x.eventIndex).OrderBy(x => x.outsBeforePlay).OrderBy(x => x.inning);

			// UGLY but works, sorry
			Dictionary<int, Inning> innings = new Dictionary<int, Inning>();
			foreach(var e in sorted)
			{
				if(!innings.ContainsKey(e.inning))
				{
					innings[e.inning] = new Inning();
				}
				innings[e.inning].Add(MakeSummary(e));
			}

			using (FileStream s = new FileStream("before.json", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, sorted, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy(), WriteIndented = true });
				}
			}

			FourthStrikeAnalyzer fsa = new FourthStrikeAnalyzer();
			var newEvents = fsa.RescoreGame(sorted);

			using(FileStream s = new FileStream("after.json", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, newEvents, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy(), WriteIndented = true });
				}
			}

			foreach (var e in newEvents)
			{
				if (!innings.ContainsKey(e.inning))
				{
					innings[e.inning] = new Inning();
				}
				innings[e.inning].Add(MakeSummary(e), true);
			}

			using (StreamWriter s = new StreamWriter("rescore.diff"))
			{
				string header = $"DBID   EIDX   INNING                   EVENT";
				s.WriteLine($"{header}   {header}");
				foreach(var inningNum in innings.Keys.OrderBy(x => x))
				{
					s.WriteLine($"========================================== Inning {inningNum+1} ========================================");
					Inning inning = innings[inningNum];
					int numLines = Math.Max(inning.awayBefore.Count, inning.awayAfter.Count);
					for(int lineNum = 0; lineNum < numLines; lineNum++)
					{
						string before = (lineNum < inning.awayBefore.Count) ? inning.awayBefore[lineNum].ToString() : "";
						string after = (lineNum < inning.awayAfter.Count) ? inning.awayAfter[lineNum].ToString() : "";
						s.WriteLine($"{before,45} | {after,45}");
					}
					s.WriteLine();
					s.WriteLine();
					numLines = Math.Max(inning.homeBefore.Count, inning.homeAfter.Count);
					for (int lineNum = 0; lineNum < numLines; lineNum++)
					{
						string before = (lineNum < inning.homeBefore.Count) ? inning.homeBefore[lineNum].ToString() : "";
						string after = (lineNum < inning.homeAfter.Count) ? inning.homeAfter[lineNum].ToString() : "";
						s.WriteLine($"{before,45} | {after,45}");
					}
				}
			}

		}
	}
}
