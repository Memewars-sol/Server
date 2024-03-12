using System;
using Npgsql;
using Memewars.RealtimeNetworking.Server;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using WebUtils;

namespace Models {

    public class BuildingAvailability
    {
        public int level = 1;
        public BuildingCount[] buildings = null;
    }

    public class BuildingCount
    {
        public string id = "global_id";
        public int count = 0;
        public int maxLevel = 1;
        public int have = 0;
    }

    public enum BuildingID
    {
        townhall = 0, goldmine = 1, goldstorage = 2, elixirmine = 3, elixirstorage = 4, darkelixirmine = 5, darkelixirstorage = 6, buildershut = 7, armycamp = 8, barracks = 9, darkbarracks = 10, wall = 11, cannon = 12, archertower = 13, mortor = 14, airdefense = 15, wizardtower = 16, hiddentesla = 19, bombtower = 20, xbow = 21, infernotower = 22, decoration = 23, obstacle = 24, boomb = 25, springtrap = 26, airbomb = 27, giantbomb = 28, seekingairmine = 29, skeletontrap = 30, clancastle = 31, spellfactory = 32, darkspellfactory = 33, laboratory = 34, airsweeper = 35, kingaltar = 36, qeenaltar = 37
    }

    public enum BuildingTargetType
    {
        none = 0, ground = 1, air = 2, all = 3
    }

    public class ServerBuilding
    {
        public string id = "";
        public int level = 0;
        public int type = 0;
        public long databaseID = 0;
        public int requiredGold = 0;
        public int requiredElixir = 0;
        public int requiredGems = 0;
        public int requiredDarkElixir = 0;
        public int columns = 0;
        public int rows = 0;
        public int buildTime = 0;
        public int gainedXp = 0;
    }

    public class Building {
        public const int GENERAL_LAYOUT = 1;
        public const int WAR_LAYOUT = 2;

        private static List<BuildingID> NonCNFTBuildings = new List<BuildingID>() {
            BuildingID.obstacle
        };

        // This building's params
        public BuildingID id = BuildingID.townhall;
        public long account_id { get; set; }
        public string address { get; set; }
        public int level = 0;
        public long databaseID = 0;
        public int x = 0;
        public int y = 0;
        public int warX = -1;
        public int warY = -1;
        public int columns = 0;
        public int rows = 0;
        public int goldStorage = 0;
        public int elixirStorage = 0;
        public int darkStorage = 0;
        public DateTime boost;
        public int health = 100;
        public float damage = 0;
        public int capacity = 0;
        public int goldCapacity = 0;
        public int elixirCapacity = 0;
        public int darkCapacity = 0;
        public float speed = 0;
        public float radius = 0;
        public DateTime constructionTime;
        public bool isConstructing = false;
        public int buildTime = 0;
        public BuildingTargetType targetType = BuildingTargetType.none;
        public float blindRange = 0;
        public float splashRange = 0;
        public float rangedSpeed = 5;
        public double percentage = 0;
        public bool is_in_inventory = false;
        public bool is_cnft = true;
        // end this building's params

        public async static Task<ServerBuilding> GetServerBuildingAsync(string id, int level)
        {
            Task<ServerBuilding> task = Task.Run(() =>
            {
                return Retry.Do(() => _GetServerBuildingAsync(id, level), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static ServerBuilding _GetServerBuildingAsync(string id, int level)
        {
            ServerBuilding data = null;
            using (NpgsqlConnection connection = Database.GetDbConnection())
            {
                string query = String.Format("SELECT * FROM server_buildings WHERE global_id = '{0}' AND level = {1};", id, level);
                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                data = new ServerBuilding();
                                data.id = id;
                                long.TryParse(reader["id"].ToString(), out data.databaseID);
                                int.TryParse(reader["req_gold"].ToString(), out data.requiredGold);
                                int.TryParse(reader["req_elixir"].ToString(), out data.requiredElixir);
                                int.TryParse(reader["req_gems"].ToString(), out data.requiredGems);
                                int.TryParse(reader["req_dark_elixir"].ToString(), out data.requiredDarkElixir);
                                data.level = level;
                                int.TryParse(reader["columns_count"].ToString(), out data.columns);
                                int.TryParse(reader["rows_count"].ToString(), out data.rows);
                                int.TryParse(reader["build_time"].ToString(), out data.buildTime);
                                int.TryParse(reader["gained_xp"].ToString(), out data.gainedXp);
                            }
                        }
                    }
                }
                connection.Close();
            }
            return data;
        }

        private static List<ServerBuilding> GetServerBuildings(NpgsqlConnection connection)
        {
            List<ServerBuilding> buildings = new List<ServerBuilding>();
            string query = String.Format("SELECT id, global_id, level, req_gold, req_elixir, req_gems, req_dark_elixir, columns_count, rows_count, build_time, gained_xp FROM server_buildings;");
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            ServerBuilding building = new ServerBuilding();
                            long.TryParse(reader["id"].ToString(), out building.databaseID);
                            // building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"].ToString());
                            building.id = reader["global_id"].ToString();
                            int.TryParse(reader["level"].ToString(), out building.level);
                            int.TryParse(reader["req_gold"].ToString(), out building.requiredGold);
                            int.TryParse(reader["req_elixir"].ToString(), out building.requiredElixir);
                            int.TryParse(reader["req_gems"].ToString(), out building.requiredGems);
                            int.TryParse(reader["req_dark_elixir"].ToString(), out building.requiredDarkElixir);
                            int.TryParse(reader["columns_count"].ToString(), out building.columns);
                            int.TryParse(reader["rows_count"].ToString(), out building.rows);
                            int.TryParse(reader["build_time"].ToString(), out building.buildTime);
                            int.TryParse(reader["gained_xp"].ToString(), out building.gainedXp);
                            buildings.Add(building);
                        }
                    }
                }
            }
            return buildings;
        }
        private static List<Building> GetBuildings(long account)
        {
            List<Building> data = new List<Building>();
            string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.level, buildings.x_position, buildings.x_war, buildings.y_war, buildings.boost, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, buildings.y_position, buildings.construction_time, buildings.is_constructing, buildings.construction_build_time, server_buildings.columns_count, server_buildings.rows_count, server_buildings.health, server_buildings.speed, server_buildings.radius, server_buildings.capacity, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity, server_buildings.damage, server_buildings.target_type, server_buildings.blind_radius, server_buildings.splash_radius, server_buildings.projectile_speed FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0};", account);
            using (NpgsqlCommand command = new NpgsqlCommand(query, Database.GetDbConnection()))
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



        public async Task<long> Create() {
            is_cnft = !NonCNFTBuildings.Contains(id);
            ServerBuilding building = await GetServerBuildingAsync(id.ToString(), level);

            if (building == null || x < 0 || y < 0 || x + building.columns > Data.gridSize /* x position more than max size */ || y + building.rows > Data.gridSize  /* y position more than max size */)
            {
                return -1;
            }

            List<Building> buildings = GetBuildings(account_id);
            for (int i = 0; i < buildings.Count; i++)
            {
                int bX = buildings[i].x;
                int bY = buildings[i].y;
                Rectangle rect1 = new Rectangle(bX, bY, buildings[i].columns, buildings[i].rows);
                Rectangle rect2 = new Rectangle(x, y, building.columns, building.rows);

                // intersected with another building
                if (rect2.IntersectsWith(rect1))
                {
                    return -1;
                }
            }

            using NpgsqlConnection connection = Database.GetDbConnection();
            string query = string.Format(@"INSERT INTO buildings (
                        global_id, 
                        account_id, 
                        x_position, 
                        y_position, 
                        level, 
                        track_time, 
                        x_war, 
                        y_war,
                        is_cnft
                    ) VALUES (
                        '{0}', 
                        {1}, 
                        {2}, 
                        {3}, 
                        {4}, 
                        NOW() at time zone 'utc' - INTERVAL '1 HOUR', 
                        {5}, 
                        {6},
                        {7}
                    ) RETURNING id;", id, account_id, x, y, level, warX, warY, is_cnft);
            using NpgsqlCommand command = new NpgsqlCommand(query, connection);
            var building_id = (long)command.ExecuteScalar();

            // mints the cNFT
            // only mint if needed
            // dont need to await since we want it to run in parallel
            if(is_cnft) {
                return building_id;
            }
            
            return -1;
        }

        public static List<Battle.Building> ConvertToBattleBuildings(List<Building> buildings, BattleType type)
        {
            List<Battle.Building> battleBuildings = new List<Battle.Building>();
            int townhallLevel = 1;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].id == BuildingID.townhall)
                {
                    townhallLevel = buildings[i].level;
                    break;
                }
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].databaseID != buildings[i].databaseID || buildings[i].id != buildings[i].id || buildings[i].health != buildings[i].health || buildings[i].damage != buildings[i].damage || buildings[i].percentage != buildings[i].percentage)
                {
                    return null;
                }

                Battle.Building building = new Battle.Building();
                building.building = buildings[i];
                if (type == BattleType.war)
                {
                    building.building.x = building.building.warX;
                    building.building.y = building.building.warY;
                }

                if (building.building.x < 0 || building.building.y < 0)
                {
                    continue;
                }

                building.building.x += Data.battleGridOffset;
                building.building.y += Data.battleGridOffset;

                // bool storage = false;
                switch (building.building.id)
                {
                    case BuildingID.townhall:
                        building.lootGoldStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.goldStorage);
                        building.lootElixirStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.elixirStorage);
                        building.lootDarkStorage = Data.GetStorageDarkElixirLoot(townhallLevel, building.building.darkStorage);
                        // storage = true;
                        break;
                    case BuildingID.goldmine:
                        building.lootGoldStorage = Data.GetMinesGoldAndElixirLoot(townhallLevel, building.building.goldStorage);
                        // storage = true;
                        break;
                    case BuildingID.goldstorage:
                        building.lootGoldStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.goldStorage);
                        // storage = true;
                        break;
                    case BuildingID.elixirmine:
                        building.lootElixirStorage = Data.GetMinesGoldAndElixirLoot(townhallLevel, building.building.elixirStorage);
                        // storage = true;
                        break;
                    case BuildingID.elixirstorage:
                        building.lootElixirStorage = Data.GetStorageGoldAndElixirLoot(townhallLevel, building.building.elixirStorage);
                        // storage = true;
                        break;
                    case BuildingID.darkelixirmine:
                        building.lootDarkStorage = Data.GetMinesDarkElixirLoot(townhallLevel, building.building.darkStorage);
                        // storage = true;
                        break;
                    case BuildingID.darkelixirstorage:
                        building.lootDarkStorage = Data.GetStorageDarkElixirLoot(townhallLevel, building.building.darkStorage);
                        // storage = true;
                        break;
                }
                /*
                if (storage)
                {
                    Data.BattleStartBuildingData st = new Data.BattleStartBuildingData();
                    st.id = building.building.id;
                    st.databaseID = building.building.databaseID;
                    st.lootGoldStorage = building.building.goldStorage;
                    st.lootElixirStorage = building.building.elixirStorage;
                    st.lootDarkStorage = building.building.darkStorage;
                    startData.Add(st);
                }
                */
                battleBuildings.Add(building);
            }
            return battleBuildings;
        }

    }
}