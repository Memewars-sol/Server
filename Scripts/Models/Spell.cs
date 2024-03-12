using System;
using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;
using Npgsql;

namespace Models {
    // Spells that their effects have been applied to the project: lightning, healing, rage, freeze, invisibility, haste
    public enum SpellID
    {
        lightning = 0, healing = 1, rage = 2, jump = 3, freeze = 4, invisibility = 5, recall = 6, earthquake = 7, haste = 8, skeleton = 9, bat = 10
    }

    public class ServerSpell
    {
        public long databaseID = 0;
        public SpellID id = SpellID.lightning;
        public int level = 0;
        public int requiredGold = 0;
        public int requiredElixir = 0;
        public int requiredGems = 0;
        public int requiredDarkElixir = 0;
        public int brewTime = 0;
        public int housing = 1;
        public float radius = 0;
        public int pulsesCount = 0;
        public float pulsesDuration = 0;
        public float pulsesValue = 0;
        public float pulsesValue2 = 0;
        public int researchTime = 0;
        public int researchGold = 0;
        public int researchElixir = 0;
        public int researchDarkElixir = 0;
        public int researchGems = 0;
        public int researchXp = 0;
    }

    public class Spell
    {
        public long databaseID = 0;
        public SpellID id = SpellID.lightning;
        public int level = 0;
        public int hosing = 1;
        public bool brewed = false;
        public bool ready = false;
        public int brewTime = 0;
        public float brewedTime = 0;
        public int housing = 1;
        public ServerSpell server = null;

        public static List<ServerSpell> GetServerSpells()
        {
            List<ServerSpell> spells = new List<ServerSpell>();
            string query = string.Format("SELECT id, global_id, level, req_gold, req_elixir, req_gem, req_dark_elixir, brew_time, housing, radius, pulses_count, pulses_duration, pulses_value, pulses_value_2, research_time, research_gold, research_elixir, research_dark_elixir, research_gems, research_xp FROM server_spells;");
            using NpgsqlConnection connection = Database.GetDbConnection();
            using NpgsqlCommand command = new(query, connection);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    ServerSpell spell = new();
                    _ = long.TryParse(reader["id"].ToString(), out spell.databaseID);
                    _ = spell.id = (SpellID)Enum.Parse(typeof(SpellID), reader["global_id"].ToString());
                    _ = int.TryParse(reader["level"].ToString(), out spell.level);
                    _ = int.TryParse(reader["req_gold"].ToString(), out spell.requiredGold);
                    _ = int.TryParse(reader["req_elixir"].ToString(), out spell.requiredElixir);
                    _ = int.TryParse(reader["req_gem"].ToString(), out spell.requiredGems);
                    _ = int.TryParse(reader["req_dark_elixir"].ToString(), out spell.requiredDarkElixir);
                    _ = int.TryParse(reader["brew_time"].ToString(), out spell.brewTime);
                    _ = int.TryParse(reader["housing"].ToString(), out spell.housing);
                    _ = float.TryParse(reader["radius"].ToString(), out spell.radius);
                    _ = int.TryParse(reader["pulses_count"].ToString(), out spell.pulsesCount);
                    _ = float.TryParse(reader["pulses_duration"].ToString(), out spell.pulsesDuration);
                    _ = float.TryParse(reader["pulses_value"].ToString(), out spell.pulsesValue);
                    _ = float.TryParse(reader["pulses_value_2"].ToString(), out spell.pulsesValue2);
                    _ = int.TryParse(reader["research_time"].ToString(), out spell.researchTime);
                    _ = int.TryParse(reader["research_gold"].ToString(), out spell.researchGold);
                    _ = int.TryParse(reader["research_elixir"].ToString(), out spell.researchElixir);
                    _ = int.TryParse(reader["research_dark_elixir"].ToString(), out spell.researchDarkElixir);
                    _ = int.TryParse(reader["research_gems"].ToString(), out spell.researchGems);
                    _ = int.TryParse(reader["research_xp"].ToString(), out spell.researchXp);
                    spells.Add(spell);
                }
            }
            return spells;
        }

    }
        
}