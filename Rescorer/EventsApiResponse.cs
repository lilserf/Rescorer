using System;
using System.Collections.Generic;
using System.Text;

namespace Rescorer
{
	public class EventsApiResponse
	{
		public int count { get; set; }

		public IEnumerable<GameEvent> results { get; set; }
	}
}
