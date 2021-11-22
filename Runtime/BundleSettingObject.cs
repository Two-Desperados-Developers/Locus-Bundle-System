using UnityEngine;

namespace BundleSystem
{
    [CreateAssetMenu(fileName = "BundleSetting.asset", menuName = "Create Bundle Setting Object", order = 999)]
    public class BundleSettingObject : ScriptableObject
    {
        public BundleSetting bundleSetting;
    }
}