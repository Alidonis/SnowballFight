using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data.SQLite;

namespace TestDiscordBot
{
	public class DatabaseManager : IDisposable
	{
		public SQLiteConnection DBConnection { get; protected init; }
		Dictionary<string, List<string>> Tables = new();
		public DatabaseManager(string _filename) 
		{
			DBConnection = new SQLiteConnection($"Data Source={_filename}");
			DBConnection.Open();
		}

		public void Dispose()
		{
			DBConnection.Close();
		}

		public void CreateTable(string tableName, Dictionary<string, string> collumnData, bool protectExisting = true)
		{
			string protect = "";
			if (protectExisting) protect = " IF NOT EXISTS";

			string collumns = "";
			foreach (KeyValuePair<string, string> KVpair in collumnData)
			{
				collumns += $"\n{KVpair.Key} {KVpair.Value},";
			}
			collumns = collumns.TrimEnd(',');

			string request = $"CREATE TABLE{protect} {tableName} ({collumns})";

			Console.WriteLine(request);

			using var cmd = new SQLiteCommand(request, DBConnection);

			Tables.Add(tableName, collumnData.Keys.ToList());
			
			cmd.ExecuteNonQuery();
		}
		public void AppendRecord(string tableName, string[] values, bool replaceIfExists = false)
		{
			List<string> collumnNames = Tables[tableName];

			var collumnsString = "";
			foreach (string collumnName in collumnNames)
			{
				collumnsString += $"{collumnName},";
			}
			collumnsString = collumnsString.TrimEnd(',');

			var valuesString = "";
			foreach (string value in values)
			{
				valuesString += $"{value},";
			}
			valuesString = valuesString.TrimEnd(',');

			var replaceString = "";
			if (replaceIfExists)
				replaceString = " OR REPLACE";

			string request = $"INSERT{replaceString} INTO {tableName}({collumnsString}) VALUES ({valuesString})";

			Console.WriteLine( request );

			using var cmd = new SQLiteCommand(request, DBConnection);
			cmd.ExecuteNonQuery();
		}
		public void UpdateRecord(string tableName, Dictionary<string, string> records, string condition)
		{
			List<string> collumnNames = Tables[tableName];

			var collumnsString = "";
			foreach (string collumnName in collumnNames)
			{
				collumnsString += $"{collumnName},";
			}
			collumnsString = collumnsString.TrimEnd(',');

			var valuesString = "";
			foreach (KeyValuePair<string, string> record in records)
			{
				valuesString += $"{record.Key} = {record.Value},";
			}
			valuesString = valuesString.TrimEnd(',');

			string request = $"UPDATE {tableName} SET {valuesString} WHERE {condition}";

			Console.WriteLine(request);

			using var cmd = new SQLiteCommand(request, DBConnection);
			cmd.ExecuteNonQuery();
		}
		public async Task<object[][]> SelectFromTableAsync(string tableName, string records, string condition = "", Dictionary<string, string>? order = null)
		{
			return SelectFromTable(tableName, records, condition, order);
		}
		public object[][] SelectFromTable(string tableName, string records, string condition = "", Dictionary<string, string>? order = null, int limit = -1)
		{
			var _limit = "";
			if (limit > 0)
				_limit = $" LIMIT {limit}";

			var where = "";
			if (condition != "")
				where = $" WHERE {condition}";

			var orderBy = "";
			if (order != null)
			{
				foreach (KeyValuePair<string,string> pair in order)
				{
					if (pair.Value != "ASC" || pair.Value != "DESC")
						throw new Exception("");
					orderBy += $"{pair.Key} {pair.Value},";
				}
				orderBy = orderBy.TrimEnd(',');

				orderBy = $" ORDER BY {orderBy}";
			}

			string request = $"SELECT {records} FROM {tableName}{where}{orderBy}{_limit}";

			Console.WriteLine(request);

			using var cmd = new SQLiteCommand(request, DBConnection);
			var reader = cmd.ExecuteReader();
			object[][] values = { };
			while (reader.Read())
			{
				object[] collumnData = [];
				for (int i = 0; i < reader.FieldCount; i++)
				{
					collumnData = collumnData.Append( reader.GetValue(i) ).ToArray();
				}
				values = values.Append( collumnData ).ToArray();
			}
			return values;
		}
		public unsafe void ExecuteRawNonQuerry(string querry)
		{
			using var cmd = new SQLiteCommand(querry, DBConnection);
			Console.WriteLine(cmd.ExecuteNonQuery());
		}
		public unsafe SQLiteDataReader ExecuteRawQuerry(string querry)
		{
			using var cmd = new SQLiteCommand(querry, DBConnection);
			return cmd.ExecuteReader();
		}
	}
}
