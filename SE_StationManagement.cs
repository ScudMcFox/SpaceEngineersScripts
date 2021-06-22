// Station Management

/* TODO

    code
    - shorten code and functions

    components
    - minimum autoproduction

    textPanel
    Storage Ore/Ingots:
    - Uranium - 1100kg > 5,7kg

    cryo stores
    - ammo and physicalgun
*/

/* HELP

    Cargo Control:
    - OresContainer: needs to have [Ores] in his name
    - IngotsContainer: needs to have [Ingots] in his name
    - ComponentsContainer: needs to have [Components] in his name

    Solar Control:
    - 1 Main-SolarPanel needs to have [Solar] in his name. best choice is one with the most possible sunlight.
    - 1 Rotor has to be named with [SolarH], the horizontal rotation.
    - 1 Rotor has to be named with [SolarV], the vertical rotation.

    Oxygen/H2 Control:
    - Main-OH2-Generator need [OH2Generator] in his name.
    - Main-O2-Tank need [O2Tank] in his name.
    - Main-H2-Tank need [H2Tank] in his name.

    LCD:
    - [Stats]
    - [ListOres]
    - [ListIngots]
    - [ListComponents]

*/

// Settings ----------------------------------------

    double oh2fillLevel = 80; // percent
    float  rotortorque  = 200000;
    
// Variables ----------------------------------------

    List<string> lcddb   = new List<string>();
    List<string> resdb   = new List<string>(); // all resources of all inventorys as SUM
    List<string> invdb   = new List<string>(); // all ressources of all inventories as LOCATION/AMOUNT
    List<string> soresdb = new List<string>(); // all ores of [Ores] containers as LOCATION/AMOUNT

// Main ----------------------------------------

public Program()
    {   // Runtime.UpdateFrequency = UpdateFrequency.Update100;
        Echo("Booting...");
    }

public void Save()
    {
    }

public void Main (string argument, UpdateType updateSource)
    {   // Init
        lcddb.Clear();

        RescanCount--;
        if (RescanCount < 1)
        {   ScanStationBlocks();
            ScanRessources();
            Control_H2Generator();
            Control_Refineries();
        }

        // ShowSettings
        Echo(Indicator() + " Solar:" + useSolar.ToString() + " - Storage:" + useStorage + " - OH2:" + useOH2 );
        if (useOH2)
        {   Echo("OH2 Generator: " + oh2switch.ToString());
            Echo(oh2stats);
        }

        // Functions
        Control_Solar();
        Echo("");
        Control_Energy();
        Control_Storage();
    
        // Show
        if (useLCD) Show();
   }

// Database ----------------------------------------

    char sep = '|';

public class DB // for later use
    {   public string Var { get; set; }
        public string Val { get; set; }
    }

public void invdb_add(IMyTerminalBlock iSrc, int iIndex, string iType, string iName, double iAmount) // id,cname,index,type,name,amont
    {   string addline = iSrc.EntityId.ToString() + sep + iSrc.CustomName + sep + iIndex + sep + iType + sep + iName + sep + iAmount;
        invdb.Add(addline);
        if (iSrc.CustomName == "[Ores]") soresdb.Add(addline);
    }

public string[] invdb_get(string iType, string iName)
    {   foreach (var line in invdb)
        {   string[] data = line.Split(sep);
            if (data[3] == iType && data[4] == iName) return data;
        }
        return null;
    }

public void resdb_add (string rType, string rName, double rAmount)
    {   bool found = false;
        for (int i = 0; i < resdb.Count; i++)
        {   string[] kData = resdb[i].Split(sep);
            if (kData[0] == rType && kData[1] == rName)
            {   double nAmount = Convert.ToDouble(kData[2]) + rAmount;
                resdb[i] = rType + sep + rName + sep + nAmount;
                found = true;
            }
        }
        if (!found) resdb.Add(rType + sep + rName + sep + rAmount);
    }
    
public double resdb_get (string gType, string gName)
    {   foreach (var line in resdb)
        {   var rData = line.Split(sep);
            if (rData[0] == gType && rData[1] == gName) return Convert.ToDouble(rData[2]);
        }
        return 0.0f;
    }

public List<string> ListSortString(List<string> hlist, int sindex, string sdir) // list,pos,normal/reverse
    {   // create value array
        for (int sli = 0; sli < hlist.Count; sli++)
        {   string[] sdata = hlist[sli].Split(sep);
            hlist[sli] = sdata[sindex] + sep + hlist[sli];
        }
        hlist.Sort(); if (sdir == "reverse") hlist.Reverse();
        // remove indexer
        for (int sli = 0; sli < hlist.Count; sli++)
        {   string[] sdata = hlist[sli].Split(sep);
            hlist[sli] = hlist[sli].Substring(sdata[0].Length+1);
        }
        return hlist;
    }

public List<string> ListSortDouble(List<string> hlist, int sindex, string sdir) // list,pos,normal/reverse
    {   // create value array
        List<double> hval = new List<double>();
        for (int sli = 0; sli < hlist.Count; sli++)
        {   string[] sdata = hlist[sli].Split(sep);
            hval.Add(Convert.ToDouble(sdata[sindex]));
        }
        hval.Sort(); if (sdir == "reverse") hval.Reverse();
        // rearange list
        List<string> nhlist = new List<string>();
        for (int sli = 0; sli < hval.Count; sli++)
        {   string needle = hval[sli].ToString();
            // find add and remove
            for (int slx = 0; slx < hlist.Count; slx++)
            {   string[] sdata = hlist[slx].Split(sep);
                if (sdata[sindex] == needle)
                {   nhlist.Add(hlist[slx]);
                    hlist.RemoveAt(slx);
                    break;
                }
            }
        }
        return nhlist;
    }

// Scan Station ----------------------------------------

    int                                      RescanCount = 0;
    IMyFunctionalBlock      oh2gen = null;
    IMyGasTank              o2tank = null;
    IMyGasTank              h2tank = null;
    List<IMyTextPanel>      lcds = new List<IMyTextPanel>();
    List<IMyTerminalBlock>  Inventories = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock>  StorageOres = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock>  StorageIngots = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock>  StorageComponents = new List<IMyTerminalBlock>();
    List<IMyRefinery>       Refineries = new List<IMyRefinery>();
    List<IMyAssembler>      Assemblers = new List<IMyAssembler>();
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
            if (iData.Contains("Generator")) { iRemove = true; }
            if (iData.Contains("Reactor")) { iRemove = true; }
            if (iRemove) { Inventories.RemoveAt(i); iRemove = false;}
        }
        // Echo("StationScanCleanup: " + Inventories.Count.ToString() + "/" + iCount.ToString());
        
        // LCD
          GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(lcds);
          // GridTerminalSystem.GetBlockWithName("LCD_001") as IMyTextPanel;

        // Oxygen/Hydrogen Generator
        List<IMyTerminalBlock> gasGenerators = new List<IMyTerminalBlock>();
          GridTerminalSystem.GetBlocksOfType<IMyGasGenerator>(gasGenerators);
        oh2gen = findblock(gasGenerators,"[OH2Generator]") as IMyFunctionalBlock;
        if (oh2gen == null && gasGenerators.Count > 0) { oh2gen = (IMyFunctionalBlock)gasGenerators[0]; } // if it is not named

        // Oxygen/Hydrogen Tanks
        List<IMyGasTank> gasTanks = new List<IMyGasTank>();
          GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gasTanks);
        foreach (var gt in gasTanks)
        {   if (gt.CustomName.Contains("[O2Tank]")) o2tank = gt;
            if (gt.CustomName.Contains("[H2Tank]")) h2tank = gt;
        }

        // Solarsystem
        
        RescanCount = 3;
    }

public void ScanRessources()
    {   resdb = new List<string>();
        invdb = new List<string>();
        soresdb = new List<string>();

        List<IMyTerminalBlock>  rInv = new List<IMyTerminalBlock>();
          GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(rInv);
        IMyInventory srcInv;
        foreach (var Inventory in rInv)
        {   if (Inventory.HasInventory)
            {   for (int i = 0; i < Inventory.InventoryCount; i++)
                {   srcInv = Inventory.GetInventory(i);
                    var itemList = new List<MyInventoryItem>();
                    srcInv.GetItems(itemList,null);
                    for (int j = 0; j < itemList.Count; j++)
                    {   string[] iData = itemList[j].Type.ToString().Substring(16).Split('/');
                        double iAmount = (double)itemList[j].Amount.RawValue / 1000000;
                        resdb_add(iData[0], iData[1], iAmount);
                        invdb_add(Inventory,j,iData[0],iData[1],iAmount);
                    }
                    // if (i > 0) Echo(Inventory.CustomName);
                }
            }
        }
        resdb = ListSortDouble(resdb,2,"reverse");
        invdb = ListSortDouble(invdb,5,"reverse");
    }

// Cargo System ----------------------------------------
    
public IMyInventory GetFreeCargoContainer (List<IMyTerminalBlock> cargos, string filter)
    {   
        IMyInventory cInv = null;
        for (int i = 0; i < cargos.Count; i++)
        {   if (cargos[i].HasInventory)
            {   cInv = cargos[i].GetInventory(0);
                double cMax = (double) cInv.MaxVolume;
                double cCur = (double) cInv.CurrentVolume;
                // Echo(cargos[i].CustomName + " - C: " + cCur.ToString() + " M:" + cMax.ToString());
                if (cCur < cMax*0.95)
                {   // Echo(cargos[i].CustomName + " > " + i);
                    return cargos[i].GetInventory(0);
                }
            }
        }
        return cInv; // return first if nothing is found
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
        if (Refineries.Count > 0)
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
        if (Assemblers.Count > 0)
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

// Refinery Priority Processing ----------------------------------------

    string RefPrioInfo = "";
    string[] prioores = null;
    string[] OrePrioList = {"Scrap","Stone","Iron","Cobalt","Magnesium"};
public void Control_Refineries()
    {   
        // cycle refinieries
        foreach (var refinery in Refineries)
        {   // find prio-ores to array
            prioores = null;
            foreach (var orePrio in OrePrioList)
            {   foreach (var sores in soresdb)
                {   string[] data = sores.Split(sep);
                    // Echo("oCP: " + orePrio + " to " + data[4]);
                    if (data[4].Contains(orePrio)) { prioores = data; break; } 
                }
                if (prioores != null) break;
            }

            // there is something, process it
            if (prioores != null)
            {   RefPrioInfo = "OrePrio: " + prioores[4] + " > " + Math.Floor(Convert.ToDouble(prioores[5])/100)/10 + " kg";
                IMyTerminalBlock srcInv = GridTerminalSystem.GetBlockWithId(Convert.ToInt64(prioores[0]));
                int srcIndex = Convert.ToInt16(prioores[2]);
                // MyFixedPoint srcAmount = (MyFixedPoint)Convert.ToDouble(prioores[5]);
                double srcAmount = 5000.0f;
                string oreToProcess = prioores[4];
                Control_Refineries_Supervise(refinery, srcInv, srcIndex, (MyFixedPoint)srcAmount, oreToProcess);
                ScanRessources();
            }
            else
            {   // Echo(refinery.CustomName + ": No Prio Ores ...");
                refinery.UseConveyorSystem = true;
            }
        }
    }

public void Control_Refineries_Supervise(IMyTerminalBlock RefAuto, IMyTerminalBlock srcInv, int srcIndex, MyFixedPoint srcAmount, string oreToProcess)
    {   
        // get refinery inventory
        IMyInventory RefAutoInv = RefAuto.GetInventory(0); // 0= ores 1= ingots
        
        // check if refinery is working on this ore if not disable conveyor and move items away
        var itemList = new List<MyInventoryItem>();
        RefAutoInv.GetItems(itemList,null);
        string[] oreProcessingData;
        if (itemList.Count > 0)
        {   oreProcessingData = itemList[0].Type.ToString().Substring(16).Split('/');
            bool isPrioWorking = false;
            // check if ref is working on prio.ore
            // first ore
            if (oreProcessingData[1] == prioores[4]) isPrioWorking = true;
            /* random prioore
            foreach (var prioore in OrePrioList)
            { if (oreProcessingData[1] == prioore) isPrioWorking = true; }
            */
            
            if (isPrioWorking) return;
            else
            {   // empty refinery
                var RefAutoR = RefAuto as IMyRefinery;
                RefAutoR.UseConveyorSystem = false;
                var dstInv = GetFreeCargoContainer(StorageOres,"[Ores]");
                InvTransferItems(RefAuto, "Ref.", RefAutoInv, itemList, "Ore", dstInv);
                // Echo(RefAuto.CustomName + " emptying.");
           }
        }

        // fill ref with the prio.ore
        bool moved = srcInv.GetInventory(0).TransferItemTo(RefAutoInv,srcIndex,null,true,srcAmount);
        Echo(RefAuto.CustomName + " filled with: " + oreToProcess + " > " + moved.ToString());
    }


// Station Control ----------------------------------------

    bool useSolar = true;
public void Control_Solar()
    {   var rotors = new List<IMyTerminalBlock>();
           GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors);
        var rotor = findblock(rotors,"[SolarH]");

        rotor.SetValueFloat("Torque",rotortorque);
        rotor.SetValueFloat("BrakingTorque", rotortorque*0.75f);

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
                
                lcddb.Add("Solar-Power: " + powerOutput + " kW\n");
                Echo("Solar-Power: " + powerOutput);
            }
            else
                Echo("cant find Solarpanel with [Solar]-tag!");
        }
        else
        {   Echo("cant find Rotor with [SolarH]-tag!");
            useSolar = false;
        }
    }

public void Control_Energy()
    {   List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
          GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(batteries);
        // set new ones to recharge
        foreach(var battery in batteries)
        {   if (battery.CustomName != "Battery") { battery.ChargeMode = ChargeMode.Discharge; }
            else battery.ChargeMode = ChargeMode.Auto;
        }
    }

    bool useOH2 = true;
    bool oh2switch = false;
    double o2tankFill = 0;
    double h2tankFill = 0;
    string oh2stats = "";
public void Control_H2Generator()
    {   oh2switch = false;
        oh2stats = "";

        if (oh2gen == null) { useOH2 = false; return; }
        if (o2tank == null && h2tank == null) { useOH2 = false; return; }
        useOH2 = true;

        // O2 Check
        if (o2tank != null)
        {   o2tankFill = Math.Floor(o2tank.FilledRatio *100);
            lcddb.Add("O2 Tank: " + o2tankFill.ToString() + "%");
            if (o2tankFill < oh2fillLevel) oh2switch = true;
            oh2stats = "Tank - Oxygen: " + o2tankFill.ToString() + "%";
        }
        
        // H2 Check
        if (h2tank != null)
        {   h2tankFill = Math.Floor(h2tank.FilledRatio *100);
            lcddb.Add("H2 Tank: " + h2tankFill.ToString() + "%");
            if (h2tankFill < oh2fillLevel) oh2switch = true;
            oh2stats += " - Hydrogen: " + h2tankFill.ToString() + "%";
        }

        oh2gen.Enabled = oh2switch;
    }

// Show ----------------------------------------

    int indicator_index = 0;
    string indicator_symbols = "|/-\\";
private string Indicator()
    {   indicator_index++; if (indicator_index > indicator_symbols.Length-1) indicator_index = 0;
        return indicator_symbols.Substring(indicator_index,1);
    }

    bool useLCD = true;
    string lcd_text = "Space Station";
public void Show()
    {   if (lcds.Count < 1) { useLCD = false; return; }
        foreach (var lcd in lcds)
        {   lcd.FontColor = Color.Gray;
            if (lcd.CustomName.Contains("[Stats]"))
            {   string lcd_stats = "";
                foreach (var k in lcddb) { lcd_stats += k + "\n"; }
                lcd.WriteText(lcd_text + "\n\n" + lcd_stats);
            }
            if (lcd.CustomName.Contains("[ListOres]"))
            {   string lcd_ores = "";
                foreach (var res in resdb)
                {   string[] rData = res.Split(sep);
                    if (rData[0] == "Ore") lcd_ores += rData[1] + " - " + Math.Floor(Convert.ToDouble(rData[2])/100)/10 + " kg" + "\n";
                }
                lcd.FontSize = 1.25f;
                lcd.WriteText("Ores in Station:" + "\n\n" + lcd_ores);
            }
            if (lcd.CustomName.Contains("[ListIngots]"))
            {   string lcd_ingots = "";
                foreach (var res in resdb)
                {   string[] rData = res.Split(sep);
                    if (rData[0] == "Ingot") lcd_ingots += rData[1] + " - " + Math.Floor(Convert.ToDouble(rData[2])/100)/10 + " kg" + "\n";
                }
                lcd.FontSize = 1.25f;
                lcd.WriteText("Ingots in Station:" + "\n\n" + lcd_ingots);
            }
            if (lcd.CustomName.Contains("[ListComponents]"))
            {   string lcd_comp = "";
                foreach (var res in resdb)
                {   string[] rData = res.Split(sep);
                    if (rData[0] == "Component") lcd_comp += rData[1] + " - " + Math.Floor(Convert.ToDouble(rData[2])) + " p." + "\n";
                }
                lcd.FontSize = 1.25f;
                lcd.WriteText("Components in Station:" + "\n\n" + lcd_comp);
            }
        }
    }

// Routines ----------------------------------------

public void DumpArray(string[] darray)
    {   for (int h = 0; h < darray.Length; h++) Echo(h.ToString() + ">" + darray[h].ToString() + "<");
    }
public void DumpList(List<string> dlist)
    {   for (int h = 0; h < dlist.Count; h++) Echo(h.ToString() + ">" + dlist[h].ToString() + "<");
    }

public IMyTerminalBlock findblock (List<IMyTerminalBlock> bList, string bSearch)
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
                    Echo(sMove); lcddb.Add(sMove);
                }
            }
        }
    }

// Specials ----------------------------------------

public void Specials()
    {   double rt_totalsec = Runtime.TimeSinceLastRun.TotalSeconds;
    }