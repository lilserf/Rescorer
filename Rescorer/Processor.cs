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

		public void Run(string gameId)
		{
			IEnumerable<GameEvent> events = FetchGame(gameId).GetAwaiter().GetResult();
			var sorted = events.OrderBy(x => x.eventIndex);

			using (FileStream s = new FileStream("before.rescore", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, sorted, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy(), WriteIndented = true });
				}
			}

			FourthStrikeAnalyzer fsa = new FourthStrikeAnalyzer();
			var newEvents = fsa.RescoreGame(sorted);

			using(FileStream s = new FileStream("after.rescore", FileMode.Create))
			{
				using (Utf8JsonWriter writer = new Utf8JsonWriter(s))
				{
					JsonSerializer.Serialize<IEnumerable<GameEvent>>(writer, newEvents, new JsonSerializerOptions() { PropertyNamingPolicy = new SnakeCaseNamingPolicy(), WriteIndented = true });
				}
			}
		}
	}
}
