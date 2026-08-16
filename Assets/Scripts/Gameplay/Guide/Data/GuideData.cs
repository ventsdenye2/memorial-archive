using System;
using System.Collections.Generic;

namespace MemorialArchive.Gameplay.Guide.Data
{
    /// <summary>
    /// 引导系统的持久化存档。仅记录已完成步骤的 id，保证「保存后已完成步骤不重复出现」。
    /// </summary>
    [Serializable]
    public sealed class GuideSaveData
    {
        public List<string> completedStepIds = new List<string>();
    }
}
