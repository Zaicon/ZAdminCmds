using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZAdminCmds
{
	public class Config
	{
		public int highestRankToDisplay = 1;
		public bool showGroupNameInsteadOfPrefix = false;
		public Dictionary<int, string> groupranks;

		public void Write(string path)
		{
			File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
		}

		public Config Read(string path)
		{
			return !File.Exists(path) ? ZAdmin.generateNewConfig() : JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
		}
	}
}
