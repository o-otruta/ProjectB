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
    /// Default Sprint 8 provider allowing all authored reactive synergies to auto-activate in runs.
    /// Will be replaced by TalentTreeManager in Sprint 11.
    /// </summary>
    public sealed class DefaultSynergyUnlockProvider : ISynergyUnlockProvider
    {
        public bool IsSynergyUnlocked(string synergyId) => !string.IsNullOrEmpty(synergyId);
    }
}
