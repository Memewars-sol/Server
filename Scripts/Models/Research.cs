using System;
using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;
using Npgsql;

namespace Models {

    public enum ResearchType
    {
        unit = 1, spell = 2
    }

    public class Research
    {
        public long id;
        public ResearchType type;
        public string globalID;
        public int level;
        public bool researching;
        public DateTime end;

        public static List<Research> GetResearchList(long account_id)
        {
            List<Research> list = new List<Research>();
            string query = String.Format("SELECT id, level, type, global_id, researching, CASE WHEN researching > NOW() at time zone 'utc' THEN 1 ELSE 0 END AS is_researching FROM research WHERE account_id = {0};", account_id);
            using NpgsqlConnection connection = Database.GetDbConnection();
            using NpgsqlCommand command = new(query, connection);
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Research research = new Research();
                    _ = long.TryParse(reader["id"].ToString(), out research.id);
                    _ = int.TryParse(reader["type"].ToString(), out int type);
                    research.type = (ResearchType)type;
                    research.globalID = reader["global_id"].ToString();
                    _ = int.TryParse(reader["level"].ToString(), out research.level);
                    _ = int.TryParse(reader["is_researching"].ToString(), out int is_researching);
                    _ = DateTime.TryParse(reader["researching"].ToString(), out research.end);
                    research.researching = is_researching == 1;
                    if (research.researching)
                    {
                        research.level -= 1;
                    }
                    list.Add(research);
                }
            }
            return list;
        }

        public static Research Get(long account_id, string global_id, ResearchType type, bool createIfNotExist = false)
        {
            Research research = null;
            string query = String.Format("SELECT id, level, researching, CASE WHEN researching > NOW() at time zone 'utc' THEN 1 ELSE 0 END AS is_researching FROM research WHERE account_id = {0} AND type = {1} AND global_id = '{2}';", account_id, (int)type, global_id);
            var ret = Database.ExecuteForSingleResult(query);
            if (ret != null)
            {
                research = new Research();
                _ = int.TryParse(ret["is_researching"], out int is_researching);
                _ = long.TryParse(ret["id"], out research.id);
                _ = int.TryParse(ret["level"], out research.level);
                _ = DateTime.TryParse(ret["researching"], out research.end);
                research.researching = is_researching == 1;
                if (research.researching)
                {
                    research.level -= 1;
                }
                research.globalID = global_id;
                research.type = type;
            }
            
            if (createIfNotExist && research == null)
            {
                research = new Research();
                query = String.Format("INSERT INTO research (account_id, type, global_id) VALUES({0}, {1}, '{2}') RETURNING id;", account_id, (int)type, global_id);
                research.id = (long)Database.ExecuteScalar(query);
                research.globalID = global_id;
                research.level = 1;
                research.type = type;
                research.researching = false;
            }
            return research;
        }

        public static (int, Research) Do(long account_id, ResearchType type, string global_id)
        {
            int response = 0;
            Research research = Get(account_id, global_id, type, true);
            if (research.researching)
            {
                response = 3;
                return (response, research);
            }
            int time = 0;
            if (type == ResearchType.unit)
            {
                ServerUnit unit = Unit.GetServerUnit(global_id, research.level + 1);
                if (unit != null)
                {
                    return (response, research);
                }
                if (!Account.SpendResources(account_id, unit.researchGold, unit.researchElixir, unit.researchGems, unit.researchDarkElixir))
                {
                    response = 2;
                    return (response, research);
                }

                time = unit.researchTime;
                Account.AddXP(account_id, unit.researchXp);
            }
            else if (type == ResearchType.spell)
            {
                ServerSpell spell = Spell.GetServerSpell(global_id, research.level + 1);
                if (spell != null)
                {
                    return (response, research);
                }

                if (!Account.SpendResources(account_id, spell.researchGold, spell.researchElixir, spell.researchGems, spell.researchDarkElixir))
                {
                    response = 2;
                    return (response, research);
                }

                time = spell.researchTime;
                Account.AddXP(account_id, spell.researchXp);
            }

            response = 1;
            string query = String.Format("UPDATE research SET level = level + 1, researching = NOW() at time zone 'utc' + INTERVAL '{0} SECOND' WHERE id = {1};", time, research.id);
            Database.ExecuteNonQuery(query);
            research = Research.Get(account_id, global_id, type);
            return (response, research);
        }

    }

}