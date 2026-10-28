#if UNITY_EDITOR
using System;
using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(32000)]
    public sealed class StageCaptureProbe:MonoBehaviour
    {
        public Action AfterLateUpdate;
        void LateUpdate()=>AfterLateUpdate?.Invoke();
    }
}
#endif
