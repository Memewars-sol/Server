using System;
using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;

namespace Models {
    public class Land {
        public long Id { get; set; }
        public string MintAddress { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string OwnerAddress { get; set; }
        public long GuildId { get; set; }
        public bool IsBooked { get; set; } // for land sale
        public DateTime MintedAt { get; set; }
        public List<LandCitizen> Citizens { get; set; }
        
        // maybe add resources here
        // level
        // gem per second -- all on chain? like LP APY, stake a land slot to get APY
        // land slots -- cNFT?
        // 
        // 

        public Land() { Citizens = new(); }
        public Land(long id) {
            Id = id;
            string query = string.Format("select * from lands where id = {0}", id);
            var ret = Database.ExecuteForSingleResult(query);
            if(ret != null) {
                MintAddress = ret["mint_address"];
                X = int.Parse(ret["x"]);
                Y = int.Parse(ret["y"]);
                OwnerAddress = ret["owner_address"];
                GuildId = long.Parse(ret["guild_id"]);
                IsBooked = bool.Parse(ret["is_booked"]);
                MintedAt = DateTime.Parse(ret["minted_date"]);

                Citizens = LandCitizen.All(Id);
            }
        }

        public static void AddCitizen(long land_id, long account_id) {
            string query = string.Format("insert into land_citizen (land_id, account_id) values ({0},{1})", land_id, account_id);
            Database.ExecuteNonQuery(query);
        }

        public static void RemoveCitizen(long land_id, long account_id) {
            string query = string.Format("delete from land_citizens where land_id = {0} and account_id = {1}", land_id, account_id);
            Database.ExecuteNonQuery(query);
        }
    }
}