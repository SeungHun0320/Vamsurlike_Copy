using UnityEngine;
using Vamsurlike.Data;

namespace Vamsurlike.Player
{
    public interface IPlayerFacade
    {
        Vector3 Position    { get; }
        float   HP          { get; }
        float   MaxHP       { get; }
        bool    IsAlive     { get; }

        void TakeDamage(float amount);
        void Heal(float amount);
        float GetStat(StatType type);
    }
}
