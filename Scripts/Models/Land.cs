using System;
using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;

namespace Models {
    public class Land {
        public long Id { get; set; }
        public string MintAddress { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Level { get; set; }
        public int CitizenCap { get; set; }
        public float GemsPerBlock { get; set; } // gems per block
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
                Level = int.Parse(ret["level"]);
                CitizenCap = int.Parse(ret["citizen_cap"]);
                GemsPerBlock = float.Parse(ret["gems_per_block"]);
                OwnerAddress = ret["owner_address"];
                GuildId = long.Parse(ret["guild_id"]);
                IsBooked = bool.Parse(ret["is_booked"]);
                MintedAt = DateTime.Parse(ret["minted_date"]);

                Citizens = LandCitizen.All(Id);
            }
        }

        public void Book(long id) {
            if(IsBooked) {
                throw new Exception("Land is already booked");
            }

            if(!string.IsNullOrEmpty(OwnerAddress)) {
                throw new Exception("Land is already minted");
            }
            string query = string.Format("update lands set is_booked = true where id = {0}", id);
            Database.ExecuteNonQuery(query);
        }

        public void Mint(string to_address) {
            // function to middleware
            string query = string.Format("update lands set owner_address = '{0}' where id = {1}", to_address, Id);
            Database.ExecuteNonQuery(query);
        }

        public static void AddCitizen(long land_id, long account_id) {
            var land = new Land(land_id);
            
            // doesn't exist
            if(string.IsNullOrEmpty(land.MintAddress)) {
                throw new Exception("Land doesn't exist");
            }

            if(land.CitizenCap <= land.Citizens.Count) {
                throw new Exception("Maximum citizen size reached");
            }
            
            string query = string.Format("insert into land_citizen (land_id, account_id) values ({0},{1})", land_id, account_id);
            Database.ExecuteNonQuery(query);
        }

        public static void RemoveCitizen(long land_id, long account_id) {
            string query = string.Format("delete from land_citizens where land_id = {0} and account_id = {1}", land_id, account_id);
            Database.ExecuteNonQuery(query);
        }
    }
}