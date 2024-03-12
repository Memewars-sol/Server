using System;
using Npgsql;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using WebUtils;
using Models;

namespace Memewars.RealtimeNetworking.Server
{
    class Database
    {

        #region Main Data And Methods

        public class Credential
        {
            public string dbIP { get; set; }
            public string dbPort { get; set; }
            public string dbUsername { get; set; }
            public string dbPassword { get; set; }
            public string dbName { get; set; }
        }

        public static Credential GetDbCredentials()
        {
            using (StreamReader r = new StreamReader("configs/credentials.json"))
            {
                string json = r.ReadToEnd();
                Credential credential = JsonConvert.DeserializeObject<Credential>(json);
                return credential;
            }
        }

        private static string dbIP;
        private static string dbPort;
        private static string dbUsername;
        private static string dbPassword;
        private static string dbName;

        static Database()
        {
            var credential = GetDbCredentials();
            dbIP = credential.dbIP;
            dbPort = credential.dbPort;
            dbUsername = credential.dbUsername;
            dbPassword = credential.dbPassword;
            dbName = credential.dbName;
        }

        public static NpgsqlConnection GetDbConnection()
        {
            var cs = string.Format("Host={0};Port={1};User ID={2};Password={3};Database={4};", dbIP, dbPort, dbUsername, dbPassword, dbName);
            var con = new NpgsqlConnection(cs);
            con.Open();
            return con;
        }

        private static DateTime collectTime = DateTime.Now;
        private static bool collecting = false;
        private static double collectPeriod = 5d;

        private static DateTime updateTime = DateTime.Now;
        private static bool updating = false;
        private static double updatePeriod = 0.1d;

        private static DateTime obstaclesTime = DateTime.Now;
        private static bool obstaclesUpdating = false;
        private static double obstaclesPeriod = 86400d;

        private static int players_ranking_per_page = 30;

        public static void Update()
        {
            if (!collecting)
            {
                double deltaTime = (DateTime.Now - collectTime).TotalSeconds;
                if (deltaTime >= collectPeriod)
                {
                    collecting = true;
                    collectTime = DateTime.Now;
                    UpdateCollectabes(deltaTime);
                }
            }
            if (!updating)
            {
                double deltaTime = (DateTime.Now - updateTime).TotalSeconds;
                if (deltaTime >= updatePeriod)
                {
                    updating = true;
                    updateTime = DateTime.Now;
                    GeneralUpdate(deltaTime);
                }
            }
            if (!obstaclesUpdating)
            {
                if ((DateTime.Now - obstaclesTime).TotalSeconds >= obstaclesPeriod)
                {
                    obstaclesUpdating = true;
                    obstaclesTime = DateTime.Now;
                    ObstaclesCreation();
                }
            }
        }

        public static void Initialize()
        {
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("UPDATE accounts SET is_online = 0, client_id = 0;");
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
        }

        public async static void PlayerDisconnected(int id)
        {
            long account_id = Server.clients[id].account;
            if (account_id > 0)
            {
                await PlayerDisconnectedAsync(account_id);
                EndBattle(account_id, true, 0);
            }
        }

        private async static Task<bool> PlayerDisconnectedAsync(long account_id)
        {
            Task<bool> task = Task.Run(() =>
            {
                return Retry.Do(() => _PlayerDisconnectedAsync(account_id), TimeSpan.FromSeconds(0.1), 100, false);
            });
            return await task;
        }

        private static bool _PlayerDisconnectedAsync(long account_id)
        {
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("UPDATE accounts SET is_online = 0, client_id = 0 WHERE id = {0}", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
            return true;
        }

        #endregion

        #region Player

        public async static void AuthenticatePlayer(int id, string address)
        {
            InitializationData auth = await Account.GetInitializationData(id, address);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.AUTH);
            if (auth != null)
            {
                Server.clients[id].address = address;
                Server.clients[id].account = auth.accountID;
                auth.versions = Terminal.clientVersions;
                string authData = await Data.SerializeAsync(auth);
                byte[] authBytes = await Data.CompressAsync(authData);
                packet.Write(1);
                packet.Write(authBytes.Length);
                packet.Write(authBytes);
                int battles = await GetUnreadBattleReportsAsync(auth.accountID);
                packet.Write(battles);
            }
            else
            {
                packet.Write(0);
            }
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> GetUnreadBattleReportsAsync(long id)
        {
            Task<int> task = Task.Run(() =>
            {
                int count = 0;
                using (NpgsqlConnection connection = GetDbConnection())
                {
                    string query = String.Format("SELECT COUNT(id) AS count FROM battles WHERE defender_id = {0} AND seen <= 0;", id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    int.TryParse(reader["count"].ToString(), out count);
                                }
                            }
                        }
                    }
                    connection.Close();
                }
                return count;
            });
            return await task;
        }

        public async static void SyncPlayerData(int id)
        {
            long account_id = Server.clients[id].account;
            Player player = await GetPlayerDataAsync(account_id);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SYNC);
            if (player != null)
            {
                packet.Write(1);
                List<Building> buildings = await GetBuildingsAsync(account_id);
                player.units = await GetUnitsAsync(account_id);
                player.spells = await GetSpellsAsync(account_id);
                player.buildings = buildings;
                string playerData = await Data.SerializeAsync<Player>(player);
                byte[] playerBytes = await Data.CompressAsync(playerData);
                packet.Write(playerBytes.Length);
                packet.Write(playerBytes);
            }
            else
            {
                packet.Write(0);
            }
            Sender.TCP_Send(id, packet);
        }

        public async static void ChangePlayerName(int id, string name)
        {
            long account_id = Server.clients[id].account;
            if(account_id > 0)
            {
                int response = await ChangePlayerNameAsync(account_id, name);
                Packet packet = new Packet();
                packet.Write((int)Terminal.RequestsID.RENAME);
                packet.Write(response);
                Sender.TCP_Send(id, packet);
            }
        }

        private async static Task<int> ChangePlayerNameAsync(long id, string name)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _ChangePlayerNameAsync(id, name), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _ChangePlayerNameAsync(long id, string name)
        {
            int response = 0;
            if (!string.IsNullOrEmpty(name))
            {
                using (NpgsqlConnection connection = GetDbConnection())
                {
                    string query = String.Format("UPDATE accounts SET name = '{0}' WHERE id = {1};", name, id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                    response = 1;
                }
            }
            return response;
        }

        private async static Task<Player> GetPlayerDataAsync(long id)
        {
            Task<Player> task = Task.Run(() =>
            {
                return Retry.Do(() => Account.Get(id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        public async static void GetPlayersRanking(int id, int page)
        {
            long account_id = Server.clients[id].account;
            Data.PlayersRanking response = await GetPlayersRankingAsync(page, account_id);
            string rawData = await Data.SerializeAsync<Data.PlayersRanking>(response);
            byte[] bytes = await Data.CompressAsync(rawData);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.PLAYERSRANK);
            packet.Write(bytes.Length);
            packet.Write(bytes);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.PlayersRanking> GetPlayersRankingAsync(int page, long account_id = 0)
        {
            Task<Data.PlayersRanking> task = Task.Run(() =>
            {
                Data.PlayersRanking response = new Data.PlayersRanking();
                response = Retry.Do(() => _GetPlayersRankingAsync(page, account_id), TimeSpan.FromSeconds(0.1), 1, false);
                if (response == null)
                {
                    response = new Data.PlayersRanking();
                    response.players = new List<Data.PlayerRank>();
                }
                return response;
            });
            return await task;
        }

        private static Data.PlayersRanking _GetPlayersRankingAsync(int page, long account_id = 0)
        {
            Data.PlayersRanking response = new Data.PlayersRanking();
            using (NpgsqlConnection connection = GetDbConnection())
            {
                int playersCount = 0;
                string query = "SELECT COUNT(*) AS count FROM accounts";
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                int.TryParse(reader["count"].ToString(), out playersCount);
                            }
                        }
                    }
                }

                response.pagesCount = Convert.ToInt32(Math.Ceiling((double)playersCount / (double)players_ranking_per_page));

                if (response.pagesCount > 0)
                {
                    if (page == 0 && account_id > 0)
                    {
                        page = 1;
                        int playerRank = GetPlayerRank(connection, account_id);
                        if (playerRank > 0)
                        {
                            page = Convert.ToInt32(Math.Ceiling((double)playerRank / (double)players_ranking_per_page));
                        }
                    }
                    else if(page <= 0)
                    {
                        page = 1;
                    }

                    response.page = page;
                }
                connection.Close();
            }
            return response;
        }

        private static int GetPlayerRank(NpgsqlConnection connection, long account_id)
        {
            int rank = 0;
            string query = String.Format("SELECT id, rank FROM (SELECT id, ROW_NUMBER() OVER(ORDER BY trophies DESC) AS 'rank' FROM accounts) AS ranks WHERE id = {0}", account_id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            int.TryParse(reader["rank"].ToString(), out rank);
                        }
                    }
                }
            }
            return rank;
        }

        #endregion

        #region Helpers

        private static int GetBuildingCount(long accountID, string globalID, NpgsqlConnection connection)
        {
            int count = 0;
            string query = String.Format("SELECT id FROM buildings WHERE account_id = {0} AND global_id = '{1}';", accountID, globalID);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private static int GetBuildingConstructionCount(long accountID, NpgsqlConnection connection)
        {
            int count = 0;
            string query = String.Format("SELECT id FROM buildings WHERE account_id = {0} AND is_constructing > 0;", accountID);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        #endregion

        #region Resource Manager

        private static bool SpendResources(NpgsqlConnection connection, long account_id, int gold, int elixir, int gems, int darkElixir)
        {
            if (CheckResources(connection, account_id, gold, elixir, gems, darkElixir))
            {
                if (gold > 0 || elixir > 0 || darkElixir > 0)
                {
                    List<Building> buildings = new List<Building>();
                    string query = String.Format("SELECT id, global_id, gold_storage, elixir_storage, dark_elixir_storage FROM buildings WHERE account_id = {0} AND global_id IN('townhall', 'goldstorage', 'elixirstorage', 'darkelixirstorage');", account_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Building building = new Building();
                                    building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                                    building.goldStorage = (int)Math.Floor(float.Parse(reader["gold_storage"].ToString()));
                                    building.elixirStorage = (int)Math.Floor(float.Parse(reader["elixir_storage"].ToString()));
                                    building.darkStorage = (int)Math.Floor(float.Parse(reader["dark_elixir_storage"].ToString()));
                                    building.databaseID = long.Parse(reader["id"].ToString());
                                    buildings.Add(building);
                                }
                            }
                        }
                    }
                    if (buildings.Count > 0)
                    {
                        int spendGold = 0;
                        int spendElixir = 0;
                        int spendDarkElixir = 0;
                        for (int i = 0; i < buildings.Count; i++)
                        {
                            if (spendGold >= gold && spendElixir >= elixir && spendDarkElixir >= darkElixir)
                            {
                                break;
                            }
                            int toSpendGold = 0;
                            int toSpendElixir = 0;
                            int toSpendDark = 0;
                            switch (buildings[i].id)
                            {
                                case BuildingID.townhall:
                                    if (spendGold < gold)
                                    {
                                        if (buildings[i].goldStorage >= (gold - spendGold))
                                        {
                                            toSpendGold = gold - spendGold;
                                        }
                                        else
                                        {
                                            toSpendGold = buildings[i].goldStorage;
                                        }
                                        spendGold += toSpendGold;
                                    }
                                    if (spendElixir < elixir)
                                    {
                                        if (buildings[i].elixirStorage >= (elixir - spendElixir))
                                        {
                                            toSpendElixir = elixir - spendElixir;
                                        }
                                        else
                                        {
                                            toSpendElixir = buildings[i].elixirStorage;
                                        }
                                        spendElixir += toSpendElixir;
                                    }
                                    if (spendDarkElixir < darkElixir)
                                    {
                                        if (buildings[i].darkStorage >= (darkElixir - spendDarkElixir))
                                        {
                                            toSpendDark = darkElixir - spendDarkElixir;
                                        }
                                        else
                                        {
                                            toSpendDark = buildings[i].darkStorage;
                                        }
                                        spendDarkElixir += toSpendDark;
                                    }
                                    break;
                                case BuildingID.goldstorage:
                                    if (spendGold < gold)
                                    {
                                        if (buildings[i].goldStorage >= (gold - spendGold))
                                        {
                                            toSpendGold = gold - spendGold;
                                        }
                                        else
                                        {
                                            toSpendGold = buildings[i].goldStorage;
                                        }
                                        spendGold += toSpendGold;
                                    }
                                    break;
                                case BuildingID.elixirstorage:
                                    if (spendElixir < elixir)
                                    {
                                        if (buildings[i].elixirStorage >= (elixir - spendElixir))
                                        {
                                            toSpendElixir = elixir - spendElixir;
                                        }
                                        else
                                        {
                                            toSpendElixir = buildings[i].elixirStorage;
                                        }
                                        spendElixir += toSpendElixir;
                                    }
                                    break;
                                case BuildingID.darkelixirstorage:
                                    if (spendDarkElixir < darkElixir)
                                    {
                                        if (buildings[i].darkStorage >= (darkElixir - spendDarkElixir))
                                        {
                                            toSpendDark = darkElixir - spendDarkElixir;
                                        }
                                        else
                                        {
                                            toSpendDark = buildings[i].darkStorage;
                                        }
                                        spendDarkElixir += toSpendDark;
                                    }
                                    break;
                            }
                            query = String.Format("UPDATE buildings SET gold_storage = gold_storage - {0}, elixir_storage = elixir_storage - {1}, dark_elixir_storage = dark_elixir_storage - {2} WHERE id = {3};", toSpendGold, toSpendElixir, toSpendDark, buildings[i].databaseID);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        if (spendGold < gold || spendElixir < elixir || spendDarkElixir < darkElixir)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                if (gems > 0)
                {
                    string query = String.Format("UPDATE accounts SET gems = gems - {0} WHERE id = {1};", gems, account_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        private static bool CheckResources(NpgsqlConnection connection, long account_id, int gold, int elixir, int gems, int darkElixir)
        {
            int haveGold = 0;
            int haveElixir = 0;
            int haveGems = 0;
            int haveDarkElixir = 0;

            if (gold > 0 || elixir > 0 || darkElixir > 0)
            {
                string query = String.Format("SELECT global_id, gold_storage, elixir_storage, dark_elixir_storage FROM buildings WHERE account_id = {0} AND global_id IN('townhall', 'goldstorage', 'elixirstorage', 'darkelixirstorage');", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                BuildingID id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                                int gold_storage = (int)Math.Floor(float.Parse(reader["gold_storage"].ToString()));
                                int elixir_storage = (int)Math.Floor(float.Parse(reader["elixir_storage"].ToString()));
                                int dark_elixir_storage = (int)Math.Floor(float.Parse(reader["dark_elixir_storage"].ToString()));
                                switch (id)
                                {
                                    case BuildingID.townhall:
                                        haveGold += gold_storage;
                                        haveElixir += elixir_storage;
                                        haveDarkElixir += dark_elixir_storage;
                                        break;
                                    case BuildingID.goldstorage:
                                        haveGold += gold_storage;
                                        break;
                                    case BuildingID.elixirstorage:
                                        haveElixir += elixir_storage;
                                        break;
                                    case BuildingID.darkelixirstorage:
                                        haveDarkElixir += dark_elixir_storage;
                                        break;
                                }
                            }
                        }
                    }
                }
                if (haveGold < gold || haveElixir < elixir || haveDarkElixir < darkElixir)
                {
                    return false;
                }
            }

            if (gems > 0)
            {
                string query = String.Format("SELECT gems FROM accounts WHERE id = {0}", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                haveGems = int.Parse(reader["gems"].ToString());
                            }
                        }
                    }
                }
                if (haveGems < gems)
                {
                    return false;
                }
            }

            return true;
        }

        private static (int, int, int, int) AddResources(NpgsqlConnection connection, long account_id, int gold, int elixir, int darkElixir, int gems)
        {
            int addedGold = 0;
            int addedElixir = 0;
            int addedDark = 0;

            if (gold > 0 || elixir > 0 || darkElixir > 0)
            {
                List<Building> storages = new List<Building>();
                string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0} AND buildings.global_id IN('{1}', '{2}', '{3}', '{4}') AND buildings.level > 0;", account_id, BuildingID.townhall.ToString(), BuildingID.goldstorage.ToString(), BuildingID.elixirstorage.ToString(), BuildingID.darkelixirstorage.ToString());
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                Building building = new Building();
                                building.databaseID = long.Parse(reader["id"].ToString());
                                building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                                building.goldStorage = (int)Math.Floor(float.Parse(reader["gold_storage"].ToString()));
                                building.elixirStorage = (int)Math.Floor(float.Parse(reader["elixir_storage"].ToString()));
                                building.darkStorage = (int)Math.Floor(float.Parse(reader["dark_elixir_storage"].ToString()));
                                building.goldCapacity = int.Parse(reader["gold_capacity"].ToString());
                                building.elixirCapacity = int.Parse(reader["elixir_capacity"].ToString());
                                building.darkCapacity = int.Parse(reader["dark_elixir_capacity"].ToString());
                                storages.Add(building);
                            }
                        }
                    }
                }

                if (storages.Count > 0)
                {
                    int remainedGold = gold;
                    int remainedElixir = elixir;
                    int remainedDatk = darkElixir;
                    for (int i = 0; i < storages.Count; i++)
                    {
                        if (remainedGold <= 0 && remainedElixir <= 0 && remainedDatk <= 0)
                        {
                            break;
                        }

                        int goldSpace = storages[i].goldCapacity - storages[i].goldStorage;
                        int elixirSpace = storages[i].elixirCapacity - storages[i].elixirStorage;
                        int darkSpace = storages[i].darkCapacity - storages[i].darkStorage;

                        int addGold = 0;
                        int addElixir = 0;
                        int addDark = 0;

                        switch (storages[i].id)
                        {
                            case BuildingID.townhall:
                                addGold = (goldSpace >= remainedGold) ? remainedGold : goldSpace;
                                addElixir = (elixirSpace >= remainedElixir) ? remainedElixir : elixirSpace;
                                addDark = (darkSpace >= remainedDatk) ? remainedDatk : darkSpace;
                                break;
                            case BuildingID.goldstorage:
                                addGold = (goldSpace >= remainedGold) ? remainedGold : goldSpace;
                                break;
                            case BuildingID.elixirstorage:
                                addElixir = (elixirSpace >= remainedElixir) ? remainedElixir : elixirSpace;
                                break;
                            case BuildingID.darkelixirstorage:
                                addDark = (darkSpace >= remainedDatk) ? remainedDatk : darkSpace;
                                break;
                        }

                        query = String.Format("UPDATE buildings SET gold_storage = gold_storage + {0}, elixir_storage = elixir_storage + {1}, dark_elixir_storage = dark_elixir_storage + {2} WHERE id = {3};", addGold, addElixir, addDark, storages[i].databaseID);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }

                        remainedGold -= addGold;
                        remainedElixir -= addElixir;
                        remainedDatk -= addDark;

                        addedGold += addGold;
                        addedElixir += addElixir;
                        addedDark += addDark;
                    }
                }
            }

            if (gems > 0)
            {
                string query = String.Format("UPDATE accounts SET gems = gems + {0} WHERE id = {1};", gems, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            return (addedGold, addedElixir, addedDark, gems);
        }

        private static void AddXP(NpgsqlConnection connection, long account_id, int xp)
        {
            int haveXp = 0;
            int level = 0;
            string query = String.Format("SELECT xp, level FROM accounts WHERE id = {0}", account_id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            int.TryParse(reader["xp"].ToString(), out haveXp);
                            int.TryParse(reader["level"].ToString(), out level);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
            int reachedLevel = level;
            int reqXp = Data.GetNexLevelRequiredXp(reachedLevel);
            int remainedXp = haveXp + xp;
            while (remainedXp >= reqXp)
            {
                remainedXp -= reqXp;
                reachedLevel++;
                reqXp = Data.GetNexLevelRequiredXp(reachedLevel);
            }
            query = String.Format("UPDATE accounts SET level = {0}, xp = {1} WHERE id = {2} AND level = {3} AND xp = {4}", reachedLevel, remainedXp, account_id, level, haveXp);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static void ChangeClanTrophies(NpgsqlConnection connection, long clan_id, int amount)
        {
            if (amount == 0) { return; }
            if (amount > 0)
            {
                string query = String.Format("UPDATE clans SET trophies = trophies + {0} WHERE id = {1}", amount, clan_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                string query = String.Format("UPDATE clans SET trophies = trophies - {0} WHERE id = {1}", -amount, clan_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
                query = String.Format("UPDATE clans SET trophies = 0 WHERE id = {0} AND trophies < 0", clan_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void ChangeTrophies(NpgsqlConnection connection, long account_id, int amount)
        {
            if (amount == 0) { return; }
            if (amount > 0)
            {
                string query = String.Format("UPDATE accounts SET trophies = trophies + {0} WHERE id = {1}", amount, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            else
            {
                string query = String.Format("UPDATE accounts SET trophies = trophies - {0} WHERE id = {1}", -amount, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
                query = String.Format("UPDATE accounts SET trophies = 0 WHERE id = {0} AND trophies < 0", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private async static Task<bool> AddShieldAsync(long account_id, int seconds)
        {
            Task<bool> task = Task.Run(() =>
            {
                return Retry.Do(() => _AddShieldAsync(account_id, seconds), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static bool AddShield(NpgsqlConnection connection, long account_id, int seconds)
        {
            if (seconds <= 0) { return false; }
            bool haveShield = false;
            string query = String.Format("SELECT shield FROM accounts WHERE id = {0} AND shield > NOW() at time zone 'utc'", account_id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        haveShield = true;
                    }
                }
            }
            if (haveShield)
            {
                query = String.Format("UPDATE accounts SET shield = shield + INTERVAL '{0} SECOND' WHERE id = {1};", seconds, account_id);
            }
            else
            {
                query = String.Format("UPDATE accounts SET shield = NOW() at time zone 'utc' + INTERVAL '{0} SECOND' WHERE id = {1};", seconds, account_id);
            }
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
            return true;
        }

        private static bool _AddShieldAsync(long account_id, int seconds)
        {
            using (NpgsqlConnection connection = GetDbConnection())
            {
                return AddShield(connection, account_id, seconds);
            }
        }

        private async static Task<bool> RemoveShieldAsync(long account_id)
        {
            Task<bool> task = Task.Run(() =>
            {
                return Retry.Do(() => _RemoveShieldAsync(account_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static bool RemoveShield(NpgsqlConnection connection, long account_id)
        {
            string query = String.Format("UPDATE accounts SET shield = NOW() at time zone 'utc' - INTERVAL '1 SECOND' WHERE id = {0};", account_id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
            return true;
        }

        private static bool _RemoveShieldAsync(long account_id)
        {
            using (NpgsqlConnection connection = GetDbConnection())
            {
                return RemoveShield(connection, account_id);
            }
        }

        public async static void BoostResource(int id, long building_id)
        {
            long account_id = Server.clients[id].account;
            int res = await BoostResourceAsync(account_id, building_id);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BOOST);
            packet.Write(res);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> BoostResourceAsync(long account_id, long building_id)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _BoostResourceAsync(account_id, building_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _BoostResourceAsync(long account_id, long building_id)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                Building building = null;
                DateTime now = DateTime.Now;
                string query = String.Format("SELECT level, global_id, boost, NOW() at time zone 'utc' as now FROM buildings WHERE id = {0} AND account_id = {1};", building_id, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            building = new Building();
                            while (reader.Read())
                            {
                                DateTime.TryParse(reader["now"].ToString(), out now);
                                DateTime.TryParse(reader["boost"].ToString(), out building.boost);
                                building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                                int.TryParse(reader["level"].ToString(), out building.level);
                            }
                        }
                    }
                }
                if(building != null)
                {
                    int cost = Data.GetBoostResourcesCost(building.id, building.level);
                    if (SpendResources(connection, account_id, 0, 0, cost, 0))
                    {
                        if(building.boost >= now)
                        {
                            query = String.Format("UPDATE buildings SET boost = boost + INTERVAL '24 HOUR' WHERE id = {0}", building_id);
                        }
                        else
                        {
                            query = String.Format("UPDATE buildings SET boost = NOW() at time zone 'utc' + INTERVAL '24 HOUR' WHERE id = {0}", building_id);
                        }
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                        response = 1;
                    }
                }
                connection.Close();
            }
            return response;
        }

        public async static void BuyResources(int id, int pack)
        {
            long account_id = Server.clients[id].account;
            int res = await BuyResourcesAsync(account_id, pack);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BUYRESOURCE);
            packet.Write(res);
            packet.Write(pack);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> BuyResourcesAsync(long account_id, int pack)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _BuyResourcesAsync(account_id, pack), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _BuyResourcesAsync(long account_id, int pack)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                int goldCapacity = 0;
                int elixirCapacity = 0;
                int darkCapacity = 0;
                int gold = 0;
                int elixir = 0;
                int dark = 0;
                string query = String.Format("SELECT buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0} AND buildings.global_id IN ('{1}', '{2}', '{3}', '{4}');", account_id, BuildingID.townhall.ToString(), BuildingID.goldstorage.ToString(), BuildingID.elixirstorage.ToString(), BuildingID.darkelixirstorage.ToString());
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                int result = 0;
                                int.TryParse(reader["gold_capacity"].ToString(), out result);
                                goldCapacity += result;
                                result = 0;
                                int.TryParse(reader["elixir_capacity"].ToString(), out result);
                                elixirCapacity += result;
                                result = 0;
                                int.TryParse(reader["dark_elixir_capacity"].ToString(), out result);
                                darkCapacity += result;
                                result = 0;
                                int.TryParse(reader["gold_storage"].ToString(), out result);
                                gold += result;
                                result = 0;
                                int.TryParse(reader["elixir_storage"].ToString(), out result);
                                elixir += result;
                                result = 0;
                                int.TryParse(reader["dark_elixir_storage"].ToString(), out result);
                                dark += result;
                            }
                        }
                    }
                }

                int tatgetGold = goldCapacity - gold;
                int tatgetElixir = elixirCapacity - elixir;
                int tatgetDark = darkCapacity - dark;

                switch ((Data.BuyResourcePack)pack)
                {
                    case Data.BuyResourcePack.gold_10:
                        tatgetGold = (int)Math.Floor(tatgetGold * 0.1d);
                        tatgetElixir = 0;
                        tatgetDark = 0;
                        break;
                    case Data.BuyResourcePack.gold_50:
                        tatgetGold = (int)Math.Floor(tatgetGold * 0.5d);
                        tatgetElixir = 0;
                        tatgetDark = 0;
                        break;
                    case Data.BuyResourcePack.gold_100:
                        tatgetElixir = 0;
                        tatgetDark = 0;
                        break;
                    case Data.BuyResourcePack.elixir_10:
                        tatgetElixir = (int)Math.Floor(tatgetElixir * 0.1d);
                        tatgetGold = 0;
                        tatgetDark = 0;
                        break;
                    case Data.BuyResourcePack.elixir_50:
                        tatgetElixir = (int)Math.Floor(tatgetElixir * 0.5d);
                        tatgetGold = 0;
                        tatgetDark = 0;
                        break;
                    case Data.BuyResourcePack.elixir_100:
                        tatgetGold = 0;
                        tatgetDark = 0;
                        break;
                    case Data.BuyResourcePack.dark_10:
                        tatgetDark = (int)Math.Floor(tatgetDark * 0.1d);
                        tatgetGold = 0;
                        tatgetElixir = 0;
                        break;
                    case Data.BuyResourcePack.dark_50:
                        tatgetDark = (int)Math.Floor(tatgetDark * 0.5d);
                        tatgetGold = 0;
                        tatgetElixir = 0;
                        break;
                    case Data.BuyResourcePack.dark_100:
                        tatgetGold = 0;
                        tatgetElixir = 0;
                        break;
                }

                if (tatgetGold < 0) { tatgetGold = 0; }
                if (tatgetElixir < 0) { tatgetElixir = 0; }
                if (tatgetDark < 0) { tatgetDark = 0; }

                int cost = Data.GetResourceGemCost(tatgetGold, tatgetElixir, tatgetDark);
                if(SpendResources(connection, account_id, 0, 0, cost, 0))
                {
                    var add = AddResources(connection, account_id, tatgetGold, tatgetElixir, tatgetDark, 0);
                    response = 1;
                }
                connection.Close();
            }
            return response;
        }

        #endregion

        #region Collect Resources

        public async static void UpdateCollectabes(double deltaTime)
        {
            await UpdateCollectabesAsync(deltaTime);
            collecting = false;
        }

        private async static Task<bool> UpdateCollectabesAsync(double deltaTime)
        {
            Task<bool> task = Task.Run(() =>
            {
                return Retry.Do(() => _UpdateCollectabesAsync(deltaTime), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static void _UpdateBuildings(NpgsqlConnection connection, double deltaTime, BuildingID buildingId)
        {
            string storage_type = "";
            switch(buildingId) {
                case BuildingID.goldmine:
                    storage_type = "gold";
                    break;
                case BuildingID.elixirmine:
                    storage_type = "elixir";
                    break;
                case BuildingID.darkelixirmine:
                    storage_type = "dark_elixir";
                    break;
            }

            if(storage_type == "") {
                throw new Exception("Unknown type");
            }

            string query = String.Format(@"
                UPDATE buildings 
                SET {0}_storage = {0}_storage + (server_buildings.speed * {1} * CASE WHEN boost >= NOW() at time zone 'utc' THEN 2 ELSE 1 END) 
                FROM server_buildings 
                WHERE buildings.global_id = server_buildings.global_id 
                AND buildings.level = server_buildings.level 
                AND buildings.global_id = '{2}' AND buildings.level > 0", 
                storage_type,
                deltaTime / 3600d, 
                buildingId.ToString()
            );

            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }


            // Limit Gold
            query = String.Format(@"
                UPDATE buildings 
                SET {0}_storage = server_buildings.{0}_capacity 
                FROM server_buildings
                WHERE buildings.gold_storage > server_buildings.gold_capacity 
                    AND buildings.global_id = '{1}' 
                    AND buildings.level > 0
                    AND buildings.global_id = server_buildings.global_id
                    AND buildings.level = server_buildings.level", 
                    storage_type,
                    BuildingID.goldmine.ToString()
                );
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static bool _UpdateCollectabesAsync(double deltaTime)
        {
            using (NpgsqlConnection connection = GetDbConnection())
            {
                try {
                    _UpdateBuildings(connection, deltaTime, BuildingID.goldmine);
                    _UpdateBuildings(connection, deltaTime, BuildingID.elixirmine);
                    _UpdateBuildings(connection, deltaTime, BuildingID.darkelixirmine);
                }

                catch(Exception exception) {
                    Console.WriteLine(exception.ToString());
                }
                connection.Close();
            }
            return true;
        }

        public async static void Collect(int id, long database_id)
        {
            long account_id = Server.clients[id].account;
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.COLLECT);
            int amount = await CollectAsync(account_id, database_id);
            packet.Write(database_id);
            packet.Write(amount);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> CollectAsync(long account_id, long database_id)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _CollectAsync(account_id, database_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _CollectAsync(long account_id, long database_id)
        {
            int amount = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                int amountGold = 0;
                int amountElixir = 0;
                int amountDark = 0;
                string query = String.Format("SELECT global_id, gold_storage, elixir_storage, dark_elixir_storage FROM buildings WHERE id = {0} AND account_id = {1};", database_id, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                BuildingID global_id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                                switch (global_id)
                                {
                                    case BuildingID.goldmine:
                                        amountGold = (int)Math.Floor(float.Parse(reader["gold_storage"].ToString()));
                                        break;
                                    case BuildingID.elixirmine:
                                        amountElixir = (int)Math.Floor(float.Parse(reader["elixir_storage"].ToString()));
                                        break;
                                    case BuildingID.darkelixirmine:
                                        amountDark = (int)Math.Floor(float.Parse(reader["dark_elixir_storage"].ToString()));
                                        break;
                                }
                            }
                        }
                    }
                }
                if (amountGold > 0)
                {
                    amount = AddResources(connection, account_id, amountGold, 0, 0, 0).Item1;
                    query = String.Format("UPDATE buildings SET gold_storage = gold_storage - {0} WHERE id = {1};", amount, database_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection)) { command.ExecuteNonQuery(); }
                }
                else if (amountElixir > 0)
                {
                    amount = AddResources(connection, account_id, 0, amountElixir, 0, 0).Item2;
                    query = String.Format("UPDATE buildings SET elixir_storage = elixir_storage - {0} WHERE id = {1};", amount, database_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection)) { command.ExecuteNonQuery(); }
                }
                else if (amountDark > 0)
                {
                    amount = AddResources(connection, account_id, 0, 0, amountDark, 0).Item3;
                    query = String.Format("UPDATE buildings SET dark_elixir_storage = dark_elixir_storage - {0} WHERE id = {1};", amount, database_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection)) { command.ExecuteNonQuery(); }
                }
                connection.Close();
            }
            return amount;
        }

        #endregion

        #region General Update

        public async static void GeneralUpdate(double deltaTime)
        {
            await GeneralUpdateAsync(deltaTime);
            updating = false;
        }

        private async static Task<bool> GeneralUpdateAsync(double deltaTime)
        {
            Task<bool> task = Task.Run(() =>
            {
                return Retry.Do(() => _GeneralUpdateAsync(deltaTime), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static bool _GeneralUpdateAsync(double deltaTime)
        {
            using (NpgsqlConnection connection = GetDbConnection())
            {
                GeneralUpdateBuildings(connection);
                GeneralUpdateUnitTraining(connection, (float)deltaTime);
                GeneralUpdateSpellBrewing(connection, (float)deltaTime);
                GeneralUpdateBattle(connection, deltaTime);
                connection.Close();
            }
            return true;
        }

        private static void GeneralUpdateBuildings(NpgsqlConnection connection)
        {
            try {
                string time = "";
                string query = String.Format("SELECT NOW() at time zone 'utc' AS time");
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                time = DateTime.Parse(reader["time"].ToString()).ToString("yyyy-MM-dd H:mm:ss");
                            }
                        }
                    }
                }

                query = String.Format("DELETE FROM buildings WHERE is_constructing > 0 AND construction_time <= NOW() at time zone 'utc' AND global_id = '{0}'", BuildingID.obstacle.ToString());
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }

                query = String.Format("UPDATE buildings SET level = level + 1, is_constructing = 0, track_time = '{0}' WHERE is_constructing > 0 AND construction_time <= NOW() at time zone 'utc' AND global_id <> '{1}'", time, BuildingID.obstacle.ToString());
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }

                Dictionary<long, int> accounts = new Dictionary<long, int>();

                query = String.Format("SELECT buildings.account_id, server_buildings.gained_xp FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.track_time = '{0}'", time);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                long id = 0;
                                int xp = 0;
                                if (long.TryParse(reader["account_id"].ToString(), out id) && int.TryParse(reader["gained_xp"].ToString(), out xp) && xp > 0)
                                {
                                    if(accounts.ContainsKey(id)) {
                                        accounts[id] += xp;
                                        continue;
                                    }
                                    accounts.Add(id, xp);
                                }
                            }
                        }
                    }
                }

                query = String.Format("UPDATE buildings SET track_time = track_time - INTERVAL '1 HOUR' WHERE track_time = '{0}'", time);

                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }

                if (accounts.Count > 0)
                {
                    foreach (var account in accounts)
                    {
                        AddXP(connection, account.Key, account.Value);
                    }
                }
            }

            catch(Exception exception) {
                Console.WriteLine(exception.ToString());
            }
        }

        private static void GeneralUpdateUnitTraining(NpgsqlConnection connection, float deltaTime)
        {
            string query = String.Format(@"UPDATE units 
                                            SET trained = 1 
                                            FROM server_units
                                            WHERE units.trained_time >= server_units.train_time
                                            AND units.global_id = server_units.global_id 
                                            AND units.level = server_units.level ");
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            query = String.Format(@"UPDATE units AS t1 
                                    SET trained_time = trained_time + {0}
                                    FROM (
										/* only train the earliest unit */
                                        SELECT
											units.account_id,
											MIN(units.id) as unit_id
                                        FROM units 
                                        LEFT JOIN server_units 
                                        ON units.global_id = server_units.global_id 
                                        AND units.level = server_units.level 
                                        WHERE units.trained <= 0 
                                        AND units.trained_time < server_units.train_time 
                                        GROUP BY units.account_id
                                    ) t2
                                    WHERE t1.id = t2.unit_id", deltaTime);

            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            query = String.Format(@"UPDATE units AS t1 
                                    SET ready = 1
                                    FROM (
                                        SELECT 
                                            units.id, 
											units.trained,
                                            (CASE WHEN buildings.capacity is null THEN 0 ELSE buildings.capacity END) - (CASE WHEN t.occupied is null THEN 0 ELSE t.occupied END) AS capacity, 
                                            server_units.housing 
                                        FROM units 
                                        LEFT JOIN server_units 
                                            ON units.global_id = server_units.global_id 
                                            AND units.level = server_units.level 
                                        LEFT JOIN (
                                            SELECT buildings.account_id, 
                                            SUM(server_buildings.capacity) AS capacity 
                                            FROM buildings 
                                            LEFT JOIN server_buildings 
                                            ON buildings.global_id = server_buildings.global_id 
                                            AND buildings.level = server_buildings.level 
                                            WHERE buildings.global_id = 'armycamp' 
                                            AND buildings.level > 0 
                                            GROUP BY buildings.account_id
                                        ) AS buildings 
                                        ON units.account_id = buildings.account_id 
                                        LEFT JOIN (
                                            SELECT units.account_id, 
                                            SUM(server_units.housing) AS occupied 
                                            FROM units 
                                            LEFT JOIN server_units 
                                            ON units.global_id = server_units.global_id 
                                            AND units.level = server_units.level 
                                            WHERE units.ready > 0 
                                            GROUP BY units.account_id
                                        ) AS t 
                                        ON units.account_id = t.account_id 
                                        WHERE units.trained > 0 
										AND units.ready <= 0 
                                        GROUP BY units.account_id, units.id, buildings.capacity, t.occupied, server_units.housing
                                    ) t2 
                                    WHERE t1.id = t2.id 
                                    AND housing <= capacity
									AND t1.trained > 0");
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static void GeneralUpdateSpellBrewing(NpgsqlConnection connection, float deltaTime)
        {
            string query = String.Format(@"UPDATE spells 
                                            SET brewed = 1
                                        FROM server_spells 
                                        WHERE spells.global_id = server_spells.global_id 
                                        AND spells.level = server_spells.level 
                                        AND spells.brewed_time >= server_spells.brew_time");
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            query = String.Format(@"UPDATE spells AS t1 
                                    SET brewed_time = brewed_time + {0}
                                    FROM (
                                        SELECT spells.id 
                                        FROM spells 
                                        LEFT JOIN server_spells 
                                        ON spells.global_id = server_spells.global_id 
                                        AND spells.level = server_spells.level 
                                        WHERE spells.brewed <= 0 
                                        AND spells.brewed_time < server_spells.brew_time 
                                        GROUP BY spells.id
                                    ) t2 
                                    WHERE t1.id = t2.id ", deltaTime);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            query = String.Format(@"UPDATE spells AS t1
                                    SET ready = 1
                                    FROM (
                                        SELECT 
                                            spells.id, 
                                            (CASE WHEN buildings.capacity is null then 0 else buildings.capacity end) - (case when t.occupied is null then 0 else t.occupied end) AS capacity, 
                                            server_spells.housing, 
                                            server_spells.building_code 
                                        FROM spells 
                                        LEFT JOIN server_spells 
                                        ON spells.global_id = server_spells.global_id 
                                        AND spells.level = server_spells.level
                                        LEFT JOIN (
                                            SELECT buildings.account_id, 
                                            SUM(server_buildings.capacity) AS capacity 
                                            FROM buildings 
                                            LEFT JOIN server_buildings 
                                            ON buildings.global_id = server_buildings.global_id 
                                            AND buildings.level = server_buildings.level 
                                            WHERE buildings.global_id = '{0}' 
                                                AND buildings.level > 0 
                                            GROUP BY buildings.account_id
                                        ) AS buildings 
                                        ON spells.account_id = buildings.account_id 
                                        LEFT JOIN (
                                            SELECT spells.account_id, 
                                            SUM(server_spells.housing) AS occupied 
                                        FROM spells 
                                        LEFT JOIN server_spells 
                                            ON spells.global_id = server_spells.global_id 
                                            AND spells.level = server_spells.level 
                                        WHERE spells.ready > 0 
                                            AND server_spells.building_code = {1}
                                        GROUP BY spells.account_id
                                        ) AS t 
                                        ON spells.account_id = t.account_id 
                                            AND spells.brewed > 0 
                                            AND spells.ready <= 0 
                                        GROUP BY spells.id, buildings.capacity, t.occupied, server_spells.housing, server_spells.building_code
                                    ) t2 
                                    WHERE t1.id = t2.id  
                                    AND housing <= capacity 
                                    AND building_code = {1}", BuildingID.spellfactory.ToString(), 0);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }

            query = String.Format(@"UPDATE spells AS t1
                                    SET ready = 1
                                    FROM (
                                        SELECT 
                                            spells.id, 
                                            (CASE WHEN buildings.capacity is null then 0 else buildings.capacity end) - (case when t.occupied is null then 0 else t.occupied end) AS capacity, 
                                            server_spells.housing, 
                                            server_spells.building_code 
                                        FROM spells 
                                        LEFT JOIN server_spells 
                                        ON spells.global_id = server_spells.global_id 
                                        AND spells.level = server_spells.level
                                        LEFT JOIN (
                                            SELECT buildings.account_id, 
                                            SUM(server_buildings.capacity) AS capacity 
                                            FROM buildings 
                                            LEFT JOIN server_buildings 
                                            ON buildings.global_id = server_buildings.global_id 
                                            AND buildings.level = server_buildings.level 
                                            WHERE buildings.global_id = '{0}' 
                                                AND buildings.level > 0 
                                            GROUP BY buildings.account_id
                                        ) AS buildings 
                                        ON spells.account_id = buildings.account_id 
                                        LEFT JOIN (
                                            SELECT spells.account_id, 
                                            SUM(server_spells.housing) AS occupied 
                                        FROM spells 
                                        LEFT JOIN server_spells 
                                            ON spells.global_id = server_spells.global_id 
                                            AND spells.level = server_spells.level 
                                        WHERE spells.ready > 0 
                                            AND server_spells.building_code = {1}
                                        GROUP BY spells.account_id
                                        ) AS t 
                                        ON spells.account_id = t.account_id 
                                            AND spells.brewed > 0 
                                            AND spells.ready <= 0 
                                        GROUP BY spells.id, buildings.capacity, t.occupied, server_spells.housing, server_spells.building_code
                                    ) t2 
                                    WHERE t1.id = t2.id  
                                    AND housing <= capacity 
                                    AND building_code = {1}", BuildingID.darkspellfactory.ToString(), 1);

            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static void GeneralUpdateBattle(NpgsqlConnection connection, double deltaTime)
        {
            for (int i = battles.Count - 1; i >= 0; i--)
            {
                // Failsafe, Just in case
                double time = (DateTime.Now - battles[i].battle.baseTime).TotalSeconds;
                if (time >= battles[i].battle.duration * 1.3f)
                {
                    battles[i].battle.end = true;
                }

                while (battles[i].frames.Count > 0)
                {
                    int from = battles[i].battle.frameCount + 1;
                    for (int f = from; f <= battles[i].frames[0].frame; f++)
                    {
                        if (f == battles[i].frames[0].frame)
                        {
                            for (int u = 0; u < battles[i].frames[0].units.Count; u++)
                            {
                                battles[i].frames[0].units[u].unit = GetUnit(connection, battles[i].frames[0].units[u].id, battles[i].battle.attacker);
                                if (battles[i].frames[0].units[u].unit != null)
                                {
                                    if (battles[i].battle.CanAddUnit(battles[i].frames[0].units[u].x, battles[i].frames[0].units[u].y))
                                    {
                                        DeleteUnit(battles[i].frames[0].units[u].id, connection);
                                        battles[i].battle.AddUnit(battles[i].frames[0].units[u].unit, battles[i].frames[0].units[u].x, battles[i].frames[0].units[u].y);
                                    }
                                }
                            }
                            for (int u = 0; u < battles[i].frames[0].spells.Count; u++)
                            {
                                battles[i].frames[0].spells[u].spell = GetSpell(connection, battles[i].frames[0].spells[u].id, battles[i].battle.attacker, true);
                                if (battles[i].frames[0].spells[u].spell != null)
                                {
                                    if (battles[i].battle.CanAddSpell(battles[i].frames[0].spells[u].x, battles[i].frames[0].spells[u].y))
                                    {
                                        DeleteSpell(battles[i].frames[0].spells[u].id, connection);
                                        battles[i].battle.AddSpell(battles[i].frames[0].spells[u].spell, battles[i].frames[0].spells[u].x, battles[i].frames[0].spells[u].y);
                                    }
                                }
                            }
                            battles[i].savedFrames.Add(battles[i].frames[0]);
                        }
                        battles[i].battle.ExecuteFrame();
                    }
                    battles[i].frames.RemoveAt(0);
                }

                if (battles[i].battle.end)
                {
                    while (battles[i].battle.CanBattleGoOn())
                    {
                        if (battles[i].battle.surrender)
                        {
                            if (battles[i].battle.surrenderFrame <= 0)
                            {
                                battles[i].battle.surrenderFrame = battles[i].battle.frameCount;
                            }
                            if (battles[i].battle.frameCount >= battles[i].battle.surrenderFrame)
                            {
                                break;
                            }
                        }
                        battles[i].battle.ExecuteFrame();
                    }

                    int client_id = 0;
                    string query = String.Format("SELECT client_id FROM accounts WHERE id = {0} AND is_online > 0;", battles[i].battle.attacker);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    int.TryParse(reader["client_id"].ToString(), out client_id);
                                }
                            }
                        }
                    }

                    long attacker_id = battles[i].battle.attacker;
                    long defender_id = battles[i].battle.defender;
                    BattleType battleType = battles[i].type;

                    if (battleType != BattleType.war)
                    {
                        query = String.Format("SELECT replay_path FROM battles WHERE defender_id = {0} AND id <= (SELECT id FROM (SELECT id FROM battles WHERE defender_id = {0} ORDER BY id DESC LIMIT 1 OFFSET 10) t);", defender_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        string p = reader["replay_path"].ToString();
                                        if (File.Exists(p))
                                        {
                                            File.Delete(p);
                                        }
                                    }
                                }
                            }
                        }

                        query = String.Format("DELETE FROM battles WHERE defender_id = {0} AND id <= (SELECT id FROM (SELECT id FROM battles WHERE defender_id = {0} ORDER BY id DESC LIMIT 1 OFFSET 10) t);", defender_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        query = String.Format("SELECT replay_path FROM clan_war_attacks WHERE defender_id = {0} AND id <= (SELECT id FROM (SELECT id FROM clan_war_attacks WHERE defender_id = {0} ORDER BY id DESC LIMIT 1 OFFSET 5) t);", defender_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        string p = reader["replay_path"].ToString();
                                        if (File.Exists(p))
                                        {
                                            File.Delete(p);
                                        }
                                    }
                                }
                            }
                        }

                        query = String.Format("UPDATE clan_war_attacks SET replay_path = '' WHERE defender_id = {0} AND id <= (SELECT id FROM (SELECT id FROM clan_war_attacks WHERE defender_id = {0} ORDER BY id DESC LIMIT 1 OFFSET 5) t);", defender_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                    
                    Data.BattleReport battleReport = new Data.BattleReport();
                    battleReport.attacker = attacker_id;
                    battleReport.defender = defender_id;
                    battleReport.type = battles[i].type;
                    battleReport.totalFrames = battles[i].battle.frameCount;
                    battleReport.buildings = battles[i].buildings;
                    battleReport.frames = battles[i].savedFrames;
                    byte[] reply = Data.Compress(Data.Serialize<Data.BattleReport>(battleReport));
                    var looted = battles[i].battle.GetlootedResources();
                    int stars = battles[i].battle.stars;
                    int unitsDeployed = battles[i].battle.unitsDeployed;
                    int lootedGold = looted.Item1;
                    int lootedElixir = looted.Item2;
                    int lootedDark = looted.Item3;
                    int trophies = battles[i].battle.GetTrophies();
                    int frame = battles[i].battle.frameCount;

                    battles.RemoveAt(i);

                    if (trophies > 0)
                    {
                        SpendResources(connection, defender_id, lootedGold, lootedElixir, 0, lootedDark);
                        AddResources(connection, attacker_id, lootedGold, lootedElixir, lootedDark, 0);
                    }

                    if (battleType == BattleType.normal)
                    {
                        ChangeTrophies(connection, attacker_id, trophies);
                        ChangeTrophies(connection, defender_id, -trophies);
                    }

                    if (client_id > 0 && Server.clients[client_id].account == attacker_id)
                    {
                        Packet packet = new Packet();
                        packet.Write((int)Terminal.RequestsID.BATTLEEND);
                        packet.Write(stars);
                        packet.Write(unitsDeployed);
                        packet.Write(lootedGold);
                        packet.Write(lootedElixir);
                        packet.Write(lootedDark);
                        packet.Write(trophies);
                        packet.Write(frame);
                        Sender.TCP_Send(client_id, packet);
                    }

                    string folder = Terminal.dataFolderPath + "Battle/";
                    string key = Guid.NewGuid().ToString();
                    string path = folder + key + ".txt";
                    if (!Directory.Exists(folder))
                    {
                        Directory.CreateDirectory(folder);
                    }
                    File.WriteAllBytes(path, reply);

                    long reply_id = 0;
                    query = String.Format("INSERT INTO battles (attacker_id, defender_id, stars, trophies, looted_gold, looted_elixir, looted_dark_elixir, replay_path) VALUES({0}, {1}, {2}, {3}, {4}, {5}, {6}, '{7}') RETURNING id", attacker_id, defender_id, stars, trophies, lootedGold, lootedElixir, lootedDark, path.Replace("\\", "\\\\"));
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        reply_id = (long)command.ExecuteScalar();
                    }
                    if (trophies > 0 && Data.shieldMinutesAmountToBattleLost > 0)
                    {
                        query = String.Format("UPDATE accounts SET shield = NOW() at time zone 'utc' + INTERVAL '{0} MINUTE' WHERE id = {1} AND shield < NOW() at time zone 'utc';", Data.shieldMinutesAmountToBattleLost, defender_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public async static void ObstaclesCreation()
        {
            await ObstaclesCreationAsync();
            obstaclesUpdating = false;
        }

        private async static Task<bool> ObstaclesCreationAsync()
        {
            Task<bool> task = Task.Run(() =>
            {
                return Retry.Do(() => _ObstaclesCreationAsync(), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static bool _ObstaclesCreationAsync()
        {
            int placed = 0;
            int count = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("SELECT COUNT(id) AS count FROM accounts WHERE last_login + INTERVAL '24 HOUR' <= NOW() at time zone 'utc';");
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                int.TryParse(reader["count"].ToString(), out count);
                            }
                        }
                    }
                }
                if(count > 0)
                {
                    int obsticleColumns = 2;
                    int obsticleRows = 2;
                    int batch = 50;
                    int sets = (int)Math.Ceiling((double)count / (double)batch);
                    for (int i = 0; i < sets; i++)
                    {
                        List<long> accounts = new List<long>();
                        query = String.Format("SELECT id FROM accounts WHERE last_login + INTERVAL '24 HOUR' <= NOW() at time zone 'utc' LIMIT {0} OFFSET {1};", batch, i * batch);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        long id = 0;
                                        long.TryParse(reader["id"].ToString(), out id);
                                        accounts.Add(id);
                                    }
                                }
                            }
                        }
                        for (int j = 0; j < accounts.Count; j++)
                        {
                            List<Building> buildings = GetBuildings(connection, accounts[j]);
                            Random rnd = new Random();
                            int centerX = rnd.Next(0, Data.gridSize);
                            int centerY = rnd.Next(0, Data.gridSize);
                            List<int> xList = new List<int>();
                            List<int> yList = new List<int>();
                            for (int k = 0; k < Data.gridSize; k++)
                            {
                                if(xList.Count < Data.gridSize)
                                {
                                    int xp = centerX + k;
                                    int xn = centerX - k;
                                    if (xp >= 0 && xp <= Data.gridSize - obsticleColumns && !xList.Contains(xp))
                                    {
                                        xList.Add(xp);
                                    }
                                    if (xn >= 0 && xn <= Data.gridSize - obsticleColumns && !xList.Contains(xn))
                                    {
                                        xList.Add(xn);
                                    }
                                }
                                if (yList.Count < Data.gridSize)
                                {
                                    int yp = centerY + k;
                                    int yn = centerY - k;
                                    if (yp >= 0 && yp <= Data.gridSize - obsticleRows && !yList.Contains(yp))
                                    {
                                        yList.Add(yp);
                                    }
                                    if (yn >= 0 && yn <= Data.gridSize - obsticleRows && !yList.Contains(yn))
                                    {
                                        yList.Add(yn);
                                    }
                                }
                                if (xList.Count >= Data.gridSize && yList.Count >= Data.gridSize)
                                {
                                    break;
                                }
                            }
                            int finalX = -1;
                            int finalY = -1;
                            for (int x = 0; x < xList.Count; x++)
                            {
                                if (finalX >= 0)
                                {
                                    break;
                                }
                                for (int y = 0; y < yList.Count; y++)
                                {
                                    if(finalX >= 0)
                                    {
                                        break;
                                    }
                                    finalX = xList[x];
                                    finalY = yList[y];
                                    for (int k = 0; k < buildings.Count; k++)
                                    {
                                        Rectangle rect1 = new Rectangle(buildings[k].x, buildings[k].y, buildings[k].columns, buildings[k].rows);
                                        Rectangle rect2 = new Rectangle(xList[x], yList[y], obsticleColumns, obsticleRows);
                                        if (rect2.IntersectsWith(rect1))
                                        {
                                            finalX = -1;
                                            finalY = -1;
                                            break;
                                        }
                                        if(buildings[k].warX >= 0 && buildings[k].warY >= 0)
                                        {
                                            rect1 = new Rectangle(buildings[k].warX, buildings[k].warY, buildings[k].columns, buildings[k].rows);
                                            if (rect2.IntersectsWith(rect1))
                                            {
                                                finalX = -1;
                                                finalY = -1;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            if (finalX >= 0 && finalY >= 0)
                            {
                                query = String.Format("INSERT INTO buildings (global_id, account_id, x_position, y_position, level, track_time, x_war, y_war) VALUES('{0}', {1}, {2}, {3}, {4}, NOW() at time zone 'utc' - INTERVAL '1 HOUR', {5}, {6});", BuildingID.obstacle.ToString(), accounts[j], finalX, finalY, rnd.Next(1, 6), finalX, finalY);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    command.ExecuteNonQuery();
                                }
                                placed++;
                            }
                        }
                        accounts.Clear();
                    }
                }
                connection.Close();
            }
            string folder = Terminal.dataFolderPath + "Other/";
            string key = Guid.NewGuid().ToString();
            string path = folder + key + ".txt";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(path, "Obstacle placement was checked for " + count.ToString() + " players and " + placed.ToString() + " obstacles was placed.");
            return true;
        }
        
        #endregion

        #region Buildings

        private async static Task<Building> GetBuildingAsync(long id, long account)
        {
            Task<Building> task = Task.Run(() =>
            {
                return Retry.Do(() => _GetBuildingAsync(id, account), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static Building _GetBuildingAsync(long id, long account)
        {
            Building building = null;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                building = GetBuilding(connection, id, account);
                connection.Close();
            }
            return building;
        }

        private static Building GetBuilding(NpgsqlConnection connection, long id, long account)
        {
            Building building = null;
            string query = String.Format("SELECT level, global_id FROM buildings WHERE id = {0} AND account_id = {1};", id, account);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        building = new Building();
                        while (reader.Read())
                        {
                            building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                            int.TryParse(reader["level"].ToString(), out building.level);
                        }
                    }
                }
            }
            return building;
        }

        private static List<Building> GetBuildingsByGlobalID(string globalID, long account, NpgsqlConnection connection)
        {
            List<Building> buildings = new List<Building>();
            string query = String.Format("SELECT buildings.level, buildings.global_id, server_buildings.capacity FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.global_id = '{0}' AND buildings.account_id = {1};", globalID, account);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            Building building = new Building();
                            building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                            int.TryParse(reader["level"].ToString(), out building.level);
                            int.TryParse(reader["capacity"].ToString(), out building.capacity);
                            buildings.Add(building);
                        }
                    }
                }
            }
            return buildings;
        }

        private async static Task<List<Building>> GetBuildingsAsync(long account)
        {
            Task<List<Building>> task = Task.Run(() =>
            {
                return Retry.Do(() => _GetBuildingsAsync(account), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static List<Building> _GetBuildingsAsync(long account)
        {
            List<Building> data = new List<Building>();
            using (NpgsqlConnection connection = GetDbConnection())
            {
                data = GetBuildings(connection, account);
                connection.Close();
            }
            return data;
        }

        private static List<Building> GetBuildings(NpgsqlConnection connection, long account)
        {
            List<Building> data = new List<Building>();
            string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.level, buildings.x_position, buildings.x_war, buildings.y_war, buildings.boost, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, buildings.y_position, buildings.construction_time, buildings.is_constructing, buildings.construction_build_time, server_buildings.columns_count, server_buildings.rows_count, server_buildings.health, server_buildings.speed, server_buildings.radius, server_buildings.capacity, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity, server_buildings.damage, server_buildings.target_type, server_buildings.blind_radius, server_buildings.splash_radius, server_buildings.projectile_speed FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0};", account);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            Building building = new Building();
                            building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                            long.TryParse(reader["id"].ToString(), out building.databaseID);
                            int.TryParse(reader["level"].ToString(), out building.level);
                            int.TryParse(reader["x_position"].ToString(), out building.x);
                            int.TryParse(reader["y_position"].ToString(), out building.y);
                            int.TryParse(reader["x_war"].ToString(), out building.warX);
                            int.TryParse(reader["y_war"].ToString(), out building.warY);
                            int.TryParse(reader["columns_count"].ToString(), out building.columns);
                            int.TryParse(reader["rows_count"].ToString(), out building.rows);

                            float storage = 0;
                            float.TryParse(reader["gold_storage"].ToString(), out storage);
                            building.goldStorage = (int)Math.Floor(storage);

                            storage = 0;
                            float.TryParse(reader["elixir_storage"].ToString(), out storage);
                            building.elixirStorage = (int)Math.Floor(storage);

                            storage = 0;
                            float.TryParse(reader["dark_elixir_storage"].ToString(), out storage);
                            building.darkStorage = (int)Math.Floor(storage);

                            DateTime.TryParse(reader["boost"].ToString(), out building.boost);
                            float.TryParse(reader["damage"].ToString(), out building.damage);
                            int.TryParse(reader["capacity"].ToString(), out building.capacity);
                            int.TryParse(reader["gold_capacity"].ToString(), out building.goldCapacity);
                            int.TryParse(reader["elixir_capacity"].ToString(), out building.elixirCapacity);
                            int.TryParse(reader["dark_elixir_capacity"].ToString(), out building.darkCapacity);
                            float.TryParse(reader["speed"].ToString(), out building.speed);
                            float.TryParse(reader["radius"].ToString(), out building.radius);
                            int.TryParse(reader["health"].ToString(), out building.health);
                            DateTime.TryParse(reader["construction_time"].ToString(), out building.constructionTime);
                            float.TryParse(reader["blind_radius"].ToString(), out building.blindRange);
                            float.TryParse(reader["splash_radius"].ToString(), out building.splashRange);
                            float.TryParse(reader["projectile_speed"].ToString(), out building.rangedSpeed);
                            string tt = reader["target_type"].ToString();
                            if (!string.IsNullOrEmpty(tt))
                            {
                                building.targetType = (BuildingTargetType)Enum.Parse(typeof(BuildingTargetType), tt);
                            }
                            int isConstructing = 0;
                            int.TryParse(reader["is_constructing"].ToString(), out isConstructing);
                            building.isConstructing = isConstructing > 0;
                            int.TryParse(reader["construction_build_time"].ToString(), out building.buildTime);
                            data.Add(building);
                        }
                    }
                }
            }
            return data;
        }

        #endregion

        #region Build And Replace

        public async static void PlaceBuilding(int id, string buildingID, int x, int y, int layout, long layoutID)
        {
            long account_id = Server.clients[id].account;
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BUILD);
            ServerBuilding building = await Building.GetServerBuildingAsync(buildingID, 1);
            int response = await PlaceBuildingAsync(account_id, building, x, y, layout, layoutID);
            packet.Write(response);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> PlaceBuildingAsync(long account_id, ServerBuilding building, int x, int y, int layout, long layoutID)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _PlaceBuildingAsync(account_id, building, x, y, layout, layoutID), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _PlaceBuildingAsync(long account_id, ServerBuilding building, int x, int y, int layout, long layoutID)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                bool canPlaceBuilding = true;
                if (building == null || x < 0 || y < 0 || x + building.columns > Data.gridSize || y + building.rows > Data.gridSize)
                {
                    canPlaceBuilding = false;
                }
                else
                {
                    List<Building> buildings = GetBuildings(connection, account_id);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        int bX = (layout == 2) ? buildings[i].warX : buildings[i].x;
                        int bY = (layout == 2) ? buildings[i].warY : buildings[i].y;
                        Rectangle rect1 = new Rectangle(bX, bY, buildings[i].columns, buildings[i].rows);
                        Rectangle rect2 = new Rectangle(x, y, building.columns, building.rows);
                        if (rect2.IntersectsWith(rect1))
                        {
                            canPlaceBuilding = false;
                            break;
                        }
                    }
                }
                if (canPlaceBuilding)
                {
                    if (layout == 2)
                    {
                        long war_id = 0;
                        string query = String.Format("SELECT war_id FROM accounts WHERE id = {0};", account_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        long.TryParse(reader["war_id"].ToString(), out war_id);
                                    }
                                }
                            }
                        }
                        if (war_id > 0)
                        {
                            int war_stage = 0;
                            query = String.Format("SELECT stage FROM clan_wars WHERE id = {0};", war_id);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                using (NpgsqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.HasRows)
                                    {
                                        while (reader.Read())
                                        {
                                            int.TryParse(reader["stage"].ToString(), out war_stage);
                                        }
                                    }
                                }
                            }
                            if (war_stage == 1)
                            {
                                query = String.Format("UPDATE buildings SET x_war = {0}, y_war = {1} WHERE id = {2}", x, y, layoutID);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    command.ExecuteNonQuery();
                                    response = 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (building.id == "buildershut")
                        {
                            // todo
                            int c = GetBuildingCount(account_id, "buildershut", connection);
                            switch (c)
                            {
                                case 0: building.requiredGems = 0; break;
                                case 1: building.requiredGems = 250; break;
                                case 2: building.requiredGems = 500; break;
                                case 3: building.requiredGems = 1000; break;
                                case 4: building.requiredGems = 2000; break;
                                default: building.requiredGems = 999999; break;
                            }
                        }
                        int time = 0;
                        bool haveBuilding = false;
                        string query = String.Format("SELECT build_time FROM server_buildings WHERE global_id = '{0}' AND level = 1;", building.id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    haveBuilding = true;
                                    while (reader.Read())
                                    {
                                        time = int.Parse(reader["build_time"].ToString());
                                    }
                                }
                            }
                        }
                        if (haveBuilding)
                        {
                            int buildersCount = GetBuildingCount(account_id, "buildershut", connection);
                            int constructingCount = GetBuildingConstructionCount(account_id, connection);
                            if (time > 0 && buildersCount <= constructingCount)
                            {
                                response = 5;
                            }
                            else
                            {
                                bool limited = false;
                                Building townHall = GetBuildingsByGlobalID("townhall", account_id, connection)[0];
                                if (building.id == "townhall")
                                {

                                }
                                else
                                {
                                    BuildingCount limits = Data.GetBuildingLimits(townHall.level, building.id);
                                    int haveCount = GetBuildingCount(account_id, building.id, connection);
                                    if (limits == null || haveCount >= limits.count)
                                    {
                                        limited = true;
                                    }
                                }
                                if (limited)
                                {
                                    response = 6;
                                }
                                else
                                {
                                    if (SpendResources(connection, account_id, building.requiredGold, building.requiredElixir, building.requiredGems, building.requiredDarkElixir))
                                    {
                                        if (time > 0)
                                        {
                                            query = String.Format("INSERT INTO buildings (global_id, account_id, x_position, y_position, level, is_constructing, construction_time, construction_build_time, track_time) VALUES('{0}', {1}, {2}, {3}, 0, 1, NOW() at time zone 'utc' + INTERVAL '{4} SECOND', {5}, NOW() at time zone 'utc' - INTERVAL '1 HOUR');", building.id, account_id, x, y, time, time);
                                        }
                                        else
                                        {
                                            query = String.Format("INSERT INTO buildings (global_id, account_id, x_position, y_position, level, is_constructing, track_time) VALUES('{0}', {1}, {2}, {3}, 1, 0, NOW() at time zone 'utc' - INTERVAL '1 HOUR');", building.id, account_id, x, y);
                                            AddXP(connection, account_id, building.gainedXp);
                                        }
                                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                        {
                                            command.ExecuteNonQuery();
                                            response = 1;
                                        }
                                    }
                                    else
                                    {
                                        response = 2;
                                    }
                                }
                            }
                        }
                        else
                        {
                            response = 3;
                        }
                    }
                }
                else
                {
                    response = 4;
                }
                connection.Close();
            }
            return response;
        }

        public async static void ReplaceBuilding(int id, long databaseID, int x, int y, int layout)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.REPLACE);
            long account_id = Server.clients[id].account;
            int response = await ReplaceBuildingAsync(account_id, databaseID, x, y, layout);
            packet.Write(response);
            packet.Write(x);
            packet.Write(y);
            packet.Write(databaseID);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> ReplaceBuildingAsync(long account_id, long building_id, int x, int y, int layout)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _ReplaceBuildingAsync(account_id, building_id, x, y, layout), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _ReplaceBuildingAsync(long account_id, long building_id, int x, int y, int layout)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                List<Building> buildings = GetBuildings(connection, account_id);
                Building building = null;

                if (buildings != null && buildings.Count > 0)
                {
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (buildings[i].databaseID == building_id)
                        {
                            building = buildings[i];
                            break;
                        }
                    }
                }
                if (building != null)
                {
                    bool canPlaceBuilding = true;
                    if (x < 0 || y < 0 || x + building.columns > Data.gridSize || y + building.rows > Data.gridSize)
                    {
                        canPlaceBuilding = false;
                    }
                    else
                    {
                        for (int i = 0; i < buildings.Count; i++)
                        {
                            if (buildings[i].databaseID != building.databaseID)
                            {
                                int bX = (layout == 2) ? buildings[i].warX : buildings[i].x;
                                int bY = (layout == 2) ? buildings[i].warY : buildings[i].y;
                                Rectangle rect1 = new Rectangle(bX, bY, buildings[i].columns, buildings[i].rows);
                                Rectangle rect2 = new Rectangle(x, y, building.columns, building.rows);
                                if (rect2.IntersectsWith(rect1))
                                {
                                    canPlaceBuilding = false;
                                    break;
                                }
                            }
                        }
                    }
                    if (canPlaceBuilding)
                    {
                        string query = "";
                        if (layout == 2)
                        {
                            long war_id = 0;
                            query = String.Format("SELECT war_id FROM accounts WHERE id = {0};", account_id);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                using (NpgsqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.HasRows)
                                    {
                                        while (reader.Read())
                                        {
                                            long.TryParse(reader["war_id"].ToString(), out war_id);
                                        }
                                    }
                                }
                            }
                            if (war_id > 0)
                            {
                                int war_stage = 0;
                                query = String.Format("SELECT stage FROM clan_wars WHERE id = {0};", war_id);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    using (NpgsqlDataReader reader = command.ExecuteReader())
                                    {
                                        if (reader.HasRows)
                                        {
                                            while (reader.Read())
                                            {
                                                int.TryParse(reader["stage"].ToString(), out war_stage);
                                            }
                                        }
                                    }
                                }
                                if (war_stage == 1)
                                {
                                    query = String.Format("UPDATE buildings SET x_war = {0}, y_war = {1} WHERE id = {2};", x, y, building_id);
                                }
                                else
                                {
                                    query = "";
                                }
                            }
                            else
                            {
                                query = "";
                            }
                        }
                        else
                        {
                            query = String.Format("UPDATE buildings SET x_position = {0}, y_position = {1} WHERE id = {2};", x, y, building_id);
                        }
                        if (!string.IsNullOrEmpty(query))
                        {
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                            }
                            response = 1;
                        }
                        else
                        {
                            response = 3;
                        }
                    }
                    else
                    {
                        response = 2;
                    }
                }
                connection.Close();
            }
            return response;
        }

        public async static void UpgradeBuilding(int id, long buildingID)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.UPGRADE);
            long account_id = Server.clients[id].account;
            Building building = await GetBuildingAsync(buildingID, account_id);
            if (building == null)
            {
                packet.Write(0);
            }
            else
            {
                int response = await UpgradeBuildingAsync(account_id, buildingID, building.level, building.id.ToString());
                packet.Write(response);
            }
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> UpgradeBuildingAsync(long account_id, long buildingID, int level, string globalID)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _UpgradeBuildingAsync(account_id, buildingID, level, globalID), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _UpgradeBuildingAsync(long account_id, long buildingID, int level, string globalID)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                int time = 0;
                bool haveLevel = false;
                int reqGold = 0;
                int reqElixir = 0;
                int reqDarkElixir = 0;
                int reqGems = 0;
                string query = String.Format("SELECT req_gold, req_elixir, req_dark_elixir, req_gems, build_time FROM server_buildings WHERE global_id = '{0}' AND level = {1};", globalID, globalID == BuildingID.obstacle.ToString() ? level : level + 1);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                haveLevel = true;
                                time = int.Parse(reader["build_time"].ToString());
                                reqGold = int.Parse(reader["req_gold"].ToString());
                                reqElixir = int.Parse(reader["req_elixir"].ToString());
                                reqDarkElixir = int.Parse(reader["req_dark_elixir"].ToString());
                                reqGems = int.Parse(reader["req_gems"].ToString());
                            }
                        }
                    }
                }

                if (haveLevel)
                {
                    int buildersCount = GetBuildingCount(account_id, "buildershut", connection);
                    int constructingCount = GetBuildingConstructionCount(account_id, connection);
                    if (time > 0 && buildersCount <= constructingCount)
                    {
                        response = 5;
                    }
                    else
                    {
                        bool limited = false;
                        if(globalID != BuildingID.obstacle.ToString())
                        {
                            Building townHall = GetBuildingsByGlobalID("townhall", account_id, connection)[0];
                            if (globalID == "townhall")
                            {

                            }
                            else
                            {
                                BuildingCount limits = Data.GetBuildingLimits(townHall.level, globalID);
                                int haveCount = GetBuildingCount(account_id, globalID, connection);
                                if (haveCount >= limits.count && level >= limits.maxLevel)
                                {
                                    limited = true;
                                }
                            }
                        }
                        if (limited)
                        {
                            response = 6;
                        }
                        else
                        {
                            if (SpendResources(connection, account_id, reqGold, reqElixir, reqGems, reqDarkElixir))
                            {
                                query = String.Format("UPDATE buildings SET is_constructing = 1, construction_time =  NOW() at time zone 'utc' + INTERVAL '{0} SECOND', construction_build_time = {1} WHERE id = {2};", time, time, buildingID);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    command.ExecuteNonQuery();
                                    response = 1;
                                }
                            }
                            else
                            {
                                response = 2;
                            }
                        }
                    }
                }
                else
                {
                    response = 3;
                }
                connection.Close();
            }
            return response;
        }

        public async static void InstantBuild(int id, long buildingID)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.INSTANTBUILD);
            long account_id = Server.clients[id].account;
            Building building = await GetBuildingAsync(buildingID, account_id);
            if (building == null)
            {
                packet.Write(0);
            }
            else
            {
                int res = await InstantBuildAsync(account_id, buildingID, building.level, building.id.ToString());
                packet.Write(res);
            }
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> InstantBuildAsync(long account_id, long buildingID, int level, string globalID)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _InstantBuildAsync(account_id, buildingID, level, globalID), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _InstantBuildAsync(long account_id, long buildingID, int level, string globalID)
        {
            int id = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                int time = 0;
                string query = String.Format("SELECT construction_time, NOW() at time zone 'utc' AS now_time FROM buildings WHERE id = {0} AND account_id = {1} AND is_constructing > 0;", buildingID, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                DateTime target = DateTime.Parse(reader["construction_time"].ToString());
                                DateTime now = DateTime.Parse(reader["now_time"].ToString());
                                if (target > now)
                                {
                                    time = (int)(target - now).TotalSeconds;
                                }
                            }
                        }
                    }
                }
                if (time > 0)
                {
                    int requiredGems = Data.GetInstantBuildRequiredGems(time);
                    if (SpendResources(connection, account_id, 0, 0, requiredGems, 0))
                    {
                        query = String.Format("UPDATE buildings SET construction_time = NOW() at time zone 'utc' WHERE id = {0}", buildingID);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                            id = 1;
                        }
                    }
                    else
                    {
                        id = 2;
                    }
                }
                connection.Close();
            }
            return id;
        }

        #endregion

        #region Units

        private async static Task<List<Unit>> GetUnitsAsync(long account_id)
        {
            Task<List<Unit>> task = Task.Run(() =>
            {
                List<Unit> units = null;
                units = Retry.Do(() => _GetUnitsAsync(account_id), TimeSpan.FromSeconds(0.1), 1, false);
                if (units == null)
                {
                    units = new List<Unit>();
                }
                return units;
            });
            return await task;
        }

        private static List<Unit> _GetUnitsAsync(long account_id)
        {
            List<Unit> units = new List<Unit>();
            using (NpgsqlConnection connection = GetDbConnection())
            {
                units = GetUnits(account_id, connection);
                connection.Close();
            }
            return units;
        }

        private static List<Unit> GetUnits(long account, NpgsqlConnection connection)
        {
            List<Unit> data = new List<Unit>();
            string query = String.Format("SELECT units.id, units.global_id, units.level, units.trained, units.ready, units.trained_time, server_units.health, server_units.train_time, server_units.housing, server_units.attack_range, server_units.attack_speed, server_units.move_speed, server_units.damage, server_units.move_type, server_units.target_priority, server_units.priority_multiplier FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.account_id = {0};", account);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            Unit unit = new Unit();
                            unit.id = (UnitID)Enum.Parse(typeof(UnitID), reader["global_id"].ToString());
                            long.TryParse(reader["id"].ToString(), out unit.databaseID);
                            int.TryParse(reader["level"].ToString(), out unit.level);
                            int.TryParse(reader["health"].ToString(), out unit.health);
                            int.TryParse(reader["housing"].ToString(), out unit.hosing);
                            int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                            float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);

                            float.TryParse(reader["damage"].ToString(), out unit.damage);
                            float.TryParse(reader["attack_speed"].ToString(), out unit.attackSpeed);
                            float.TryParse(reader["move_speed"].ToString(), out unit.moveSpeed);
                            float.TryParse(reader["attack_range"].ToString(), out unit.attackRange);

                            unit.movement = (UnitMoveType)Enum.Parse(typeof(UnitMoveType), reader["move_type"].ToString());
                            unit.priority = (TargetPriority)Enum.Parse(typeof(TargetPriority), reader["target_priority"].ToString());
                            float.TryParse(reader["priority_multiplier"].ToString(), out unit.priorityMultiplier);

                            int isTrue = 0;
                            int.TryParse(reader["trained"].ToString(), out isTrue);
                            unit.trained = isTrue > 0;

                            isTrue = 0;
                            int.TryParse(reader["ready"].ToString(), out isTrue);
                            unit.ready = isTrue > 0;
                            data.Add(unit);
                        }
                    }
                }
            }
            return data;
        }

        public async static void TrainUnit(int id, string globalID)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.TRAIN);
            long account_id = Server.clients[id].account;
            int res = await TrainUnitAsync(account_id, globalID);
            packet.Write(res);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> TrainUnitAsync(long account_id, string globalID)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _TrainUnitAsync(account_id, globalID), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _TrainUnitAsync(long account_id, string globalID)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                int level = 1;
                Research research = GetResearch(connection, account_id, globalID, ResearchType.unit);
                if (research != null)
                {
                    level = research.level;
                }
                ServerUnit unit = Unit.GetServerUnit(globalID, level);
                if (unit != null)
                {
                    int capacity = 0;
                    List<Building> barracks = GetBuildingsByGlobalID(BuildingID.barracks.ToString(), account_id, connection);
                    for (int i = 0; i < barracks.Count; i++)
                    {
                        capacity += barracks[i].capacity;
                    }

                    int occupied = 999;
                    string query = String.Format("SELECT SUM(server_units.housing) AS occupied FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.account_id = {0} AND ready <= 0;", account_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    int.TryParse(reader["occupied"].ToString(), out occupied);
                                }
                            }
                        }
                    }

                    if (capacity - occupied >= unit.housing)
                    {
                        if (SpendResources(connection, account_id, unit.requiredGold, unit.requiredElixir, unit.requiredGems, unit.requiredDarkElixir))
                        {
                            query = String.Format("INSERT INTO units (global_id, level, account_id) VALUES('{0}', {1}, {2})", globalID, level, account_id);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                                response = 1;
                            }
                        }
                        else
                        {
                            response = 2;
                        }
                    }
                    else
                    {
                        response = 4;
                    }
                }
                else
                {
                    response = 3;
                }
                connection.Close();
            }
            return response;
        }
        public async static void CancelTrainUnit(int id, long databaseID)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.CANCELTRAIN);
            long account_id = Server.clients[id].account;
            int res = await CancelTrainUnitAsync(account_id, databaseID);
            packet.Write(res);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> CancelTrainUnitAsync(long account_id, long databaseID)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _CancelTrainUnitAsync(account_id, databaseID), TimeSpan.FromSeconds(0.1), 10, false);
            });
            return await task;
        }

        private static int _CancelTrainUnitAsync(long account_id, long databaseID)
        {
            int id = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("DELETE FROM units WHERE id = {0} AND account_id = {1} AND ready <= 0", databaseID, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                    id = 1;
                }
                connection.Close();
            }
            return id;
        }

        public static void DeleteUnit(long id, NpgsqlConnection connection)
        {
            string query = String.Format("DELETE FROM units WHERE id = {0};", id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        private static Unit GetUnit(NpgsqlConnection connection, long database_id, long account_id)
        {
            Unit unit = null;
            string query = String.Format("SELECT units.id, units.global_id, units.level, units.trained, units.ready, units.trained_time, server_units.health, server_units.train_time, server_units.housing, server_units.attack_range, server_units.attack_speed, server_units.move_speed, server_units.damage, server_units.move_type, server_units.target_priority, server_units.priority_multiplier FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.id = {0} AND units.account_id = {1};", database_id, account_id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            unit = new Unit();
                            unit.id = (UnitID)Enum.Parse(typeof(UnitID), reader["global_id"].ToString());
                            long.TryParse(reader["id"].ToString(), out unit.databaseID);
                            int.TryParse(reader["level"].ToString(), out unit.level);
                            int.TryParse(reader["health"].ToString(), out unit.health);
                            int.TryParse(reader["housing"].ToString(), out unit.hosing);
                            int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                            float.TryParse(reader["trained_time"].ToString(), out unit.trainedTime);

                            float.TryParse(reader["damage"].ToString(), out unit.damage);
                            float.TryParse(reader["attack_speed"].ToString(), out unit.attackSpeed);
                            float.TryParse(reader["move_speed"].ToString(), out unit.moveSpeed);
                            float.TryParse(reader["attack_range"].ToString(), out unit.attackRange);

                            unit.movement = (UnitMoveType)Enum.Parse(typeof(UnitMoveType), reader["move_type"].ToString());
                            unit.priority = (TargetPriority)Enum.Parse(typeof(TargetPriority), reader["target_priority"].ToString());
                            float.TryParse(reader["priority_multiplier"].ToString(), out unit.priorityMultiplier);

                            int isTrue = 0;
                            int.TryParse(reader["trained"].ToString(), out isTrue);
                            unit.trained = isTrue > 0;

                            isTrue = 0;
                            int.TryParse(reader["ready"].ToString(), out isTrue);
                            unit.ready = isTrue > 0;
                        }
                    }
                }
            }
            return unit;
        }

        #endregion

        #region Battle

        private static List<Data.BattleData> battles = new List<Data.BattleData>();

        public async static void FindBattleTarget(int id)
        {
            long account_id = Server.clients[id].account;
            long target = await FindBattleTargetAsync(account_id);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BATTLEFIND);
            if (target > 0)
            {
                Data.OpponentData opponent = new Data.OpponentData();
                opponent.id = target;
                opponent.data = await GetPlayerDataAsync(target);
                if (opponent.data != null)
                {
                    opponent.buildings = await GetBuildingsAsync(target);
                    if (opponent.buildings != null)
                    {
                        opponent.buildings = await SetBuildingsPercentAsync(opponent.buildings, BattleType.normal);
                        if (opponent.buildings != null)
                        {
                            packet.Write(target);
                            string data = await Data.SerializeAsync<Data.OpponentData>(opponent);
                            byte[] bytes = await Data.CompressAsync(data);
                            packet.Write(bytes.Length);
                            packet.Write(bytes);
                        }
                        else
                        {
                            target = 0;
                            packet.Write(target);
                        }
                    }
                    else
                    {
                        target = 0;
                        packet.Write(target);
                    }
                }
                else
                {
                    target = 0;
                    packet.Write(target);
                }
            }
            else
            {
                target = 0;
                packet.Write(target);
            }
            Sender.TCP_Send(id, packet);
        }

        private async static Task<long> FindBattleTargetAsync(long account_id)
        {
            Task<long> task = Task.Run(() =>
            {
                return Retry.Do(() => _FindBattleTargetAsync(account_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static long _FindBattleTargetAsync(long account_id)
        {
            long id = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("SELECT id FROM accounts WHERE id <> {0} AND shield < NOW() at time zone 'utc' AND is_online <= 0 ORDER BY RANDOM() LIMIT 1;", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                long.TryParse(reader["id"].ToString(), out id);
                            }
                        }
                    }
                }
                if (id > 0)
                {
                    int townHallLevel = 1;
                    query = String.Format("SELECT level FROM buildings WHERE account_id = {0} AND global_id = '{1}';", account_id, BuildingID.townhall.ToString());
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    int.TryParse(reader["level"].ToString(), out townHallLevel);
                                }
                            }
                        }
                    }
                    if (SpendResources(connection, account_id, Data.GetBattleSearchCost(townHallLevel), 0, 0, 0) == false)
                    {
                        id = 0;
                    }
                }
                connection.Close();
            }
            return id;
        }

        public async static void AddBattleFrame(int id, byte[] data)
        {
            long account_id = Server.clients[id].account;
            string frameData = await Data.DecompressAsync(data);
            Data.BattleFrame frame = await Data.DesrializeAsync<Data.BattleFrame>(frameData);
            for (int i = 0; i < battles.Count; i++)
            {
                if (battles[i].battle.attacker == account_id)
                {
                    double time = frame.frame * Data.battleFrameRate;
                    if (battles[i].battle.percentage < 1f && battles[i].battle.end == false && time <= battles[i].battle.duration)
                    {
                        battles[i].frames.Add(frame);
                    }
                    break;
                }
            }
        }

        public static void EndBattle(long account_id, bool surrender, int surrenderFrame)
        {
            try
            {
                for (int i = battles.Count - 1; i >= 0; i--)
                {
                    if (battles[i].battle.attacker == account_id)
                    {
                        battles[i].battle.surrender = surrender;
                        battles[i].battle.surrenderFrame = surrenderFrame;
                        battles[i].battle.end = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.LogError(ex.Message, ex.StackTrace);
            }
        }

        private async static Task<List<Building>> SetBuildingsPercentAsync(List<Building> buildings, BattleType battleType)
        {
            Task<List<Building>> task = Task.Run(() =>
            {
                return Retry.Do(() => _SetBuildingsPercentAsync(buildings, battleType), TimeSpan.FromSeconds(0.1), 10, false);
            });
            return await task;
        }

        private static List<Building> _SetBuildingsPercentAsync(List<Building> buildings, BattleType battleType)
        {
            double count = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].id != BuildingID.wall && (battleType == BattleType.war && buildings[i].warX < 0 && buildings[i].warY < 0) == false && Battle.IsBuildingCanBeAttacked(buildings[i].id))
                {
                    count += (buildings[i].rows * buildings[i].columns);
                }
            }
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].id != BuildingID.wall && (battleType == BattleType.war && buildings[i].warX < 0 && buildings[i].warY < 0) == false && Battle.IsBuildingCanBeAttacked(buildings[i].id))
                {
                    buildings[i].percentage = (double)(buildings[i].rows * buildings[i].columns) / count;
                }
                else
                {
                    buildings[i].percentage = 0;
                }
            }
            return buildings;
        }

        public async static void StartBattle(int id, byte[] bytes, BattleType type)
        {
            long account_id = Server.clients[id].account;
            bool canAttack = true;

            List<Data.BattleStartBuildingData> startData = new List<Data.BattleStartBuildingData>();
            List<Battle.Building> buildings = new List<Battle.Building>();
            string data = await Data.DecompressAsync(bytes);
            Data.OpponentData opponentClent = await Data.DesrializeAsync<Data.OpponentData>(data);
            long defender = opponentClent.id;

            for (int i = 0; i < battles.Count; i++)
            {
                if ((type == BattleType.normal && (battles[i].battle.attacker == account_id || battles[i].battle.defender == defender)) || (type == BattleType.war && battles[i].battle.attacker == account_id))
                {
                    canAttack = false;
                    break;
                }
            }

            bool match = true;
            Data.OpponentData opponentServer = new Data.OpponentData();
            if (canAttack)
            {
                opponentServer.buildings = await GetBuildingsAsync(opponentClent.id);
                if (opponentServer.buildings != null)
                {
                    opponentServer.buildings = await SetBuildingsPercentAsync(opponentServer.buildings, type);
                    if (opponentServer.buildings == null)
                    {
                        opponentServer.buildings = new List<Building>();
                    }
                }
                else
                {
                    opponentServer.buildings = new List<Building>();
                }

                if (opponentServer.buildings.Count == opponentClent.buildings.Count)
                {
                    buildings = Building.ConvertToBattleBuildings(opponentServer.buildings, type);
                    if(buildings == null)
                    {
                        match = false;
                        buildings = new List<Battle.Building>();
                    }
                }
                else
                {
                    match = false;
                }
            }
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BATTLESTART);
            packet.Write(match);
            packet.Write(canAttack);
            if (match && canAttack)
            {
                if (type == BattleType.normal)
                {
                    await RemoveShieldAsync(account_id);
                }
                Player attackerData = await GetPlayerDataAsync(account_id);
                Player defenderData = await GetPlayerDataAsync(defender);
                var trophies = Data.GetBattleTrophies(attackerData.trophies, defenderData.trophies);
                Data.BattleData battle = new Data.BattleData();
                battle.type = type;
                battle.battle = new Battle();
                // battle.buildings = Data.CloneClass<List<Battle.Building>>(buildings);
                battle.buildings = opponentServer.buildings;
                battle.battle.Initialize(buildings, DateTime.Now);
                battle.battle.attacker = account_id;
                battle.battle.defender = defender;
                battle.battle.winTrophies = trophies.Item1;
                battle.battle.loseTrophies = trophies.Item2;
                battles.Add(battle);
                packet.Write(battle.battle.winTrophies);
                packet.Write(battle.battle.loseTrophies);
                string bd = await Data.SerializeAsync<List<Data.BattleStartBuildingData>>(startData);
                byte[] battleBytes = await Data.CompressAsync(bd);
                packet.Write(battleBytes.Length);
                packet.Write(battleBytes);
            }
            Sender.TCP_Send(id, packet);
        }

        public async static void Scout(int id, long target, int type)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SCOUT);
            Player player = await GetPlayerDataAsync(target);
            if (player != null)
            {
                packet.Write(1);
                packet.Write(type);
                player.buildings = await GetBuildingsAsync(target);
                string data = await Data.SerializeAsync<Player>(player);
                byte[] bytes = await Data.CompressAsync(data);
                packet.Write(bytes.Length);
                packet.Write(bytes);
            }
            else
            {
                packet.Write(0);
                packet.Write(type);
            }
            Sender.TCP_Send(id, packet);
        }

        public async static void GetBattlesList(int id)
        {
            long account_id = Server.clients[id].account;
            List<Data.BattleReportItem> reports = await GetBattlesListAsync(account_id);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BATTLEREPORTS);
            if(reports != null && reports.Count > 0)
            {
                packet.Write(1);
                string data = await Data.SerializeAsync<List<Data.BattleReportItem>>(reports);
                byte[] bytes = await Data.CompressAsync(data);
                packet.Write(bytes.Length);
                packet.Write(bytes);
            }
            else
            {
                packet.Write(0);
            }
            Sender.TCP_Send(id, packet);
        }

        private async static Task<List<Data.BattleReportItem>> GetBattlesListAsync(long account_id)
        {
            Task<List<Data.BattleReportItem>> task = Task.Run(() =>
            {
                return Retry.Do(() => _GetBattlesListAsync(account_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static List<Data.BattleReportItem> _GetBattlesListAsync(long account_id)
        {
            List<Data.BattleReportItem> reports = new List<Data.BattleReportItem>();
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("SELECT battles.id, battles.attacker_id, battles.defender_id, battles.end_time, battles.stars, battles.trophies, battles.looted_gold, battles.looted_elixir, battles.looted_dark_elixir, battles.seen, battles.replay_path, accounts.name FROM battles LEFT JOIN accounts ON accounts.id = (battles.attacker_id + battles.defender_id - {0}) WHERE battles.attacker_id = {0} OR battles.defender_id = {0} ORDER BY battles.end_time DESC LIMIT 20", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                Data.BattleReportItem report = new Data.BattleReportItem();
                                long.TryParse(reader["id"].ToString(), out report.id);
                                long.TryParse(reader["attacker_id"].ToString(), out report.attacker);
                                long.TryParse(reader["defender_id"].ToString(), out report.defender);
                                int s = 0;
                                int.TryParse(reader["seen"].ToString(), out s);
                                report.seen = s > 0;
                                DateTime.TryParse(reader["end_time"].ToString(), out report.time);
                                int.TryParse(reader["stars"].ToString(), out report.stars);
                                int.TryParse(reader["trophies"].ToString(), out report.trophies);
                                int.TryParse(reader["looted_gold"].ToString(), out report.gold);
                                int.TryParse(reader["looted_elixir"].ToString(), out report.elixir);
                                int.TryParse(reader["looted_dark_elixir"].ToString(), out report.dark);
                                report.username = reader["name"].ToString();
                                report.hasReply = !string.IsNullOrEmpty(reader["replay_path"].ToString());
                                reports.Add(report);
                            }
                        }
                    }
                }
                query = String.Format("UPDATE battles SET seen = 1 WHERE defender_id = {0} AND seen <= 0", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
                connection.Close();
            }
            return reports;
        }

        public async static void GetBattleReport(int id, long report_id)
        {
            long account_id = Server.clients[id].account;
            Data.BattleReport report = await GetBattleReportAsync(account_id, report_id);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BATTLEREPORT);
            if(report == null)
            {
                packet.Write(0);
            }
            else
            {
                packet.Write(1);
                string data = await Data.SerializeAsync<Data.BattleReport>(report);
                byte[] bytes = await Data.CompressAsync(data);
                packet.Write(bytes.Length);
                packet.Write(bytes);
                Player player = await GetPlayerDataAsync(account_id == report.attacker ? report.defender : report.attacker);
                data = await Data.SerializeAsync<Player>(player);
                bytes = await Data.CompressAsync(data);
                packet.Write(bytes.Length);
                packet.Write(bytes);
            }
            Sender.TCP_Send(id, packet);
        }

        private async static Task<Data.BattleReport> GetBattleReportAsync(long account_id, long report_id)
        {
            Task<Data.BattleReport> task = Task.Run(() =>
            {
                return Retry.Do(() => _GetBattleReportAsync(account_id, report_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static Data.BattleReport _GetBattleReportAsync(long account_id, long report_id)
        {
            Data.BattleReport report = null;
            string path = "";
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("SELECT replay_path FROM battles WHERE id = {0}", report_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                path = reader["replay_path"].ToString();
                            }
                        }
                    }
                }
                connection.Close();
            }
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string data = Data.Decompress(File.ReadAllBytes(path));
                report = Data.Desrialize<Data.BattleReport>(data);
            }
            return report;
        }

        #endregion

        #region Email

        public async static void SendRecoveryCode(int id, string address, string email)
        {
            var code = await SendRecoveryCodeAsync(id, address, email);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SENDCODE);
            packet.Write(code.Item1);
            packet.Write(code.Item2);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<(int, int)> SendRecoveryCodeAsync(int id, string address, string email)
        {
            Task<(int, int)> task = Task.Run(() =>
            {
                return Retry.Do(() => _SendRecoveryCodeAsync(id, address, email), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static (int, int) _SendRecoveryCodeAsync(int id, string address, string email)
        {
            int expiration = 0;
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                long account_id = 0;
                if (!string.IsNullOrEmpty(email))
                {
                    string query = String.Format("SELECT id FROM accounts WHERE email = '{0}' AND is_online <= 0;", email);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    long.TryParse(reader["id"].ToString(), out account_id);
                                }
                            }
                        }
                    }
                }
                if (account_id > 0)
                {
                    long code_id = 0;
                    DateTime nowTime = DateTime.Now;
                    DateTime expireTime = nowTime;
                    string query = String.Format("SELECT id, NOW() at time zone 'utc' AS now_time, expire_time FROM verification_codes WHERE address = '{0}' AND target = '{1}' AND NOW() at time zone 'utc' < expire_time;", address, email);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    long.TryParse(reader["id"].ToString(), out code_id);
                                    DateTime.TryParse(reader["now_time"].ToString(), out nowTime);
                                    DateTime.TryParse(reader["expire_time"].ToString(), out expireTime);
                                }
                            }
                        }
                    }
                    if (code_id > 0)
                    {
                        response = 1;
                        expiration = (int)Math.Floor((expireTime - nowTime).TotalSeconds);
                    }
                    else
                    {
                        string code = Data.RandomCode(Data.recoveryCodeLength);
                        if (Email.SendEmailVerificationCode(code, email))
                        {
                            query = String.Format("INSERT INTO verification_codes (target, address, code, expire_time) VALUES('{0}', '{1}', '{2}', NOW() at time zone 'utc' + INTERVAL '{3} SECOND')", email, address, code, Data.recoveryCodeExpiration);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                            }
                            response = 1;
                            expiration = Data.recoveryCodeExpiration;
                        }
                        else
                        {
                            response = 2;
                        }
                    }
                }
                connection.Close();
            }
            return (response, expiration);
        }

        public async static void ConfirmRecoveryCode(int id, string address, string email, string code)
        {
            var response = await ConfirmRecoveryCodeAsync(id, address, email, code);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.CONFIRMCODE);
            packet.Write(response.Item1);
            packet.Write(response.Item2);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<(int, string)> ConfirmRecoveryCodeAsync(int id, string address, string email, string code)
        {
            Task<(int, string)> task = Task.Run(() =>
            {
                return Retry.Do(() => _ConfirmRecoveryCodeAsync(id, address, email, code), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static (int, string) _ConfirmRecoveryCodeAsync(int id, string address, string email, string code)
        {
            string password = "";
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                long account_id = 0;
                if (!string.IsNullOrEmpty(email))
                {
                    string query = String.Format("SELECT id FROM accounts WHERE email = '{0}' AND is_online <= 0;", email);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    long.TryParse(reader["id"].ToString(), out account_id);
                                }
                            }
                        }
                    }
                }
                if (account_id > 0)
                {
                    long code_id = 0;
                    string query = String.Format("SELECT id FROM verification_codes WHERE address = '{0}' AND target = '{1}' AND code = '{2}' AND NOW() at time zone 'utc' < expire_time;", address, email, code);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    long.TryParse(reader["id"].ToString(), out code_id);
                                }
                            }
                        }
                    }
                    if (code_id > 0)
                    {
                        password = Data.EncrypteToMD5(Tools.GenerateToken());
                        query = String.Format("UPDATE accounts SET password = '{0}' WHERE id = {1};", password, account_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                        query = String.Format("DELETE FROM verification_codes WHERE id = {0};", code_id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                        response = 1;
                    }
                    else
                    {
                        response = 2;
                    }
                }
                connection.Close();
            }
            return (response, password);
        }

        public async static void SendEmailCode(int id, string address, string email)
        {
            long account_id = Server.clients[id].account;
            var code = await SendEmailCodeAsync(id, account_id, address, email);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.EMAILCODE);
            packet.Write(code.Item1);
            packet.Write(code.Item2);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<(int, int)> SendEmailCodeAsync(int id, long account_id, string address, string email)
        {
            Task<(int, int)> task = Task.Run(() =>
            {
                return Retry.Do(() => _SendEmailCodeAsync(id, account_id, address, email), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static (int, int) _SendEmailCodeAsync(int id, long account_id, string address, string email)
        {
            int expiration = 0;
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                bool found = false;
                if (!string.IsNullOrEmpty(email))
                {
                    string query = String.Format("SELECT id FROM accounts WHERE id = {0} AND address = '{1}' AND is_online > 0;", account_id, address);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                found = true;
                            }
                        }
                    }
                }
                if (found)
                {
                    found = false;
                    string query = String.Format("SELECT id FROM accounts WHERE email = '{0}';", email);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                found = true;
                            }
                        }
                    }
                    if (!found)
                    {
                        long code_id = 0;
                        DateTime nowTime = DateTime.Now;
                        DateTime expireTime = nowTime;
                        query = String.Format("SELECT id, NOW() at time zone 'utc' AS now_time, expire_time FROM verification_codes WHERE address = '{0}' AND target = '{1}' AND NOW() at time zone 'utc' < expire_time;", address, email);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        long.TryParse(reader["id"].ToString(), out code_id);
                                        DateTime.TryParse(reader["now_time"].ToString(), out nowTime);
                                        DateTime.TryParse(reader["expire_time"].ToString(), out expireTime);
                                    }
                                }
                            }
                        }
                        if (code_id > 0)
                        {
                            response = 1;
                            expiration = (int)Math.Floor((expireTime - nowTime).TotalSeconds);
                        }
                        else
                        {
                            string code = Data.RandomCode(Data.recoveryCodeLength);
                            if (Email.SendEmailConfirmationCode(code, email))
                            {
                                query = String.Format("INSERT INTO verification_codes (target, address, code, expire_time) VALUES('{0}', '{1}', '{2}', NOW() at time zone 'utc' + INTERVAL '{3} SECOND')", email, address, code, Data.confirmationCodeExpiration);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    command.ExecuteNonQuery();
                                }
                                response = 1;
                                expiration = Data.confirmationCodeExpiration;
                            }
                            else
                            {
                                response = 2;
                            }
                        }
                    }
                    else
                    {
                        response = 3;
                    }
                }
                connection.Close();
            }
            return (response, expiration);
        }

        public async static void ConfirmEmailCode(int id, string address, string email, string code)
        {
            long account_id = Server.clients[id].account;
            int response = await ConfirmEmailCodeAsync(account_id, address, email, code);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.EMAILCONFIRM);
            packet.Write(response);
            packet.Write(email);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> ConfirmEmailCodeAsync(long account_id, string address, string email, string code)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _ConfirmEmailCodeAsync(account_id, address, email, code), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _ConfirmEmailCodeAsync(long account_id, string address, string email, string code)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                bool found = false;
                if (!string.IsNullOrEmpty(email))
                {
                    string query = String.Format("SELECT id FROM accounts WHERE id = {0} AND address = '{1}' AND is_online > 0;", account_id, address);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                found = true;
                            }
                        }
                    }
                }
                if (found)
                {
                    found = false;
                    string query = String.Format("SELECT id FROM accounts WHERE email = '{0}';", email);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                found = false;
                            }
                        }
                    }
                    if (!found)
                    {
                        long code_id = 0;
                        query = String.Format("SELECT id FROM verification_codes WHERE address = '{0}' AND target = '{1}' AND code = '{2}' AND NOW() at time zone 'utc' < expire_time;", address, email, code);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            using (NpgsqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        long.TryParse(reader["id"].ToString(), out code_id);
                                    }
                                }
                            }
                        }
                        if (code_id > 0)
                        {
                            query = String.Format("UPDATE accounts SET email = '{0}' WHERE id = {1};", email, account_id);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                            }
                            query = String.Format("DELETE FROM verification_codes WHERE id = {0};", code_id);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                            }
                            response = 1;
                        }
                        else
                        {
                            response = 2;
                        }
                    }
                    else
                    {
                        response = 3;
                    }
                }
                connection.Close();
            }
            return response;
        }

        #endregion

        #region Messages

        public async static void SyncMessages(int id, Data.ChatType type, long lastMessage)
        {
            long account_id = Server.clients[id].account;
            List<Data.CharMessage> response = await GetChatMessagesAsync(account_id, type, lastMessage);
            string data = await Data.SerializeAsync<List<Data.CharMessage>>(response);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.GETCHATS);
            byte[] bytes = await Data.CompressAsync(data);
            packet.Write(bytes.Length);
            packet.Write(bytes);
            packet.Write((int)type);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<List<Data.CharMessage>> GetChatMessagesAsync(long account_id, Data.ChatType type, long lastMessage)
        {
            Task<List<Data.CharMessage>> task = Task.Run(() =>
            {
                List<Data.CharMessage> response = null;
                response = Retry.Do(() => _GetChatMessagesAsync(account_id, type, lastMessage), TimeSpan.FromSeconds(0.1), 1, false);
                if (response == null)
                {
                    response = new List<Data.CharMessage>();
                }
                return response;
            });
            return await task;
        }

        private static List<Data.CharMessage> _GetChatMessagesAsync(long account_id, Data.ChatType type, long lastMessage)
        {
            List<Data.CharMessage> response = new List<Data.CharMessage>();
            using (NpgsqlConnection connection = GetDbConnection())
            {
                response = GetChatMessages(connection, account_id, type, lastMessage);
                connection.Close();
            }
            return response;
        }

        private static List<Data.CharMessage> GetChatMessages(NpgsqlConnection connection, long account_id, Data.ChatType type, long lastMessage)
        {
            List<Data.CharMessage> messages = new List<Data.CharMessage>();

            long global_id = 0;

            string query = "";

            string filterTime = "";
            if (lastMessage > 0)
            {
                query = String.Format("SELECT DATE_FORMAT(send_time, '{0}') AS send_time FROM chat_messages WHERE id = {1}", Data.mysqlDateTimeFormat, lastMessage);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                filterTime = reader["send_time"].ToString();
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(filterTime))
            {
                query = String.Format("SELECT chat_messages.id, chat_messages.account_id, chat_messages.type, chat_messages.global_id, chat_messages.clan_id, chat_messages.message, DATE_FORMAT(chat_messages.send_time, '{0}') AS send_time, accounts.name, accounts.chat_color FROM chat_messages LEFT JOIN accounts ON chat_messages.account_id = accounts.id WHERE chat_messages.global_id = {1} AND chat_messages.type = {2} AND chat_messages.send_time > '{3}'", Data.mysqlDateTimeFormat, global_id, (int)type, filterTime);
            }
            else
            {
                query = String.Format("SELECT chat_messages.id, chat_messages.account_id, chat_messages.type, chat_messages.global_id, chat_messages.clan_id, chat_messages.message, DATE_FORMAT(chat_messages.send_time, '{0}') AS send_time, accounts.name, accounts.chat_color FROM chat_messages LEFT JOIN accounts ON chat_messages.account_id = accounts.id WHERE chat_messages.global_id = {1} AND chat_messages.type = {2}", Data.mysqlDateTimeFormat, global_id, (int)type);
            }
        
            if (!string.IsNullOrEmpty(query))
            {
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                Data.CharMessage message = new Data.CharMessage();
                                long.TryParse(reader["id"].ToString(), out message.id);
                                long.TryParse(reader["account_id"].ToString(), out message.accountID);
                                int t = 0;
                                int.TryParse(reader["type"].ToString(), out t);
                                message.type = (Data.ChatType)t;
                                long.TryParse(reader["global_id"].ToString(), out message.globalID);
                                long.TryParse(reader["clan_id"].ToString(), out message.clanID);
                                message.message = reader["message"].ToString();
                                message.name = reader["name"].ToString();
                                message.color = reader["chat_color"].ToString();
                                message.time = reader["send_time"].ToString();
                                messages.Add(message);
                            }
                        }
                    }
                }
            }
            return messages;
        }

        public async static void SendChatMessage(int id, string message, Data.ChatType type, long target)
        {
            long account_id = Server.clients[id].account;
            int response = await SendChatMessageAsync(account_id, message, type, target);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.SENDCHAT);
            packet.Write(response);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> SendChatMessageAsync(long account_id, string message, Data.ChatType type, long target)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _SendChatMessageAsync(account_id, message, type, target), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _SendChatMessageAsync(long account_id, string message, Data.ChatType type, long target)
        {
            int response = 0;
            if (!string.IsNullOrEmpty(message) && Data.IsMessageGoodToSend(message))
            {
                using (NpgsqlConnection connection = GetDbConnection())
                {
                    long clan_id = 0;
                    int global_chat_blocked = 0;
                    int clan_chat_blocked = 0;
                    bool timeOk = false;
                    string query = String.Format("SELECT clan_id, global_chat_blocked, clan_chat_blocked FROM accounts WHERE id = {0} AND last_chat <= NOW() at time zone 'utc' - INTERVAL '1 SECOND';", account_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    long.TryParse(reader["clan_id"].ToString(), out clan_id);
                                    int.TryParse(reader["global_chat_blocked"].ToString(), out global_chat_blocked);
                                    int.TryParse(reader["clan_chat_blocked"].ToString(), out clan_chat_blocked);
                                    timeOk = true;
                                }
                            }
                        }
                    }
                    if (timeOk)
                    {
                        if (global_chat_blocked > 0)
                        {
                            // Banned from sending message
                            response = 2;
                        }
                        else
                        {
                            bool sent = false;
                            if (type == Data.ChatType.global)
                            {
                                query = String.Format("INSERT INTO chat_messages (account_id, type, global_id, clan_id, message) VALUES({0}, {1}, {2}, {3}, '{4}');", account_id, (int)type, target, clan_id, message);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    command.ExecuteNonQuery();
                                }
                                query = String.Format("DELETE FROM chat_messages WHERE type = {0} AND global_id = {1} AND send_time <= (SELECT send_time FROM (SELECT send_time FROM chat_messages WHERE type = {0} AND global_id = {1} ORDER BY send_time DESC LIMIT 1 OFFSET {2}) messages);", (int)type, target, Data.globalChatArchiveMaxMessages);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    command.ExecuteNonQuery();
                                }
                                sent = true;
                            }
                            if (sent)
                            {
                                query = String.Format("UPDATE accounts SET last_chat = NOW() at time zone 'utc' WHERE id = {0};", account_id);
                                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                                {
                                    command.ExecuteNonQuery();
                                }
                                response = 1;
                            }
                        }
                    }
                    connection.Close();
                }
            }
            return response;
        }

        #endregion

        #region Spell
        public async static void BrewSpell(int id, string globalID)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.BREW);
            long account_id = Server.clients[id].account;
            int res = await BrewSpellAsync(account_id, globalID);
            packet.Write(res);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> BrewSpellAsync(long account_id, string globalID)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _BrewSpellAsync(account_id, globalID), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static int _BrewSpellAsync(long account_id, string globalID)
        {
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                int level = 1;
                Research research = GetResearch(connection, account_id, globalID, ResearchType.spell);
                if (research != null)
                {
                    level = research.level;
                }
                ServerSpell spell = GetServerSpell(connection, globalID, level);
                if (spell != null)
                {
                    int capacity = 0;
                    List<Building> spellFactory = GetBuildingsByGlobalID(BuildingID.spellfactory.ToString(), account_id, connection);
                    for (int i = 0; i < spellFactory.Count; i++)
                    {
                        capacity += spellFactory[i].capacity;
                    }

                    int occupied = 999;
                    string query = String.Format("SELECT SUM(server_spells.housing) AS occupied FROM spells LEFT JOIN server_spells ON spells.global_id = server_spells.global_id AND spells.level = server_spells.level WHERE spells.account_id = {0} AND ready <= 0;", account_id);
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        using (NpgsqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    int.TryParse(reader["occupied"].ToString(), out occupied);
                                }
                            }
                        }
                    }

                    if (capacity - occupied >= spell.housing)
                    {
                        if (SpendResources(connection, account_id, spell.requiredGold, spell.requiredElixir, spell.requiredGems, spell.requiredDarkElixir))
                        {
                            query = String.Format("INSERT INTO spells (global_id, level, account_id) VALUES('{0}', {1}, {2})", globalID, level, account_id);
                            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                                response = 1;
                            }
                        }
                        else
                        {
                            response = 2;
                        }
                    }
                    else
                    {
                        response = 4;
                    }
                }
                else
                {
                    response = 3;
                }
                connection.Close();
            }
            return response;
        }

        private static ServerSpell GetServerSpell(NpgsqlConnection connection, string id, int level)
        {
            ServerSpell spell = null;
            string query = String.Format("SELECT id, global_id, level, req_gold, req_elixir, req_gem, req_dark_elixir, brew_time, housing, radius, pulses_count, pulses_duration, pulses_value, pulses_value_2, research_time, research_gold, research_elixir, research_dark_elixir, research_gems, research_xp FROM server_spells WHERE global_id = '{0}' AND level = {1};", id, level);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            spell = new ServerSpell();
                            long.TryParse(reader["id"].ToString(), out spell.databaseID);
                            spell.id = (SpellID)Enum.Parse(typeof(SpellID), reader["global_id"].ToString());
                            int.TryParse(reader["level"].ToString(), out spell.level);
                            int.TryParse(reader["req_gold"].ToString(), out spell.requiredGold);
                            int.TryParse(reader["req_elixir"].ToString(), out spell.requiredElixir);
                            int.TryParse(reader["req_gem"].ToString(), out spell.requiredGems);
                            int.TryParse(reader["req_dark_elixir"].ToString(), out spell.requiredDarkElixir);
                            int.TryParse(reader["brew_time"].ToString(), out spell.brewTime);
                            int.TryParse(reader["housing"].ToString(), out spell.housing);
                            float.TryParse(reader["radius"].ToString(), out spell.radius);
                            int.TryParse(reader["pulses_count"].ToString(), out spell.pulsesCount);
                            float.TryParse(reader["pulses_duration"].ToString(), out spell.pulsesDuration);
                            float.TryParse(reader["pulses_value"].ToString(), out spell.pulsesValue);
                            float.TryParse(reader["pulses_value_2"].ToString(), out spell.pulsesValue2);
                            int.TryParse(reader["research_time"].ToString(), out spell.researchTime);
                            int.TryParse(reader["research_gold"].ToString(), out spell.researchGold);
                            int.TryParse(reader["research_elixir"].ToString(), out spell.researchElixir);
                            int.TryParse(reader["research_dark_elixir"].ToString(), out spell.researchDarkElixir);
                            int.TryParse(reader["research_gems"].ToString(), out spell.researchGems);
                            int.TryParse(reader["research_xp"].ToString(), out spell.researchXp);
                        }
                    }
                }
            }
            return spell;
        }

        public async static void CancelBrewSpell(int id, long databaseID)
        {
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.CANCELBREW);
            long account_id = Server.clients[id].account;
            int res = await CancelBrewSpellAsync(account_id, databaseID);
            packet.Write(res);
            Sender.TCP_Send(id, packet);
        }

        private async static Task<int> CancelBrewSpellAsync(long account_id, long databaseID)
        {
            Task<int> task = Task.Run(() =>
            {
                return Retry.Do(() => _CancelBrewSpellAsync(account_id, databaseID), TimeSpan.FromSeconds(0.1), 10, false);
            });
            return await task;
        }

        private static int _CancelBrewSpellAsync(long account_id, long databaseID)
        {
            int id = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("DELETE FROM spells WHERE id = {0} AND account_id = {1} AND ready <= 0", databaseID, account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                    id = 1;
                }
                connection.Close();
            }
            return id;
        }

        private async static Task<List<Spell>> GetSpellsAsync(long account_id)
        {
            Task<List<Spell>> task = Task.Run(() =>
            {
                return Retry.Do(() => _GetSpellsAsync(account_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static List<Spell> _GetSpellsAsync(long account_id)
        {
            List<Spell> spells = new List<Spell>();
            using (NpgsqlConnection connection = GetDbConnection())
            {
                string query = String.Format("SELECT spells.id, spells.global_id, spells.level, spells.brewed, spells.ready, spells.brewed_time, server_spells.brew_time, server_spells.housing FROM spells LEFT JOIN server_spells ON spells.global_id = server_spells.global_id AND spells.level = server_spells.level WHERE spells.account_id = {0};", account_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                Spell spell = new Spell();
                                spell.id = (SpellID)Enum.Parse(typeof(SpellID), reader["global_id"].ToString());
                                long.TryParse(reader["id"].ToString(), out spell.databaseID);
                                int.TryParse(reader["level"].ToString(), out spell.level);
                                int.TryParse(reader["housing"].ToString(), out spell.hosing);
                                int.TryParse(reader["brew_time"].ToString(), out spell.brewTime);
                                float.TryParse(reader["brewed_time"].ToString(), out spell.brewedTime);

                                int isTrue = 0;
                                int.TryParse(reader["brewed"].ToString(), out isTrue);
                                spell.brewed = isTrue > 0;

                                isTrue = 0;
                                int.TryParse(reader["ready"].ToString(), out isTrue);
                                spell.ready = isTrue > 0;
                                spells.Add(spell);
                            }
                        }
                    }
                }
                connection.Close();
            }
            return spells;
        }

        private static Spell GetSpell(NpgsqlConnection connection, long database_id, long account_id, bool get_server = false)
        {
            Spell spell = null;
            string query = String.Format("SELECT spells.id, spells.global_id, spells.level, spells.brewed, spells.ready, spells.brewed_time, server_spells.brew_time, server_spells.housing FROM spells LEFT JOIN server_spells ON spells.global_id = server_spells.global_id AND spells.level = server_spells.level WHERE spells.id = {0} AND spells.account_id = {1};", database_id, account_id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            spell = new Spell();
                            spell.id = (SpellID)Enum.Parse(typeof(SpellID), reader["global_id"].ToString());
                            long.TryParse(reader["id"].ToString(), out spell.databaseID);
                            int.TryParse(reader["level"].ToString(), out spell.level);
                            int.TryParse(reader["housing"].ToString(), out spell.hosing);
                            int.TryParse(reader["brew_time"].ToString(), out spell.brewTime);
                            float.TryParse(reader["brewed_time"].ToString(), out spell.brewedTime);

                            int isTrue = 0;
                            int.TryParse(reader["brewed"].ToString(), out isTrue);
                            spell.brewed = isTrue > 0;

                            isTrue = 0;
                            int.TryParse(reader["ready"].ToString(), out isTrue);
                            spell.ready = isTrue > 0;
                        }
                    }
                }
            }
            if (spell != null && get_server)
            {
                spell.server = GetServerSpell(connection, spell.id.ToString(), spell.level);
            }
            return spell;
        }

        public static void DeleteSpell(long id, NpgsqlConnection connection)
        {
            string query = String.Format("DELETE FROM spells WHERE id = {0};", id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                command.ExecuteNonQuery();
            }
        }

        #endregion

        #region Research

        private static Research GetResearch(NpgsqlConnection connection, long account_id, string global_id, ResearchType type, bool createIfNotExist = false)
        {
            Research research = null;
            string query = String.Format("SELECT id, level, researching, CASE WHEN researching > NOW() at time zone 'utc' THEN 1 ELSE 0 END AS is_researching FROM research WHERE account_id = {0} AND type = {1} AND global_id = '{2}';", account_id, (int)type, global_id);
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            research = new Research();
                            int is_researching = 0;
                            int.TryParse(reader["is_researching"].ToString(), out is_researching);
                            long.TryParse(reader["id"].ToString(), out research.id);
                            int.TryParse(reader["level"].ToString(), out research.level);
                            DateTime.TryParse(reader["researching"].ToString(), out research.end);
                            research.researching = (is_researching == 1);
                            if (research.researching)
                            {
                                research.level -= 1;
                            }
                            research.globalID = global_id;
                            research.type = type;
                        }
                    }
                }
            }
            if (createIfNotExist && research == null)
            {
                research = new Research();
                query = String.Format("INSERT INTO research (account_id, type, global_id) VALUES({0}, {1}, '{2}') RETURNING id;", account_id, (int)type, global_id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    research.id = (long)command.ExecuteScalar();
                }
                research.globalID = global_id;
                research.level = 1;
                research.type = type;
                research.researching = false;
            }
            return research;
        }

        public async static void DoResearch(int id, ResearchType type, string global_id)
        {
            long account_id = Server.clients[id].account;
            var res = await DoResearchAsync(account_id, type, global_id);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.RESEARCH);
            packet.Write(res.Item1);
            if (res.Item1 == 1)
            {
                string data = Data.Serialize<Research>(res.Item2);
                byte[] bytes = await Data.CompressAsync(data);
                packet.Write(bytes.Length);
                packet.Write(bytes);
            }
            Sender.TCP_Send(id, packet);
        }

        private async static Task<(int, Research)> DoResearchAsync(long account_id, ResearchType type, string global_id)
        {
            Task<(int, Research)> task = Task.Run(() =>
            {
                return Retry.Do(() => _DoResearchAsync(account_id, type, global_id), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static (int, Research) _DoResearchAsync(long account_id, ResearchType type, string global_id)
        {
            Research research = null;
            int response = 0;
            using (NpgsqlConnection connection = GetDbConnection())
            {
                research = GetResearch(connection, account_id, global_id, type, true);
                if (research.researching)
                {
                    response = 3;
                }
                else
                {
                    int time = 0;
                    if (type == ResearchType.unit)
                    {
                        ServerUnit unit = Unit.GetServerUnit(global_id, research.level + 1);
                        if (unit != null)
                        {
                            if (SpendResources(connection, account_id, unit.researchGold, unit.researchElixir, unit.researchGems, unit.researchDarkElixir))
                            {
                                time = unit.researchTime;
                                AddXP(connection, account_id, unit.researchXp);
                                response = 1;
                            }
                            else
                            {
                                response = 2;
                            }
                        }
                    }
                    else if (type == ResearchType.spell)
                    {
                        ServerSpell spell = GetServerSpell(connection, global_id, research.level + 1);
                        if (spell != null)
                        {
                            if (SpendResources(connection, account_id, spell.researchGold, spell.researchElixir, spell.researchGems, spell.researchDarkElixir))
                            {
                                time = spell.researchTime;
                                AddXP(connection, account_id, spell.researchXp);
                                response = 1;
                            }
                            else
                            {
                                response = 2;
                            }
                        }
                    }
                    if (response == 1)
                    {
                        string query = String.Format("UPDATE research SET level = level + 1, researching = NOW() at time zone 'utc' + INTERVAL '{0} SECOND' WHERE id = {1};", time, research.id);
                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                        research = GetResearch(connection, account_id, global_id, type);
                    }
                }
                connection.Close();
            }
            return (response, research);
        }

        #endregion

    }
}