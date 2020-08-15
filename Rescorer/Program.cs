using System;

namespace Rescorer
{
	class Program
	{
		static void Main(string[] args)
		{
			Processor p = new Processor();

			//p.Run("00018bf4-9498-4ec7-ad49-cdf0a60efbed");
			
			p.Run("0078d5d5-dc02-4025-821a-21fb7cc20556");

			// No fourth strike, no known errors
			//p.Run("6cbe5b8b-d332-41fc-b627-23503f4daaf4");
		}
	}
}
