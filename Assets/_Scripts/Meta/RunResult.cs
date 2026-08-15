namespace ProjectB.Meta
{
    public struct RunResult
    {
        public int waveReached;
        public int enemiesKilled;
        public int coinsEarned;
        public float playTime;
        
        // Можно добавить больше статистики позже
        public int abilitiesCollected;
        public int maxAbilityLevel;
        public bool noDamageClear; // для ачивки "без урона"
    }
}
