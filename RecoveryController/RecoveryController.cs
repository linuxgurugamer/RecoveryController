using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.UI.Screens;


namespace RecoveryController
{

    /// <summary>
    /// Part module to hold the identity of the mod which can/will control the stage when detached from main vessel
    /// The module will be added to all parts other than decouplers/seperators by a MM script
    /// </summary>
    public class ControllingRecoveryModule : PartModule
    {
#if DEBUG
        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = true, guiName = "Recovery-Owner")]
#else
          [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Recovery-Owner")]
#endif
        string recoveryOwner = "x";

        public string RecoveryOwner
        {
            get { if (recoveryOwner == null) return "";  return recoveryOwner; }
            set { recoveryOwner = value; }
        }

        private void Start()
        {
          //  if (RecoveryController.registeredMods != null && RecoveryController.registeredMods.Count > 0)
          //      RecoveryOwner = RecoveryController.registeredMods.FirstOrDefault();
        }
    }

    /// <summary>
    /// Part module to control the identity of the mod which can/will control the stage after decoupling
    /// The module will be added to all decouplers/seperators by a MM script
    /// </summary>
    public class RecoveryIDModule : PartModule
    {
        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false, guiName = "Recovery-Owner")]
        string recoveryOwner = "";

        [KSPField(isPersistant = true, guiActiveEditor = false, guiActive = false)]
        int recoveryOwnerIdx = -1;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "RecoveryOwner: n/a")]
        public void CycleRecoveryOwner()
        {            
            recoveryOwnerIdx++;
            if ((int)recoveryOwnerIdx >= RecoveryController.registeredMods.Count)
                recoveryOwnerIdx = 0;
            recoveryOwner = RecoveryController.registeredMods[recoveryOwnerIdx];
            UpdateEventsAndSymmetry(true);
        }

        public void updateRecoveryOwner(string ro)
        {
            RecoveryOwner = ro;
            UpdateEventsAndSymmetry();
        }

        public string RecoveryOwner
        {
            get { if (recoveryOwner == null) return "";  return recoveryOwner; }
            set {
                recoveryOwner = value;
                recoveryOwnerIdx = RecoveryController.registeredMods.FindIndex(a => a == RecoveryOwner);
            }
        }

        void UpdateEventsAndSymmetry(bool updateSym = false)
        {
            if (HighLogic.LoadedSceneIsEditor | HighLogic.LoadedSceneIsFlight)
            {
                Events["CycleRecoveryOwner"].guiName = "RecoveryOwner: " + RecoveryOwner;
                if (updateSym)
                {
                    if (this.part.symmetryCounterparts.Count > 0)
                    {
                        foreach (var p in this.part.symmetryCounterparts)
                        {
                            RecoveryIDModule ridm = p.FindModuleImplementing<RecoveryIDModule>();
                            ridm.updateRecoveryOwner(RecoveryOwner);
                        }
                    }

                }
                UpdateChildren(part);
            }
        }

        void UpdateChildren(Part p)
        {
            foreach (Part child in p.children)
            {
                if (!idUtil.IsDecoupler(child))
                {
                    ControllingRecoveryModule crm = child.FindModuleImplementing<ControllingRecoveryModule>();
                    if (crm == null)
                        Log.Info("Missing module ControllingRecoveryModule");
                    crm.RecoveryOwner = RecoveryOwner;
                    UpdateChildren(child);
                }
            }
        }


        private void Start()
        {
#if DEBUG
            if (HighLogic.LoadedSceneIsEditor)
                Log.Info("RecoveryIDModule: Start, parentPart: " + this.part.partInfo.name);
            if (HighLogic.LoadedSceneIsFlight)
                Log.Info("RecoveryIDModule: Start, parentPart: " + this.part.partInfo.name + "   parentVessel: " + this.part.vessel.name);
#endif

            if (RecoveryController.registeredMods != null && RecoveryController.registeredMods.Count > 0)
            {
                if (RecoveryOwner != null)
                {
                    recoveryOwnerIdx = RecoveryController.registeredMods.FindIndex(a => a == RecoveryOwner);

                }
                if (RecoveryOwner == null || recoveryOwnerIdx == -1)
                    RecoveryOwner = RecoveryController.registeredMods.FirstOrDefault();
            }
            UpdateEventsAndSymmetry();
        }
       
    }

    /// <summary>
    /// Helper class with some static functions
    /// </summary>     
    static class idUtil
    {
#if false
        /// <summary>
        ///     Gets whether the part contains a PartModule.
        /// </summary>
        static public  bool  HasModule<T>(this Part part) where T : PartModule
        {
            Log.Info("idUtil: HasModule part: " + part.partInfo.name);
            for (int i = 0; i < part.Modules.Count; i++)
            {
                if (part.Modules[i] is T)
                    return true;
            }
            return false;
        }
#endif

        /// <summary>
        ///     Gets whether the part is a decoupler.
        /// </summary>
        static public bool IsDecoupler(this Part part)
        {
            //bool b = HasModule<ModuleDecouple>(part);
            ModuleDecouple md = part.FindModuleImplementing<ModuleDecouple>();
            ModuleAnchoredDecoupler mad = part.FindModuleImplementing<ModuleAnchoredDecoupler>();
            if (mad != null && !mad.isDecoupled)
                return true;

            if (md != null && !md.isDecoupled)
                return true;
            return false;
        }
        // Following for unloaded vessels
        static public bool IsDecoupler(this ProtoPartSnapshot part)
        {
            //bool b = HasModule<ModuleDecouple>(part);
            if (part.modules.Count > 0)
            {
                Log.Info("IsDecoupler, modules.Count: " + part.modules.Count.ToString());
                ProtoPartModuleSnapshot md = part.modules.FirstOrDefault(mod => mod.moduleName == "ModuleDecouple");
                ProtoPartModuleSnapshot mad = part.modules.FirstOrDefault(mod => mod.moduleName == "ModuleAnchoredDecoupler");

                if (mad != null && mad.moduleRef != null)
                {
//                                        && !((ModuleAnchoredDecoupler)mad.moduleRef).isDecoupled)

                   if (mad.moduleValues.GetValue("isDecoupled") == "false")
                    return true;
                }

                if (md != null && md.moduleRef != null)
                {
                    // && !((ModuleAnchoredDecoupler)md.moduleRef).isDecoupled)
                    if (md.moduleValues.GetValue("isDecoupled") == "false")
                        return true;
                }
            }
            return false;
        }

        static public void UpdateChildren(Part p, string recoveryOwner, bool entireVessel = false)
        {
            foreach (Part child in p.children)
            {
                if (!idUtil.IsDecoupler(child))
                {
                    ControllingRecoveryModule crm = child.FindModuleImplementing<ControllingRecoveryModule>();
                    if (crm == null)
                        Log.Info("Missing module ControllingRecoveryModule");
                    crm.RecoveryOwner = recoveryOwner;
                    UpdateChildren(child, recoveryOwner, entireVessel);
                }
                else
                {
                    if (entireVessel)
                    {
                        // get child, and call using child and it's recoveryOwner
                        RecoveryIDModule ridm = child.FindModuleImplementing<RecoveryIDModule>();
                        UpdateChildren(child, ridm.RecoveryOwner, true);
                    }
                }
            }
        }
    }
#if false
    /// <summary>
    /// VesselModule used by mods to identify controlling mod for vessel 
    /// </summary>
    public class RecoveryIDVesselModule : VesselModule
    {
        Vessel v;
        string recoveryOwner;

        public string UseRecoveryOwner
        {
            get { return recoveryOwner; }
        }

        new private void Start()
        {
            v = this.GetComponent<Vessel>();
            
            getNextSeparator();
            base.Start();
            recoveryOwner = RecoveryController.registeredMods.First();
        }
        
        private void OnDestroy()
        {
        }

        void getNextSeparator()
        {
            Log.Info("RecoveryIDVesselModule: getNextSeparator, vessel: " + v.name + "   currentStage: " + v.currentStage.ToString() + "   last stage: " + v.currentStage.ToString());
            foreach (Part p in v.Parts)
            {
                if (p.inverseStage >= v.currentStage - 1)
                {
                    if (idUtil.IsDecoupler(p))
                    {
                        RecoveryIDModule m = p.FindModuleImplementing<RecoveryIDModule>();
                        if (m != null)
                        {
                            recoveryOwner = m.RecoveryOwner;
                            return;
                        }
                    }
                }
            }
        }
    }
#endif

    /// <summary>
    /// Class to take care of things in the editor
    /// </summary>
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class RecoveryController : MonoBehaviour
    {
        const string AUTO = "auto";

        public static List<string> registeredMods = new List<string>();

        private void Awake()
        {
            if (registeredMods.Contains("StageRecovery"))
            {
                GameEvents.onEditorPartPlaced.Add(onEditorPartPlaced);
                GameEvents.onEditorLoad.Add(onEditorLoad);
                GameEvents.onVesselLoaded.Add(onVesselLoaded);
                GameEvents.onVesselCreate.Add(onVesselCreate);
                GameEvents.onVesselWasModified.Add(onVesselWasModified);

                DontDestroyOnLoad(this);
                RegisterMod(AUTO);
                RegisterMod("none");
                DontDestroyOnLoad(this);
            }
        }

        public bool RegisterMod(string modName)
        {
            Log.Info("RegisterMod, modname: " + modName);
            if (!registeredMods.Contains(modName))
                registeredMods.Add(modName);
            return true;
        }

        
        public bool UnRegisterMod(string modName)
        {
            try
            {
                registeredMods.Remove(modName);
                return true;
            }
            catch { return false ; }
        }

        public string ControllingMod(Vessel v)
        {
            if (v.name.StartsWith("Ast."))
            {
                Log.Info("Vessel: Asteroid");
                return "";
            }

            Log.Info("ControllingMod, vessel: " + v.name);
            if (!v.loaded)
            {
                Log.Info("Vessel is unloaded");
                foreach (ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
                {
                    Log.Info("ProtoPartsnapshot, currentStage: " + v.currentStage.ToString() + "  stageIndex: " + p.stageIndex.ToString() + "  inverseStageIndex: " + p.inverseStageIndex.ToString());
                    if (p.inverseStageIndex >= v.currentStage - 1)
                    {
                        if (idUtil.IsDecoupler(p))
                        {
                            ProtoPartModuleSnapshot m = p.modules.FirstOrDefault(mod => mod.moduleName == "RecoveryIDModule");

                            // FindModuleImplementing<RecoveryIDModule>();
                            if (m != null /*&& m.moduleRef != null */)
                            {
                                Log.Info("Part: " + p.partInfo.name + ", decoupler, Returning: " + m.moduleValues.GetValue("recoveryOwner"));
                                return m.moduleValues.GetValue("recoveryOwner");
                                //return ((RecoveryIDModule)m.moduleRef).RecoveryOwner;
                            }
                        }
                        else
                        {
                            if (p.modules.Count > 0)
                            {
                                {
                                    ProtoPartModuleSnapshot m = p.modules.FirstOrDefault(mod => mod.moduleName == "ControllingRecoveryModule");
                                    if (m != null /* && m.moduleRef != null */ )
                                    {
                                        Log.Info("Part: " + p.partInfo.name + ", part,  Returning: " + m.moduleValues.GetValue("recoveryOwner"));
                                        return m.moduleValues.GetValue("recoveryOwner");
                                        //return ((RecoveryIDModule)m.moduleRef).RecoveryOwner;
                                    }
                                }
                            }
                            else
                            {
                                Log.Info("ControllingRecoveryModule not found");
                                return "ControllingRecoveryModule not found";
                            }
                        }

                    }
                }

            }
            else
            {
                foreach (Part p in v.Parts)
                {
                    if (p.inverseStage >= v.currentStage - 1)
                    {
                        if (idUtil.IsDecoupler(p))
                        {
                            RecoveryIDModule m = p.FindModuleImplementing<RecoveryIDModule>();
                            if (m != null)
                            {
                                Log.Info("Part: " + p.partInfo.name + ", Returning: " + m.RecoveryOwner);
                                return m.RecoveryOwner;
                            }
                        }
                        else
                        {
                            ControllingRecoveryModule m = p.FindModuleImplementing<ControllingRecoveryModule>();
                            if (m != null)
                            {
                                Log.Info("Part: " + p.partInfo.name + ", Returning: " + m.RecoveryOwner);
                                return m.RecoveryOwner;
                            }
                        }

                    }
                }
            }
            Log.Info("returning null");
            return null;
        }

        private void OnDestroy()
        {
            Log.Info("RecoveryController.OnDestroy");
            GameEvents.onEditorPartPlaced.Remove(onEditorPartPlaced);
            GameEvents.onEditorLoad.Remove(onEditorLoad);
            GameEvents.onVesselLoaded.Remove(onVesselLoaded);
            GameEvents.onVesselCreate.Remove(onVesselCreate);
            GameEvents.onVesselWasModified.Remove(onVesselWasModified);
        }
        
        void onEditorLoad(ShipConstruct ct, CraftBrowserDialog.LoadType loadType)
        {
            Log.Info("onEditorLoad 1");
            if (!registeredMods.Contains("StageRecovery"))                
                return;
            // if (loadType == CraftBrowserDialog.LoadType.Normal)
            if (ct != null && ct.Parts.Count() > 0)
            {
                Log.Info("onEditorLoad 2");
                Part root = ct.Parts[0];
                Log.Info("onEditorLoad 3");

                if (!idUtil.IsDecoupler(root))
                {
                    Log.Info("onEditorLoad 4");
                    idUtil.UpdateChildren(root, root.FindModuleImplementing<ControllingRecoveryModule>().RecoveryOwner, true);
                }
                else
                {
                    Log.Info("onEditorLoad 5");
                    idUtil.UpdateChildren(root, root.FindModuleImplementing<RecoveryIDModule>().RecoveryOwner, true);
                }
            }
        }

        void onVesselLoaded(Vessel v)
        {
            Log.Info("onVesselLoaded");
            if (v == null || v.rootPart == null)
                return;
            if (!registeredMods.Contains("StageRecovery"))                
                return;
            if (!idUtil.IsDecoupler(v.rootPart))
            {
                var m = v.rootPart.FindModuleImplementing<ControllingRecoveryModule>();
                if (m != null)
                {
                    idUtil.UpdateChildren(v.rootPart, m.RecoveryOwner, true);
                }
            }
            else
            {
                var m = v.rootPart.FindModuleImplementing<RecoveryIDModule>();
                if (m != null)
                    idUtil.UpdateChildren(v.rootPart, m.RecoveryOwner, true);
            }
        }

        void onVesselCreate(Vessel v)
        {
            Log.Info("onVesselCreate");
            if (v.rootPart != null)
                if (!idUtil.IsDecoupler(v.rootPart))
                    idUtil.UpdateChildren(v.rootPart, v.rootPart.FindModuleImplementing<ControllingRecoveryModule>().RecoveryOwner, true);
                else
                    idUtil.UpdateChildren(v.rootPart, v.rootPart.FindModuleImplementing<RecoveryIDModule>().RecoveryOwner, true);
        }

        void onVesselWasModified(Vessel v)
        {
            Log.Info("onVesselWasModified");
            if (!idUtil.IsDecoupler(v.rootPart))
            {

                var m = v.rootPart.FindModuleImplementing<ControllingRecoveryModule>();
                if (m != null)
                    idUtil.UpdateChildren(v.rootPart, m.RecoveryOwner, true);
            }
            else
            {
                var m = v.rootPart.FindModuleImplementing<RecoveryIDModule>();
                idUtil.UpdateChildren(v.rootPart, v.rootPart.FindModuleImplementing<RecoveryIDModule>().RecoveryOwner, true);
            }
        }

        void onEditorPartPlaced(Part p) 
        {
            if (p == null)
            {
                Log.Info("RecoveryController.onEditorPartPlaced, part is null");
                return;
            }
            Log.Info("RecoveryController.onEditorPartPlaced, part: " + p.partInfo.name);
            if (p.parent == null)
            {
                // First part placed, set to auto
                Log.Info("Initial part placement");
                if (!idUtil.IsDecoupler(p))
                    p.FindModuleImplementing<ControllingRecoveryModule>().RecoveryOwner = AUTO;
                else
                    p.FindModuleImplementing<RecoveryIDModule>().RecoveryOwner = AUTO;
                
                return;
            }
            if (!idUtil.IsDecoupler(p))
            {
                string roParent;

                ControllingRecoveryModule m = p.FindModuleImplementing<ControllingRecoveryModule>();
                if (!idUtil.IsDecoupler(p.parent))
                    roParent = p.parent.FindModuleImplementing<ControllingRecoveryModule>().RecoveryOwner;
                else
                    roParent = p.parent.FindModuleImplementing<RecoveryIDModule>().RecoveryOwner;

                m.RecoveryOwner = roParent;
            }
            else
            {
                Log.Info("Decoupler placed");
                // Always set decoupler to auto when placed
                p.FindModuleImplementing<RecoveryIDModule>().RecoveryOwner = AUTO;
            }
        }
    }
}