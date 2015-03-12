using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace ZAdminCmds
{
	[ApiVersion(1,17)]
    public class ZAdmin : TerrariaPlugin
    {
		public override string Name { get { return "ZAdminCmds"; } }
		public override string Author { get { return "Zaicon"; } }
		public override string Description { get { return "Misc Commands"; } }
		public override Version Version { get { return new Version(1, 0, 0, 0); } }

		public static Config config = new Config();
		public static string configpath = "tshock/ZAdmin.json";

		private Timer mutecheck;
		public static List<string> permamuted = new List<string>();


		public ZAdmin(Main game)
			: base(game)
		{
			base.Order = 1;
		}

		#region Initialize/Dispose
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
		}

		protected override void Dispose(bool Disposing)
		{
			if (Disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
			}
			base.Dispose(Disposing);
		}
		#endregion

		#region Hooks
		private void OnInitialize(EventArgs args)
		{
			config = config.Read(configpath);

			mutecheck = new Timer { Interval = 1000, AutoReset = true, Enabled = true };
			mutecheck.Elapsed += new ElapsedEventHandler(OnUpdate);

			Commands.ChatCommands.Add(new Command("zadmin.baninfo", ZABanInfo, "baninfo"));
			Commands.ChatCommands.Add(new Command("zadmin.baninfo", ZABanSearch, "bansearch"));
			Commands.ChatCommands.Add(new Command("zadmin.xid", ZAXID, "xid"));
			Commands.ChatCommands.Add(new Command(ZAUserGroups, "usergroups", "ug"));
			Commands.ChatCommands.Add(new Command("zadmin.pmute", ZAPMute, "pmute"));
		}

		private void OnUpdate(object sender, ElapsedEventArgs args)
		{
			foreach (TSPlayer player in TShock.Players)
				if (player != null)
				{
					if (permamuted.Contains(player.IP))
						player.mute = true;
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

				var playersingroup = (from player in TShock.Players where player != null && player.Group.Name == grouprank.First() select player.Name);

				if (playersingroup.Count() == 0)
					continue;

				Group group = TShock.Groups.GetGroupByName(grouprank.First());

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
	}
}
