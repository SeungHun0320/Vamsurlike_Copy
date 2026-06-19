namespace Vamsurlike.Skills
{
    internal abstract class ProjectileModeBase
    {
        protected readonly NetworkProjectile Projectile;

        protected ProjectileModeBase(NetworkProjectile projectile)
        {
            Projectile = projectile;
        }

        public virtual void Enter() { }

        public virtual void Exit() { }

        public abstract void Tick(float deltaTime);
    }
}
