using UnityEngine;

namespace LateSubmission.Core
{
    /// <summary>
    /// Tracks persistent checkpoint and spawn point names across scene loads.
    /// </summary>
    public static class SceneTransitionManager
    {
        public static string TargetSpawnPointName = null;
    }
}
