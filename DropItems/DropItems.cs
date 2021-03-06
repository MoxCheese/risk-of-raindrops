﻿namespace DropItems
{
	using BepInEx;
	using RoR2;
	using RoR2.UI;
	using System.Collections.Generic;
	using System.Reflection;
	using UnityEngine;
	using UnityEngine.EventSystems;
	using UnityEngine.SceneManagement;
	using UnityEngine.Networking;

	[BepInPlugin("com.kookehs.dropitems", "DropItems", "1.1.2")]

	public class DropItems : BaseUnityPlugin
	{
		internal static List<HUD> CachedHudInstancesList { get; set; }

		private void Awake()
		{
			SceneManager.sceneUnloaded += OnSceneUnloaded;
			Debug.Log("Loaded DropItemsMod");
		}

		private void OnSceneUnloaded(Scene current)
		{
			CachedHudInstancesList = null;
		}

		private void Update()
		{
			if (!NetworkServer.active) return;

			if (CachedHudInstancesList == null)
			{
				CachedHudInstancesList = (List<HUD>)typeof(HUD).GetField("instancesList", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
			}

			foreach (HUD hud in CachedHudInstancesList)
			{
				if (hud == null) continue;
				FieldInfo fieldInfo = typeof(ItemInventoryDisplay).GetField("itemIcons", BindingFlags.NonPublic | BindingFlags.Instance);
				UpdateHud(hud, fieldInfo);
				UpdateScoreboard(hud, fieldInfo);
			}
		}

		private void UpdateHud(HUD hud, FieldInfo fieldInfo)
		{
			if (hud.itemInventoryDisplay != null)
			{
				if (fieldInfo != null)
				{
					List<ItemIcon> itemIcons = (List<ItemIcon>)fieldInfo.GetValue(hud.itemInventoryDisplay);

					foreach (ItemIcon itemIcon in itemIcons)
					{
						if (itemIcon != null && itemIcon.GetComponent<DropItemController>() == null)
						{
							DropItemController dropItemController = itemIcon.gameObject.AddComponent<DropItemController>();
							Inventory inventory = (Inventory)typeof(ItemInventoryDisplay).GetField("inventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud.itemInventoryDisplay);
							dropItemController.Inventory = inventory;
							dropItemController.ItemIcon = itemIcon;
						}
					}
				}
			}

			foreach (EquipmentIcon equipmentIcon in hud.equipmentIcons)
			{
				if (equipmentIcon != null && equipmentIcon.GetComponent<DropItemController>() == null)
				{
					if (equipmentIcon.targetInventory == null) continue;
					EquipmentIndex equipmentIndex = (equipmentIcon.displayAlternateEquipment) ? equipmentIcon.targetInventory.alternateEquipmentIndex : equipmentIcon.targetInventory.currentEquipmentIndex;
					if (equipmentIndex != EquipmentIndex.None)
					{
						DropItemController dropItemController = equipmentIcon.gameObject.AddComponent<DropItemController>();
						dropItemController.Inventory = equipmentIcon.targetInventory;
						dropItemController.EquipmentIcon = equipmentIcon;
					}
				}
			}
		}

		private void UpdateScoreboard(HUD hud, FieldInfo fieldInfo)
		{
			ScoreboardController scoreboardController = hud.scoreboardPanel.GetComponent<ScoreboardController>();

			if (scoreboardController != null)
			{
				UIElementAllocator<ScoreboardStrip> elements = (UIElementAllocator<ScoreboardStrip>)typeof(ScoreboardController)
					.GetField("stripAllocator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scoreboardController);

				if (elements != null)
				{
					List<ScoreboardStrip> scoreboardStrips = (List<ScoreboardStrip>)typeof(UIElementAllocator<ScoreboardStrip>)
						.GetField("elementControllerComponentsList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(elements);

					foreach (ScoreboardStrip scoreboardStrip in scoreboardStrips)
					{
						if (scoreboardStrip == null) continue;

						if (fieldInfo != null)
						{
							List<ItemIcon> itemIcons = (List<ItemIcon>)fieldInfo.GetValue(scoreboardStrip.itemInventoryDisplay);

							foreach (ItemIcon itemIcon in itemIcons)
							{
								if (itemIcon != null && itemIcon.GetComponent<DropItemController>() == null)
								{
									DropItemController dropItemController = itemIcon.gameObject.AddComponent<DropItemController>();
									Inventory inventory = (Inventory)typeof(ItemInventoryDisplay).GetField("inventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scoreboardStrip.itemInventoryDisplay);
									dropItemController.Inventory = inventory;
									dropItemController.ItemIcon = itemIcon;
								}
							}
						}

						EquipmentIcon equipmentIcon = scoreboardStrip.equipmentIcon;

						if (equipmentIcon != null && equipmentIcon.GetComponent<DropItemController>() == null)
						{
							if (equipmentIcon.targetInventory == null) continue;
							EquipmentIndex equipmentIndex = (equipmentIcon.displayAlternateEquipment) ? equipmentIcon.targetInventory.alternateEquipmentIndex : equipmentIcon.targetInventory.currentEquipmentIndex;

							if (equipmentIndex != EquipmentIndex.None)
							{
								DropItemController dropItemController = equipmentIcon.gameObject.AddComponent<DropItemController>();
								dropItemController.Inventory = equipmentIcon.targetInventory;
								dropItemController.EquipmentIcon = equipmentIcon;
							}
						}
					}
				}
			}
		}
	}

	public class DropItemController : MonoBehaviour, IPointerClickHandler
	{
		public static List<DropItemController> InstancesList { get; set; } = new List<DropItemController>();
		public static bool ShouldDestroy { get; set; }
		public Inventory Inventory { get; set; } = null;
		public ItemIcon ItemIcon { get; set; } = null;
		public EquipmentIcon EquipmentIcon { get; set; } = null;

		private void Awake()
		{
			// TODO(kookehs): Map instances of this to players.
			InstancesList.Add(this);
		}

		private void LateUpdate()
		{
			if (ShouldDestroy)
			{
				foreach (DropItemController dropItemController in InstancesList)
				{
					Destroy(dropItemController);
				}

				InstancesList.Clear();
				ShouldDestroy = false;
			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			// TODO(kookehs): Add multiplayer support.
			if (!NetworkServer.active || ShouldDestroy) return;

			CharacterBody characterBody = Inventory.GetComponent<CharacterMaster>().GetBody();
			if (characterBody == null || characterBody.healthComponent == null || characterBody.healthComponent.alive == false) return;

			if (Inventory == null || (ItemIcon == null && EquipmentIcon == null))
			{
				ShouldDestroy = true;
				return;
			}

			Notification notification = characterBody.gameObject.AddComponent<Notification>();
			notification.transform.SetParent(characterBody.transform);
			notification.SetPosition(new Vector3((float)(Screen.width * 0.8), (float)(Screen.height * 0.25), 0));
			Transform transform = characterBody.transform;

			if (EquipmentIcon != null)
			{
				EquipmentIndex equipmentIndex = (EquipmentIcon.displayAlternateEquipment) ? EquipmentIcon.targetInventory.alternateEquipmentIndex : EquipmentIcon.targetInventory.currentEquipmentIndex;

				if (equipmentIndex != EquipmentIndex.None)
				{
					EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
					notification.SetIcon(Resources.Load<Texture>(equipmentDef.pickupIconPath));
					notification.GetTitle = () => "Equipment dropped";
					notification.GetDescription = () => $"{Language.GetString(equipmentDef.nameToken)}";
					Inventory.SetEquipmentIndex(EquipmentIndex.None);
					PickupDropletController.CreatePickupDroplet(new PickupIndex(equipmentIndex), transform.position, Vector3.up * 20f + transform.forward * 10f);
					ShouldDestroy = true;
					return;
				}
			}

			int[] itemStacks = (int[])typeof(Inventory).GetField("itemStacks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Inventory);
			ItemIndex itemIndex = (ItemIndex)typeof(ItemIcon).GetField("itemIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ItemIcon);

			if (itemStacks[(int)itemIndex] > 0)
			{
				ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
				notification.SetIcon(Resources.Load<Texture>(itemDef.pickupIconPath));
				notification.GetTitle = () => "Item dropped";
				notification.GetDescription = () => $"{Language.GetString(itemDef.nameToken)}";
				Inventory.RemoveItem(itemIndex, 1);
				PickupDropletController.CreatePickupDroplet(new PickupIndex(itemIndex), transform.position, Vector3.up * 20f + transform.forward * 10f);
			}

			if (itemStacks[(int)itemIndex] <= 0)
			{
				ShouldDestroy = true;
			}
		}
	}
}
