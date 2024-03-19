using System;
using System.Collections.Generic;
using Memewars.RealtimeNetworking.Server;

namespace Models {
    public class ForumComment {

        public long Id { get; set; }
        public long ForumPostId { get; set; }
        public string Comment { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public ForumComment() {}
        public ForumComment(long id) {
            Id = id;

            string query = string.Format("select * from forum_comments where id = {0}", id);
            var ret = Database.ExecuteForSingleResult(query);

            if(ret != null) {
                ForumPostId = long.Parse(ret["forum_post_id"]);
                Comment = ret["comment"];
                CreatedBy = ret["created_by"];
                CreatedAt = DateTime.Parse(ret["created_at"]);
            }
        }

        public static List<ForumComment> All(long forum_post_id) {
            var comments = new List<ForumComment>();
            string query = string.Format("select * from forum_comments where forum_post_id = {0}", forum_post_id);
            var ret = Database.ExecuteForResults(query);
            if(ret.Count == 0) {
                return comments;
            }

            foreach(var res in ret) {
                var comment = new ForumComment() {
                    Id = long.Parse(res["id"]),
                    ForumPostId = long.Parse(res["forum_post_id"]),
                    Comment = res["comment"],
                    CreatedBy = res["created_by"],
                    CreatedAt = DateTime.Parse(res["created_at"]),
                };

                comments.Add(comment);
            }
            return comments;
        }

        public void Create() {
            string query = string.Format("insert into forum_comments (forum_post_id, comment, created_by) values ({0}, '{1}', '{2}')", ForumPostId, Comment, CreatedBy);
            Database.ExecuteNonQuery(query);
        }

        public void Delete() {
            Delete(Id);
        }

        public static void Delete(long id) {
            string query = string.Format("delete from forum_comments where id = {0}", id);
            Database.ExecuteNonQuery(query);
        }
    }
}