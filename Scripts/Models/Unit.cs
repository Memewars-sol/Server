using System;
using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;
using Npgsql;

namespace Models {
    public enum UnitID
    {
        barbarian = 0, archer = 1, goblin = 2, healer = 3, wallbreaker = 4, giant = 5, miner = 6, balloon = 7, wizard = 8, dragon = 9, pekka = 10, babydragon = 11, electrodragon = 12, yeti = 13, dragonrider = 14, electrotitan = 15, minion = 16, hogrider = 17, valkyrie = 18, golem = 19, witch = 20, lavahound = 21, bowler = 22, icegolem = 23, headhunter = 24, skeleton = 25, bat = 26
    }

    public enum UnitMoveType
    {
        ground = 0, jump = 1, fly = 2, underground = 3
    }
    public class ServerUnit
    {
        public UnitID id = UnitID.barbarian;
        public int level = 0;
        public int requiredGold = 0;
        public int requiredElixir = 0;
        public int requiredGems = 0;
        public int requiredDarkElixir = 0;
        public int trainTime = 0;
        public int health = 0;
        public int housing = 0;
        public int researchTime = 0;
        public int researchGold = 0;
        public int researchElixir = 0;
        public int researchDarkElixir = 0;
        public int researchGems = 0;
        public int researchXp = 0;
    }
    

    public class Unit
    {
        public UnitID id = UnitID.barbarian;
        public int level = 0;
        public long databaseID = 0;
        public int hosing = 1;
        public bool trained = false;
        public bool ready = false;
        public int health = 0;
        public int trainTime = 0;
        public float trainedTime = 0;
        public float moveSpeed = 1;
        public float attackSpeed = 1;
        public float attackRange = 1;
        public float damage = 1;
        public float splashRange = 0;
        public float rangedSpeed = 5;
        public TargetPriority priority = TargetPriority.none;
        public UnitMoveType movement = UnitMoveType.ground;
        public float priorityMultiplier = 1;

        public static List<ServerUnit> GetServerUnits()
        {
            List<ServerUnit> units = new List<ServerUnit>();
            string query = string.Format("SELECT global_id, level, req_gold, req_elixir, req_gem, req_dark_elixir, train_time, health, housing, research_time, research_gold, research_elixir, research_dark_elixir, research_gems, research_xp FROM server_units;");
            using NpgsqlConnection connection = Database.GetDbConnection();
            using NpgsqlCommand command = new(query, connection);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    ServerUnit unit = new()
                    {
                        id = (UnitID)Enum.Parse(typeof(UnitID), reader["global_id"].ToString())
                    };
                    _ = int.TryParse(reader["level"].ToString(), out unit.level);
                    _ = int.TryParse(reader["req_gold"].ToString(), out unit.requiredGold);
                    _ = int.TryParse(reader["req_elixir"].ToString(), out unit.requiredElixir);
                    _ = int.TryParse(reader["req_gem"].ToString(), out unit.requiredGems);
                    _ = int.TryParse(reader["req_dark_elixir"].ToString(), out unit.requiredDarkElixir);
                    _ = int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                    _ = int.TryParse(reader["health"].ToString(), out unit.health);
                    _ = int.TryParse(reader["housing"].ToString(), out unit.housing);
                    _ = int.TryParse(reader["research_time"].ToString(), out unit.researchTime);
                    _ = int.TryParse(reader["research_gold"].ToString(), out unit.researchGold);
                    _ = int.TryParse(reader["research_elixir"].ToString(), out unit.researchElixir);
                    _ = int.TryParse(reader["research_dark_elixir"].ToString(), out unit.researchDarkElixir);
                    _ = int.TryParse(reader["research_gems"].ToString(), out unit.researchGems);
                    _ = int.TryParse(reader["research_xp"].ToString(), out unit.researchXp);
                    units.Add(unit);
                }
            }
            connection.Close();
            return units;
        }

        public static ServerUnit GetServerUnit(string id, int level)
        {
            ServerUnit unit = null;
            string query = String.Format("SELECT global_id, level, req_gold, req_elixir, req_gem, req_dark_elixir, train_time, health, housing, research_time, research_gold, research_elixir, research_dark_elixir, research_gems, research_xp FROM server_units WHERE global_id = '{0}' AND level = {1};", id, level);
            using NpgsqlConnection connection = Database.GetDbConnection();
            using NpgsqlCommand command = new(query, connection);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    unit = new ServerUnit
                    {
                        id = (UnitID)Enum.Parse(typeof(UnitID), reader["global_id"].ToString())
                    };
                    _ = int.TryParse(reader["level"].ToString(), out unit.level);
                    _ = int.TryParse(reader["req_gold"].ToString(), out unit.requiredGold);
                    _ = int.TryParse(reader["req_elixir"].ToString(), out unit.requiredElixir);
                    _ = int.TryParse(reader["req_gem"].ToString(), out unit.requiredGems);
                    _ = int.TryParse(reader["req_dark_elixir"].ToString(), out unit.requiredDarkElixir);
                    _ = int.TryParse(reader["train_time"].ToString(), out unit.trainTime);
                    _ = int.TryParse(reader["health"].ToString(), out unit.health);
                    _ = int.TryParse(reader["housing"].ToString(), out unit.housing);
                    _ = int.TryParse(reader["research_time"].ToString(), out unit.researchTime);
                    _ = int.TryParse(reader["research_gold"].ToString(), out unit.researchGold);
                    _ = int.TryParse(reader["research_elixir"].ToString(), out unit.researchElixir);
                    _ = int.TryParse(reader["research_dark_elixir"].ToString(), out unit.researchDarkElixir);
                    _ = int.TryParse(reader["research_gems"].ToString(), out unit.researchGems);
                    _ = int.TryParse(reader["research_xp"].ToString(), out unit.researchXp);
                }
            }
            connection.Close();
            return unit;
        }
    
        public static List<Unit> GetUnits(long account)
        {
            List<Unit> data = new List<Unit>();
            string query = String.Format("SELECT units.id, units.global_id, units.level, units.trained, units.ready, units.trained_time, server_units.health, server_units.train_time, server_units.housing, server_units.attack_range, server_units.attack_speed, server_units.move_speed, server_units.damage, server_units.move_type, server_units.target_priority, server_units.priority_multiplier FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.account_id = {0};", account);
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    Unit unit = new()
                    {
                        id = (UnitID)Enum.Parse(typeof(UnitID), res["global_id"])
                    };
                    _ = long.TryParse(res["id"], out unit.databaseID);
                    _ = int.TryParse(res["level"], out unit.level);
                    _ = int.TryParse(res["health"], out unit.health);
                    _ = int.TryParse(res["housing"], out unit.hosing);
                    _ = int.TryParse(res["train_time"], out unit.trainTime);
                    _ = float.TryParse(res["trained_time"], out unit.trainedTime);

                    _ = float.TryParse(res["damage"], out unit.damage);
                    _ = float.TryParse(res["attack_speed"], out unit.attackSpeed);
                    _ = float.TryParse(res["move_speed"], out unit.moveSpeed);
                    _ = float.TryParse(res["attack_range"], out unit.attackRange);

                    unit.movement = (UnitMoveType)Enum.Parse(typeof(UnitMoveType), res["move_type"]);
                    unit.priority = (TargetPriority)Enum.Parse(typeof(TargetPriority), res["target_priority"]);
                    _ = float.TryParse(res["priority_multiplier"], out unit.priorityMultiplier);

                    _ = int.TryParse(res["trained"], out int isTrue);
                    unit.trained = isTrue > 0;
                    _ = int.TryParse(res["ready"], out isTrue);
                    unit.ready = isTrue > 0;
                    data.Add(unit);
                }
            }
            return data;
        }
    
        public static int Train(long account_id, string globalID)
        {
            int response = 0;
            int level = 1;
            Research research = Research.Get(account_id, globalID, ResearchType.unit);
            if (research != null)
            {
                level = research.level;
            }

            ServerUnit unit = GetServerUnit(globalID, level);
            if(unit == null) {
                response = 3;
                return response;
            }
            
            int capacity = Building.GetCapacityByGlobalID(BuildingID.barracks.ToString(), account_id);
            int occupied = 999;
            string query = String.Format("SELECT SUM(server_units.housing) AS occupied FROM units LEFT JOIN server_units ON units.global_id = server_units.global_id AND units.level = server_units.level WHERE units.account_id = {0} AND ready <= 0;", account_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                _ = int.TryParse(ret["occupied"].ToString(), out occupied);
            }

            if (capacity - occupied < unit.housing)
            {
                response = 4;
                return response;
            }
            if (!Account.SpendResources(account_id, unit.requiredGold, unit.requiredElixir, unit.requiredGems, unit.requiredDarkElixir))
            {
                response = 2;
                return response;
            }

            query = String.Format("INSERT INTO units (global_id, level, account_id) VALUES('{0}', {1}, {2})", globalID, level, account_id);
            Database.ExecuteNonQuery(query);
            response = 1;
            return response;
        }
    }
}