namespace Models {
        public enum UnitID
        {
            barbarian = 0, archer = 1, goblin = 2, healer = 3, wallbreaker = 4, giant = 5, miner = 6, balloon = 7, wizard = 8, dragon = 9, pekka = 10, babydragon = 11, electrodragon = 12, yeti = 13, dragonrider = 14, electrotitan = 15, minion = 16, hogrider = 17, valkyrie = 18, golem = 19, witch = 20, lavahound = 21, bowler = 22, icegolem = 23, headhunter = 24, skeleton = 25, bat = 26
        }

        public enum UnitMoveType
        {
            ground = 0, jump = 1, fly = 2, underground = 3
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
        }


}