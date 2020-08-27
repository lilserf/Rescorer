using System;
using System.Collections.Generic;
using System.Text;

namespace Rescorer
{
	public class StatisticsEntry
	{
		public IDictionary<string, int> Stats => m_stats;
		Dictionary<string, int> m_stats;
		public string Stat { get; set; }
		public int Value { get; set; }

		public StatisticsEntry()
		{
			m_stats = new Dictionary<string, int>();
		}

		public void Add(string stat, int value)
		{
			if(!m_stats.ContainsKey(stat))
			{
				m_stats[stat] = 0;
			}
			m_stats[stat] += value;
		}

		public void Subtract(string stat, int value) => Add(stat, -value);

		public void Merge(StatisticsEntry other)
		{
			foreach(var kvp in other.m_stats)
			{
				Add(kvp.Key, kvp.Value);
			}
		}
	}

	public class Statistics
	{
		public IDictionary<string, StatisticsEntry> PlayerStats => m_playerStats;
		Dictionary<string, StatisticsEntry> m_playerStats;

		public Statistics()
		{
			m_playerStats = new Dictionary<string, StatisticsEntry>();
		}

		private StatisticsEntry GetEntryForPlayer(string playerId)
		{
			if(!m_playerStats.ContainsKey(playerId))
			{
				m_playerStats[playerId] = new StatisticsEntry();
			}

			return m_playerStats[playerId];
		}

		public void AddStat(string playerId, string stat, int value) => GetEntryForPlayer(playerId).Add(stat, value);
		
		public void SubtractStat(string playerId, string stat, int value) => GetEntryForPlayer(playerId).Subtract(stat, value);

		public void Merge(Statistics other)
		{
			foreach(var kvp in other.m_playerStats)
			{
				GetEntryForPlayer(kvp.Key).Merge(kvp.Value);
			}
		}
	}
}
