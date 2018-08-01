﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using StardewModdingAPI;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley;

using xTile;

using SObject = StardewValley.Object;
using StardewModdingAPI.Events;
using System.Reflection;

using Harmony;
using Microsoft.Xna.Framework.Input;
using StardewValley.Objects;
using System.Linq;
using xTile.Layers;
using xTile.Tiles;

namespace Sauvignon_in_Stardew
{
    class ModEntry : Mod, IAssetLoader
    {
        /*
         * FIELDS
         * 
         */
        public static IModHelper helper;
        public static IMonitor monitor;

        public Texture2D Winery_outdoors;
        public Map Winery_indoors;

        public List<KeyValuePair<int, int>> wineryCoords;

        public Layer layer;
        public TileSheet tilesheet;
        public int tileID;

        public readonly Dictionary<string, string> dataForBlueprint = new Dictionary<string, string>() { ["Winery"] = "709 200 330 100 390 100/11/6/5/5/-1/-1/Winery/Winery/Kegs and Casks inside work 30% faster and display time remaining./Buildings/none/96/96/20/null/Farm/20000/false" };

        public readonly Vector2 TooltipOffset = new Vector2(Game1.tileSize / 2);
        public readonly Rectangle TooltipSourceRect = new Rectangle(0, 256, 60, 60);
        /*
         * END FIELDS
         * 
         */



        /*
         * ENTRY
         * 
         */
        public override void Entry(IModHelper helper)
        {
            ModEntry.monitor = this.Monitor;
            ModEntry.helper = helper;

                //Loaded Textures for outside and indside the Winery
            Winery_outdoors = helper.Content.Load<Texture2D>("assets/Winery_outside.png", ContentSource.ModFolder);
            Winery_indoors = helper.Content.Load<Map>("assets/Winery.tbin", ContentSource.ModFolder);

                //Event for adding blueprint to carpenter menu
            MenuEvents.MenuChanged += MenuEvents_MenuChanged;

                //Event for Keg Speed
            TimeEvents.TimeOfDayChanged += TimeEvents_TimeOfDayChanged_Kegs;

                //Event for Cask Speed
            TimeEvents.AfterDayStarted += TimeEvents_AfterDayStarted_Casks;

                //Event for showing time remaining on hover
            GraphicsEvents.OnPostRenderEvent += GraphicsEvents_OnPostRenderEvent;
            GraphicsEvents.OnPreRenderEvent += GraphicsEvents_OnPreRenderEvent;

                //Events for editing Winery width
            LocationEvents.BuildingsChanged += LocationEvents_BuildingsChanged;

                //Event for proventing buidling overlays
            MenuEvents.MenuClosed += MenuEvents_MenuClosed;

                /*
                 * Events for save and loading
                 * 
                 */ 
            SaveEvents.BeforeSave += SaveEvents_BeforeSave;

            SaveEvents.AfterSave += SaveEvents_AfterSaveLoad;
            SaveEvents.AfterLoad += SaveEvents_AfterSaveLoad;
                /*
                 * End of Events for save and loading
                 * 
                 */


                /*
                 * HARMONY PATCHING
                 * 
                 */
            var harmony = HarmonyInstance.Create("com.jesse.winery");
            Type type = typeof(Cask);
            MethodInfo method = type.GetMethod("performObjectDropInAction");
            HarmonyMethod patchMethod = new HarmonyMethod(typeof(ModEntry).GetMethod(nameof(Patch_performObjectDropInAction)));
            harmony.Patch(method, patchMethod, null);
                /*
                 * END OF HARMONY PATCHING
                 * 
                 */ 
        }
        /*
        * END ENTRY
        * 
        */


        

        /*
         * ADD WINERY TO CARPENTER MENU
         * 
         */
        public void MenuEvents_MenuChanged(object sender, EventArgsClickableMenuChanged e)
        {
            if (Game1.activeClickableMenu is CarpenterMenu carpenterMenu)
            {
                    //Sets current Winery buildings to 11 width to stop overlay
                foreach (Building building in Game1.getFarm().buildings)
                {
                    if (building.buildingType.Value == "Winery")
                    {
                        building.tilesWide.Value = 11;                       
                    }
                }
                if (!IsMagical(carpenterMenu) && !HasBluePrint(carpenterMenu))
                {
                    BluePrint wineryBluePrint = new BluePrint("Slime Hutch")
                    {
                        name = "Winery",
                        displayName = "Winery",
                        description = "Kegs and Casks inside work 30% faster and display time remaining.",
                        daysToConstruct = 4,//4
                        moneyRequired = 40000 //40000
                    };
                    wineryBluePrint.itemsRequired.Clear();
                    wineryBluePrint.itemsRequired.Add(709, 200);//200
                    wineryBluePrint.itemsRequired.Add(330, 100);//100
                    wineryBluePrint.itemsRequired.Add(390, 100);//100

                    SetBluePrintField(wineryBluePrint, "textureName", "Buildings\\Winery");
                    SetBluePrintField(wineryBluePrint, "texture", Game1.content.Load<Texture2D>(wineryBluePrint.textureName));

                    GetBluePrints(carpenterMenu).Add(wineryBluePrint);
                }
            }
        }

        public static bool IsMagical(CarpenterMenu carpenterMenu)
        {
            return (bool)typeof(CarpenterMenu).GetField("magicalConstruction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        public static bool HasBluePrint(CarpenterMenu carpenterMenu)
        {
            return GetBluePrints(carpenterMenu).Exists(bluePrint => bluePrint.name == "Winery");
        }

        public static List<BluePrint> GetBluePrints(CarpenterMenu carpenterMenu)
        {
            return (List<BluePrint>)typeof(CarpenterMenu).GetField("blueprints", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(carpenterMenu);
        }

        public static void SetBluePrintField(BluePrint bluePrint, string field, object value)
        {
            typeof(BluePrint).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(bluePrint, value);
        }

            //sets back Winery widths to 8 for Archway walkthrough
        private void MenuEvents_MenuClosed(object sender, EventArgsClickableMenuClosed e)
        {
            if (e.PriorMenu is CarpenterMenu)
            {
                foreach (Building building in Game1.getFarm().buildings)
                {
                    if (building.buildingType.Value == "Winery")
                    {
                        building.tilesWide.Value = 8;
                    }
                }
            }
        }
        /*
         * END OF ADDING WINERY TO CARPENTER MENU
         * 
         */


         

        /*
         * EDIT WINERY WIDTH
         * 
         */
        public void LocationEvents_BuildingsChanged(object sender, EventArgsLocationBuildingsChanged e)
        {
            foreach(Building building in e.Added)
            {
                if(building.buildingType.Value == "Winery")
                {
                    foreach (Building b in Game1.getFarm().buildings)
                    {
                        if (b.buildingType.Value == "Winery")
                        {
                            b.tilesWide.Value = 8;
                            //monitor.Log($"" + b.buildingType.Value + " has width of " + b.tilesWide.Value);
                            layer = Game1.getFarm().map.GetLayer("Buildings");
                            tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
                            tileID = 131;
                            for (int x = b.tileX.Value + 9; x < b.tileX.Value + 11; x++)
                            {
                                for (int y = b.tileY.Value; y < b.tileY.Value + 6; y++)
                                {
                                    layer.Tiles[x, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileID);
                                }
                            }
                            if(b.daysOfConstructionLeft.Value > 0)
                            {
                                b.tilesWide.Value = 11;
                            }
                        }
                    }
                }
            }
            foreach(Building building in e.Removed)
            {
                if(building.buildingType.Value == "Winery")
                {
                    tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
                    tileID = 131;
                    for (int x = building.tileX.Value + 9; x < building.tileX.Value + 11; x++)
                    {
                        for (int y = building.tileY.Value; y < building.tileY.Value + 6; y++)
                        {
                            Game1.getFarm().removeTile(x, y, "Buildings");
                        }
                    }
                }
            }
            
        }
        /*
         * END EDIT WINERY WIDTH
         * 
         */



        /*
         * SAVE AND LOADING
         * Removing the Wineries, saving their coordinates, replacing with Slime Hutches, and reloading the wineries.
         */
        public void SaveEvents_AfterSaveLoad(object sender, EventArgs e)
        {
            wineryCoords = this.Helper.ReadJsonFile<List<KeyValuePair<int, int>>>($"{Constants.CurrentSavePath}/Winery_Coords.json") ?? new List<KeyValuePair<int, int>>();
            foreach (Building b in Game1.getFarm().buildings)
            {
                foreach (var pair in wineryCoords)
                {
                    if (b.tileX.Value == pair.Key && b.tileY.Value == pair.Value && b.buildingType.Value == "Slime Hutch")
                    {
                        b.tilesWide.Value = 8;
                        b.buildingType.Value = "Winery";
                        b.indoors.Value.mapPath.Value = "Maps\\Winery";
                        b.indoors.Value.updateMap();
                        layer = Game1.getFarm().map.GetLayer("Buildings");
                        tilesheet = Game1.getFarm().map.GetTileSheet("untitled tile sheet");
                        tileID = 131;
                        for (int x = b.tileX.Value + 9; x < b.tileX.Value + 11; x++)
                        {
                            for (int y = b.tileY.Value; y < b.tileY.Value + 6; y++)
                            {
                                layer.Tiles[x, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileID);
                            }
                        }
                        if(b.daysOfConstructionLeft.Value > 0)
                        {
                            b.tilesWide.Value = 11;
                        }
                    }
                }
            }
        }

        public void SaveEvents_BeforeSave(object sender, EventArgs e)
        {
            wineryCoords.Clear();
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                {
                    wineryCoords.Add(new KeyValuePair<int, int>(b.tileX.Value, b.tileY.Value));
                    b.tilesWide.Value = 11;
                    b.buildingType.Value = "Slime Hutch";
                    b.indoors.Value.mapPath.Value = "Maps\\SlimeHutch";
                    b.indoors.Value.updateMap();
                    for (int x = b.tileX.Value + 9; x < b.tileX.Value + 11; x++)
                    {
                        for (int y = b.tileY.Value; y < b.tileY.Value + 6; y++)
                        {
                            Game1.getFarm().removeTile(x, y, "Buildings");
                        }
                    }
                }
            }
            this.Helper.WriteJsonFile($"{Constants.CurrentSavePath}/Winery_Coords.json", wineryCoords);
        }
         /*
          * END SAVE AND LOADING
          * 
          */

        


        /*
         * SPEED UP KEG INSIDE WINERY
         * 
         */
        public void TimeEvents_TimeOfDayChanged_Kegs(object sender, EventArgsIntChanged e)
        {
            foreach (Building b in Game1.getFarm().buildings)
            {                                  
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                    foreach (SObject o in b.indoors.Value.Objects.Values)
                    {
                        if (o.Name.Equals("Keg"))
                            o.MinutesUntilReady -= 3;
                    }
            }
        }
        /*
         * END SPEED UP KEG INSIDE WINERY
         * 
         */



        /*
         * SPEED UP CASK INSIDE WINERY
         * 
         */
        public void TimeEvents_AfterDayStarted_Casks(object sender, EventArgs e)
        {
            foreach (Building b in Game1.getFarm().buildings)
            {
                if (b.indoors.Value != null && b.buildingType.Value.Equals("Winery"))
                    foreach (SObject o in b.indoors.Value.Objects.Values)
                    {
                        if (o is Cask c)
                            c.daysToMature.Value -= (float)(.3 * c.agingRate.Value);
                    }
            }
        }
        /*
         * END SPEED UP CASK INSIDE WINERY
         * 
         */




        /*
         * FIX ISSUE WITH WINERY MAP
         * 
         */
        public void GraphicsEvents_OnPreRenderEvent(object sender, EventArgs e)
        {
            if (Game1.hasLoadedGame)
            {
                if (Game1.currentLocation.mapPath.Value == "Maps\\Winery" && Game1.currentLocation != null)
                {
                    layer = Game1.currentLocation.map.GetLayer("Buildings");
                    tilesheet = Game1.currentLocation.map.GetTileSheet("untitled tile sheet");
                    tileID = 1367;
                    for (int y = 6; y < 10; y++)
                    {
                        Game1.currentLocation.removeTile(16, y, "Buildings");
                        layer.Tiles[16, y] = new StaticTile(layer, tilesheet, BlendMode.Alpha, tileID);
                    }
                }
            }
        }
        /*
         * END ISSUE FIX
         * 
         */ 




        /*
         * TIME REMAINING ON HOVER INSIDE WINERY
         * 
         */
        public static float ToFloat(double value)
        {
            return (float)value;
        }

        public void GraphicsEvents_OnPostRenderEvent(object sender, EventArgs e)
        {
            if (Game1.hasLoadedGame)
            {                
                //monitor.Log($"" + Game1.currentLocation.Name);
                if (Game1.currentLocation.mapPath.Value == "Maps\\Winery" && Game1.currentLocation != null)
                {
                    var timeRemaining = Game1.spriteBatch;
                    ICursorPosition cursorPos = this.Helper.Input.GetCursorPosition();
                    foreach (var entry in Game1.currentLocation.objects)
                    {
                        if (Game1.currentLocation.Objects.TryGetValue(cursorPos.Tile, out SObject obj))
                        {
                            if (obj.heldObject.Value != null)
                            {
                                Vector2 vector2;
                                Vector2 vector2_1;
                                string text1;
                                float num2;
                                float num3;
                                Vector2 tooltipOffset = this.TooltipOffset;

                                if (obj.Name.Equals("Keg"))
                                {
                                    text1 = Math.Round((obj.MinutesUntilReady * 0.6)/84, 1).ToString() + " minutes";
                                    vector2 = Game1.smallFont.MeasureString(text1);
                                    vector2_1 = new Vector2(ToFloat(vector2.X + 5.0 + 5.0), ToFloat(vector2.Y + 5.0));
                                    num2 = (float)Mouse.GetState().X / Game1.options.zoomLevel - tooltipOffset.X - vector2_1.X;
                                    num3 = (float)((double)Mouse.GetState().Y / (double)Game1.options.zoomLevel + (double)tooltipOffset.Y + 12.0);
                                    IClickableMenu.drawTextureBox(Game1.spriteBatch, Game1.menuTexture, this.TooltipSourceRect, (int)num2, (int)num3, (int)vector2_1.X + 27, (int)vector2_1.Y + 20, Color.White, 1f, true);
                                    Utility.drawTextWithShadow(Game1.spriteBatch, text1, Game1.smallFont, new Vector2((float)((double)num2 + (double)vector2_1.X) - (vector2.X - 4), (float)((double)num3 + 6.0 + 5.0)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
                                }

                                if (obj is Cask c)
                                {
                                    text1 = Math.Round(c.daysToMature.Value * 0.6, 1).ToString() + " days";
                                    vector2 = Game1.smallFont.MeasureString(text1);
                                    vector2_1 = new Vector2(ToFloat(vector2.X + 5.0 + 5.0), ToFloat(vector2.Y + 5.0));
                                    num2 = (float)Mouse.GetState().X / Game1.options.zoomLevel - tooltipOffset.X - vector2_1.X;
                                    num3 = (float)((double)Mouse.GetState().Y / (double)Game1.options.zoomLevel + (double)tooltipOffset.Y + 12.0);
                                    IClickableMenu.drawTextureBox(Game1.spriteBatch, Game1.menuTexture, this.TooltipSourceRect, (int)num2, (int)num3, (int)vector2_1.X + 27, (int)vector2_1.Y + 20, Color.White, 1f, true);
                                    Utility.drawTextWithShadow(Game1.spriteBatch, text1, Game1.smallFont, new Vector2((float)((double)num2 + (double)vector2_1.X) - (vector2.X - 4), (float)((double)num3 + 6.0 + 5.0)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
                                }
                            }
                        }
                    }
                }
            }
        }
        /*
         * END OF TIME REMAINING INSIDE WINERY
         * 
         */



        /*
         * ASSET LOADER
         * 
         */
        public bool CanLoad<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Buildings\\Winery"))
            {
                return true;
            }
            else if (asset.AssetNameEquals("Maps/Winery"))
            {
                return true;
            }
            return false;
        }

        public T Load<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Buildings\\Winery"))
            {
                return (T)(object)Winery_outdoors;
            }
            else if (asset.AssetNameEquals("Maps/Winery"))
            {
                return (T)(object)Winery_indoors;
            }
            return (T)(object)null;
        }
        /*
         * END ASSET LOADER
         * 
         */


        /*
         * PATCH METHOD FOR HARMONY
         * Has instance of Cask and reference result manipulation. Always returns false to suppress original method.
         */
        public static bool Patch_performObjectDropInAction(Cask __instance, ref bool __result, Item dropIn, bool probe, Farmer who)
        {
            if (dropIn != null && dropIn is SObject && (dropIn as SObject).bigCraftable.Value || __instance.heldObject.Value != null)
            {
                __result = false;
                //monitor.Log($"1, returning " + __result);
                return false;
            }

            if (!probe && (who == null || !(who.currentLocation is Cellar || who.currentLocation.mapPath.Value == "Maps\\Winery")))
            {
                Game1.showRedMessageUsingLoadString("Strings\\Objects:CaskNoCellar");
                __result = false;
                //monitor.Log($"2, returning " + __result);
                return false;
            }

            if (__instance.Quality >= 4)
            {
                __result = false;
                //monitor.Log($"3, returning " + __result);
                return false;
            }

            bool flag = false;
            float num = 1f;

            switch (dropIn.ParentSheetIndex)
            {
                case 303:
                    flag = true;
                    //monitor.Log($"303, flag is " + flag);
                    num = 1.66f;
                    break;

                case 346:
                    flag = true;
                    //monitor.Log($"346, flag is " + flag);
                    num = 2f;
                    break;

                case 348:
                    flag = true;
                    //monitor.Log($"348, flag is " + flag);
                    num = 1f;
                    break;

                case 424:
                    flag = true;
                    //monitor.Log($"424, flag is " + flag);
                    num = 4f;
                    break;

                case 426:
                    flag = true;
                    //monitor.Log($"426, flag is " + flag);
                    num = 4f;
                    break;
                case 459:
                    flag = true;
                    //monitor.Log($"459, flag is "+flag);
                    num = 2f;
                    break;
            }

            if (!flag)
            {
                __result = false;
                //monitor.Log($"4, returning "+__result);
                return false;
            }

            __instance.heldObject.Value = dropIn.getOne() as SObject;
            //monitor.Log($"Setting Cask's Held Object to "+__instance.heldObject.Value.DisplayName);

            if (!probe)
            {
                __instance.agingRate.Value = num;
                __instance.daysToMature.Value = 56f;
                __instance.MinutesUntilReady = 999999;

                if (__instance.heldObject.Value.Quality == 1)
                {
                    __instance.daysToMature.Value = 42f;
                }
                else if (__instance.heldObject.Value.Quality == 2)
                {
                    __instance.daysToMature.Value = 28f;
                }
                else if (__instance.heldObject.Value.Quality == 4)
                {
                    __instance.daysToMature.Value = 0.0f;
                    __instance.MinutesUntilReady = 1;
                }

                who.currentLocation.playSound("Ship");
                who.currentLocation.playSound("bubbles");
                who.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Rectangle(256, 1856, 64, 128), 80f, 6, 999999, __instance.TileLocation * 64f + new Vector2(0.0f, (float)sbyte.MinValue), false, false, (float)(((double)__instance.TileLocation.Y + 1.0) * 64.0 / 10000.0 + 9.99999974737875E-05), 0.0f, Color.Yellow * 0.75f, 1f, 0.0f, 0.0f, 0.0f, false)
                {
                    alphaFade = 0.005f
                });
            }
            __result = true;
            //monitor.Log($"5, returning " + __result);
            return false;
        }
        /*
         * END OF PATCH METHOD FOR HARMONY
         * 
         */
    }
}