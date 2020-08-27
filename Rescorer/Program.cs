using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rescorer
{
	class TeamRecord
	{
		public string Name { get; set; }
		public int Wins { get; set; }
		public int Losses { get; set; }

		public int Ties { get; set; }

		public TeamRecord(string name, int wins, int losses)
		{
			Name = name;
			Wins = wins;
			Losses = losses;
			Ties = 0;
		}
	}

	class Player
	{
		public string Name { get; set; }
	}

	class Program
	{
		//static string testGame = "005a2ae5-1727-44f8-88c1-d24e17e1582b";
		static string testGame = null;

		static HttpClient s_client;
		public static async Task<Player> FetchPlayer(string playerId)
		{
			HttpResponseMessage response = await s_client.GetAsync($"players?ids={playerId}");

			if (response.IsSuccessStatusCode)
			{
				string strResponse = await response.Content.ReadAsStringAsync();

				IEnumerable<Player> r = JsonSerializer.Deserialize<IEnumerable<Player>>(strResponse, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy() });
				return r.FirstOrDefault();
			}
			else
			{
				return null;
			}
		}

		static async Task Main(string[] args)
		{
			s_client = new HttpClient();
			s_client.BaseAddress = new Uri("https://www.blaseball.com/database/");
			s_client.DefaultRequestHeaders.Accept.Clear();
			s_client.DefaultRequestHeaders.Accept.Add(
				new MediaTypeWithQualityHeaderValue("application/json"));


			string gameListFile = args[0];
			string outputFolder = args[1];
			string singleAdvanceFile = args[2];
			string groundOutAdvanceFile = args[3];
			string flyOutAdvanceFile = args[4];
			string jsonFile = null;
			if(args.Length > 5)
			{
				jsonFile = args[5];
			}

			// Load the single-advance stats
			Dictionary<string, Tuple<float, float>> singleAdvance = new Dictionary<string, Tuple<float, float>>();
			using (StreamReader reader = new StreamReader(singleAdvanceFile))
			{
				while (!reader.EndOfStream)
				{
					string[] line = reader.ReadLine().Split('\t');
					singleAdvance[line[0]] = new Tuple<float, float>(float.Parse(line[1]), float.Parse(line[2]));
				}
			}

			Dictionary<string, Tuple<int, float, float>> groundOutAdvance = new Dictionary<string, Tuple<int, float, float>>();
			using (StreamReader reader = new StreamReader(groundOutAdvanceFile))
			{
				while (!reader.EndOfStream)
				{
					string[] line = reader.ReadLine().Split('\t');
					groundOutAdvance[line[0]] = new Tuple<int, float, float>(int.Parse(line[1]), float.Parse(line[2]), float.Parse(line[3]));
				}
			}

			Dictionary<string, Tuple<int, float, float>> flyOutAdvance = new Dictionary<string, Tuple<int, float, float>>();
			using (StreamReader reader = new StreamReader(flyOutAdvanceFile))
			{
				while (!reader.EndOfStream)
				{
					string[] line = reader.ReadLine().Split('\t');
					flyOutAdvance[line[0]] = new Tuple<int, float, float>(int.Parse(line[1]), float.Parse(line[2]), float.Parse(line[3]));
				}
			}
			Processor p = new Processor(outputFolder, singleAdvance, groundOutAdvance, flyOutAdvance);

			if (jsonFile != null)
			{
				Console.WriteLine($"Loading GameEvents from {jsonFile}...");
				p.LoadFromJson(jsonFile);
				//p.FilterToGameList(gameListFile);
				//p.WriteFiltered($"jsonFile-filtered.json");
			}

			if(!Directory.Exists(outputFolder))
			{
				Directory.CreateDirectory(outputFolder);
			}

			if (testGame != null)
			{
				// Temp check this game
				p.Run(testGame);
				return;
			}

			using (StreamReader sr = new StreamReader(gameListFile))
			{
				while (!sr.EndOfStream)
				{
					string id = sr.ReadLine();
					// RESCORE THIS GAME
					p.Run(id);
				}
			}

			Statistics totalStats = new Statistics();
			foreach(var s in p.Results)
			{
				totalStats.Merge(s.Stats);
			}


			Dictionary<string, TeamRecord> teamLookup = new Dictionary<string, TeamRecord>();
			teamLookup["b72f3061-f573-40d7-832a-5ad475bd7909"] = new TeamRecord("San Francisco Lovers", 59, 40);
			teamLookup["878c1bf6-0d21-4659-bfee-916c8314d69c"] = new TeamRecord("Unlimited Tacos", 38, 62);
			teamLookup["b024e975-1c4a-4575-8936-a3754a08806a"] = new TeamRecord("Dallas Steaks", 55, 54);
			teamLookup["adc5b394-8f76-416d-9ce9-813706877b84"] = new TeamRecord("Kansas City Breath Mints", 49, 50);
			teamLookup["ca3f1c8c-c025-4d8e-8eef-5be6accbeb16"] = new TeamRecord("Chicago Firefighters", 35, 64);
			teamLookup["bfd38797-8404-4b38-8b82-341da28b1f83"] = new TeamRecord("Charleston Shoe Thieves", 60, 40);
			teamLookup["3f8bbb15-61c0-4e3f-8e4a-907a5fb1565e"] = new TeamRecord("Boston Flowers", 42, 57);
			teamLookup["979aee4a-6d80-4863-bf1c-ee1a78e06024"] = new TeamRecord("Hawaii Fridays", 48, 51);
			teamLookup["7966eb04-efcc-499b-8f03-d13916330531"] = new TeamRecord("Yellowstone Magic", 53, 46);
			teamLookup["36569151-a2fb-43c1-9df7-2df512424c82"] = new TeamRecord("New York Millennials", 65, 34);
			teamLookup["8d87c468-699a-47a8-b40d-cfb73a5660ad"] = new TeamRecord("Baltimore Crabs", 44, 55);
			teamLookup["23e4cbc1-e9cd-47fa-a35b-bfa06f726cb7"] = new TeamRecord("Philly Pies", 53, 46);
			teamLookup["f02aeae2-5e6a-4098-9842-02d2273f25c7"] = new TeamRecord("Hellmouth Sunbeams", 38, 61);
			teamLookup["57ec08cc-0411-4643-b304-0e80dbc15ac7"] = new TeamRecord("Mexico City Wild Wings", 42, 57);
			teamLookup["747b8e4a-7e50-4638-a973-ea7950a3e739"] = new TeamRecord("Hades Tigers", 70, 29);
			teamLookup["eb67ae5e-c4bf-46ca-bbbc-425cd34182ff"] = new TeamRecord("Canada Moist Talkers", 53, 46);
			teamLookup["9debc64f-74b7-4ae1-a4d6-fce0144b6ea5"] = new TeamRecord("Houston Spies", 49, 50);
			teamLookup["b63be8c2-576a-4d6e-8daf-814f8bcea96f"] = new TeamRecord("Miami Dal&eacute;", 40, 59);
			teamLookup["105bc3ff-1320-4e37-8ef0-8d595cb95dd0"] = new TeamRecord("Seattle Garages", 48, 51);
			teamLookup["a37f9158-7f82-46bc-908c-c9e2dda7c33b"] = new TeamRecord("Breckenridge Jazz Hands", 50, 49);

			using (StreamWriter page = new StreamWriter($"{outputFolder}/playerStats.html"))
			{
				page.WriteLine("<html><head><link rel=\"stylesheet\" href=\"style.css\"/></head><body>");
				// Individual stats
				page.WriteLine("<div class=summary>Pitching Stats");
				page.WriteLine("<table class=stats>");
				page.WriteLine("<tr><th>Player ID</th><th>Player</th><th>Stat</th><th>Change</th></tr>");
				foreach (var entry in totalStats.PlayerStats)
				{
					Player player = await FetchPlayer(entry.Key);
					foreach (var stat in entry.Value.Stats.Where(x => x.Key.Contains("pitched")))
					{
						string statName = stat.Key.Substring(0, stat.Key.LastIndexOf('-'));
						page.WriteLine($"<tr><td>{entry.Key}</td><td>{player.Name}</td><td>{statName}</td><td>{stat.Value:+0;-#}</td></tr>");
					}
				}
				page.WriteLine("</table></div>");

				page.WriteLine("<div class=summary>Batting Stats");
				page.WriteLine("<table class=stats>");
				page.WriteLine("<tr><th>Player ID</th><th>Player</th><th>Stat</th><th>Change</th></tr>");
				foreach (var entry in totalStats.PlayerStats)
				{
					Player player = await FetchPlayer(entry.Key);
					foreach (var stat in entry.Value.Stats.Where(x => x.Key.Contains("batted")))
					{
						string statName = stat.Key.Substring(0, stat.Key.LastIndexOf('-'));
						page.WriteLine($"<tr><td>{entry.Key}</td><td>{player.Name}</td><td>{statName}</td><td>{stat.Value:+0;-#}</td></tr>");
					}
				}
				page.WriteLine("</table></div>");
				page.WriteLine("</body></html>");
			}

			// Produce index.html
			using (StreamWriter index = new StreamWriter($"{outputFolder}/index.html"))
			{
				index.WriteLine("<html><head><link rel=\"stylesheet\" href=\"style.css\"/></head><body>");
				
				// Summary of game outcomes
				index.WriteLine("<div class=summary>Outcome Summary:");
				index.WriteLine("<table class=summary>");
				foreach(GameResult.ScoreOutcome outcome in Enum.GetValues(typeof(GameResult.ScoreOutcome)))
				{
					index.WriteLine($"<tr><td><span class={outcome}>{outcome}</span></td><td>{p.Results.Where(x => x.Outcome == outcome).Count()}</td></tr>");
				}
				index.WriteLine("</table>");

				// Info on team records
				index.WriteLine("<table class=records>");
				index.WriteLine("<tr><th>Team</th><th>Wins</th><th>Losses</th><th>Ties</th><th>New Record</th></tr>");
				foreach(var entry in p.RecordAdjustments)
				{
					TeamRecord team = teamLookup[entry.Key];
					Record adj = entry.Value;
					Record oldRecord = new Record(team.Wins, team.Losses, 0);
					Record newRec = oldRecord + adj;
					index.WriteLine($"<tr><td>{team.Name}</td><td>{adj.Wins:+0;-#}</td><td>{adj.Losses:+0;-#}</td><td>{adj.Ties:+0;-#}</td><td>{newRec.Wins}/{newRec.Losses}/{newRec.Ties}</td></tr>");
				}
				index.WriteLine("</table>");

				// Info on some statistics
				index.WriteLine("<table class=stats>");
				index.WriteLine($"<tr><td>Total new strikeouts:</td><td>{p.Results.Sum(x => x.NewAwayStrikeouts + x.NewHomeStrikeouts)}</td></tr>");
				index.WriteLine($"<tr><td>Total dropped appearances:</td><td>{p.Results.Sum(x => x.DroppedAppearances)}</td></tr>");
				index.WriteLine("</table>");
				index.WriteLine("</div>");

				// Reversed Games
				index.WriteLine("<div>Reversed Games:</div>");
				index.WriteLine("<table class=interestingGames>");
				index.WriteLine("<tr><th>Link</th><th>Teams</th><th>Original Score</th><th>New Score</th></tr>");
				foreach (var game in p.Results.Where(x => x.Outcome == GameResult.ScoreOutcome.Reversed))
				{
					string away = teamLookup[game.AwayTeamId].Name;
					string home = teamLookup[game.HomeTeamId].Name;
					index.WriteLine($"<tr><td><a href='{game.GameId}.html'>{game.GameId}</a></td><td>{away} at {home}</td><td>{game.OldAwayScore}-{game.OldHomeScore}</td><td>{game.NewAwayScore}-{game.NewHomeScore}</td></tr>");
				}
				
				// Tied Games
				index.WriteLine("</table>");
				index.WriteLine("<div>Tied Games:</div>");
				index.WriteLine("<table class=interestingGames>");
				index.WriteLine("<tr><th>Link</th><th>Teams</th><th>Original Score</th><th>New Score</th></tr>");
				foreach (var game in p.Results.Where(x => x.Outcome == GameResult.ScoreOutcome.Tied))
				{
					string away = teamLookup[game.AwayTeamId].Name;
					string home = teamLookup[game.HomeTeamId].Name;
					index.WriteLine($"<tr><td><a href='{game.GameId}.html'>{game.GameId}</a></td><td>{away} at {home}</td><td>{game.OldAwayScore}-{game.OldHomeScore}</td><td>{game.NewAwayScore}-{game.NewHomeScore}</td></tr>");
				}
				index.WriteLine("</table>");

				// Links to rescored games
				index.WriteLine("<div class=gameHeader>All Rescored Games:</div>");
				foreach(var result in p.Results)
				{
					index.WriteLine($"<span class={result.Outcome}>{result.Outcome}</span>");
					index.WriteLine($"<a href='{result.GameId}.html'>{result.GameId}</a><br/>");
				}
				index.WriteLine("</body></html>");
			}


		}
	}
}
