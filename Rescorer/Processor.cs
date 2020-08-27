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

		string m_outputFolderName;

		Dictionary<string, Tuple<float, float>> m_singleAdvance;
		Dictionary<string, Tuple<int, float, float>> m_groundOutAdvance;
		Dictionary<string, Tuple<int, float, float>> m_flyOutAdvance;

		public IEnumerable<GameResult> Results => m_gameResults;
		List<GameResult> m_gameResults;

		public IDictionary<string, Record> RecordAdjustments => m_recordAdjustments;
		Dictionary<string, Record> m_recordAdjustments;

		public Processor(string outputFolder, 
			Dictionary<string, Tuple<float,float>> singleAdvance,
			Dictionary<string, Tuple<int, float, float>> groundOutAdvance,
			Dictionary<string, Tuple<int, float, float>> flyOutAdvance)
		{
			m_client = new HttpClient();
			m_client.BaseAddress = new Uri("http://api.blaseball-reference.com/v1/");
			m_client.DefaultRequestHeaders.Accept.Clear();
			m_client.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));

			m_outputFolderName = outputFolder;

			m_singleAdvance = singleAdvance;
			m_groundOutAdvance = groundOutAdvance;
			m_flyOutAdvance = flyOutAdvance;

			m_gameResults = new List<GameResult>();
			m_recordAdjustments = new Dictionary<string, Record>();
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
			public string lastText;
			public int homeScore;
			public int awayScore;
			public bool outcomeChanged;

			public override string ToString()
			{
				string topbot = topOfInning ? "Top" : "Bot";
				string truncLastText = lastText;
				if (truncLastText.Length > 40)
				{
					truncLastText = lastText.Substring(0, 40);
				}
				return $"{dbId,7} [{eventId,3}]: {awayScore,2}-{homeScore,2} {topbot}{inning+1,2}, {outs} out  {type,16} {truncLastText,-40}";
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
			return new Summary {
				dbId = e.id,
				eventId = e.eventIndex,
				inning = e.inning,
				topOfInning = e.topOfInning,
				outs = e.outsBeforePlay,
				type = e.eventType,
				batterId = e.batterId,
				lastText = e.eventText.Last(),
				homeScore = (int)e.homeScore,
				awayScore = (int)e.awayScore,
				outcomeChanged = e.rescoreNewStrikeout
			};
		}

		List<GameEvent> m_gameEvents = null;

		public void LoadFromJson(string jsonFile)
		{
			m_gameEvents = new List<GameEvent>();

			using (StreamReader sr = new StreamReader(jsonFile))
			{
				while (!sr.EndOfStream)
				{
					string line = sr.ReadLine();
					GameEvent e = JsonSerializer.Deserialize<GameEvent>(line, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
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

		public GameResult Run(string gameId)
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

			if (events.Count() == 0)
			{
				using (StreamWriter invalid = new StreamWriter($"{m_outputFolderName}/invalid.txt", true))
				{
					invalid.WriteLine($"{gameId}");
				}
				return null;
			}

			// Sort the incoming events properly
			var sorted = events.OrderBy(x => x.eventIndex).OrderBy(x => x.outsBeforePlay).OrderBy(x => !x.topOfInning).OrderBy(x => x.inning);

			int homeBefore = (int)sorted.Last().homeScore;
			int awayBefore = (int)sorted.Last().awayScore;

			// UGLY but works, sorry
			Dictionary<int, Inning> innings = new Dictionary<int, Inning>();
			// Store a BEFORE summary for each event
			foreach(var e in sorted)
			{
				if(!innings.ContainsKey(e.inning))
				{
					innings[e.inning] = new Inning();
				}
				innings[e.inning].Add(MakeSummary(e));
			}

			if(!Directory.Exists(m_outputFolderName))
			{
				Directory.CreateDirectory(m_outputFolderName);
			}

			#region JSON to diff
			// Write the before JSON
			using (FileStream s = new FileStream($"{m_outputFolderName}/{gameId}-before.json", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, sorted, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy(), WriteIndented = true });
				}
			}
			#endregion

			// ACTUALLY DO THE RESCORING
			FourthStrikeAnalyzer fsa = new FourthStrikeAnalyzer(m_singleAdvance, m_groundOutAdvance, m_flyOutAdvance);
			var analyzerResults = fsa.RescoreGame(sorted);

			Console.WriteLine($"Game {gameId} had {analyzerResults.numNewStrikeouts} new strikeouts.");
			var newEvents = analyzerResults.newEvents;

			#region JSON to diff
			using (FileStream s = new FileStream($"{m_outputFolderName}/{gameId}-after.json", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					var options = new JsonSerializerOptions();
					options.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
					options.WriteIndented = true;

					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, newEvents, options);
				}
			}
			#endregion

			// Store an AFTER summary for each event
			foreach (var e in newEvents)
			{
				if (!innings.ContainsKey(e.inning))
				{
					innings[e.inning] = new Inning();
				}
				innings[e.inning].Add(MakeSummary(e), true);
			}

			GameResult result = new GameResult(newEvents, awayBefore, homeBefore, analyzerResults.numDroppedAppearances);
			result.Stats.Merge(analyzerResults.statistics);

			m_gameResults.Add(result);

			var awayRecordAdjust = result.NewAwayRecord - result.OldAwayRecord;
			var homeRecordAdjust = result.NewHomeRecord - result.OldHomeRecord;

			if(!m_recordAdjustments.ContainsKey(result.AwayTeamId))
			{
				m_recordAdjustments[result.AwayTeamId] = new Record(0, 0, 0);
			}
			if (!m_recordAdjustments.ContainsKey(result.HomeTeamId))
			{
				m_recordAdjustments[result.HomeTeamId] = new Record(0, 0, 0);
			}

			m_recordAdjustments[result.AwayTeamId] += awayRecordAdjust;
			m_recordAdjustments[result.HomeTeamId] += homeRecordAdjust;

			#region HTML output
			StringBuilder sb = new StringBuilder();
			sb.Append("<html>");
			sb.Append("<head>");
			sb.Append("<link rel=\"stylesheet\" href=\"style.css\"/>");
			sb.Append("</head>");
			sb.Append("<body>");
			sb.Append($"<div class='gameHeader'>Rescorer Report for Game ID {gameId}</div>");
			sb.Append($"<div class='scoreReport'>Before: {result.OldAwayScore}-{result.OldHomeScore} After: {result.NewAwayScore}-{result.NewHomeScore}</div>");
			sb.Append($"<div class='scoreReport'>Extra strikeouts for Away: {result.NewAwayStrikeouts} Home: {result.NewHomeStrikeouts}</div>");
			sb.Append($"<div class='scoreReport'>Outcome: {result.Outcome}</div>");
			sb.Append($"<div class='scoreReport'>Dropped Appearances: {analyzerResults.numDroppedAppearances}</div>");
			if (result.WasOutcomeReversed)
			{
				sb.Append($"<span class='outcomeReversed'>OUTCOME REVERSED!</span>");
			}
			using(Html.Table table = new Html.Table(sb))
			{
				table.StartHead();
				using (var thead = table.AddRow(id:"tableHeader"))
				{
					thead.AddCell("Event");
					thead.AddCell("Outs");
					thead.AddCell("Type");
					thead.AddCell("Score");
					thead.AddCell("Score");
					thead.AddCell("Type");
					thead.AddCell("Outs");
					thead.AddCell("Event");
				}
				table.EndHead();
				table.StartBody();
				foreach(var inningNum in innings.Keys.OrderBy(x => x))
				{
					// Inning Header
					using (var tr = table.AddRow())
					{
						tr.AddCell($"Inning {inningNum+1}", colSpan: 8, id: "inningHeader");
					}

					Inning inning = innings[inningNum];
					// Away events
					int numLines = Math.Max(inning.awayBefore.Count, inning.awayAfter.Count);
					for (int lineNum = 0; lineNum < numLines; lineNum++)
					{
						using (var tr = table.AddRow())
						{
							// Before cells
							if (lineNum < inning.awayBefore.Count)
							{
								var summary = inning.awayBefore[lineNum];
								tr.AddCell($"{summary.lastText}", classAttributes: "eventTextBefore");
								tr.AddCell($"{summary.outs} out");
								tr.AddCell($"{summary.type}", classAttributes: "typeBefore");
								tr.AddCell($"{summary.awayScore}-{summary.homeScore}");
							}
							else
							{
								tr.AddCell("", colSpan: 4);
							}

							// After cells
							if (lineNum < inning.awayAfter.Count)
							{
								var summary = inning.awayAfter[lineNum];
								tr.AddCell($"{summary.awayScore}-{summary.homeScore}");
								string typeClass = "typeAfter";
								if (summary.outcomeChanged)
									typeClass += " changed";
								tr.AddCell($"{summary.type}", classAttributes: typeClass);
								tr.AddCell($"{summary.outs} out");
								tr.AddCell($"{summary.lastText}", classAttributes: "eventTextAfter");
							}
							else
							{
								tr.AddCell("", colSpan: 4);
							}
						}
					}

					using(var tr = table.AddRow())
					{
						tr.AddCell("  ", colSpan: 8, id:"inningDivider");
					}

					// Away events
					numLines = Math.Max(inning.homeBefore.Count, inning.homeAfter.Count);
					for (int lineNum = 0; lineNum < numLines; lineNum++)
					{
						using (var tr = table.AddRow())
						{
							// Before cells
							if (lineNum < inning.homeBefore.Count)
							{
								var summary = inning.homeBefore[lineNum];
								tr.AddCell($"{summary.lastText}", classAttributes: "eventTextBefore");
								tr.AddCell($"{summary.outs} out");
								tr.AddCell($"{summary.type}", classAttributes:"typeBefore");
								tr.AddCell($"{summary.awayScore}-{summary.homeScore}");
							}
							else
							{
								tr.AddCell("", colSpan: 4);
							}

							// After cells
							if (lineNum < inning.homeAfter.Count)
							{
								var summary = inning.homeAfter[lineNum];
								tr.AddCell($"{summary.awayScore}-{summary.homeScore}");
								string typeClass = "typeAfter";
								if (summary.outcomeChanged)
									typeClass += " changed";
								tr.AddCell($"{summary.type}", classAttributes:typeClass);
								tr.AddCell($"{summary.outs} out");
								tr.AddCell($"{summary.lastText}", classAttributes: "eventTextAfter");
							}
							else
							{
								tr.AddCell("", colSpan: 4);
							}
						}
					}
				}
				table.EndBody();
			}

			sb.Append("</body></html>");

			File.WriteAllText($"{m_outputFolderName}/{gameId}.html", sb.ToString());
			File.Copy("style.css", $"{m_outputFolderName}/style.css", true);

			return result;
			#endregion
		}
	}
}
