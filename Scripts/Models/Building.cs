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

    public enum BuildingID
    {
        townhall = 0, goldmine = 1, goldstorage = 2, elixirmine = 3, elixirstorage = 4, darkelixirmine = 5, darkelixirstorage = 6, buildershut = 7, armycamp = 8, barracks = 9, darkbarracks = 10, wall = 11, cannon = 12, archertower = 13, mortor = 14, airdefense = 15, wizardtower = 16, hiddentesla = 19, bombtower = 20, xbow = 21, infernotower = 22, decoration = 23, obstacle = 24, boomb = 25, springtrap = 26, airbomb = 27, giantbomb = 28, seekingairmine = 29, skeletontrap = 30, clancastle = 31, spellfactory = 32, darkspellfactory = 33, laboratory = 34, airsweeper = 35, kingaltar = 36, qeenaltar = 37
    }

    public class Building {
        public const int GENERAL_LAYOUT = 1;
        public const int WAR_LAYOUT = 2;

        private static List<BuildingID> NonCNFTBuildings = new List<BuildingID>() {
            BuildingID.obstacle
        };

        public enum BuildingTargetType
        {
            none = 0, ground = 1, air = 2, all = 3
        }

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

        public async static Task<Data.ServerBuilding> GetServerBuildingAsync(string id, int level)
        {
            Task<Data.ServerBuilding> task = Task.Run(() =>
            {
                return Retry.Do(() => _GetServerBuildingAsync(id, level), TimeSpan.FromSeconds(0.1), 1, false);
            });
            return await task;
        }

        private static Data.ServerBuilding _GetServerBuildingAsync(string id, int level)
        {
            Data.ServerBuilding data = null;
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
                                data = new Data.ServerBuilding();
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

        private static List<Data.ServerBuilding> GetServerBuildings(NpgsqlConnection connection)
        {
            List<Data.ServerBuilding> buildings = new List<Data.ServerBuilding>();
            string query = String.Format("SELECT id, global_id, level, req_gold, req_elixir, req_gems, req_dark_elixir, columns_count, rows_count, build_time, gained_xp FROM server_buildings;");
            using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            Data.ServerBuilding building = new Data.ServerBuilding();
                            long.TryParse(reader["id"].ToString(), out building.databaseID);
                            // building.id = (Data.BuildingID)Enum.Parse(typeof(Data.BuildingID), reader["global_id"].ToString());
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
        private static List<Data.Building> GetBuildings(long account)
        {
            List<Data.Building> data = new List<Data.Building>();
            string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.level, buildings.x_position, buildings.x_war, buildings.y_war, buildings.boost, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, buildings.y_position, buildings.construction_time, buildings.is_constructing, buildings.construction_build_time, server_buildings.columns_count, server_buildings.rows_count, server_buildings.health, server_buildings.speed, server_buildings.radius, server_buildings.capacity, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity, server_buildings.damage, server_buildings.target_type, server_buildings.blind_radius, server_buildings.splash_radius, server_buildings.projectile_speed FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0};", account);
            using (NpgsqlCommand command = new NpgsqlCommand(query, Database.GetDbConnection()))
            {
                using (NpgsqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            Data.Building building = new Data.Building();
                            building.id = (Data.BuildingID)Enum.Parse(typeof(Data.BuildingID), reader["global_id"].ToString());
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
                                building.targetType = (Data.BuildingTargetType)Enum.Parse(typeof(Data.BuildingTargetType), tt);
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



        public async Task<bool> Create() {
            is_cnft = !NonCNFTBuildings.Contains(id);
            Data.ServerBuilding building = await GetServerBuildingAsync(id.ToString(), level);

            if (building == null || x < 0 || y < 0 || x + building.columns > Data.gridSize /* x position more than max size */ || y + building.rows > Data.gridSize  /* y position more than max size */)
            {
                return false;
            }

            List<Data.Building> buildings = GetBuildings(account_id);
            for (int i = 0; i < buildings.Count; i++)
            {
                int bX = buildings[i].x;
                int bY = buildings[i].y;
                Rectangle rect1 = new Rectangle(bX, bY, buildings[i].columns, buildings[i].rows);
                Rectangle rect2 = new Rectangle(x, y, building.columns, building.rows);

                // intersected with another building
                if (rect2.IntersectsWith(rect1))
                {
                    return false;
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
                _ = HttpSender.PostJson("/mintBuilding", new Dictionary<string, string>(){
                    ["address"] = address,
                    ["building_id"] = building_id.ToString(),
                });
            }
            return true;
        }
    }
}