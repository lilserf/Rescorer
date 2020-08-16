using System;
using System.IO;

namespace Rescorer
{
	class Program
	{
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
				p.FilterToGameList(gameListFile);
				p.WriteFiltered($"jsonFile-filtered.json");
			}

			// Temp check this game
			p.Run("005a2ae5-1727-44f8-88c1-d24e17e1582b", outputFolder);

			using(StreamReader sr = new StreamReader(gameListFile))
			{
				while (!sr.EndOfStream)
				{
					string id = sr.ReadLine();
					p.Run(id, outputFolder);
				}
			}
		}
	}
}
