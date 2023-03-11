using UnityEngine;
using UnityModManagerNet;
using Object = UnityEngine.Object;

namespace BillORumble
{
    [EnableReloading]
    public class Main
    {
        public static bool Load(UnityModManager.ModEntry mod_entry)
        {
            mod_entry.OnToggle = OnToggle;
            mod_entry.OnUnload = Unload;

            return true;
        }

        static bool Unload(UnityModManager.ModEntry mod_entry)
        {
            return OnToggle(mod_entry, false);
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {

        }

        private static bool OnToggle(UnityModManager.ModEntry mod_entry, bool value)
        {
            if (value)
            {
                var go = new GameObject("RumbleManager", typeof(RumbleManager));

                Object.DontDestroyOnLoad(go);
            }
            else
            {
                var rm = Object.FindObjectOfType<RumbleManager>();
                if (rm != null)
                    Object.Destroy(rm.gameObject);
            }

            return true;
        }
    }
}
