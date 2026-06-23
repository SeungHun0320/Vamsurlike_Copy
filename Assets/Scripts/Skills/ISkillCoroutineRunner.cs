using System.Collections;
using UnityEngine;

namespace Vamsurlike.Skills
{
    public interface ISkillCoroutineRunner
    {
        Coroutine StartSkillCoroutine(IEnumerator routine);
    }
}
