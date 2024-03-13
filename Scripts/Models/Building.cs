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

    public enum BuildingLayoutType
    {
        normal = 1, war = 2,
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

        public static ServerBuilding GetServerBuilding(string id, int level)
        {
            ServerBuilding data = null;
            string query = String.Format("SELECT * FROM server_buildings WHERE global_id = '{0}' AND level = {1};", id, level);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                data = new ServerBuilding
                {
                    id = id
                };
                _ = long.TryParse(ret["id"], out data.databaseID);
                _ = int.TryParse(ret["req_gold"], out data.requiredGold);
                _ = int.TryParse(ret["req_elixir"], out data.requiredElixir);
                _ = int.TryParse(ret["req_gems"], out data.requiredGems);
                _ = int.TryParse(ret["req_dark_elixir"], out data.requiredDarkElixir);
                data.level = level;
                _ = int.TryParse(ret["columns_count"], out data.columns);
                _ = int.TryParse(ret["rows_count"], out data.rows);
                _ = int.TryParse(ret["build_time"], out data.buildTime);
                _ = int.TryParse(ret["gained_xp"], out data.gainedXp);
            }
            return data;
        }

        public static List<ServerBuilding> GetServerBuildings()
        {
            List<ServerBuilding> buildings = new List<ServerBuilding>();
            string query = String.Format("SELECT id, global_id, level, req_gold, req_elixir, req_gems, req_dark_elixir, columns_count, rows_count, build_time, gained_xp FROM server_buildings;");
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    ServerBuilding building = new();
                    _ = long.TryParse(res["id"], out building.databaseID);
                    // building.id = (BuildingID)Enum.Parse(typeof(BuildingID), reader["global_id"]);
                    _ = building.id = res["global_id"];
                    _ = int.TryParse(res["level"], out building.level);
                    _ = int.TryParse(res["req_gold"], out building.requiredGold);
                    _ = int.TryParse(res["req_elixir"], out building.requiredElixir);
                    _ = int.TryParse(res["req_gems"], out building.requiredGems);
                    _ = int.TryParse(res["req_dark_elixir"], out building.requiredDarkElixir);
                    _ = int.TryParse(res["columns_count"], out building.columns);
                    _ = int.TryParse(res["rows_count"], out building.rows);
                    _ = int.TryParse(res["build_time"], out building.buildTime);
                    _ = int.TryParse(res["gained_xp"], out building.gainedXp);
                    buildings.Add(building);
                }
            }
            return buildings;
        }
        private static List<Building> GetBuildings(long account)
        {
            List<Building> data = new List<Building>();
            string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.level, buildings.x_position, buildings.x_war, buildings.y_war, buildings.boost, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, buildings.y_position, buildings.construction_time, buildings.is_constructing, buildings.construction_build_time, server_buildings.columns_count, server_buildings.rows_count, server_buildings.health, server_buildings.speed, server_buildings.radius, server_buildings.capacity, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity, server_buildings.damage, server_buildings.target_type, server_buildings.blind_radius, server_buildings.splash_radius, server_buildings.projectile_speed FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0};", account);
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    Building building = new()
                    {
                        id = (BuildingID)Enum.Parse(typeof(BuildingID), res["global_id"].ToString())
                    };
                    _ = long.TryParse(res["id"], out building.databaseID);
                    _ = int.TryParse(res["level"], out building.level);
                    _ = int.TryParse(res["x_position"], out building.x);
                    _ = int.TryParse(res["y_position"], out building.y);
                    _ = int.TryParse(res["x_war"], out building.warX);
                    _ = int.TryParse(res["y_war"], out building.warY);
                    _ = int.TryParse(res["columns_count"], out building.columns);
                    _ = int.TryParse(res["rows_count"], out building.rows);

                    _ = float.TryParse(res["gold_storage"], out float storage);
                    building.goldStorage = (int)Math.Floor(storage);

                    storage = 0;
                    _ = float.TryParse(res["elixir_storage"], out storage);
                    building.elixirStorage = (int)Math.Floor(storage);

                    storage = 0;
                    _ = float.TryParse(res["dark_elixir_storage"], out storage);
                    building.darkStorage = (int)Math.Floor(storage);

                    _ = DateTime.TryParse(res["boost"], out building.boost);
                    _ = float.TryParse(res["damage"], out building.damage);
                    _ = int.TryParse(res["capacity"], out building.capacity);
                    _ = int.TryParse(res["gold_capacity"], out building.goldCapacity);
                    _ = int.TryParse(res["elixir_capacity"], out building.elixirCapacity);
                    _ = int.TryParse(res["dark_elixir_capacity"], out building.darkCapacity);
                    _ = float.TryParse(res["speed"], out building.speed);
                    _ = float.TryParse(res["radius"], out building.radius);
                    _ = int.TryParse(res["health"], out building.health);
                    _ = DateTime.TryParse(res["construction_time"], out building.constructionTime);
                    _ = float.TryParse(res["blind_radius"], out building.blindRange);
                    _ = float.TryParse(res["splash_radius"], out building.splashRange);
                    _ = float.TryParse(res["projectile_speed"], out building.rangedSpeed);
                    string tt = res["target_type"];
                    if (!string.IsNullOrEmpty(tt))
                    {
                        building.targetType = (BuildingTargetType)Enum.Parse(typeof(BuildingTargetType), tt);
                    }
                    _ = int.TryParse(res["is_constructing"], out int isConstructing);
                    building.isConstructing = isConstructing > 0;
                    _ = int.TryParse(res["construction_build_time"], out building.buildTime);
                    data.Add(building);
                }
            }
            return data;
        }

        public long Create() {
            is_cnft = !NonCNFTBuildings.Contains(id);
            ServerBuilding building = GetServerBuilding(id.ToString(), level);

            if (building == null || x < 0 || y < 0 || x + building.columns > Data.gridSize /* x position more than max size */ || y + building.rows > Data.gridSize  /* y position more than max size */)
            {
                return -1;
            }

            List<Building> buildings = GetBuildings(account_id);
            for (int i = 0; i < buildings.Count; i++)
            {
                int bX = buildings[i].x;
                int bY = buildings[i].y;
                Rectangle rect1 = new(bX, bY, buildings[i].columns, buildings[i].rows);
                Rectangle rect2 = new(x, y, building.columns, building.rows);

                // intersected with another building
                if (rect2.IntersectsWith(rect1))
                {
                    return -1;
                }
            }

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
            var building_id = (long)Database.ExecuteScalar(query);

            // mints the cNFT
            // only mint if needed
            // dont need to await since we want it to run in parallel
            if(is_cnft) {
                return building_id;
            }
            
            return -1;
        }


        public static Building Get(long id)
        {
            Building building = null;
            string query = String.Format("SELECT level, global_id FROM buildings WHERE id = {0};", id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                building = new Building
                {
                    id = (BuildingID)Enum.Parse(typeof(BuildingID), ret["global_id"])
                };
                _ = int.TryParse(ret["level"], out building.level);
            }
            return building;
        }
        public static List<Building> GetByGlobalID(string globalID, long account)
        {
            List<Building> buildings = new List<Building>();
            string query = String.Format("SELECT buildings.level, buildings.global_id, server_buildings.capacity FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.global_id = '{0}' AND buildings.account_id = {1};", globalID, account);
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    Building building = new()
                    {
                        id = (BuildingID)Enum.Parse(typeof(BuildingID), res["global_id"])
                    };
                    _ = int.TryParse(res["level"], out building.level);
                    _ = int.TryParse(res["capacity"], out building.capacity);
                    buildings.Add(building);
                }
            }
            return buildings;
        }

        public static int? GetBuildTime(string globalId, int level) {
            int? time = null;
            string query = String.Format("SELECT build_time FROM server_buildings WHERE global_id = '{0}' AND level = {1};", globalId, level);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                time = int.Parse(ret["build_time"]);
                
            }
            return time;
        }
        
        public static int Place(long account_id, ServerBuilding building, int x, int y, int layout, long layoutID)
        {
            int response = 0;
            if (building == null || x < 0 || y < 0 || x + building.columns > Data.gridSize || y + building.rows > Data.gridSize)
            {
                response = 4;
                return response;
            }

            bool IsWarLayout = layout == (int)BuildingLayoutType.war;
            List<Building> buildings = GetBuildings(account_id);
            for (int i = 0; i < buildings.Count; i++)
            {
                int bX = IsWarLayout ? buildings[i].warX : buildings[i].x;
                int bY = IsWarLayout ? buildings[i].warY : buildings[i].y;
                Rectangle rect1 = new(bX, bY, buildings[i].columns, buildings[i].rows);
                Rectangle rect2 = new(x, y, building.columns, building.rows);

                // intersected
                if (rect2.IntersectsWith(rect1))
                {
                    response = 4;
                    return response;
                }
            }

            // war layout
            if (IsWarLayout)
            {
                long war_id = 0;
                string warQuery = String.Format("SELECT war_id FROM accounts WHERE id = {0};", account_id);
                var ret = Database.ExecuteForSingleResult(warQuery);
                if (ret != null)
                {
                    _ = long.TryParse(ret["war_id"], out war_id);
                }

                // no war
                if (war_id <= 0)
                {
                    return response;
                }

                int war_stage = 0;
                warQuery = string.Format("SELECT stage FROM clan_wars WHERE id = {0};", war_id);
                ret = Database.ExecuteForSingleResult(warQuery);
                if (ret != null)
                {
                    _ = int.TryParse(ret["stage"], out war_stage);
                }

                if (war_stage == 1)
                {
                    warQuery = string.Format("UPDATE buildings SET x_war = {0}, y_war = {1} WHERE id = {2}", x, y, layoutID);
                    Database.ExecuteNonQuery(warQuery);
                    response = 1;
                }

                return response;
            }
            
            int buildersCount = Account.GetBuildingCount(account_id, "buildershut");
            if (building.id == "buildershut")
            {
                // todo
                building.requiredGems = buildersCount switch
                {
                    0 => 0,
                    1 => 250,
                    2 => 500,
                    3 => 1000,
                    4 => 2000,
                    _ => 999999,
                };
            }

            int? time = GetBuildTime(building.id, 1);
            bool haveBuilding = time != null; // -1 = no building

            if(!haveBuilding) {
                response = 3;
                return response;
            }

            int constructingCount = Account.GetBuildingConstructionCount(account_id);

            // out of workers
            if (time > 0 && buildersCount <= constructingCount)
            {
                response = 5;
                return response;
            }

            Building townHall = GetByGlobalID("townhall", account_id)[0];
            if (building.id == "townhall")
            {
                // dont place townhall
                return response;
            }

            BuildingCount limits = Data.GetBuildingLimits(townHall.level, building.id);
            int haveCount = Account.GetBuildingCount(account_id, building.id);

            // limit reached
            if (limits == null || haveCount >= limits.count)
            {
                response = 6;
                return response;
            }

            if (!Account.SpendResources(account_id, building.requiredGold, building.requiredElixir, building.requiredGems, building.requiredDarkElixir))
            {
                response = 2;
                return response;
            }

            string query;
            if (time > 0)
            {
                query = String.Format("INSERT INTO buildings (global_id, account_id, x_position, y_position, level, is_constructing, construction_time, construction_build_time, track_time) VALUES('{0}', {1}, {2}, {3}, 0, 1, NOW() at time zone 'utc' + INTERVAL '{4} SECOND', {5}, NOW() at time zone 'utc' - INTERVAL '1 HOUR');", building.id, account_id, x, y, time, time);
            }
            else
            {
                // instant build
                query = String.Format("INSERT INTO buildings (global_id, account_id, x_position, y_position, level, is_constructing, track_time) VALUES('{0}', {1}, {2}, {3}, 1, 0, NOW() at time zone 'utc' - INTERVAL '1 HOUR');", building.id, account_id, x, y);
                Account.AddXP(account_id, building.gainedXp);
            }
            Database.ExecuteNonQuery(query);
            response = 1;
            return response;
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