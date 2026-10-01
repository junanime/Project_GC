using UnityEngine;
namespace Vampire
{
    public sealed class RemakeMaterialCleanup : MonoBehaviour
    {
        public Material Material;
        void OnDestroy(){if(Material!=null)Destroy(Material);}
    }
}
