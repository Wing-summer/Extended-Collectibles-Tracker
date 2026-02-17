
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using HUD;
using MoreSlugcats;
using UnityEngine;

namespace ExtendedCollectiblesTracker {
	static class MapDataExtensions {
		public class Extension {
			public int region;
			public Dictionary<DataPearl.AbstractDataPearl.DataPearlType, int> locatedPearls = new();
			public World world; 
			public class CollectibleData {
				public int order;
				public int room;
				public Vector2 pos;
				public Color color;
				public Color innerColor;
				public bool collected;
				public bool isPearl;
				public bool isRelocated;
			}
			public List<CollectibleData> collectibleData = new();
		}

		static ConditionalWeakTable<Map.MapData, Extension> extensions = new();

		public static Extension GetExtension(this Map.MapData self) {
			return extensions.GetOrCreateValue(self);
		}

		public static void ctor(Map.MapData self, World initWorld, RainWorld rainWorld) {
			if (initWorld == null) {
				return; // no data between map loads, no world exists in The Watcher DLC
			}
			
			RainWorldGame game = rainWorld.processManager.currentMainLoop as RainWorldGame;
			Extension extendedSelf = self.GetExtension();
			extendedSelf.region = initWorld.region.regionNumber;
			extendedSelf.world = initWorld;
			
			PlayerProgression.MiscProgressionData miscProgressionData = rainWorld.progression.miscProgressionData;

			SaveState saveState = null;
			if (rainWorld.progression.IsThereASavedGame(rainWorld.progression.PlayingAsSlugcat)) {
				if (rainWorld.progression.starvedSaveState != null) {
					saveState = rainWorld.progression.starvedSaveState;
				} else if (rainWorld.progression.currentSaveState != null) {
					saveState = rainWorld.progression.currentSaveState;
				}
			}

			// per room
			foreach (var roomIndex in self.roomIndices) {
				AbstractRoom abstractRoom = initWorld.GetAbstractRoom(roomIndex);

				RoomSettings roomSettings = new RoomSettings(abstractRoom.name, initWorld.region, false, false, game?.TimelinePoint, game);
				
				// per object
				for (int i = 0; i < roomSettings.placedObjects.Count; i++) {
					PlacedObject placedObject = roomSettings.placedObjects[i];

					if (placedObject.active && placedObject.data is PlacedObject.DataPearlData pearlData) {
						var pearlType = pearlData.pearlType;
						if (!DataPearl.PearlIsNotMisc(pearlType))
							continue;

						extendedSelf.locatedPearls[pearlType] = extendedSelf.collectibleData.Count;
						
						bool pearlRead = Mod.IsPearlRead(rainWorld, pearlType);

						extendedSelf.collectibleData.Add(new Extension.CollectibleData() {
							room = roomIndex,
							pos = placedObject.pos,
							color = Mod.GetPearlIconColor(pearlType),
							innerColor = DataPearl.UniquePearlHighLightColor(pearlType).GetValueOrDefault(Color.white),
							collected = pearlRead || (saveState != null && saveState.ItemConsumed(initWorld, false, roomIndex, i)),
							isPearl = true
						});
					} else if (placedObject.type == PlacedObject.Type.BlueToken || placedObject.type == PlacedObject.Type.GoldToken) {
						CollectToken.CollectTokenData tokenData = (CollectToken.CollectTokenData)placedObject.data;
						if (!tokenData.availableToPlayers.Contains(rainWorld.progression.PlayingAsSlugcat))
							continue;

						extendedSelf.collectibleData.Add(new Extension.CollectibleData() {
							order = tokenData.isBlue ? 0 : 1,
							room = roomIndex,
							pos = placedObject.pos,
							color = tokenData.isBlue ? RainWorld.AntiGold.rgb : RainWorld.GoldRGB,
							collected = miscProgressionData.GetTokenCollected(tokenData.tokenString, tokenData.isBlue)
						});
					} else if (placedObject.type == MoreSlugcatsEnums.PlacedObjectType.RedToken) {
						CollectToken.CollectTokenData tokenData = (CollectToken.CollectTokenData)placedObject.data;
						if (!tokenData.availableToPlayers.Contains(rainWorld.progression.PlayingAsSlugcat))
							continue;
						
						extendedSelf.collectibleData.Add(new Extension.CollectibleData() {
							order = -1,
							room = roomIndex,
							pos = placedObject.pos,
							color = CollectToken.RedColor.rgb,
							collected = miscProgressionData.GetTokenCollected(new MultiplayerUnlocks.SafariUnlockID(tokenData.tokenString, false))
						});
					} else if (placedObject.type == PlacedObject.Type.GreenToken) {
						CollectToken.CollectTokenData tokenData = (CollectToken.CollectTokenData)placedObject.data;
						if (!tokenData.availableToPlayers.Contains(rainWorld.progression.PlayingAsSlugcat))
							continue;
						
						extendedSelf.collectibleData.Add(new Extension.CollectibleData {
							order = -2,
							room = roomIndex,
							pos = placedObject.pos,
							color = CollectToken.GreenColor.rgb,
							collected = miscProgressionData.GetTokenCollected(new MultiplayerUnlocks.SlugcatUnlockID(tokenData.tokenString, false))
						});
					} else if (placedObject.type == MoreSlugcatsEnums.PlacedObjectType.WhiteToken) {
						if (rainWorld.progression.PlayingAsSlugcat == MoreSlugcatsEnums.SlugcatStatsName.Spear) {
							CollectToken.CollectTokenData tokenData = (CollectToken.CollectTokenData)placedObject.data;
							if (!tokenData.availableToPlayers.Contains(rainWorld.progression.PlayingAsSlugcat))
								continue;

							if (ChatlogData.HasUnique(tokenData.ChatlogCollect)) {
								extendedSelf.collectibleData.Add(new Extension.CollectibleData {
									
									order = 2,
									room = roomIndex,
									pos = placedObject.pos,
									color = CollectToken.WhiteColor.rgb,
									collected = miscProgressionData.GetBroadcastListened(tokenData.ChatlogCollect)
								});
							} else {
								bool collected = false;
								if (saveState != null) {
									collected = saveState.miscWorldSaveData.SSaiConversationsHad == 0 ?
										saveState.deathPersistentSaveData.prePebChatlogsRead.Contains(tokenData.ChatlogCollect) :
										saveState.deathPersistentSaveData.chatlogsRead.Contains(tokenData.ChatlogCollect);
								}

								extendedSelf.collectibleData.Add(new Extension.CollectibleData {
									order = 2,
									room = roomIndex,
									pos = placedObject.pos,
									color = Color.white,
									collected = collected
								});
							}
						}
					}
				}
			}
		}

		public static void LocatePearls(this Map.MapData self, RainWorld rainWorld)
		{
			Extension extendedSelf = self.GetExtension();

			if (!rainWorld.progression.IsThereASavedGame(rainWorld.progression.PlayingAsSlugcat) ||
			    rainWorld.progression.currentSaveState == null)
			{
				return;
			}

			if (extendedSelf.region < 0 ||
			    extendedSelf.region >= rainWorld.progression.currentSaveState.regionStates.Length)
				return;

			RegionState currentRegion = rainWorld.progression.currentSaveState.regionStates[extendedSelf.region];
			if (currentRegion == null || currentRegion.savedObjects == null)
			{
				return;
			}

			World worldForDeserialization = extendedSelf.world;
			foreach (string savedObject in currentRegion.savedObjects)
			{
				AbstractPhysicalObject abstractPhysicalObject =
					SaveState.AbstractPhysicalObjectFromString(worldForDeserialization, savedObject);

				if (abstractPhysicalObject is DataPearl.AbstractDataPearl abstractDataPearl)
				{
					var pearlType = abstractDataPearl.dataPearlType;
					if (!DataPearl.PearlIsNotMisc(pearlType)) continue;

					WorldCoordinate pos = abstractPhysicalObject.pos;
					Vector2 pixelPos = new Vector2(pos.x * 20f, pos.y * 20f);

					if (extendedSelf.locatedPearls.TryGetValue(pearlType, out int index))
					{
						if (index >= 0 && index < extendedSelf.collectibleData.Count)
						{
							Extension.CollectibleData collectibleData = extendedSelf.collectibleData[index];
							collectibleData.room = pos.room;
							collectibleData.pos = pixelPos;
							collectibleData.isRelocated = true;
						}
						else
						{
							extendedSelf.locatedPearls.Remove(pearlType);
						}
					}

					if (!extendedSelf.locatedPearls.ContainsKey(pearlType))
					{
						extendedSelf.locatedPearls[pearlType] = extendedSelf.collectibleData.Count;
						bool pearlRead = Mod.IsPearlRead(rainWorld, pearlType);
						extendedSelf.collectibleData.Add(new Extension.CollectibleData()
						{
							room = pos.room,
							pos = pixelPos,
							color = Mod.GetPearlIconColor(pearlType),
							innerColor =
								DataPearl.UniquePearlHighLightColor(pearlType).GetValueOrDefault(Color.white),
							collected = pearlRead,
							isPearl = true,
							isRelocated = true,
						});
					}
				}
			}

			if (rainWorld.progression.currentSaveState.objectTrackers != null)
			{
				foreach (PersistentObjectTracker trackedObject in rainWorld.progression.currentSaveState.objectTrackers)
				{
					if (trackedObject.lastSeenRegion != self.regionName) continue;

					if (trackedObject.obj == null && !string.IsNullOrEmpty(trackedObject.objRepresentation))
					{
						trackedObject.obj = SaveState.AbstractPhysicalObjectFromString(worldForDeserialization,
							trackedObject.objRepresentation);
					}

					if (trackedObject.obj is DataPearl.AbstractDataPearl abstractDataPearl2)
					{
						var pearlType = abstractDataPearl2.dataPearlType;
						if (!DataPearl.PearlIsNotMisc(pearlType)) continue;

						WorldCoordinate pos = trackedObject.desiredSpawnLocation;
						Vector2 pixelPos = new Vector2(pos.x * 20f, pos.y * 20f);

						if (extendedSelf.locatedPearls.TryGetValue(pearlType, out int index))
						{
							if (index >= 0 && index < extendedSelf.collectibleData.Count)
							{
								Extension.CollectibleData collectibleData = extendedSelf.collectibleData[index];
								collectibleData.room = pos.room;
								collectibleData.pos = pixelPos;
								collectibleData.isRelocated = true;
							}
							else
							{
								extendedSelf.locatedPearls.Remove(pearlType);
							}
						}
						else
						{
							bool pearlRead = Mod.IsPearlRead(rainWorld, pearlType);
							extendedSelf.locatedPearls[pearlType] = extendedSelf.collectibleData.Count;
							extendedSelf.collectibleData.Add(new Extension.CollectibleData()
							{
								room = pos.room,
								pos = pixelPos,
								color = Mod.GetPearlIconColor(pearlType),
								innerColor =
									DataPearl.UniquePearlHighLightColor(pearlType).GetValueOrDefault(Color.white),
								collected = pearlRead,
								isPearl = true,
								isRelocated = true,
							});
						}
					}
				}
			}
		}

	}
}