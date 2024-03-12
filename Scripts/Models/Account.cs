using System.Threading.Tasks;
using System;
using Npgsql;
using Memewars.RealtimeNetworking.Server;
using System.Collections.Generic;
using Newtonsoft.Json;
using WebUtils;

namespace Models {

        public class Player
        {
            public long id = 0;
            public string name = "Player";
            public int gems = 0;
            public int trophies = 0;
            public bool banned = false;
            public DateTime nowTime;
            public DateTime shield;
            public int xp = 0;
            public int level = 1;
            public DateTime clanTimer;
            public long clanID = 0;
            public int clanRank = 0;
            public long warID = 0;
            public string email = "";
            public int layout = 0;
            public DateTime shield1;
            public DateTime shield2;
            public DateTime shield3;
            public long guild_id;
            public string guild_name;
            public string guild_logo;
            public List<Building> buildings = new List<Building>();
            public List<Unit> units = new List<Unit>();
            public List<Spell> spells = new List<Spell>();
        }

    public class Account {
        public long Id { get; set; }
        public string Address { get; set; }

        private (int, int, int, int) AddResources(NpgsqlConnection connection, int gold, int elixir, int darkElixir, int gems)
        {
            int addedGold = 0;
            int addedElixir = 0;
            int addedDark = 0;

            if (gold > 0 || elixir > 0 || darkElixir > 0)
            {
                List<Building> storages = new List<Building>();
                string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0} AND buildings.global_id IN('{1}', '{2}', '{3}', '{4}') AND buildings.level > 0;", Id, BuildingID.townhall.ToString(), BuildingID.goldstorage.ToString(), BuildingID.elixirstorage.ToString(), BuildingID.darkelixirstorage.ToString());
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
                string query = String.Format("UPDATE accounts SET gems = gems + {0} WHERE id = {1};", gems, Id);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }

            return (addedGold, addedElixir, addedDark, gems);
        }

        public async Task<long> Create() {
            using var connection = Database.GetDbConnection();
            string query = String.Format("INSERT INTO accounts (device_id, password, name, address) VALUES('{0}', '{1}', '{2}', '{3}') RETURNING id;", "", "", "", Address);
            
            // get Id
            using NpgsqlCommand command = new NpgsqlCommand(query, connection);
            Id = (long)command.ExecuteScalar();

            // mints the cNFT
            // dont need to await since we want it to run in parallel
            _ = HttpSender.PostJson("/mintAccount", new Dictionary<string, string>(){
                ["address"] = Address,
            });

            List<long> BuildingIds = new List<long>
            {
                // need to find a way to create cnfts using one tx
                await new Building
                {
                    id = BuildingID.townhall,
                    account_id = Id,
                    x = 25,
                    y = 25,
                    warX = 25,
                    warY = 25,
                    level = 1,
                    address = Address,
                }.Create(),
                await new Building
                {
                    id = BuildingID.goldmine,
                    account_id = Id,
                    x = 27,
                    y = 21,
                    warX = 27,
                    warY = 21,
                    level = 1,
                    address = Address,
                }.Create(),
                await new Building
                {
                    id = BuildingID.goldstorage,
                    account_id = Id,
                    x = 30,
                    y = 28,
                    warX = 30,
                    warY = 28,
                    level = 1,
                    address = Address,
                }.Create(),
                await new Building
                {
                    id = BuildingID.elixirmine,
                    account_id = Id,
                    x = 21,
                    y = 27,
                    warX = 21,
                    warY = 27,
                    level = 1,
                    address = Address,
                }.Create(),
                await new Building
                {
                    id = BuildingID.elixirstorage,
                    account_id = Id,
                    x = 25,
                    y = 30,
                    warX = 25,
                    warY = 30,
                    level = 1,
                    address = Address,
                }.Create(),
                await new Building
                {
                    id = BuildingID.buildershut,
                    account_id = Id,
                    x = 22,
                    y = 24,
                    warX = 22,
                    warY = 24,
                    level = 1,
                    address = Address,
                }.Create()
            };

            List<int> xl = new List<int> { 19, 24, 32, 32, 34, 30, 26, 17, 8, 3, 2, 5, 16, 26, 35, 40 };
            List<int> yl = new List<int> { 20, 15, 16, 24, 30, 33, 35, 37, 32, 39, 10, 4, 1, 3, 1, 5 };
            Random rnd = new Random();
            for (int i = 1; i <= 5; i++)
            {
                int index = rnd.Next(0, xl.Count);
                int level = rnd.Next(1, 6);

                // add random obstacles
                // dont mint these as cnft
                await new Building {
                    id = BuildingID.obstacle,
                    account_id = Id,
                    x = xl[index],
                    y = yl[index],
                    warX = xl[index],
                    warY = yl[index],
                    level = level,
                    address = Address,
                }.Create();

                xl.RemoveAt(index);
                yl.RemoveAt(index);
            }

            AddResources(connection, 10000, 10000, 0, 250);

            // builk mint the buildings
            _ = HttpSender.PostJson("/mintBuildings", new Dictionary<string, string>(){
                ["address"] = Address,
                ["building_ids"] = JsonConvert.SerializeObject(BuildingIds),
            });
            connection.Close();
            return Id;
        }
    
        public static Player Get(long id) {
            string query = string.Format(@"
                SELECT 
                    accounts.id, 
                    accounts.name, 
                    gems, 
                    trophies, 
                    banned, 
                    shield, 
                    level, 
                    xp, 
                    clan_join_timer, 
                    clan_id, 
                    clan_rank, 
                    war_id, 
                    NOW() at time zone 'utc' AS now_time, 
                    email, 
                    map_layout, 
                    shld_cldn_1, 
                    shld_cldn_2, 
                    shld_cldn_3,
                    guild_id,
                    guilds.logo as guild_logo,
                    guilds.name as guild_name
                FROM accounts 
                left join guilds
                on guilds.id = accounts.guild_id
                WHERE accounts.id = {0};", id);
                
            Player data = new Player();
            using NpgsqlConnection connection = Database.GetDbConnection();
            using NpgsqlCommand command = new(query, connection);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    data.id = id;
                    data.name = reader["name"].ToString();
                    data.email = reader["email"].ToString();
                    _ = int.TryParse(reader["gems"].ToString(), out data.gems);
                    _ = int.TryParse(reader["trophies"].ToString(), out data.trophies);
                    _ = int.TryParse(reader["banned"].ToString(), out int ban);
                    data.banned = ban > 0;
                    _ = DateTime.TryParse(reader["now_time"].ToString(), out data.nowTime);
                    _ = DateTime.TryParse(reader["shield"].ToString(), out data.shield);
                    _ = DateTime.TryParse(reader["clan_join_timer"].ToString(), out data.clanTimer);
                    _ = DateTime.TryParse(reader["shld_cldn_1"].ToString(), out data.shield1);
                    _ = DateTime.TryParse(reader["shld_cldn_2"].ToString(), out data.shield2);
                    _ = DateTime.TryParse(reader["shld_cldn_3"].ToString(), out data.shield3);
                    _ = int.TryParse(reader["level"].ToString(), out data.level);
                    _ = int.TryParse(reader["xp"].ToString(), out data.xp);
                    _ = long.TryParse(reader["clan_id"].ToString(), out data.clanID);
                    _ = int.TryParse(reader["clan_rank"].ToString(), out data.clanRank);
                    _ = long.TryParse(reader["war_id"].ToString(), out data.warID);
                    _ = int.TryParse(reader["map_layout"].ToString(), out data.layout);
                    _ = long.TryParse(reader["guild_id"].ToString(), out data.guild_id);
                    data.guild_logo = string.IsNullOrEmpty(reader["guild_logo"].ToString())? "" : (string) reader["guild_logo"];
                    data.guild_name = string.IsNullOrEmpty(reader["guild_name"].ToString())? "" : (string) reader["guild_name"];
                }
            }
            return data;
        }
    }
}