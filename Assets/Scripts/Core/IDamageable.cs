namespace Vamsurlike.Core
{
    public interface IDamageable
    {
        float HP     { get; }
        float MaxHP  { get; }
        bool  IsAlive { get; }

        void TakeDamage(float amount);
        void Heal(float amount);
    }
}
