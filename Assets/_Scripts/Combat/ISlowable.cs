namespace ProjectB.Combat
{
    public interface ISlowable
    {
        bool IsSlowed { get; }
        void ApplySlow(float factor, float duration);
    }
}
