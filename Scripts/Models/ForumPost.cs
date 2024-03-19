using System;
using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;

namespace Models {
    public class ForumPost {
        public long Id { get; set; }
        public long GuildId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Content { get; set; }
        public string VotingId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CommentCount { get; set; }
        public List<ForumComment> Comments { get; set; }

        public ForumPost() {
            Comments = new();
        }
        public ForumPost(long id) {
            Id = id;
            string query = string.Format("select * from forum_posts where id = {0}", id);
            var ret = Database.ExecuteForSingleResult(query);
            if(ret != null ){
                GuildId = long.Parse(ret["guild_id"]);
                Title = ret["title"];
                Description = ret["description"];
                Content = ret["content"];
                VotingId = ret["voting_id"];
                CreatedBy = ret["created_by"];
                CreatedAt = DateTime.Parse(ret["created_at"]);

                Comments = ForumComment.All(Id);
                CommentCount = Comments.Count;
            }
        }

        public static List<ForumPost> All(long guild_id) {
            string query = string.Format(@"
                select 
                    p.*, 
                    count(distinct c.id) as comment_count 
                from forum_posts p 
                left join forum_comments c 
                on c.forum_post_id = p.id 
                where guild_id = {0} 
                group by p.id 
                order by p.id", guild_id);
            var posts = new List<ForumPost>();
            var ret = Database.ExecuteForResults(query);
            if(ret.Count == 0) {
                return posts;
            }
            foreach(var res in ret) {
                var post = new ForumPost { 
                    Id = long.Parse(res["id"]),
                    GuildId = long.Parse(res["guild_id"]),
                    Title = res["title"],
                    Description = res["description"],
                    Content = res["content"],
                    VotingId = res["voting_id"],
                    CreatedBy = res["created_by"],
                    CreatedAt = DateTime.Parse(res["created_at"]),
                    CommentCount = int.Parse(res["comment_count"]),
                };

                posts.Add(post);
            }

            return posts;
        }

        public void Create() {
            string query = string.Format("insert into forum_posts (title, description, content, created_by, guild_id) values ('{0}', '{1}', '{2}', '{3}', {4})", Title, Description, Content, CreatedBy, GuildId);
            Database.ExecuteNonQuery(query);
        }

        // no edits
        public static void Update(long id) {
            // do nothing
        }

        public void Delete() {
            string query = string.Format("delete from forum_posts where id = {0}", Id);
            Database.ExecuteNonQuery(query);
        }

        public void PushToGovernance() {
            // function to push to governance and return voting_id
            //
            string query = string.Format("update forum_posts set voting_id = '{0}' where id = {1}", Data.RandomCode(9), Id);
            Database.ExecuteNonQuery(query);
        }

        public static void Vote(long id, string option) {
            // function to vote
            // get result from vote
        }
    }
}