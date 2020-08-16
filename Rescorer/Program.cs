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
				//p.FilterToGameList(gameListFile);
				//p.WriteFiltered($"jsonFile-filtered.json");
			}

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
