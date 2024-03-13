using System.Threading.Tasks;
using System;
using Npgsql;
using Memewars.RealtimeNetworking.Server;
using System.Collections.Generic;
using Newtonsoft.Json;
using WebUtils;

namespace Models {

    public class InitializationData
    {
        public long accountID = 0;
        public string password = "";
        public string[] versions;
        public List<ServerBuilding> serverBuildings = new List<ServerBuilding>();
        public List<ServerUnit> serverUnits = new List<ServerUnit>();
        public List<ServerSpell> serverSpells = new List<ServerSpell>();
        public List<Research> research = new List<Research>();
    }

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

    public class PlayerRank
    {
        public long id = 0;
        public int rank = 0;
        public string name = "";
        public int trophies = 0;
        public int xp = 0;
        public int level = 0;
    }

    public class PlayersRanking
    {
        public int page = 1;
        public int pagesCount = 1;
        public List<PlayerRank> players = new List<PlayerRank>();
    }

    public class Account {
        private static double players_ranking_per_page = 30;

        public long Id { get; set; }
        public string Address { get; set; }

        public async Task<long> Create() {
            string query = string.Format("INSERT INTO accounts (device_id, password, name, address) VALUES('{0}', '{1}', '{2}', '{3}') RETURNING id;", "", "", "", Address);
            
            // get Id
            Id = (long)Database.ExecuteScalar(query);

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

            AddResources(Id, 10000, 10000, 0, 250);

            // builk mint the buildings
            _ = HttpSender.PostJson("/mintBuildings", new Dictionary<string, string>(){
                ["address"] = Address,
                ["building_ids"] = JsonConvert.SerializeObject(BuildingIds),
            });

            return Id;
        }

        public static async Task<InitializationData> GetInitializationData(int id, string address) {
            InitializationData initializationData = new();
            string query = string.Format("SELECT id, password, is_online, client_id FROM accounts WHERE address = '{0}'", address);
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                bool online = int.Parse(ret[0]["is_online"]) > 0;
                int online_id = int.Parse(ret[0]["client_id"]);
                long _id = long.Parse(ret[0]["id"].ToString());
                if (online && Server.clients[online_id].account == _id)
                {
                    Server.clients[online_id].Disconnect();
                }
                initializationData.accountID = _id;
                initializationData.password = ""; // dont have password
            }

            // dont have account so we initialize
            if (initializationData.accountID == 0)
            {
                initializationData.accountID = await new Account{ Address = address }.Create();
            }
            
            // set account as online
            LogIn(id, initializationData.accountID);

            initializationData.serverUnits = Unit.GetServerUnits();
            initializationData.serverSpells = Spell.GetServerSpells();
            initializationData.serverBuildings = Building.GetServerBuildings();
            initializationData.research = Research.GetResearchList(initializationData.accountID);
            return initializationData;
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
                
            Player data = new();
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                data.id = id;
                data.name = ret["name"];
                data.email = ret["email"];
                _ = int.TryParse(ret["gems"], out data.gems);
                _ = int.TryParse(ret["trophies"], out data.trophies);
                _ = int.TryParse(ret["banned"], out int ban);
                data.banned = ban > 0;
                _ = DateTime.TryParse(ret["now_time"], out data.nowTime);
                _ = DateTime.TryParse(ret["shield"], out data.shield);
                _ = DateTime.TryParse(ret["clan_join_timer"], out data.clanTimer);
                _ = DateTime.TryParse(ret["shld_cldn_1"], out data.shield1);
                _ = DateTime.TryParse(ret["shld_cldn_2"], out data.shield2);
                _ = DateTime.TryParse(ret["shld_cldn_3"], out data.shield3);
                _ = int.TryParse(ret["level"], out data.level);
                _ = int.TryParse(ret["xp"], out data.xp);
                _ = long.TryParse(ret["clan_id"], out data.clanID);
                _ = int.TryParse(ret["clan_rank"], out data.clanRank);
                _ = long.TryParse(ret["war_id"], out data.warID);
                _ = int.TryParse(ret["map_layout"], out data.layout);
                _ = long.TryParse(ret["guild_id"], out data.guild_id);
                data.guild_logo = string.IsNullOrEmpty(ret["guild_logo"])? "" : (string) ret["guild_logo"];
                data.guild_name = string.IsNullOrEmpty(ret["guild_name"])? "" : (string) ret["guild_name"];
                
            }
            return data;
        }
    
        public static List<Building> GetBuildings(long id) {

            List<Building> data = new List<Building>();
            string query = String.Format("SELECT buildings.id, buildings.global_id, buildings.level, buildings.x_position, buildings.x_war, buildings.y_war, buildings.boost, buildings.gold_storage, buildings.elixir_storage, buildings.dark_elixir_storage, buildings.y_position, buildings.construction_time, buildings.is_constructing, buildings.construction_build_time, server_buildings.columns_count, server_buildings.rows_count, server_buildings.health, server_buildings.speed, server_buildings.radius, server_buildings.capacity, server_buildings.gold_capacity, server_buildings.elixir_capacity, server_buildings.dark_elixir_capacity, server_buildings.damage, server_buildings.target_type, server_buildings.blind_radius, server_buildings.splash_radius, server_buildings.projectile_speed FROM buildings LEFT JOIN server_buildings ON buildings.global_id = server_buildings.global_id AND buildings.level = server_buildings.level WHERE buildings.account_id = {0};", id);
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    Building building = new()
                    {
                        id = (BuildingID)Enum.Parse(typeof(BuildingID), res["global_id"])
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
        public static int GetBuildingCount(long accountId, string globalId) {
            int count = 0;
            string query = string.Format("SELECT count(id) as building_count FROM buildings WHERE account_id = {0} AND global_id = '{1}';", accountId, globalId);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                _ = int.TryParse(ret["building_count"], out count);
            }
            return count;
        }
        
        public static int GetBuildingConstructionCount(long accountId) {
            int count = 0;
            string query = string.Format("SELECT count(id) as building_count FROM buildings WHERE account_id = {0} AND is_constructing > 0;", accountId);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                _ = int.TryParse(ret["building_count"], out count);
            }
            return count;

        }

        public static bool CheckResources(long account_id, int gold, int elixir, int gems, int darkElixir) {
            float haveGold = 0;
            float haveElixir = 0;
            float haveGems = 0;
            float haveDarkElixir = 0;

            if(gold == 0 && elixir == 0 && darkElixir == 0) {
                return true;
            }

            string query = string.Format(@"
                SELECT 
                    max(gems) as total_gems,
                    sum(gold_storage) as total_gold, 
                    sum(elixir_storage) as total_elixir, 
                    sum(dark_elixir_storage) as total_dark_elixir 
                FROM buildings 
                JOIN accounts
                ON accounts.id = buildings.account_id
                LEFT JOIN server_buildings 
                ON buildings.global_id = server_buildings.global_id 
                AND buildings.level = server_buildings.level 
                WHERE 
                    account_id = {0}
                    AND (buildings.global_id ilike '%storage' OR buildings.global_id = 'townhall') -- make sure is a storage", account_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                _ = float.TryParse(ret["total_gems"], out haveGems);
                _ = float.TryParse(ret["total_gold"], out haveGold);
                _ = float.TryParse(ret["total_elixir"], out haveElixir);
                _ = float.TryParse(ret["total_dark_elixir"], out haveDarkElixir);
            }
            
            if (haveGold < gold || haveElixir < elixir || haveDarkElixir < darkElixir || haveGems < gems)
            {
                return false;
            }
            return true;
        }
        
        public static bool SpendResources(long account_id, int gold, int elixir, int gems, int darkElixir)
        {
            if (!CheckResources(account_id, gold, elixir, gems, darkElixir)) {
                return false;
            }
            
            if (gold == 0 && elixir == 0 && darkElixir == 0) {
                return false;
            }

            List<Building> buildings = new();
            string query = string.Format(@"
                SELECT 
                    buildings.id, 
                    buildings.global_id, 
                    gold_storage, 
                    elixir_storage, 
                    dark_elixir_storage 
                FROM buildings 
                LEFT JOIN server_buildings 
                ON buildings.global_id = server_buildings.global_id 
                AND buildings.level = server_buildings.level 
                WHERE account_id = {0} 
                    AND (gold_storage > 0 OR elixir_storage > 0 OR dark_elixir_storage > 0)
                    AND (buildings.global_id ilike '%storage' OR buildings.global_id = 'townhall') -- make sure is a storage
                ORDER BY id;", account_id);
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    Building building = new()
                    {
                        id = (BuildingID)Enum.Parse(typeof(BuildingID), res["global_id"]),
                        goldStorage = (int)Math.Floor(float.Parse(res["gold_storage"])),
                        elixirStorage = (int)Math.Floor(float.Parse(res["elixir_storage"])),
                        darkStorage = (int)Math.Floor(float.Parse(res["dark_elixir_storage"])),
                        databaseID = long.Parse(res["id"])
                    };
                    buildings.Add(building);
                }
            }

            if (buildings.Count == 0) {
                return false;
            }

            string spendQuery = "";
            foreach(var building in buildings) {
                // already have enough
                if (gold == 0 && elixir == 0 && darkElixir == 0)
                {
                    break;
                }

                int buildingGoldSpent = building.goldStorage >= gold? gold : building.goldStorage;
                int buildingElixirSpent = building.elixirStorage >= elixir? elixir : building.elixirStorage;
                int buildingDarkElixirSpent = building.darkStorage >= darkElixir? darkElixir : building.darkStorage;
                
                gold -= buildingGoldSpent;
                elixir -= buildingElixirSpent;
                darkElixir -= buildingDarkElixirSpent;
                
                spendQuery += string.Format(@"
                    UPDATE buildings 
                    SET 
                        gold_storage = gold_storage - {0}, 
                        elixir_storage = elixir_storage - {1}, 
                        dark_elixir_storage = dark_elixir_storage - {2} 
                    WHERE id = {3};", buildingGoldSpent, buildingElixirSpent, buildingDarkElixirSpent, building.databaseID);
            }

            // dont have enough resources
            if(gold > 0 || elixir > 0 || darkElixir > 0) {
                return false;
            }

            Database.ExecuteNonQuery(spendQuery);

            // update gems
            if (gems > 0)
            {
                string updateGemQuery = String.Format("UPDATE accounts SET gems = gems - {0} WHERE id = {1};", gems, account_id);
                Database.ExecuteNonQuery(updateGemQuery);
            }

            return true;
        }

        public static void AddResources(long account_id, int gold, int elixir, int darkElixir, int gems)
        {
            if(gold == 0 && elixir == 0 && darkElixir == 0 && gems == 0) {
                return;
            }

            if (gold > 0 || elixir > 0 || darkElixir > 0)
            {
                // update storage
                List<Building> storages = new List<Building>();
                string query = String.Format(@"
                    SELECT 
                        buildings.id, 
                        buildings.global_id, 
                        buildings.gold_storage, 
                        buildings.elixir_storage, 
                        buildings.dark_elixir_storage, 
                        server_buildings.gold_capacity, 
                        server_buildings.elixir_capacity, 
                        server_buildings.dark_elixir_capacity 
                    FROM buildings 
                    LEFT JOIN server_buildings 
                    ON buildings.global_id = server_buildings.global_id 
                    AND buildings.level = server_buildings.level 
                    WHERE buildings.account_id = {0} 
                    AND (buildings.global_id ilike '%storage' OR buildings.global_id = 'townhall') -- make sure is a storage
                    AND buildings.level > 0;", account_id);
                var ret = Database.ExecuteForResults(query);
                if (ret.Count > 0)
                {
                    foreach(var res in ret)
                    {
                        Building building = new()
                        {
                            databaseID = long.Parse(res["id"]),
                            id = (BuildingID)Enum.Parse(typeof(BuildingID), res["global_id"]),
                            goldStorage = (int)Math.Floor(float.Parse(res["gold_storage"])),
                            elixirStorage = (int)Math.Floor(float.Parse(res["elixir_storage"])),
                            darkStorage = (int)Math.Floor(float.Parse(res["dark_elixir_storage"])),
                            goldCapacity = int.Parse(res["gold_capacity"]),
                            elixirCapacity = int.Parse(res["elixir_capacity"]),
                            darkCapacity = int.Parse(res["dark_elixir_capacity"])
                        };
                        storages.Add(building);
                    }
                }

                string updateQuery = "";
                foreach(var storage in storages) {
                    if (gold <= 0 && elixir <= 0 && darkElixir <= 0)
                    {
                        break;
                    }

                    int goldStored = storage.goldCapacity >= gold? gold : storage.goldCapacity;
                    int elixirStored = storage.elixirCapacity >= elixir? elixir : storage.elixirCapacity;
                    int darkElixirStored = storage.darkCapacity >= darkElixir? darkElixir : storage.darkCapacity;

                    updateQuery += string.Format(@"
                        UPDATE buildings 
                        SET gold_storage = gold_storage + {0}, 
                        elixir_storage = elixir_storage + {1}, 
                        dark_elixir_storage = dark_elixir_storage + {2} 
                        WHERE id = {3};", goldStored, elixirStored, darkElixirStored, storage.databaseID);

                    gold -= goldStored;
                    elixir -= elixirStored;
                    darkElixir -= darkElixirStored;
                }

                if(!string.IsNullOrEmpty(updateQuery)) {
                    Database.ExecuteNonQuery(updateQuery);
                }

            }

            if (gems > 0)
            {
                string query = string.Format("UPDATE accounts SET gems = gems + {0} WHERE id = {1};", gems, account_id);
                Database.ExecuteNonQuery(query);
            }

            return;
        }

        public static void AddGold(long account_id, int amount) {
            AddResources(account_id, amount, 0, 0, 0);
        }
        public static void AddElixir(long account_id, int amount) {
            AddResources(account_id, 0, amount, 0, 0);
        }
        public static void AddDarkElixir(long account_id, int amount) {
            AddResources(account_id, 0, 0, amount, 0);
        }
        public static void AddGem(long account_id, int amount) {
            AddResources(account_id, 0, 0, 0, amount);
        }
        public static int CollectResources(long account_id, long database_id)
        {
            int amount = 0;
            int amountGold = 0;
            int amountElixir = 0;
            int amountDark = 0;

            string query = string.Format(@"
                SELECT 
                    global_id, 
                    gold_storage, 
                    elixir_storage, 
                    dark_elixir_storage 
                FROM 
                    buildings 
                WHERE id = {0} 
                    AND account_id = {1}", database_id, account_id);
                    
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                amountGold = (int)Math.Floor(float.Parse(ret["gold_storage"]));
                amountElixir = (int)Math.Floor(float.Parse(ret["elixir_storage"]));
                amountDark = (int)Math.Floor(float.Parse(ret["dark_elixir_storage"]));
            }
            if (amountGold > 0)
            {
                AddGold(account_id, amountGold);
                // reset current mined amount
                query = String.Format("UPDATE buildings SET gold_storage = gold_storage - {0} WHERE id = {1};", amountGold, database_id);
                Database.ExecuteNonQuery(query);
            }
            if (amountElixir > 0)
            {
                AddElixir(account_id, amountElixir);
                // reset current mined amount
                query = String.Format("UPDATE buildings SET elixir_storage = elixir_storage - {0} WHERE id = {1};", amountElixir, database_id);
                Database.ExecuteNonQuery(query);
            }
            if (amountDark > 0)
            {
                AddDarkElixir(account_id, amountDark);
                // reset current mined amount
                query = String.Format("UPDATE buildings SET dark_elixir_storage = dark_elixir_storage - {0} WHERE id = {1};", amountDark, database_id);
                Database.ExecuteNonQuery(query);
            }
            return amount;
        }

        public static void AddXP(long account_id, int xp)
        {
            string query = String.Format("SELECT xp, level FROM accounts WHERE id = {0}", account_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                return;
            }

            _ = int.TryParse(ret["xp"], out int haveXp);
            _ = int.TryParse(ret["level"], out int level);
            
            int reachedLevel = level;
            int reqXp = Data.GetNexLevelRequiredXp(reachedLevel);
            int remainedXp = haveXp + xp;
            while (remainedXp >= reqXp)
            {
                remainedXp -= reqXp;
                reachedLevel++;
                reqXp = Data.GetNexLevelRequiredXp(reachedLevel);
            }
            string updateQuery = string.Format("UPDATE accounts SET level = {0}, xp = {1} WHERE id = {2} AND level = {3} AND xp = {4}", reachedLevel, remainedXp, account_id, level, haveXp);
            Database.ExecuteNonQuery(updateQuery);
        }

        public static void LogIn(long id, long account_id) {
            string query = string.Format(@"UPDATE accounts SET is_online = 1, client_id = {0}, last_login = NOW() at time zone 'utc' WHERE id = {1};", id, account_id);
            Database.ExecuteNonQuery(query);
            return;
        }
    
        public static void LogOut(string address) {
            string query = string.Format(@"UPDATE accounts SET is_online = 0 WHERE address = '{0}';", address);
            Database.ExecuteNonQuery(query);
            return;
        }
    
        public static bool OnDisconnect(long account_id) {
            string query = string.Format("UPDATE accounts SET is_online = 0, client_id = 0 WHERE id = {0}", account_id);
            Database.ExecuteNonQuery(query);
            return true;
        }

        public static int UpdateName(long id, string newName) {
            int response = 0;
            if (string.IsNullOrEmpty(newName))
            {
                return response;
            }

            string query = String.Format("UPDATE accounts SET name = '{0}' WHERE id = {1};", newName, id);
            Database.ExecuteNonQuery(query);
            response = 1;
            return response;
        }
    

        public static void UpdateTrophies(long account_id, int amount)
        {
            if (amount == 0) { return; }
            if (amount > 0)
            {
                string addQuery = string.Format("UPDATE accounts SET trophies = trophies + {0} WHERE id = {1}", amount, account_id);
                Database.ExecuteNonQuery(addQuery);
                return;
            }

            string removeQuery = string.Format("UPDATE accounts SET trophies = trophies - {0} WHERE id = {1}", -amount, account_id);
            Database.ExecuteNonQuery(removeQuery);

            // if trophy is negative set it to 0
            removeQuery = string.Format("UPDATE accounts SET trophies = 0 WHERE id = {0} AND trophies < 0", account_id);
            Database.ExecuteNonQuery(removeQuery);
        }

        public static bool RemoveShield(long account_id)
        {
            string query = string.Format("UPDATE accounts SET shield = NOW() at time zone 'utc' - INTERVAL '1 SECOND' WHERE id = {0};", account_id);
            Database.ExecuteNonQuery(query);
            return true;
        }
        public static int BoostResources(long account_id, long building_id)
        {
            int response = 0;
            Building building = null;
            DateTime now = DateTime.Now;
            string query = String.Format("SELECT level, global_id, boost, NOW() at time zone 'utc' as now FROM buildings WHERE id = {0} AND account_id = {1};", building_id, account_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                building = new Building();
                _ = DateTime.TryParse(ret["now"], out now);
                _ = DateTime.TryParse(ret["boost"], out building.boost);
                building.id = (BuildingID)Enum.Parse(typeof(BuildingID), ret["global_id"]);
                _ = int.TryParse(ret["level"], out building.level);
            }

            if(building == null) {
                return response;
            }

            int cost = Data.GetBoostResourcesCost(building.id, building.level);
            if (!SpendResources(account_id, 0, 0, cost, 0))
            {
                return response;
            }

            query = building.boost >= now?
                    string.Format("UPDATE buildings SET boost = boost + INTERVAL '24 HOUR' WHERE id = {0}", building_id)
                    : string.Format("UPDATE buildings SET boost = NOW() at time zone 'utc' + INTERVAL '24 HOUR' WHERE id = {0}", building_id);
            

            Database.ExecuteNonQuery(query);

            response = 1;
            return response;
        }

        public static int GetRank(long account_id)
        {
            int rank = 0;
            string query = String.Format("SELECT id, rank FROM (SELECT id, ROW_NUMBER() OVER(ORDER BY trophies DESC) AS 'rank' FROM accounts) AS ranks WHERE id = {0}", account_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                _ = int.TryParse(ret["rank"], out rank);
            }
            return rank;
        }

        public async static void GetPlayersRanking(int id, int page)
        {
            long account_id = Server.clients[id].account;
            PlayersRanking response = GetRankings(page, account_id);
            string rawData = await Data.SerializeAsync(response);
            byte[] bytes = await Data.CompressAsync(rawData);
            Packet packet = new Packet();
            packet.Write((int)Terminal.RequestsID.PLAYERSRANK);
            packet.Write(bytes.Length);
            packet.Write(bytes);
            Sender.TCP_Send(id, packet);
        }

        public static PlayersRanking GetRankings(int page, long account_id = 0) {

            PlayersRanking response = new();
            int playersCount = 0;
            string query = "SELECT COUNT(*) AS count FROM accounts";
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                _ = int.TryParse(ret["count"], out playersCount);
                
            }

            // dont have ranking
            response.pagesCount = Convert.ToInt32(Math.Ceiling((double)playersCount / players_ranking_per_page));
            if (response.pagesCount == 0)
            {
                return response;
            }

            // ??
            page = 1;
            if(page <= 0 || account_id == 0) {
                return response;
            }

            // get their rank
            int playerRank = GetRank(account_id);
            if (playerRank == 0)
            {
                page = Convert.ToInt32(Math.Ceiling((double)playerRank / (double)players_ranking_per_page));
            }
            
            // ??
            response.page = page;
            response.players = new List<PlayerRank>();
            return response;
        }
    }
}