namespace ProjectB.Meta
{
    /// <summary>
    /// Determines whether a reactive synergy is unlocked in meta-progression (e.g. Constellation Tree).
    /// </summary>
    public interface ISynergyUnlockProvider
    {
        bool IsSynergyUnlocked(string synergyId);
    }

    /// <summary>
    /// Permissive provider allowing all authored reactive synergies to auto-activate (tests / debug).
    /// In the game, TalentTreeManager (Constellation Tree) is registered as the ISynergyUnlockProvider.
    /// </summary>
    public sealed class DefaultSynergyUnlockProvider : ISynergyUnlockProvider
    {
        public bool IsSynergyUnlocked(string synergyId) => !string.IsNullOrEmpty(synergyId);
    }
}
