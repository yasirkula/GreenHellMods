using Enums;
using System.Collections.Generic;
using UnityEngine;

namespace GreenHell_AlwaysVisibleItemInfos
{
	public class HUDTriggerExtended : HUDTrigger
	{
		// While hovering over an item, if inventory is closed and RMB isn't held, show item's durability
		// Code below is mostly copy&pasted from original implementation
		protected override void SetupDurabilityInfo()
		{
			if( !m_DurabilityParent )
				return;

			m_DurabilityParent.SetActive( false );

			if( IsExpanded() )
				return;

			Trigger trigger = GetTrigger();
			if( trigger == null || !trigger.IsItem() )
				return;

			Item obj = trigger as Item;
			if( obj && obj.IsFood() )
			{
				Food food = obj as Food;
				if( !food || !food.CanSpoil() )
					return;

				m_DurabilityParent.SetActive( true );
				m_DurabilityName.text = GreenHellGame.Instance.GetLocalization().Get( "HUDTrigger_Decay", true );
				float num1 = food.m_FInfo.m_SpoilOnlyIfTriggered ? food.m_FirstTriggerTime : food.m_FInfo.m_CreationTime;
				double num2 = food.m_FInfo.m_SpoilTime - ( (float) MainLevel.Instance.m_TODSky.Cycle.GameTime - num1 );
				float num3 = (float) ( num2 % 1.0 );
				float num4 = (float) num2 - num3;
				float num5 = Mathf.Floor( num4 / 24f );
				float num6 = num4 - num5 * 24f;
				float num7 = num3 * 60f;
				m_Durability.text = num5.ToString( "F0" ) + "d " + num6.ToString( "F0" ) + "h " + num7.ToString( "F0" ) + "m";
			}
			else
			{
				if( !obj.m_Info.IsWeapon() && !obj.m_Info.IsTool() && ( !obj.m_Info.IsArmor() || obj.m_Info.m_ID == ItemID.broken_armor ) )
					return;

				m_DurabilityParent.SetActive( true );
				m_DurabilityName.text = GreenHellGame.Instance.GetLocalization().Get( "HUDTrigger_Durability", true );
				m_Durability.text = ( ( obj.m_Info.m_Health / obj.m_Info.m_MaxHealth * 100.0 ) ).ToString( "F0" ) + "%";
			}
		}

		// While hovering over a food, show food's consumable effects
		// Code below is mostly copy&pasted from original implementation
		protected override void SetupConsumableEffects()
		{
			if( !m_ConsumableEffects )
				return;

			if( IsExpanded() )
				m_ConsumableEffects.gameObject.SetActive( false );
			else
			{
				Trigger trigger = GetTrigger();
				if( !trigger || ( !trigger.IsItem() && !( trigger is PlantFruit ) ) )
					m_ConsumableEffects.gameObject.SetActive( false );
				else
				{
					ItemInfo m_Info = trigger.IsItem() ? ( (Item) trigger ).m_Info : ( (PlantFruit) trigger ).m_ItemInfo;
					if( !m_Info.IsConsumable() && !m_Info.IsLiquidContainer() )
						m_ConsumableEffects.gameObject.SetActive( false );
					else
					{
						int index1 = 0;
						if( m_Info.IsConsumable() )
						{
							if( !ItemsManager.Get().WasConsumed( m_Info.m_ID ) )
								m_UnknownEffect.SetActive( true );
							else
							{
								m_UnknownEffect.SetActive( false );
								ConsumableInfo info = (ConsumableInfo) m_Info;
								if( info.m_Proteins > 0.0 )
									SetupEffect( "Watch_protein_icon", IconColors.GetColor( IconColors.Icon.Proteins ), info.m_Proteins, "HUD_Nutrition_Protein", ref index1, -1f );
								if( info.m_Fat > 0.0 )
									SetupEffect( "Watch_fat_icon", IconColors.GetColor( IconColors.Icon.Fat ), info.m_Fat, "HUD_Nutrition_Fat", ref index1, -1f );
								if( info.m_Carbohydrates > 0.0 )
									SetupEffect( "Watch_carbo_icon", IconColors.GetColor( IconColors.Icon.Carbo ), info.m_Carbohydrates, "HUD_Nutrition_Carbo", ref index1, -1f );
								if( info.m_Water > 0.0 )
									SetupEffect( "Watch_water_icon", IconColors.GetColor( IconColors.Icon.Hydration ), info.m_Water, "HUD_Hydration", ref index1, -1f );
								if( info.m_Dehydration > 0.0 )
									SetupEffect( "Watch_water_icon", IconColors.GetColor( IconColors.Icon.Hydration ), -1f * info.m_Dehydration, "HUD_Hydration", ref index1, -1f );
								if( info.m_AddEnergy > 0.0 )
									SetupEffect( "Energy_icon", Color.white, info.m_AddEnergy, "HUD_Energy", ref index1, -1f );
								if( info.m_SanityChange != 0.0 )
									SetupEffect( "sanity_icon_H", Color.white, info.m_SanityChange, "HUD_Sanity", ref index1, -1f );
								if( info.m_ConsumeEffect == ConsumeEffect.Fever )
									SetupEffect( "Fever_icon_T", Color.white, info.m_ConsumeEffectLevel, "Fever", ref index1, -1f );
								if( info.m_ConsumeEffect == ConsumeEffect.FoodPoisoning )
									SetupEffect( "Vomit_icon_H", Color.white, info.m_ConsumeEffectLevel, "HUD_FoodPoisoning", ref index1, -1f );
								else if( info.m_ConsumeEffect == ConsumeEffect.ParasiteSickness )
									SetupEffect( "ParasiteSichness_icon_H", Color.white, info.m_ConsumeEffectLevel, "HUD_ParasiteSickness", ref index1, -1f );
							}
						}
						else if( m_Info.IsLiquidContainer() )
						{
							LiquidContainerInfo info = (LiquidContainerInfo) m_Info;
							if( info.m_Amount > 0.0 )
							{
								LiquidData liquidData = LiquidManager.Get().GetLiquidData( info.m_LiquidType );
								if( info.m_Amount >= 1.0 )
									SetupEffect( "Watch_water_icon", IconColors.GetColor( IconColors.Icon.Hydration ), info.m_Amount, "HUD_Hydration", ref index1, info.m_Capacity );
								if( liquidData.m_Energy > 0.0 )
									SetupEffect( "Energy_icon", Color.white, liquidData.m_Energy, "HUD_Energy", ref index1, -1f );
								for( int index2 = 0; index2 < liquidData.m_ConsumeEffects.Count; ++index2 )
								{
									if( liquidData.m_ConsumeEffects[index2].m_ConsumeEffect == ConsumeEffect.FoodPoisoning )
										SetupEffect( "Vomit_icon_H", Color.white, liquidData.m_ConsumeEffects[index2].m_ConsumeEffectLevel, "HUD_FoodPoisoning", ref index1, -1f );
									else if( liquidData.m_ConsumeEffects[index2].m_ConsumeEffect == ConsumeEffect.Fever )
										SetupEffect( "Fever_icon_T", Color.white, liquidData.m_ConsumeEffects[index2].m_ConsumeEffectLevel, "Fever", ref index1, -1f );
									else if( liquidData.m_ConsumeEffects[index2].m_ConsumeEffect == ConsumeEffect.ParasiteSickness )
										SetupEffect( "ParasiteSichness_icon_H", Color.white, liquidData.m_ConsumeEffects[index2].m_ConsumeEffectLevel, "Parasite Sickness", ref index1, -1f );
								}
								if( info.IsBowl() )
								{
									if( liquidData.m_Proteins > 0.0 )
										SetupEffect( "Watch_protein_icon", IconColors.GetColor( IconColors.Icon.Proteins ), liquidData.m_Proteins, "HUD_Nutrition_Protein", ref index1, -1f );
									if( liquidData.m_Fat > 0.0 )
										SetupEffect( "Watch_fat_icon", IconColors.GetColor( IconColors.Icon.Fat ), liquidData.m_Fat, "HUD_Nutrition_Fat", ref index1, -1f );
									if( liquidData.m_Carbohydrates > 0.0 )
										SetupEffect( "Watch_carbo_icon", IconColors.GetColor( IconColors.Icon.Carbo ), liquidData.m_Carbohydrates, "HUD_Nutrition_Carbo", ref index1, -1f );
									if( liquidData.m_Dehydration > 0.0 )
										SetupEffect( "Watch_water_icon", IconColors.GetColor( IconColors.Icon.Hydration ), -1f * liquidData.m_Dehydration, "HUD_Hydration", ref index1, -1f );
									if( liquidData.m_SanityChange != 0 )
										SetupEffect( "sanity_icon_H", Color.white, liquidData.m_SanityChange, "HUD_Sanity", ref index1, -1f );
								}
							}
							m_UnknownEffect.SetActive( index1 == 0 );
						}
						for( int index2 = index1; index2 < m_EffectsData.Count; ++index2 )
							m_EffectsData[index2].m_Parent.SetActive( false );
						m_ConsumableEffects.gameObject.SetActive( true );
					}
				}
			}
		}
	}
}