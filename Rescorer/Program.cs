﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rescorer
{
	class Program
	{
		//static string testGame = "005a2ae5-1727-44f8-88c1-d24e17e1582b";
		static string testGame = null;

		static void Main(string[] args)
		{

			string gameListFile = args[0];
			string outputFolder = args[1];
			string singleAdvanceFile = args[2];
			string jsonFile = null;
			if(args.Length > 3)
			{
				jsonFile = args[3];
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

			Processor p = new Processor(outputFolder, singleAdvance);

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

			// Produce index.html
			using (StreamWriter index = new StreamWriter($"{outputFolder}/index.html"))
			{
				index.WriteLine("<html><head><link rel=\"stylesheet\" href=\"style.css\"/></head><body>");
				index.WriteLine("<div class=summary>Outcome Summary:");
				index.WriteLine("<table class=summary>");
				foreach(GameResult.ScoreOutcome outcome in Enum.GetValues(typeof(GameResult.ScoreOutcome)))
				{
					index.WriteLine($"<tr><td><span class={outcome}>{outcome}</span></td><td>{p.Results.Where(x => x.Outcome == outcome).Count()}</td></tr>");
				}
				index.WriteLine("</table>");
				index.WriteLine("<table class=stats>");
				index.WriteLine($"<tr><td>Total new strikeouts:</td><td>{p.Results.Sum(x => x.NewAwayStrikeouts + x.NewHomeStrikeouts)}</td></tr>");
				index.WriteLine($"<tr><td>Total dropped appearances:</td><td>{p.Results.Sum(x => x.DroppedAppearances)}</td></tr>");
				index.WriteLine("</table>");
				index.WriteLine("</div>");
				index.WriteLine("<div class=gameHeader>Rescored Games:</div>");
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
