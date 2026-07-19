using UnityEngine;

namespace MemorialArchive.Gameplay.Camera.View
{
    // 历史组件：曾经负责 LateUpdate Lerp 跟随玩家。
    // 现已被 Cinemachine VirtualCamera 接管。本类保留为空壳，原因：
    //   1) Stage1DemoSceneValidator 强制要求场景里恰好存在 1 个 CameraFollowView（冻结文件，不可改）
    //   2) PlayerMotor.BindCamera 仍会调用 SetTarget（兼容旧路径）
    // 实际相机跟随逻辑由 MainCamera 上的 CinemachineBrain + CM_vcam_Player 子 GO 完成。
    public sealed class CameraFollowView : MonoBehaviour
    {
        public Transform Target { get; private set; }

        public void SetTarget(Transform value) => Target = value;
    }
}
