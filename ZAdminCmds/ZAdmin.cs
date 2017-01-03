/*
 * ZAdminCmds by Zaicon
 * Credit to InanZen & Enerdy for Dayregion command.
 * Credit to Essentials (Scavenger3, WhiteXZ & others) for /ptime idea.
 * 
 */

using Microsoft.Xna.Framework;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace ZAdminCmds
{
    public class Time
	{
		public bool day;
		public double frames;

		public static double NOON = 27000;
		public static double DAY = 0;
		public static double NIGHT = 0;
		public static double MIDNIGHT = 16200;
	}

	[ApiVersion(2,0)]
    public class ZAdmin : TerrariaPlugin
    {
		public override string Name { get { return "ZAdminCmds"; } }
		public override string Author { get { return "Zaicon"; } }
		public override string Description { get { return "Misc Commands"; } }
		public override Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

		public static Config config = new Config();
		public static string configpath = "tshock/ZAdmin.json";
		public static bool initialized = false;

		public static IDbConnection db;

		private Timer update;
		public static List<string> permamuted = new List<string>();
		public static List<Region> RegionList = new List<Region>();
		public static List<int> DayClients = new List<int>();
		public static Dictionary<int, Time> playertime = new Dictionary<int, Time>();


		public ZAdmin(Main game)
			: base(game)
		{
			base.Order = 1;
		}

		#region Initialize/Dispose
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetSendData.Register(this, SendData);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
		}

		protected override void Dispose(bool Disposing)
		{
			if (Disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetSendData.Deregister(this, SendData);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
			}
			base.Dispose(Disposing);
		}
		#endregion

		#region Hooks
		private void OnInitialize(EventArgs args)
		{
			config = config.Read(configpath);

			update = new Timer { Interval = 1000, AutoReset = true, Enabled = true };
			update.Elapsed += new ElapsedEventHandler(OnUpdate);

			Commands.ChatCommands.Add(new Command("zadmin.baninfo", ZABanInfo, "baninfo"));
			Commands.ChatCommands.Add(new Command("zadmin.baninfo", ZABanSearch, "bansearch"));
			Commands.ChatCommands.Add(new Command("zadmin.xid", ZAXID, "xid"));
			Commands.ChatCommands.Add(new Command(ZAUserGroups, "usergroups", "ug"));
			Commands.ChatCommands.Add(new Command("zadmin.pmute", ZAPMute, "pmute"));
			Commands.ChatCommands.Add(new Command("zadmin.dayregion", ZADayregion, "dayregion"));
			Commands.ChatCommands.Add(new Command("zadmin.ptime", ZAPTime, "ptime"));

			SetupDb();
			DayRegions_Read();
		}

		private void OnLeave(LeaveEventArgs args)
		{
			if (DayClients.Contains(args.Who))
				DayClients.Remove(args.Who);

			if (playertime.ContainsKey(args.Who))
				playertime.Remove(args.Who);
		}

		public void SendData(SendDataEventArgs e)
		{
			if (e.MsgId == PacketTypes.WorldInfo)
			{
				if (e.remoteClient == -1)
				{
					double temp = Main.time;
					bool tempday = Main.dayTime;
					for (int i = 0; i < TShock.Players.Length; i++)
					{
						TSPlayer plr = TShock.Players[i];
						if (plr == null || plr.Active == false)
							continue;

						if (playertime.ContainsKey(plr.Index))
						{
							Main.dayTime = playertime[plr.Index].day;
							Main.time = playertime[plr.Index].frames;
						}

						if (!DayClients.Contains(plr.Index))
							plr.SendData(PacketTypes.WorldInfo);
					}
					e.Handled = true;
					Main.time = temp;
					Main.dayTime = tempday;
				}
			}

			if (e.MsgId == PacketTypes.TimeSet)
			{
				if (e.remoteClient == -1)
				{
					foreach (TSPlayer plr in TShock.Players)
					{
						if (plr == null || !plr.Active)
							continue;

						if (playertime.ContainsKey(plr.Index))
							continue;

						plr.SendData(PacketTypes.TimeSet);
					}

					e.Handled = true;
				}
			}
		}

		private void OnUpdate(object sender, ElapsedEventArgs args)
		{
			foreach (TSPlayer player in TShock.Players)
				if (player != null && player.Active)
				{
					if (permamuted.Contains(player.IP))
						player.mute = true;

					if (player.CurrentRegion != null && RegionList.Any(p => p.Name == player.CurrentRegion.Name))
					{
						if (!DayClients.Contains(player.Index))
						{
							DayClients.Add(player.Index);
							double oldWS = Main.worldSurface;
							double oldRL = Main.rockLayer;
							Main.worldSurface = player.CurrentRegion.Area.Bottom;
							Main.rockLayer = player.CurrentRegion.Area.Bottom + 10;
							player.SendData(PacketTypes.WorldInfo);
							Main.worldSurface = oldWS;
							Main.rockLayer = oldRL;
						}
					}
					else if (DayClients.Contains(player.Index))
					{
						DayClients.Remove(player.Index);
						player.SendData(PacketTypes.WorldInfo);
					}
				}


		}

		public static Config generateNewConfig()
		{
			Config newconfig = new Config();
			int count = TShock.Groups.Count();

			newconfig.groupranks = new Dictionary<int, string>();

			foreach (Group group in TShock.Groups)
			{
				newconfig.groupranks.Add(count--, group.Name);
			}

			newconfig.Write(configpath);

			return newconfig;
		}

		private void LoadConfig()
		{
			config = config.Read(configpath);
		}
		#endregion

		#region Commands
		private void ZABanInfo(CommandArgs args)
		{
			if (args.Parameters.Count != 1)
				args.Player.SendErrorMessage("Invalid syntax: /baninfo \"Player Name\"");
			else
			{
				string playername = args.Parameters[0];
				TShockAPI.DB.Ban bannedplayer = TShock.Bans.GetBanByName(playername);
				if (bannedplayer == null)
				{
					args.Player.SendErrorMessage("No bans by this name were found.");
				}
				else
				{
					args.Player.SendInfoMessage("Account name: " + bannedplayer.Name + " (" + bannedplayer.IP + ")");
					args.Player.SendInfoMessage("Date banned: " + bannedplayer.Date);
					if (bannedplayer.Expiration != "")
						args.Player.SendInfoMessage("Expiration date: " + bannedplayer.Expiration);
					args.Player.SendInfoMessage("Banning user: " + bannedplayer.BanningUser);
					args.Player.SendInfoMessage("Reason: " + bannedplayer.Reason);
				}
			}
		}

		private void ZABanSearch(CommandArgs args)
		{
			if (args.Parameters.Count != 2)
			{
				args.Player.SendErrorMessage("Invalid syntax: /bansearch <-un/-ip> <username/IP>");
				return;
			}
			else
			{
				if (args.Parameters[0] == "-un" || args.Parameters[0] == "-ip")
				{
					if (args.Parameters[0] == "-un")
					{
						var completebanlist = TShock.Bans.GetBans();

						var bans = (from ban in completebanlist where ban.Name.ToLower().Contains(args.Parameters[1].ToLower()) select ban.Name);

						if (bans.Count() > 0)
							args.Player.SendInfoMessage("Banned players found: {0}", string.Join(", ", bans));
						else
							args.Player.SendErrorMessage("No banned players found by that name.");

						return;
					}
					else if (args.Parameters[0] == "-ip")
					{
						string[] ip = args.Parameters[1].Split('.');

						if (ip.Length != 4)
						{
							args.Player.SendErrorMessage("Invalid IP!");
							return;
						}

						foreach (string part in ip)
						{
							if (part != "*")
							{
								int ipnum = -1;
								bool parsed = int.TryParse(part, out ipnum);
								if (!parsed || (ipnum < 1 || ipnum > 255))
								{
									args.Player.SendErrorMessage("Invalid IP!");
									return;
								}
							}

						}

						var completebanlist = TShock.Bans.GetBans();

						List<TShockAPI.DB.Ban> banlist = new List<TShockAPI.DB.Ban>();

						foreach (TShockAPI.DB.Ban ban in completebanlist)
						{
							//shaddup I know it's poor code
							var bannedipsplit = ban.IP.Split('.');
							if (ip[0] == "*" || bannedipsplit[0] == ip[0])
								if (ip[1] == "*" || bannedipsplit[1] == ip[1])
									if (ip[2] == "*" || bannedipsplit[2] == ip[2])
										if (ip[3] == "*" || bannedipsplit[3] == ip[3])
											banlist.Add(ban);
						}

						if (banlist.Count > 0)
							args.Player.SendInfoMessage("Banned players found: {0}", string.Join(", ", banlist.Select(p => p.Name)));
						else
							args.Player.SendErrorMessage("No banned players found by that IP.");

						return;
					}
					else
					{
						args.Player.SendErrorMessage("Invalid syntax: /bansearch <-un/-ip> <username/IP>");
						return;
					}
				}
				else
				{
					args.Player.SendErrorMessage("Invalid syntax: /bansearch <-un/-ip> <username/IP>");
					return;
				}
			}
		}

		private void ZAXID(CommandArgs args)
		{
			var players = (from player in TShock.Players where player != null select player);
			args.Player.SendInfoMessage("Online players: {0}", string.Join(", ", players.Select(p => "(" + p.Name + ", " + p.Index.ToString() + ")")));
		}

		private void ZAUserGroups(CommandArgs args)
		{
			if (args.Parameters.Count == 1 && args.Parameters[0] == "reload" && args.Player.Group.HasPermission("zadmin.reload"))
			{
				LoadConfig();
				args.Player.SendSuccessMessage("ZAdminConfig.json reloaded successfully!");
				return;
			}

			int highestrank = -1;

			foreach (KeyValuePair<int, string> kvp in config.groupranks)
			{
				if (kvp.Key > highestrank)
					highestrank = kvp.Key;
			}

			if ((highestrank > config.highestRankToDisplay && !(args.Parameters.Count == 1 && args.Parameters[0] == "all")))
			{
				highestrank = config.highestRankToDisplay;
			}

			args.Player.SendSuccessMessage("Online Players:");
			for (int i = 1; i < highestrank + 1; i++)
			{
				var grouprank = (from kvp in config.groupranks where kvp.Key == i select kvp.Value);

				if (grouprank.Count() == 0)
					continue;

				var playersingroup = (from player in TShock.Players where player != null && player.Group.Name == grouprank.First() select player.Name);

				Group group = TShock.Groups.GetGroupByName(grouprank.First());

				if (group == null)
					TShock.Log.Warn("Unknown group name in ZAdminConfig.json at rank {0}: {1}", i.ToString(), grouprank.First());

				if (playersingroup.Count() == 0)
					continue;

				if (!config.showGroupNameInsteadOfPrefix)
					args.Player.SendMessage("{0}: {1}".SFormat(group.Prefix, string.Join(", ", playersingroup)), new Color(group.R, group.G, group.B));
				else
					args.Player.SendMessage("{0}: {1}".SFormat(group.Name, string.Join(", ", playersingroup)), new Color(group.R, group.G, group.B));
			}
		}

		private void ZAPMute(CommandArgs args)
		{
			if (args.Parameters.Count == 0 || args.Parameters.Count > 2)
			{
				args.Player.SendErrorMessage("Invalid syntax:");
				args.Player.SendErrorMessage("/pmute <player>");
				args.Player.SendErrorMessage("/pmute -list");
				args.Player.SendErrorMessage("/pmute -clear");
				args.Player.SendErrorMessage("/pmute -check <player>");
			}
			else if (args.Parameters.Count == 1)
			{
				if (args.Parameters[0] == "-all" || args.Parameters[0] == "-list")
				{
					var pmuted = (from player in TShock.Players where player != null && permamuted.Contains(player.IP) select player.Name);
					var muted = (from player in TShock.Players where player != null && !permamuted.Contains(player.IP) && player.mute select player.Name);
					
					if (pmuted.Count() > 0)
						args.Player.SendInfoMessage("Permamuted players: {0}", string.Join(", ", pmuted));
					else
						args.Player.SendInfoMessage("No permamuted players.");
					if (muted.Count() > 0)
						args.Player.SendInfoMessage("Muted players: {0}", string.Join(", ", muted));
					else
						args.Player.SendInfoMessage("No muted players.");

					return;
				}

				List<TSPlayer> mutedplayerlist = TShock.Utils.FindPlayer(args.Parameters[0]);

				if (mutedplayerlist.Count == 0)
					args.Player.SendErrorMessage("No players matched.");
				else if (mutedplayerlist.Count > 1)
					TShock.Utils.SendMultipleMatchError(args.Player, mutedplayerlist.Select(p => p.Name));
				else
				{
					TSPlayer mutedplayer = mutedplayerlist[0];
					if (mutedplayer.mute)
					{
						if (permamuted.Contains(mutedplayer.IP))
						{
							mutedplayer.mute = false;
							permamuted.Remove(mutedplayer.IP);
								mutedplayer.SendInfoMessage("You are no longer muted.");
							if (!args.Silent)
								TSPlayer.All.SendInfoMessage("{0} has unmuted {1}!", args.Player.Name, mutedplayer.Name);
							args.Player.SendSuccessMessage("{0} has been unmuted.", mutedplayer.Name);
						}
						else
						{
							permamuted.Add(mutedplayer.IP);
							if (!args.Silent)
								TSPlayer.All.SendInfoMessage("{0} has permamuted {1}!", args.Player.Name, mutedplayer.Name);
							mutedplayer.SendInfoMessage("You have been permamuted!");
							args.Player.SendSuccessMessage("{0}'s mute has been made permanent.", mutedplayer.Name);
						}
					}
					else if (mutedplayer.Group.HasPermission(Permissions.mute) && !args.Player.Group.HasPermission("zadmin.muteall"))
					{
						args.Player.SendErrorMessage("You cannot mute this player.");
					}
					else
					{
						mutedplayer.mute = true;
						permamuted.Add(mutedplayer.IP);
						if (!args.Silent)
							TSPlayer.All.SendInfoMessage("{0} has permamuted {1}!", args.Player.Name, mutedplayer.Name);
						mutedplayer.SendInfoMessage("You have been permamuted!");
						args.Player.SendSuccessMessage("{0} has been permamuted.", mutedplayer.Name);
					}
				}
			}
			else
			{
				if (args.Parameters[0] == "-check")
				{
					List<TSPlayer> mutedplayerlist = TShock.Utils.FindPlayer(args.Parameters[1]);

					if (mutedplayerlist.Count == 0)
						args.Player.SendErrorMessage("No players matched.");
					else if (mutedplayerlist.Count > 1)
						TShock.Utils.SendMultipleMatchError(args.Player, mutedplayerlist.Select(p => p.Name));
					else
					{
						TSPlayer mutedplayer = mutedplayerlist[0];
						if (mutedplayer.mute)
						{
							if (permamuted.Contains(mutedplayer.IP))
							{
								args.Player.SendInfoMessage("{0} is permamuted.", mutedplayer.Name);
							}
							else
							{
								args.Player.SendInfoMessage("{0} is muted.", mutedplayer.Name);
							}
						}
						else
						{
							args.Player.SendInfoMessage("{0} is not muted.", mutedplayer.Name);
						}
					}
				}
				else if (args.Parameters[0].ToLower() == "-clear")
				{
					int temp = 0;
					if (args.Parameters[1] == "all" || args.Parameters[1].ToLower() == "mute" || args.Parameters[1].ToLower() == "pmute")
					{
						temp = clearMutes(args.Parameters[1]);
						if (!args.Silent)
						{
							if (args.Parameters[1] == "all")
								TSPlayer.All.SendInfoMessage("{0} has unmuted everyone!", args.Player.Name);
							else if (args.Parameters[1] == "mute")
								TSPlayer.All.SendInfoMessage("{0} has unmuted everyone that isn't permamuted!", args.Player.Name);
							else
								TSPlayer.All.SendInfoMessage("{0} has unmuted everyone that was permamuted!", args.Player.Name);
						}
						args.Player.SendSuccessMessage("{0} players were unmuted.", temp.ToString());
					}
					else
					{
						args.Player.SendErrorMessage("Invalid syntax:");
						args.Player.SendErrorMessage("/pmute <player>");
						args.Player.SendErrorMessage("/pmute list");
						args.Player.SendErrorMessage("/pmute clear <mute/pmute/all>");
						args.Player.SendErrorMessage("/pmute check <player>");
					}
				}
				else
				{
					args.Player.SendErrorMessage("Invalid syntax:");
					args.Player.SendErrorMessage("/pmute <player>");
					args.Player.SendErrorMessage("/pmute -list");
					args.Player.SendErrorMessage("/pmute -clear <mute/pmute/all>");
					args.Player.SendErrorMessage("/pmute -check <player>");
				}
			}
		}

		private static void ZADayregion(CommandArgs args)
		{
			if (args.Parameters.Count > 1)
			{
				var region = TShock.Regions.GetRegionByName(args.Parameters[1]);
				if (region != null && region.Name != "")
				{
					if (args.Parameters[0] == "add")
					{
						RegionList.Add(region);
						args.Player.SendMessage(String.Format("Region '{0}' added to Day Region list", region.Name), Color.BurlyWood);
						return;
					}
					else if (args.Parameters[0] == "del")
					{
						DayRegions_Delete(region.Name);
						args.Player.SendMessage(String.Format("Region '{0}' deleted from Day Region list", region.Name), Color.BurlyWood);
						return;
					}
				}
				else
				{
					args.Player.SendErrorMessage("Region '{0}' not found", args.Parameters[1]);
				}
			}

			args.Player.SendErrorMessage("Invalid syntax: /dayregion <add/del> <region name>");
		}

		private void ZAPTime(CommandArgs args)
		{
			Time temp = new Time() { frames = -2, day = true };

			if (args.Parameters.Count == 1)
			{
				switch (args.Parameters[0].ToLower())
				{
					case "day":
						temp.day = true;
						temp.frames = Time.DAY;
						break;
					case "night":
						temp.day = false;
						temp.frames = Time.NIGHT;
						break;
					case "noon":
						temp.day = true;
						temp.frames = Time.NOON;
						break;
					case "midnight":
						temp.day = false;
						temp.frames = Time.MIDNIGHT;
						break;
					case "off":
						temp.day = true;
						temp.frames = -1;
						break;
					default:
						break;
				}
			}

			if (temp.frames == -1)
			{
				if (playertime.ContainsKey(args.Player.Index))
				{
					playertime.Remove(args.Player.Index);
					args.Player.SendSuccessMessage("Set your time to server time.");
					SendData(new SendDataEventArgs() { MsgId = PacketTypes.WorldInfo, Handled = false, remoteClient = -1 });
					return;
				}
				else
				{
					args.Player.SendErrorMessage("Your time is already the server time!");
					return;
				}
			}

			if (temp.frames == -2)
			{
				args.Player.SendErrorMessage("Invalid usage: /ptime <day/noon/night/midnight/off>");
				return;
			}

			if (playertime.ContainsKey(args.Player.Index))
				playertime[args.Player.Index] = temp;
			else
				playertime.Add(args.Player.Index, temp);

			SendData(new SendDataEventArgs() { MsgId = PacketTypes.WorldInfo, Handled = false, remoteClient = -1 });

			args.Player.SendSuccessMessage("Set your personal time to {0}.", args.Parameters[0]);
		}
		#endregion

		private int clearMutes(string type)
		{
			int count = 0;

			switch (type)
			{
				case "all":
					permamuted.Clear();
					foreach (TSPlayer player in TShock.Players)
					{
						if (player != null && player.mute)
						{
							player.mute = false;
							count++;
						}
					}
					break;
				case "pmute":
					foreach (TSPlayer player in TShock.Players)
					{
						if (player != null && permamuted.Contains(player.IP))
						{
							player.mute = false;
							count++;
						}
					}
					permamuted.Clear();
					break;
				case "mute":
					foreach (TSPlayer player in TShock.Players)
					{
						if (player != null && player.mute)
						{
							//While this temporarily unmutes permamuted players, they will be muted again in one second or less.
							player.mute = false;
							count++;
						}
					}
					break;
				default:
					break;
			}

			return count;
		}

		#region Database
		private void SetupDb()
		{
			if (TShock.Config.StorageType.ToLower() == "sqlite")
			{
				string sql = Path.Combine(TShock.SavePath, "Dayregions.sqlite");
				db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
			}
			else if (TShock.Config.StorageType.ToLower() == "mysql")
			{
				try
				{
					var hostport = TShock.Config.MySqlHost.Split(':');
					db = new MySqlConnection()
					{
						ConnectionString =
						String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
									  hostport[0],
									  hostport.Length > 1 ? hostport[1] : "3306",
									  TShock.Config.MySqlDbName,
									  TShock.Config.MySqlUsername,
									  TShock.Config.MySqlPassword
							)
					};
				}
				catch (MySqlException ex)
				{
					TShock.Log.Error(ex.ToString());
					throw new Exception("MySql not setup correctly");
				}
			}
			else
			{
				throw new Exception("Invalid storage type");
			}

			SqlTableCreator SQLcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

			var table = new SqlTable("Dayregions",
				new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true, NotNull = true },
				new SqlColumn("Region", MySqlDbType.VarChar) { Unique = true, Length = 30 }
			);
			SQLcreator.EnsureTableStructure(table);
		}

		private static void DayRegions_Read()
		{
			QueryResult reader;
			lock (db)
			{
				reader = db.QueryReader("SELECT Region FROM DayRegions");
			}
			lock (RegionList)
			{
				while (reader.Read())
				{
					var region = TShock.Regions.GetRegionByName(reader.Get<string>("Region"));
					if (region != null && region.Name != "")
						RegionList.Add(region);
					else
						DayRegions_Delete(reader.Get<string>("Region"));
				}
				reader.Dispose();
			}
		}

		private static void DayRegions_Add(string name)
		{
			db.Query("INSERT INTO DayRegions SET (Region) VALUES @0;", name);
		}
		private static void DayRegions_Delete(string name)
		{
			lock (db)
			{
				db.Query("DELETE FROM DayRegions WHERE Region = @0", name);
			}
			lock (RegionList)
			{
				RegionList.RemoveAll(p => p.Name == name);
			}
		}
		#endregion
	}
}
