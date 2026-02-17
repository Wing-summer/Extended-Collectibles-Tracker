using System.Security;
using System.Security.Permissions;

using BepInEx;

// Allows access to private members
#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ExtendedCollectiblesTracker {
	[BepInPlugin(GUID, MOD_NAME, VERSION)]
	public class Plugin : BaseUnityPlugin {
		public const string VERSION = "1.0.5";
		public const string MOD_NAME = "Extended Collectibles Tracker";
		public const string MOD_ID = "extendedcollectiblestracker.fixed";
		public const string AUTHOR = "franklygd";
		public const string GUID = AUTHOR + "." + MOD_ID;

		public void OnEnable() {
			Mod.Logger = Logger;
			On.RainWorld.OnModsInit += Mod.Initialize;
		}
	}
}