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

        public static ServerSpell GetServerSpell(string id, int level)
        {
            ServerSpell spell = null;
            string query = String.Format("SELECT id, global_id, level, req_gold, req_elixir, req_gem, req_dark_elixir, brew_time, housing, radius, pulses_count, pulses_duration, pulses_value, pulses_value_2, research_time, research_gold, research_elixir, research_dark_elixir, research_gems, research_xp FROM server_spells WHERE global_id = '{0}' AND level = {1};", id, level);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                spell = new ServerSpell();
                _ = long.TryParse(ret["id"], out spell.databaseID);
                spell.id = (SpellID)Enum.Parse(typeof(SpellID), ret["global_id"]);
                _ = int.TryParse(ret["level"], out spell.level);
                _ = int.TryParse(ret["req_gold"], out spell.requiredGold);
                _ = int.TryParse(ret["req_elixir"], out spell.requiredElixir);
                _ = int.TryParse(ret["req_gem"], out spell.requiredGems);
                _ = int.TryParse(ret["req_dark_elixir"], out spell.requiredDarkElixir);
                _ = int.TryParse(ret["brew_time"], out spell.brewTime);
                _ = int.TryParse(ret["housing"], out spell.housing);
                _ = float.TryParse(ret["radius"], out spell.radius);
                _ = int.TryParse(ret["pulses_count"], out spell.pulsesCount);
                _ = float.TryParse(ret["pulses_duration"], out spell.pulsesDuration);
                _ = float.TryParse(ret["pulses_value"], out spell.pulsesValue);
                _ = float.TryParse(ret["pulses_value_2"], out spell.pulsesValue2);
                _ = int.TryParse(ret["research_time"], out spell.researchTime);
                _ = int.TryParse(ret["research_gold"], out spell.researchGold);
                _ = int.TryParse(ret["research_elixir"], out spell.researchElixir);
                _ = int.TryParse(ret["research_dark_elixir"], out spell.researchDarkElixir);
                _ = int.TryParse(ret["research_gems"], out spell.researchGems);
                _ = int.TryParse(ret["research_xp"], out spell.researchXp);
            }
            return spell;
        }

        public static List<ServerSpell> GetServerSpells()
        {
            List<ServerSpell> spells = new List<ServerSpell>();
            string query = string.Format("SELECT id, global_id, level, req_gold, req_elixir, req_gem, req_dark_elixir, brew_time, housing, radius, pulses_count, pulses_duration, pulses_value, pulses_value_2, research_time, research_gold, research_elixir, research_dark_elixir, research_gems, research_xp FROM server_spells;");
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    ServerSpell spell = new();
                    _ = long.TryParse(res["id"], out spell.databaseID);
                    _ = spell.id = (SpellID)Enum.Parse(typeof(SpellID), res["global_id"]);
                    _ = int.TryParse(res["level"], out spell.level);
                    _ = int.TryParse(res["req_gold"], out spell.requiredGold);
                    _ = int.TryParse(res["req_elixir"], out spell.requiredElixir);
                    _ = int.TryParse(res["req_gem"], out spell.requiredGems);
                    _ = int.TryParse(res["req_dark_elixir"], out spell.requiredDarkElixir);
                    _ = int.TryParse(res["brew_time"], out spell.brewTime);
                    _ = int.TryParse(res["housing"], out spell.housing);
                    _ = float.TryParse(res["radius"], out spell.radius);
                    _ = int.TryParse(res["pulses_count"], out spell.pulsesCount);
                    _ = float.TryParse(res["pulses_duration"], out spell.pulsesDuration);
                    _ = float.TryParse(res["pulses_value"], out spell.pulsesValue);
                    _ = float.TryParse(res["pulses_value_2"], out spell.pulsesValue2);
                    _ = int.TryParse(res["research_time"], out spell.researchTime);
                    _ = int.TryParse(res["research_gold"], out spell.researchGold);
                    _ = int.TryParse(res["research_elixir"], out spell.researchElixir);
                    _ = int.TryParse(res["research_dark_elixir"], out spell.researchDarkElixir);
                    _ = int.TryParse(res["research_gems"], out spell.researchGems);
                    _ = int.TryParse(res["research_xp"], out spell.researchXp);
                    spells.Add(spell);
                }
            }
            return spells;
        }

        public static Spell Get(long database_id, long account_id, bool get_server = false)
        {
            Spell spell = null;
            string query = String.Format("SELECT spells.id, spells.global_id, spells.level, spells.brewed, spells.ready, spells.brewed_time, server_spells.brew_time, server_spells.housing FROM spells LEFT JOIN server_spells ON spells.global_id = server_spells.global_id AND spells.level = server_spells.level WHERE spells.id = {0} AND spells.account_id = {1};", database_id, account_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                spell = new Spell
                {
                    id = (SpellID)Enum.Parse(typeof(SpellID), ret["global_id"])
                };
                _ = long.TryParse(ret["id"], out spell.databaseID);
                _ = int.TryParse(ret["level"], out spell.level);
                _ = int.TryParse(ret["housing"], out spell.hosing);
                _ = int.TryParse(ret["brew_time"], out spell.brewTime);
                _ = float.TryParse(ret["brewed_time"], out spell.brewedTime);

                _ = int.TryParse(ret["brewed"], out int isTrue);
                spell.brewed = isTrue > 0;
                _ = int.TryParse(ret["ready"], out isTrue);
                spell.ready = isTrue > 0;
            }
            if (spell != null && get_server)
            {
                spell.server = Spell.GetServerSpell(spell.id.ToString(), spell.level);
            }
            return spell;
        }
        public static List<Spell> All(long account_id)
        {
            List<Spell> spells = new List<Spell>();
            string query = String.Format("SELECT spells.id, spells.global_id, spells.level, spells.brewed, spells.ready, spells.brewed_time, server_spells.brew_time, server_spells.housing FROM spells LEFT JOIN server_spells ON spells.global_id = server_spells.global_id AND spells.level = server_spells.level WHERE spells.account_id = {0};", account_id);
            var ret = Database.ExecuteForResults(query);
            if (ret.Count > 0)
            {
                foreach(var res in ret)
                {
                    Spell spell = new()
                    {
                        id = (SpellID)Enum.Parse(typeof(SpellID), res["global_id"])
                    };
                    _ = long.TryParse(res["id"], out spell.databaseID);
                    _ = int.TryParse(res["level"], out spell.level);
                    _ = int.TryParse(res["housing"], out spell.hosing);
                    _ = int.TryParse(res["brew_time"], out spell.brewTime);
                    _ = float.TryParse(res["brewed_time"], out spell.brewedTime);

                    _ = int.TryParse(res["brewed"], out int isTrue);
                    spell.brewed = isTrue > 0;
                    _ = int.TryParse(res["ready"], out isTrue);
                    spell.ready = isTrue > 0;
                    spells.Add(spell);
                }
            }
            return spells;
        }

        public static int Brew(long account_id, string globalID)
        {
            int response = 0;
            int level = 1;
            Research research = Research.Get(account_id, globalID, ResearchType.spell);
            if (research != null)
            {
                level = research.level;
            }
            ServerSpell spell = GetServerSpell(globalID, level);
            if (spell == null)
            {
                response = 3;
                return response;
            }

            int capacity = 0;
            List<Building> spellFactory = Building.GetByGlobalID(BuildingID.spellfactory.ToString(), account_id);
            for (int i = 0; i < spellFactory.Count; i++)
            {
                capacity += spellFactory[i].capacity;
            }

            int occupied = 999;
            string query = String.Format("SELECT SUM(server_spells.housing) AS occupied FROM spells LEFT JOIN server_spells ON spells.global_id = server_spells.global_id AND spells.level = server_spells.level WHERE spells.account_id = {0} AND ready <= 0;", account_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                int.TryParse(ret["occupied"], out occupied);
            }

            if (capacity - occupied < spell.housing)
            {
                response = 4;
                return response;
            }

            if (!Account.SpendResources(account_id, spell.requiredGold, spell.requiredElixir, spell.requiredGems, spell.requiredDarkElixir))
            {
                response = 2;
                return response;
            }

            query = String.Format("INSERT INTO spells (global_id, level, account_id) VALUES('{0}', {1}, {2})", globalID, level, account_id);
            Database.ExecuteNonQuery(query);
            response = 1;
            return response;
        }
        
        public static int CancelBrew(long account_id, long databaseID)
        {
            string query = String.Format("DELETE FROM spells WHERE id = {0} AND account_id = {1} AND ready <= 0", databaseID, account_id);
            Database.ExecuteNonQuery(query);
            return 1;
        }
        public static void Delete(long id)
        {
            string query = String.Format("DELETE FROM spells WHERE id = {0};", id);
            Database.ExecuteNonQuery(query);
        }
    }
        
}