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

	class Processor
	{
		HttpClient m_client;

		public Processor()
		{
			m_client = new HttpClient();
			m_client.BaseAddress = new Uri("http://api.blaseball-reference.com/v1/");
			m_client.DefaultRequestHeaders.Accept.Clear();
			m_client.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));

		}

		public async Task<IEnumerable<GameEvent>> FetchGame(string gameId)
		{
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

		List<GameEvent> m_gameEvents = null;

		public void LoadFromJson(string jsonFile)
		{
			m_gameEvents = new List<GameEvent>();

			using (StreamReader sr = new StreamReader(jsonFile))
			{
				while (!sr.EndOfStream)
				{
					GameEvent e = JsonSerializer.Deserialize<GameEvent>(sr.ReadLine(), new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
					m_gameEvents.Add(e);
				}
			}
			Console.WriteLine($"Loaded {m_gameEvents.Count} events from {jsonFile}");
		}

		public void FilterToGameList(string filename)
		{
			List<string> ids = new List<string>();
			using (StreamReader sr = new StreamReader(filename))
			{
				while (!sr.EndOfStream)
				{
					ids.Add(sr.ReadLine());
				}
			}
			m_gameEvents = m_gameEvents.Where(x => ids.Contains(x.gameId)).ToList();
		}

		public void WriteFiltered(string filename)
		{
			using(StreamWriter sw = new StreamWriter(filename))
			{
				foreach(var e in m_gameEvents)
				{
					sw.WriteLine(JsonSerializer.Serialize(e, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
				}
			}
		}

		public void Run(string gameId, string outputFolderName)
		{
			IEnumerable<GameEvent> events = null;
			if (m_gameEvents == null)
			{
				Console.WriteLine($"Fetching game {gameId} from Datablase...");
				events = FetchGame(gameId).GetAwaiter().GetResult();
			}
			else
			{
				Console.WriteLine($"Fetching game {gameId} from loaded JSON...");
				events = m_gameEvents.Where(x => x.gameId == gameId).ToList();
			}

			var sorted = events.OrderBy(x => x.eventIndex).OrderBy(x => x.outsBeforePlay).OrderBy(x => !x.topOfInning).OrderBy(x => x.inning);

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

			if(!Directory.Exists(outputFolderName))
			{
				Directory.CreateDirectory(outputFolderName);
			}

			using (FileStream s = new FileStream($"{outputFolderName}/{gameId}-before.json", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, sorted, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy(), WriteIndented = true });
				}
			}

			// ACTUALLY DO THE RESCORING
			FourthStrikeAnalyzer fsa = new FourthStrikeAnalyzer();
			var newEvents = fsa.RescoreGame(sorted);

			using(FileStream s = new FileStream($"{outputFolderName}/{gameId}-after.json", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					var options = new JsonSerializerOptions();
					options.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
					options.WriteIndented = true;

					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, newEvents, options);
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

			using (StreamWriter s = new StreamWriter($"{outputFolderName}/{gameId}.diff"))
			{
				s.WriteLine($"Diff for game {gameId}");
				string header = $"DBID   EIDX   INNING                   EVENT";
				s.WriteLine($" {header}    {header}");
				foreach(var inningNum in innings.Keys.OrderBy(x => x))
				{
					s.WriteLine($"========================================== Inning {inningNum+1} =========================================");
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
