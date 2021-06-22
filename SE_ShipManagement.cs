// Ship Auto-Management -------------------------

/*  ToDO
    
    - PowerSave on LandingGearLock
        - conveyor / 0.001 kW
        - ejector / 0.010 kW
        - assember / 1.0 kW
        - refinery / 1.000 kW
        - oxytank / 1.00 kW
        - oxygen / 1.00 kW
        - gyro / 2.00 kW
        - ore detect / 2.0 kW
        - beacon 0 - 20 kW
        - antenna 0 - 200 kW

*/


/*  Help

    StatusPanel needs to be named [StatusLeft] or [StatusRight]

*/

// Settings -------------------------

    int ScreenIDData = 1;
    int ScreenIDIntegrity = 2;
    int EnergyMaxPerc = 80;
    List<string> blockdb = new List<string>();

// Main -------------------------

public Program()
    {   Runtime.UpdateFrequency = UpdateFrequency.Update10;
        Alarm(false);
    }

public void Save()
    {
    }

public void Main()
    {   Echo(Indicator() + "Solar:" + useSolar.ToString() + " - Storage:" + useStorage.ToString() + " - OH2:" + OxygenControl.ToString() + "\n");
        initLists();
        
        // Scan
        ScanStationBlocks();

        // Checks
        CheckCockpit(); // playersit & oxygen
        CheckShipIntegrity();
        CheckRefineries();
        CheckDrillsFull();
        CheckStone();
        CheckEnergy();
        
        // Controlling
        if (useSolar) Control_Solar();
        Control_Storage();
        setReactors();
        setEnergy();
        
        // Show Information
        CockpitScreen(-1,""); // update all

        AutoDisableConnectorThrowOut();
    }

// Lists -------------------------

    List<IMyTerminalBlock> controllers = new List<IMyTerminalBlock>();
    List<IMyLandingGear> landinggears = new List<IMyLandingGear>();
    List<IMyTerminalBlock> o2gens = new List<IMyTerminalBlock>();
    List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
    List<IMyReactor> reactors = new List<IMyReactor>();
    List<IMyRefinery> refineries = new List<IMyRefinery>();
    List<IMyShipDrill> drills = new List<IMyShipDrill>();
    List<IMyGasTank> gastanks = new List<IMyGasTank>();
public void initLists()
    {   GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
        GridTerminalSystem.GetBlocksOfType<IMyLandingGear>(landinggears);
        GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
        GridTerminalSystem.GetBlocksOfType<IMyReactor>(reactors);    
        GridTerminalSystem.GetBlocksOfType<IMyRefinery>(refineries);
        GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(drills);
        GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(o2gens);
        GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gastanks);
    }

// Scan Blocks -------------------------

    int                                      RescanCount = 0;
    IMyFunctionalBlock          oh2gen = null;
    IMyGasTank                      o2tank = null;
    IMyGasTank                      h2tank = null;
    List<IMyTextPanel>          lcds = new List<IMyTextPanel>();
    List<IMyTerminalBlock>  Inventories = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock>  StorageOres = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock>  StorageIngots = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock>  StorageComponents = new List<IMyTerminalBlock>();
    List<IMyRefinery>            Refineries = new List<IMyRefinery>();
    List<IMyAssembler>        Assemblers = new List<IMyAssembler>();
public void ScanStationBlocks()
    {   // Storages
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(Inventories);
        GridTerminalSystem.SearchBlocksOfName("[Ores]", StorageOres);
        GridTerminalSystem.SearchBlocksOfName("[Ingots]", StorageIngots);
        GridTerminalSystem.SearchBlocksOfName("[Components]", StorageComponents);
        GridTerminalSystem.GetBlocksOfType<IMyRefinery>(Refineries);
        GridTerminalSystem.GetBlocksOfType<IMyAssembler>(Assemblers);
        
        int iCount = Inventories.Count;
        // cleanup inventories to dont be "sorted"
            bool iRemove = false;
            string iData; 
        for (int i = Inventories.Count-1; i >= 0; i--)
        {   iData = Inventories[i].DetailedInfo;
            if (!Inventories[i].HasInventory) { iRemove = true; }
            if (iData.Contains("Refinery")) { iRemove = true; }
            if (iData.Contains("Assembler")) { iRemove = true; }
            if (iRemove) { Inventories.RemoveAt(i); iRemove = false;}
        }
        // Echo("StationScanCleanup: " + Inventories.Count.ToString() + "/" + iCount.ToString());
        
        // LCD
          GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(lcds);
          // GridTerminalSystem.GetBlockWithName("LCD_001") as IMyTextPanel;

        // Oxygen/Hydrogen Generator
        List<IMyTerminalBlock> gasGenerators = new List<IMyTerminalBlock>();
          GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(gasGenerators);
        var oh2gen = findblock(gasGenerators,"[OH2Generator]");
        if (oh2gen == null && gasGenerators.Count > 0) oh2gen = (IMyFunctionalBlock)gasGenerators[0]; // if it is not named

        // Oxygen/Hydrogen Tanks
        List<IMyGasTank> gasTanks = new List<IMyGasTank>();
          GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gasTanks);
        foreach (var gt in gasTanks)
        {   if (gt.CustomName.Contains("[O2Tank]")) o2tank = gt;
            if (gt.CustomName.Contains("[H2Tank]")) h2tank = gt;
        }

        // Solarsystem
        
        RescanCount = 2;
    }

// Routines Checks -------------------------

    bool PlayerSit = false;
    bool OxygenControl = true;
    string sOxygen = "";
public void CheckCockpit()
    {   PlayerSit = true;
        bool o2genNeed = false;
        var o2gen = o2gens[0] as IMyGasGenerator;
        foreach (var controller in controllers)
        {   if (controller.CustomName == "Cockpit")
            {   var ctrl = controller as IMyShipController;
                PlayerSit = ctrl.IsUnderControl;
                if (OxygenControl)
                {   // Cockpit
                    var cockpit = (IMyCockpit)ctrl;
                    var cockpitOxygen = cockpit.OxygenFilledRatio *100;
                    if (cockpitOxygen < 1)
                        o2genNeed = true;
                    sOxygen = "Oxygen: " + Math.Floor(cockpitOxygen) + "%";
                    // Tanks
                    foreach(var gastank in gastanks)
                    {   if (gastank.FilledRatio*100 < 95)
                            o2genNeed = true;
                    }
                }
            }
        }
        o2gen.Enabled = o2genNeed;
    }

    double stoneAmount = 0.0f;
    double oreAmount = 0.0f;
public void CheckStone()
    {   stoneAmount = 0.0f;
        var sBlocks = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocks(sBlocks);
        foreach(var block in sBlocks)
        {   if (block.HasInventory)
            {   IMyInventory inv = block.GetInventory(0);
                var itemList = new List<MyInventoryItem>();
                inv.GetItems(itemList,null);
                foreach(var item in itemList)
                {   string iType = item.Type.ToString();
                    VRage.MyFixedPoint iAmount = item.Amount;
                    if (iType.Contains("Ore/Stone"))
                    {   // Echo(block.CustomName + " has " + iAmount.ToString() + " Stone.");
                        stoneAmount += (float)iAmount;
                    }
                    if (iType.Contains("Ore/"))
                    {   // Echo(block.CustomName + " has " + iAmount.ToString() + " Ore.");
                        oreAmount += (float)iAmount;
                    }
                }
            }
        }
        oreAmount = Math.Floor((oreAmount - stoneAmount)/100)/10;
        stoneAmount = Math.Floor(stoneAmount/100)/10;
    }

    string sIntegrity = "";
public void CheckShipIntegrity()
    {   var sBlocks = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocks(sBlocks);
        
        var iCount = 0;
        sIntegrity = "";
        foreach (var block in sBlocks)
        {   var iMax = block.CubeGrid.GetCubeBlock(block.Position).MaxIntegrity;
            var iBld = block.CubeGrid.GetCubeBlock(block.Position).BuildIntegrity *100 / iMax;
            if (iBld != 100)
            {   sIntegrity += block.CustomName + "  \nintegrity at " + iBld.ToString() + " %.\n";
                iCount++;
            }
        }
        if (sIntegrity != "") Echo(sIntegrity);

        if (iCount >= 1) { Alarm(true); }
        else { Alarm(false); sIntegrity = ""; }

        sScreenTexts[ScreenIDIntegrity] = sIntegrity;
        
    }

    bool RefineriesWorking = false;
public void CheckRefineries()
    {   RefineriesWorking = false;
        foreach (var refinery in refineries)
        {   if (refinery.IsProducing) RefineriesWorking = true;
            if (stoneAmount > 0)
            {   var items = new List<MyInventoryItem>();
                refinery.GetInventory(0).GetItems(items, null);
                if (items.Count > 0)
                {   string firstItem = items[0].Type.ToString().Substring(16);
                    if (firstItem != "Ore/Stone")
                    {   Echo("RefFirstItem: " + firstItem);
                    }
                }
            }
        }
    }

    string sDrills = "";
public void CheckDrillsFull()
    {   sDrills = "";
        double maxFill = 6750.0;
        double drillFillPerc = 0;
        double highestPerc = 0;
        foreach (var drill in drills)
        {   var drillFill = Math.Floor((double)drill.GetInventory(0).CurrentVolume*1000);
            drillFillPerc = Math.Floor(drillFill / maxFill *100);
            if (drillFillPerc > 0)
            {   Echo(drill.CustomName + ": " + drillFill.ToString() + " kg");
                if (highestPerc < drillFillPerc)
                {   highestPerc = drillFillPerc;
                }
            }
            if (drillFillPerc > 50) sLeftColor = Color.Orange;
            if (drillFillPerc > 80) sLeftColor = Color.Red;
        }
        if (highestPerc > 95)
            Alarm(true);
        if (highestPerc > 30)
        {   sDrills += "Drill Max: " + highestPerc.ToString() + "%";
            sScreenTexts[ScreenIDData] = sDrills;
        }
    }

    bool useStorage = true;
public void Control_Storage()
    {   
        // Declare
        IMyInventory dstInv = null;
        string nameFrom = "";
        string itemType = "";
        useStorage = false;

        // sort Ores
        if (StorageOres.Count > 0)
        {   useStorage = true;
            dstInv = GetFreeCargoContainer(StorageOres,"[Ores]");
            nameFrom = "Cargo"; itemType = "Ore";
            foreach (var Inventory in Inventories)
            {   if (Inventory.CustomName != "[Ores]" && Inventory.HasInventory)
                {   IMyInventory srcInv = Inventory.GetInventory(0);
                    var itemList = new List<MyInventoryItem>();
                    srcInv.GetItems(itemList,null);
                    InvTransferItems(Inventory, nameFrom, srcInv, itemList, itemType, dstInv);
                }
            }
        } else Echo("Storage [Ores] not found...");
        
        // sort Components
        if (StorageComponents.Count > 0)
        {   useStorage = true;
            dstInv = GetFreeCargoContainer(StorageComponents,"[Components]");
            nameFrom = "Cargo"; itemType = "Component";
            foreach (var Inventory in Inventories)
            {   if (Inventory.CustomName != "[Components]" && Inventory.HasInventory)
                {   IMyInventory srcInv = Inventory.GetInventory(0);
                    var itemList = new List<MyInventoryItem>();
                    srcInv.GetItems(itemList,null);
                    InvTransferItems(Inventory, nameFrom, srcInv, itemList, itemType, dstInv);
                }
            }
        } else Echo("Storage [Components] not found...");

        // sort Ingots
        if (StorageIngots.Count > 0)
        {   useStorage = true;
            dstInv = GetFreeCargoContainer(StorageIngots,"[Ingots]");
            nameFrom = "Cargo"; itemType = "Ingot";
            foreach (var Inventory in Inventories)
            {   if (Inventory.CustomName != "[Ingots]" && Inventory.HasInventory)
                {   IMyInventory srcInv = Inventory.GetInventory(0);
                    var itemList = new List<MyInventoryItem>();
                    srcInv.GetItems(itemList,null);
                    InvTransferItems(Inventory, nameFrom, srcInv, itemList, itemType, dstInv);
                }
            }
        } else Echo("Storage [Ingots] not found...");

        // empty Refineries
        if (StorageIngots.Count > 0)
        {   dstInv = StorageIngots[0].GetInventory(0);
            nameFrom = "Ref."; itemType = "Ingot";
            foreach (var Refinery in Refineries)
            {   IMyInventory srcInv = Refinery.GetInventory(1);
                var itemList = new List<MyInventoryItem>();
                srcInv.GetItems(itemList,null);
                InvTransferItems(Refinery, nameFrom, srcInv, itemList, itemType, dstInv);
            }
        }

        // empty Assemblers
        if (StorageIngots.Count > 0)
        {   dstInv = StorageIngots[0].GetInventory(0);
            nameFrom = "Asb."; itemType = "Ingot";
            foreach (var Assembler in Assemblers)
            {   if (Assembler.IsProducing == true) continue;
                IMyInventory srcInv = Assembler.GetInventory(0);
                var itemList = new List<MyInventoryItem>();
                srcInv.GetItems(itemList,null);
                InvTransferItems(Assembler, nameFrom, srcInv, itemList, itemType, dstInv);
            }
        }    
    }

    bool useSolar = true;
public void Control_Solar()
    {   var rotors = new List<IMyTerminalBlock>();
           GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors);
        var rotor = findblock(rotors,"[SolarH]");
        if (rotor != null)
        {   var solarpanels = new List<IMyTerminalBlock>();
              GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(solarpanels);
            var solarpanel = findblock(solarpanels,"[Solar]");
            if (solarpanel != null)
            {   string output = solarpanel.DetailedInfo;
                string[] powerLine = output.Split('\n')[1].Split(' ');
                double powerOutput = double.Parse(powerLine[2]);

                if (powerOutput <= 40) { rotor.SetValueFloat("Velocity", -2.0f); }
                else if (powerOutput <= 80) { rotor.SetValueFloat("Velocity", -0.5f); }
                else if (powerOutput <= 120) { rotor.SetValueFloat("Velocity", -0.1f); }
                else { rotor.SetValueFloat("Velocity", 0f); }
                
                Echo("Solar-Power: " + powerOutput);
            }
            else
            {   Echo("cant find Solarpanel with [Solar]-tag!"); useSolar = false; }
        }
        else
        {   Echo("cant find Rotor with [SolarH]-tag!"); useSolar = false; }
    }

    float sEnergyPerc = 0;
public void CheckEnergy()
    {   float sFil = 0;
        foreach (var batterie in batteries)
        {   var bMax = batterie.MaxStoredPower;
            var bCur = batterie.CurrentStoredPower;
            sFil += bCur / bMax *100;
            // recharge switcher
            if (sEnergyRecharge) batterie.ChargeMode = ChargeMode.Recharge;
            else batterie.ChargeMode = ChargeMode.Auto;
        }
        sEnergyPerc = sFil / batteries.Count;
        sEnergyPerc = (float)Math.Round((double)sEnergyPerc*100)/100;
    }

// Function Control -------------------------

    bool sEnergyRecharge = false;
public void setEnergy()
    {   sEnergyRecharge = false;
        int ERCount = 0;

        // Check PlayerSit
        if (!PlayerSit) { ERCount++; }

        // Check Refineries
        if (!RefineriesWorking) { ERCount++;}

        // Check LandingGears - Prevents GravityAreas from crashing ships down
        foreach(var landinggear in landinggears)
        {   if (landinggear.IsLocked == true)
            {   ERCount++; break; }
         }
    
        if (ERCount >= 3)
        {   sEnergyRecharge = true; }
        // Echo("ERCount: " + ERCount);
    }

    string sReactors = "";
public void setReactors()
    {   bool rSwitch = false;
        if (sEnergyPerc < EnergyMaxPerc) rSwitch = true;
        sReactors = "";
        if (reactors != null)
        {   foreach (var reactor in reactors) { reactor.Enabled = rSwitch; }
            sReactors += "Reactors: " + rSwitch.ToString();
            sScreenTexts[ScreenIDData] = sReactors;
        }
    }

    bool throwout = false;
public void AutoDisableConnectorThrowOut()
    {   var connectors = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors);
        foreach (var connector in connectors)
        {   if (connector.CustomName.Contains("Connector"))
            {   throwout = false;
                if (connector.ThrowOut && connector.IsWorking)
                {  throwout = true;
                   if (connector.GetInventory(0).ItemCount == 0) connector.ThrowOut = false;
                }
            }
        }
    }

    bool AlarmOn = false;
public void Alarm(bool aSwitch)
    {   var sound = GridTerminalSystem.GetBlockWithName("Sound Block") as IMySoundBlock;
        var aLight = GridTerminalSystem.GetBlockWithName("Light [Alarm]") as IMyInteriorLight;
        if (sound != null)
        {   if (aSwitch)
            {   if (!AlarmOn) sound.Play();
                AlarmOn = true;
                if (aLight != null) aLight.Enabled = true;
                foreach(var drill in drills)
                {   drill.Enabled = false;
                }
            }
            else
            {   AlarmOn = false;
                sound.Stop();
                if (aLight != null) aLight.Enabled = false;
            }
        }
        else
            Echo("SoundBlock not found...");
    }

// Show Information -------------------------

    int indicator_index = 0;
    string indicator_symbols = "|/-\\";
private string Indicator()
    {   indicator_index++; if (indicator_index > indicator_symbols.Length-1) indicator_index = 0;
        return indicator_symbols.Substring(indicator_index,1);
    }

    // screens = -1 update all / 1 left / 0 mid / 2 right / 3 keyboard
    Color sLeftColor;
    Color sRightColor;
    string[] sScreenTexts = new string[3];
public void CockpitScreen(int sNum, string sText)
    {   // collect texts // LEFT
        string sLeft = "";
        sLeft += sReactors + "\n";
        sLeft += "Energy: " + sEnergyPerc + "%\n";
        sLeft += sOxygen + "\n";
        sLeft += sDrills + "\n";
        if (oreAmount > 1) sLeft += "Ores: " + oreAmount + "k\n";
        if (stoneAmount > 1) sLeft += "Stone: " + stoneAmount + "k\n";
        if (throwout) sLeft += "Connector: ThrowOut\n";
        sScreenTexts[ScreenIDData] = sLeft;

        // is there a status-panel
        List<IMyTextPanel> textPanels = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(textPanels);
        if (textPanels != null)
        {   foreach(var textPanel in textPanels)
            {   if (textPanel.CustomName.Contains("[StatusLeft]"))
                {   textPanel.FontSize = 2;
                    textPanel.FontColor = sLeftColor;
                    textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
                    textPanel.WriteText(sLeft);
                }
            }
        }

        // get cockpit
        var cockpit = GridTerminalSystem.GetBlockWithName("Cockpit") as IMyCockpit;
        Echo(cockpit.DetailedInfo);
        IMyTextSurfaceProvider sScreen = cockpit;
        // replace text in array
        if (sNum != -1) sScreenTexts[sNum] = sText;
        // show all texts on screens
        for (int i = 0; i < 3; i++) 
        {   if (sScreenTexts[i] != null) 
            {   if (i == 1)
                {   sScreen.GetSurface(i).FontSize = 2;
                }
                if (i == 2)
                {   sScreen.GetSurface(i).FontSize = 1.5f;
                }
                sScreen.GetSurface(i).WriteText(sScreenTexts[i]); 
            }
        }

        sLeftColor = Color.White; // for reset
    }

// Routines ----------------------------------------

public IMyTerminalBlock findblock(List<IMyTerminalBlock> bList, string bSearch)
    {   if (bList.Count > 0) { foreach (var data in bList) { if (data.CustomName.Contains(bSearch) == true) return data; } }
        return null;
    }
    
    // nameFrom("Ref."), srcInv, itemList, itemType("Ingot"), dstInv)
public void InvTransferItems (IMyTerminalBlock bSrc, string nameFrom, IMyInventory srcInv, List<MyInventoryItem>itemList, string itemType, IMyInventory dstInv)
    {   for (int i = 0; i < itemList.Count; i++)
        {   string iType = itemList[i].Type.ToString();
            VRage.MyFixedPoint iAmount = itemList[i].Amount;
            double iAmountFloor = Math.Floor((double)iAmount.RawValue / 1000)/1000;
            if (iType.Contains("_" + itemType))
            {   srcInv.TransferItemTo(dstInv, i, null, true, iAmount); // (ReceiverCargo, SrcItemIndex, DstItemIndex, asStack, Amount)
            
                if (nameFrom == "Ref.")
                {   string iTypeName = iType.Split('/')[1];
                    string sMove = bSrc.CustomName + " (" + iTypeName + ") = " + iAmountFloor.ToString();
                    Echo(sMove); blockdb.Add(sMove);
                }
            }
        }
    }
    
public IMyInventory GetFreeCargoContainer(List<IMyTerminalBlock> cargos, string filter)
    {   for (int i = 0; i < cargos.Count; i++)
        {   IMyInventory cInv = cargos[i].GetInventory(0);
            double cMax = (double) cInv.MaxVolume;
            double cCur = (double) cInv.CurrentVolume;
            // Echo(cargos[i].CustomName + " - C: " + cCur.ToString() + " M:" + cMax.ToString());
            if (cCur < cMax*0.95) // 95% fill
            {   // Echo(cargos[i].CustomName + " > " + i);
                return cargos[i].GetInventory(0);
            }
        }
        return cargos[0].GetInventory(0); // return first if nothing is found
    }

/*
    reactors[i].GetActionWithName("OnOff_On").Apply(reactors[i]);
*/
