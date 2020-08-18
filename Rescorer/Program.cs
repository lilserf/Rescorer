using System;
using System.IO;

namespace Rescorer
{
	class Program
	{
		//static string testGame = "005a2ae5-1727-44f8-88c1-d24e17e1582b";
		static string testGame = null;

		static void Main(string[] args)
		{
			Processor p = new Processor();

			string gameListFile = args[0];
			string outputFolder = args[1];
			string jsonFile = null;
			if(args.Length > 2)
			{
				jsonFile = args[2];
			}

			if(jsonFile != null)
			{
				Console.WriteLine($"Loading GameEvents from {jsonFile}...");
				p.LoadFromJson(jsonFile);
				//p.FilterToGameList(gameListFile);
				//p.WriteFiltered($"jsonFile-filtered.json");
			}

			if (testGame != null)
			{
				// Temp check this game
				p.Run(testGame, outputFolder);
				return;
			}

			using (StreamWriter index = new StreamWriter($"{outputFolder}/index.html"))
			{
				index.WriteLine("<html><head><link rel=\"stylesheet\" href=\"style.css\"/></head><body>");
				index.WriteLine("<div class=gameHeader>Rescored Games:</div>");
				using (StreamReader sr = new StreamReader(gameListFile))
				{
					while (!sr.EndOfStream)
					{
						string id = sr.ReadLine();
						p.Run(id, outputFolder);

						index.WriteLine($"<a href='{id}.html'>{id}</a><br/>");
					}
				}
				index.WriteLine("</body></html>");
			}
		}
	}
}
