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
    }
}