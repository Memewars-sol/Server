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
        public int CitizenCount { get; set; }
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
                GuildId = string.IsNullOrEmpty(ret["guild_id"])? 0 : long.Parse(ret["guild_id"]);
                IsBooked = bool.Parse(ret["is_booked"]);
                MintedAt = string.IsNullOrEmpty(ret["minted_at"])? DateTime.MinValue : DateTime.Parse(ret["minted_at"]);

                Citizens = LandCitizen.All(Id);
                CitizenCount = Citizens.Count;
            }
        }

        // without citizen details
        public static List<Land> All() {
            string query = string.Format("select l.*, count(distinct lc.id)::int as citizen_count from lands l left join land_citizens lc on lc.land_id = l.id group by l.id");
            var ret = Database.ExecuteForResults(query);
            var lands = new List<Land>();
            if(ret == null) {
                return lands;
            }

            foreach(var res in ret) {
                lands.Add(new Land() {
                    Id = long.Parse(res["id"]),
                    MintAddress = res["mint_address"],
                    X = int.Parse(res["x"]),
                    Y = int.Parse(res["y"]),
                    Level = int.Parse(res["level"]),
                    CitizenCap = int.Parse(res["citizen_cap"]),
                    GemsPerBlock = float.Parse(res["gems_per_block"]),
                    OwnerAddress = res["owner_address"],
                    GuildId = string.IsNullOrEmpty(res["guild_id"])? 0 : long.Parse(res["guild_id"]),
                    IsBooked = bool.Parse(res["is_booked"]),
                    MintedAt = string.IsNullOrEmpty(res["minted_at"])? DateTime.MinValue : DateTime.Parse(res["minted_at"]),
                    CitizenCount = int.Parse(res["citizen_count"]),
                });
            }

            return lands;
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