using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;

namespace Models {
    public class Guild {
        public long Id { get; set; }
        public string MintAddress { get; set; }
        public string Logo { get; set; }
        public string Name { get; set; }

        public Guild() {
            // do nothing
        }

        public Guild(long id) {
            Id = id;
            
            string query = string.Format("select * from guilds where id = {0}", id);
            var ret = Database.ExecuteForSingleResult(query);
            if(ret != null) {
                MintAddress = ret["mint_address"]; // if it's nft, maybe use the collection address
                Logo = ret["logo"];
                Name = ret["name"];
            }
        }

        public static List<Guild> All() {
            string query = "select * from guilds";
            var ret = Database.ExecuteForResults(query);
            var guilds = new List<Guild>();
            if(ret.Count == 0) {
                return guilds;
            }

            foreach(var res in ret) {
                var guild = new Guild { 
                    Id = long.Parse(res["id"]),
                    MintAddress = res["mint_address"],
                    Logo = res["logo"],
                    Name = res["name"],
                };

                guilds.Add(guild);
            }

            return guilds;
        }

        // maybe need to preseed guilds
        public static Guild Get(long id) {
            return new Guild(id);
        }

        public static void Join(long id, long account_id) {
            // guilds follow the account not the address connected
            string query = string.Format("update accounts set guild_id = {0} where id = {1}", id, account_id);
            Database.ExecuteNonQuery(query);
        }

        public static List<ForumPost> GetForumPosts(long id) {
            return ForumPost.All(id);
        }

        // dont do anything for now
        public static void Update(long id) {
            // do nothing
        }

        public static void UpdateLogo(long id, string logo) {
            string query = string.Format("update guilds set logo = '{0}' where id = {1}", logo, id);
            Database.ExecuteNonQuery(query);
        }

        public static void UpdateName(long id, string name) {
            string query = string.Format("update guilds set name = '{0}' where id = {1}", name, id);
            Database.ExecuteNonQuery(query);

        }

        public static void Delete(long id) {
            string query = string.Format("delete from guilds where id = {0}", id);
            Database.ExecuteNonQuery(query);
        }
    }
}