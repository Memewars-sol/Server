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
    }
        
}